using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineActionsTests
{
    [Fact]
    public async Task RefreshOnceAsync_AppliesProviderSourceOverrideWithoutSavingConfig()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        codex.IsEnabled = false;
        codex.SourceKind = DataSourceKind.Manual;
        var chatGpt = config.Providers.Single(provider => provider.ProviderId == ProviderId.ChatGPT);
        chatGpt.IsEnabled = true;
        chatGpt.SourceKind = DataSourceKind.Mock;
        await configStore.SaveAsync(config, CancellationToken.None);

        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Codex", "Mock"),
            CancellationToken.None,
            paths);

        var reloaded = await configStore.LoadAsync(CancellationToken.None);
        var reloadedCodex = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        var reloadedChatGpt = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.ChatGPT);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Snapshots: 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("Codex", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("ChatGPT", result.Output, StringComparison.Ordinal);
        Assert.False(reloadedCodex.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, reloadedCodex.SourceKind);
        Assert.True(reloadedChatGpt.IsEnabled);
        Assert.Equal(DataSourceKind.Mock, reloadedChatGpt.SourceKind);
    }

    [Fact]
    public async Task RefreshOnceAsync_ReturnsErrorForUnknownProvider()
    {
        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Nope", "Mock"),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown provider", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshOnceAsync_ReturnsErrorForUnsupportedProviderSource()
    {
        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Gemini", "LocalAppServer"),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("does not support source", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PruneSupportArtifactsAsync_PrunesBackupsAndDiagnosticsExports()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        var secretPath = Path.Combine(paths.SecretsDirectory, "diagnostics-export-20260708-090000.txt");
        await File.WriteAllTextAsync(secretPath, "secret");
        var unrelatedBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "manual-note.json");
        await File.WriteAllTextAsync(unrelatedBackupPath, "keep");
        var oldBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-100000.json",
            "old backup",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var keptBackupA = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-110000.json",
            "kept backup a",
            new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));
        var keptBackupB = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-before-reset-20260708-120000.json",
            "kept backup b",
            new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
        var oldExport = await WriteTimestampedFileAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-20260708-100000.txt",
            "old export",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var keptExportA = await WriteTimestampedFileAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-20260708-110000.txt",
            "kept export a",
            new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));
        var keptExportB = await WriteTimestampedFileAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-20260708-120000.txt",
            "kept export b",
            new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));

        try
        {
            var result = await CommandLineActions.PruneSupportArtifactsAsync(
                new CommandLinePruneSupportArtifactsOptions(2),
                CancellationToken.None,
                paths);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Support artifact pruning", result.Output, StringComparison.Ordinal);
            Assert.Contains("Keep newest: 2", result.Output, StringComparison.Ordinal);
            Assert.Contains("Config backups", result.Output, StringComparison.Ordinal);
            Assert.Contains("Diagnostics exports", result.Output, StringComparison.Ordinal);
            Assert.Contains("Deleted: 1", result.Output, StringComparison.Ordinal);
            Assert.False(File.Exists(oldBackup));
            Assert.False(File.Exists(oldExport));
            Assert.True(File.Exists(keptBackupA));
            Assert.True(File.Exists(keptBackupB));
            Assert.True(File.Exists(keptExportA));
            Assert.True(File.Exists(keptExportB));
            Assert.True(File.Exists(unrelatedBackupPath));
            Assert.True(File.Exists(secretPath));
        }
        finally
        {
            if (Directory.Exists(paths.RootDirectory))
            {
                Directory.Delete(paths.RootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateHealthReportAsync_IncludesStoragePressureGuidance()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.HistoryRetention.MaxBytes = 1_000;
        config.HistoryRetention.MaxDays = 30;

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);
            await File.WriteAllTextAsync(paths.HistoryPath, new string('x', 95_000));

            var report = await CommandLineActions.CreateHealthReportAsync(
                CancellationToken.None,
                paths,
                new FakeCliEnvironmentService());

            Assert.Contains("Storage pressure", report, StringComparison.Ordinal);
            Assert.Contains("Retained history: High", report, StringComparison.Ordinal);
            Assert.Contains("Use Clear History soon", report, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", report, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(paths.RootDirectory))
            {
                Directory.Delete(paths.RootDirectory, recursive: true);
            }
        }
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }

    private static async Task<string> WriteTimestampedFileAsync(
        string directory,
        string fileName,
        string content,
        DateTime lastWriteTimeUtc)
    {
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        return path;
    }

    private sealed class FakeCliEnvironmentService : ICliEnvironmentService
    {
        public Task<CliEnvironmentReport> GetReportAsync(
            IReadOnlyList<CliCommandCheck> commands,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CliEnvironmentReport([]));
        }
    }
}
