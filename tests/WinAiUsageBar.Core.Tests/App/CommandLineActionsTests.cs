using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

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
            Assert.Contains("Recovery guidance", report, StringComparison.Ordinal);
            Assert.Contains("Export a config backup: Available", report, StringComparison.Ordinal);
            Assert.Contains("Restore the latest backup: Not ready", report, StringComparison.Ordinal);
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

    [Fact]
    public async Task CheckForUpdatesAsync_FormatsUpdateCheckResult()
    {
        var result = await CommandLineActions.CheckForUpdatesAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0+local"),
            new FakeUpdateCheckService(new ReleaseUpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                CurrentVersion: "0.1.0",
                LatestVersion: "0.2.0",
                "A newer GitHub release is available.",
                IsUpdateAvailable: true,
                new Uri("https://example.test/releases/v0.2.0"),
                new UpdatePackageAsset(
                    "WinAIUsageBar-0.2.0-win-x64.zip",
                    new Uri("https://example.test/package.zip"),
                    2048),
                new UpdatePackageAsset(
                    "WinAIUsageBar-0.2.0-win-x64.zip.sha256",
                    new Uri("https://example.test/package.zip.sha256"),
                    128))),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Update check", result.Output, StringComparison.Ordinal);
        Assert.Contains("Status: UpdateAvailable", result.Output, StringComparison.Ordinal);
        Assert.Contains("Current version: 0.1.0", result.Output, StringComparison.Ordinal);
        Assert.Contains("Latest version: 0.2.0", result.Output, StringComparison.Ordinal);
        Assert.Contains("Update available: yes", result.Output, StringComparison.Ordinal);
        Assert.Contains("WinAIUsageBar-0.2.0-win-x64.zip", result.Output, StringComparison.Ordinal);
        Assert.Contains("package.zip.sha256", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsNonZeroForUpdateCheckErrors()
    {
        var result = await CommandLineActions.CheckForUpdatesAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            new FakeUpdateCheckService(new ReleaseUpdateCheckResult(
                UpdateCheckStatus.Error,
                CurrentVersion: "0.1.0",
                LatestVersion: null,
                "network failed",
                IsUpdateAvailable: false,
                ReleasePageUrl: null,
                Package: null,
                Checksum: null)),
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("network failed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadUpdateAsync_DownloadsWhenUpdateIsAvailable()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "downloaded",
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"),
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('a', 64)));

        var result = await CommandLineActions.DownloadUpdateAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            new FakeUpdateCheckService(AvailableUpdate()),
            downloader,
            paths,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Contains("Update download", result.Output, StringComparison.Ordinal);
        Assert.Contains("Download status: Downloaded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Package path:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadUpdateAsync_SkipsWhenNoUpdateIsAvailable()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "should not run",
            "package.zip",
            "package.zip.sha256",
            ExpectedSha256: null,
            ActualSha256: null));

        var result = await CommandLineActions.DownloadUpdateAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            new FakeUpdateCheckService(new ReleaseUpdateCheckResult(
                UpdateCheckStatus.UpToDate,
                CurrentVersion: "0.1.0",
                LatestVersion: "0.1.0",
                "The current app version is up to date.",
                IsUpdateAvailable: false,
                ReleasePageUrl: null,
                Package: null,
                Checksum: null)),
            downloader,
            paths,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Contains("Download status: Skipped", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadUpdateAsync_ReturnsNonZeroWhenVerificationFails()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.ChecksumMismatch,
            "hash mismatch",
            PackagePath: null,
            ChecksumPath: null,
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('b', 64)));

        var result = await CommandLineActions.DownloadUpdateAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            new FakeUpdateCheckService(AvailableUpdate()),
            downloader,
            paths,
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Contains("Download status: ChecksumMismatch", result.Output, StringComparison.Ordinal);
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

    private sealed class FakeUpdateCheckService(ReleaseUpdateCheckResult result) : IReleaseUpdateCheckService
    {
        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUpdatePackageDownloader(UpdateDownloadResult result) : IUpdatePackageDownloader
    {
        public int DownloadCount { get; private set; }

        public Task<UpdateDownloadResult> DownloadAndVerifyAsync(
            UpdatePackageAsset package,
            UpdatePackageAsset checksum,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadCount++;
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", package.Name);
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", checksum.Name);
            Assert.False(string.IsNullOrWhiteSpace(targetDirectory));
            return Task.FromResult(result);
        }
    }

    private static ReleaseUpdateCheckResult AvailableUpdate()
    {
        return new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.0",
            LatestVersion: "0.2.0",
            "A newer GitHub release is available.",
            IsUpdateAvailable: true,
            new Uri("https://example.test/releases/v0.2.0"),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip",
                new Uri("https://example.test/package.zip"),
                2048),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip.sha256",
                new Uri("https://example.test/package.zip.sha256"),
                128));
    }
}
