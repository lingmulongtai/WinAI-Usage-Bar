using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
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
    public async Task SetProviderCliOverrideAsync_SavesNormalizedOverrideWithoutEchoingValue()
    {
        var paths = TestPaths();
        var commandPath = @"C:\Program Files\Codex\codex.cmd";

        try
        {
            var result = await CommandLineActions.SetProviderCliOverrideAsync(
                new CommandLineSetProviderCliOverrideOptions("Codex", $" \"{commandPath}\" "),
                CancellationToken.None,
                paths);

            var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);
            var codex = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(commandPath, codex.Cli.CommandPathOverride);
            Assert.Contains("Provider CLI override", result.Output, StringComparison.Ordinal);
            Assert.Contains("Codex", result.Output, StringComparison.Ordinal);
            Assert.Contains("configured", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(commandPath, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(paths.RootDirectory))
            {
                Directory.Delete(paths.RootDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Nope", "codex", "Unknown provider")]
    [InlineData("Gemini", "gemini", "does not support CLI command overrides")]
    [InlineData("Codex", "\"C:\\Tools\\codex.cmd\" --verbose", "quotes must wrap")]
    [InlineData("Codex", "codex.exe --token=secret", "must not contain tokens")]
    public async Task SetProviderCliOverrideAsync_ReturnsValidationErrors(
        string providerId,
        string commandPathOverride,
        string expectedError)
    {
        var result = await CommandLineActions.SetProviderCliOverrideAsync(
            new CommandLineSetProviderCliOverrideOptions(providerId, commandPathOverride),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expectedError, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearProviderCliOverrideAsync_ClearsSavedOverrideWithoutEchoingValue()
    {
        var paths = TestPaths();
        var commandPath = @"C:\Program Files\Codex\codex.cmd";
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        var codex = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));
        codex.Cli.CommandPathOverride = commandPath;

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var result = await CommandLineActions.ClearProviderCliOverrideAsync(
                new CommandLineClearProviderCliOverrideOptions("Codex"),
                CancellationToken.None,
                paths);

            var reloaded = await configStore.LoadAsync(CancellationToken.None);
            var reloadedCodex = reloaded.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));

            Assert.Equal(0, result.ExitCode);
            Assert.Null(reloadedCodex.Cli.CommandPathOverride);
            Assert.Contains("Provider CLI override", result.Output, StringComparison.Ordinal);
            Assert.Contains("Codex", result.Output, StringComparison.Ordinal);
            Assert.Contains("cleared", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(commandPath, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(paths.RootDirectory))
            {
                Directory.Delete(paths.RootDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Nope", "Unknown provider")]
    [InlineData("Gemini", "does not support CLI command overrides")]
    public async Task ClearProviderCliOverrideAsync_ReturnsValidationErrors(
        string providerId,
        string expectedError)
    {
        var result = await CommandLineActions.ClearProviderCliOverrideAsync(
            new CommandLineClearProviderCliOverrideOptions(providerId),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expectedError, result.Output, StringComparison.Ordinal);
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
    public async Task ListConfigBackupsAsync_ListsOnlyTopLevelMatchedBackups()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        var secretPath = Path.Combine(paths.SecretsDirectory, "config-backup-20260708-090000.json");
        await File.WriteAllTextAsync(secretPath, "secret");
        var unrelatedBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "manual-note.json");
        await File.WriteAllTextAsync(unrelatedBackupPath, "keep");
        var nestedDirectory = Path.Combine(paths.ConfigBackupsDirectory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedBackupPath = Path.Combine(nestedDirectory, "config-backup-20260708-140000.json");
        await File.WriteAllTextAsync(nestedBackupPath, "nested");
        var oldBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-100000.json",
            "old backup",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var newestBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-before-reset-20260708-120000.json",
            "newest backup",
            new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));

        try
        {
            var result = await CommandLineActions.ListConfigBackupsAsync(
                new CommandLineListConfigBackupsOptions(1),
                CancellationToken.None,
                paths);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config backups", result.Output, StringComparison.Ordinal);
            Assert.Contains("Matched: 2", result.Output, StringComparison.Ordinal);
            Assert.Contains("Listed: 1", result.Output, StringComparison.Ordinal);
            Assert.Contains("Limit: 1", result.Output, StringComparison.Ordinal);
            Assert.Contains(newestBackup, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(oldBackup, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedBackupPath, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(nestedBackupPath, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(secretPath, result.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(oldBackup));
            Assert.True(File.Exists(newestBackup));
            Assert.True(File.Exists(unrelatedBackupPath));
            Assert.True(File.Exists(nestedBackupPath));
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
    public async Task ListConfigBackupsAsync_ReturnsEmptyListWithoutMutatingFiles()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        var unrelatedBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "manual-note.json");
        await File.WriteAllTextAsync(unrelatedBackupPath, "keep");

        try
        {
            var result = await CommandLineActions.ListConfigBackupsAsync(
                new CommandLineListConfigBackupsOptions(10),
                CancellationToken.None,
                paths);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Matched: 0", result.Output, StringComparison.Ordinal);
            Assert.Contains("Listed: 0", result.Output, StringComparison.Ordinal);
            Assert.Contains("No config backups are available.", result.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(unrelatedBackupPath));
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
    public async Task ExportConfigBackupAsync_WritesConfigOnlyBackup()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Appearance.Theme = "Dark";
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.ApiKey.SecretName = "gemini-secret-ref";

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var result = await CommandLineActions.ExportConfigBackupAsync(
                CancellationToken.None,
                paths);

            var backupPath = Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-*.json")
                .Single();
            var backupText = await File.ReadAllTextAsync(backupPath);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config backup export", result.Output, StringComparison.Ordinal);
            Assert.Contains("Path:", result.Output, StringComparison.Ordinal);
            Assert.Contains(backupPath, result.Output, StringComparison.Ordinal);
            Assert.Contains("\"theme\": \"Dark\"", backupText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gemini-secret-ref", backupText, StringComparison.Ordinal);
            Assert.DoesNotContain("actual-secret-value", backupText, StringComparison.Ordinal);
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
    public async Task ValidateLatestConfigBackupAsync_ValidatesNewestBackup()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var original = AppConfig.CreateDefault();
        original.Appearance.Theme = "Dark";
        original.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).ApiKey.SecretName = "gemini-secret-ref";

        try
        {
            await configStore.SaveAsync(original, CancellationToken.None);
            var oldExport = await CommandLineActions.ExportConfigBackupAsync(CancellationToken.None, paths);
            var oldBackupPath = Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-*.json")
                .Single();
            File.SetLastWriteTimeUtc(oldBackupPath, new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc));

            var changed = await configStore.LoadAsync(CancellationToken.None);
            changed.Appearance.Theme = "Light";
            await configStore.SaveAsync(changed, CancellationToken.None);
            var newExport = await CommandLineActions.ExportConfigBackupAsync(CancellationToken.None, paths);
            var newBackupPath = Directory
                .GetFiles(paths.ConfigBackupsDirectory, "config-backup-*.json")
                .Single(path => !string.Equals(path, oldBackupPath, StringComparison.OrdinalIgnoreCase));
            File.SetLastWriteTimeUtc(newBackupPath, new DateTime(2026, 7, 9, 1, 0, 0, DateTimeKind.Utc));

            var result = await CommandLineActions.ValidateLatestConfigBackupAsync(
                CancellationToken.None,
                paths);
            var reloaded = await configStore.LoadAsync(CancellationToken.None);

            Assert.Equal(0, oldExport.ExitCode);
            Assert.Equal(0, newExport.ExitCode);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config backup validation: valid", result.Output, StringComparison.Ordinal);
            Assert.Contains(newBackupPath, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(oldBackupPath, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("gemini-secret-ref", result.Output, StringComparison.Ordinal);
            Assert.Equal("Light", reloaded.Appearance.Theme);
            Assert.Empty(Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-*.json"));
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
    public async Task ValidateLatestConfigBackupAsync_ReturnsFailureWhenNoBackupExists()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Appearance.Theme = "Dark";

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var result = await CommandLineActions.ValidateLatestConfigBackupAsync(
                CancellationToken.None,
                paths);
            var reloaded = await configStore.LoadAsync(CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Config backup validation: invalid", result.Output, StringComparison.Ordinal);
            Assert.Contains("No config backup is available.", result.Output, StringComparison.Ordinal);
            Assert.Equal("Dark", reloaded.Appearance.Theme);
            Assert.Empty(Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-*.json"));
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
    public async Task RestoreLatestConfigBackupAsync_RestoresNewestBackup()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var original = AppConfig.CreateDefault();
        original.Appearance.Theme = "Dark";
        var gemini = original.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.ApiKey.SecretName = "gemini-secret-ref";

        try
        {
            await configStore.SaveAsync(original, CancellationToken.None);
            var export = await CommandLineActions.ExportConfigBackupAsync(CancellationToken.None, paths);
            var backupPath = Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-*.json")
                .Single();
            var changed = await configStore.LoadAsync(CancellationToken.None);
            changed.Appearance.Theme = "Light";
            changed.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).ApiKey.SecretName = "changed-secret-ref";
            await configStore.SaveAsync(changed, CancellationToken.None);

            var result = await CommandLineActions.RestoreLatestConfigBackupAsync(
                CancellationToken.None,
                paths);
            var restored = await configStore.LoadAsync(CancellationToken.None);
            var rollbackBackups = Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-restore-*.json");

            Assert.Equal(0, export.ExitCode);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config backup restore: restored", result.Output, StringComparison.Ordinal);
            Assert.Contains(backupPath, result.Output, StringComparison.Ordinal);
            Assert.Equal("Dark", restored.Appearance.Theme);
            Assert.Equal(
                "gemini-secret-ref",
                restored.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).ApiKey.SecretName);
            Assert.Single(rollbackBackups);
            Assert.DoesNotContain("changed-secret-ref", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("gemini-secret-ref", result.Output, StringComparison.Ordinal);
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
    public async Task RestoreLatestConfigBackupAsync_ReturnsFailureWhenNoBackupExists()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Appearance.Theme = "Dark";

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var result = await CommandLineActions.RestoreLatestConfigBackupAsync(
                CancellationToken.None,
                paths);
            var reloaded = await configStore.LoadAsync(CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Config backup restore: not restored", result.Output, StringComparison.Ordinal);
            Assert.Contains("No config backup is available.", result.Output, StringComparison.Ordinal);
            Assert.Equal("Dark", reloaded.Appearance.Theme);
            Assert.Empty(Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-restore-*.json"));
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
    public async Task ResetConfigToDefaultsAsync_BacksUpConfigAndKeepsSecrets()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Appearance.Theme = "Dark";
        config.Startup.LaunchOnLogin = true;
        config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled = true;
        var secretPath = Path.Combine(paths.SecretsDirectory, "gemini-secret");

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);
            await File.WriteAllTextAsync(secretPath, "secret-value");

            var result = await CommandLineActions.ResetConfigToDefaultsAsync(
                CancellationToken.None,
                paths);
            var reset = await configStore.LoadAsync(CancellationToken.None);
            var rollbackBackups = Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-reset-*.json");
            var rollbackText = await File.ReadAllTextAsync(rollbackBackups.Single());

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config reset: reset", result.Output, StringComparison.Ordinal);
            Assert.Contains("Rollback backup:", result.Output, StringComparison.Ordinal);
            Assert.Contains("Saved secrets were not deleted", result.Output, StringComparison.Ordinal);
            Assert.Equal("System", reset.Appearance.Theme);
            Assert.False(reset.Startup.LaunchOnLogin);
            Assert.False(reset.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled);
            Assert.Contains("\"theme\": \"Dark\"", rollbackText, StringComparison.Ordinal);
            Assert.True(File.Exists(secretPath));
            Assert.Equal("secret-value", await File.ReadAllTextAsync(secretPath));
            Assert.DoesNotContain("secret-value", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("gemini-secret", result.Output, StringComparison.Ordinal);
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
    public async Task CreateHealthReportAsync_DoesNotRewriteNormalizedConfig()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Refresh.Interval = RefreshIntervalKind.FifteenMinutes;

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);
            var expectedLastWriteTime = new DateTime(2026, 7, 8, 1, 2, 3, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(paths.ConfigPath, expectedLastWriteTime);

            var report = await CommandLineActions.CreateHealthReportAsync(
                CancellationToken.None,
                paths,
                new FakeCliEnvironmentService());

            Assert.Contains("Refresh interval: FifteenMinutes", report, StringComparison.Ordinal);
            Assert.Equal(expectedLastWriteTime, File.GetLastWriteTimeUtc(paths.ConfigPath));
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
    public async Task CreateHealthReportAsync_PassesProviderCliOverridesToCliHealthChecks()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        var codex = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));
        var claudeCode = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.ClaudeCode));
        codex.Cli.CommandPathOverride = @" C:\Tools\codex.cmd ";
        claudeCode.Cli.CommandPathOverride = @"C:\Tools\claude.cmd";
        var cliEnvironmentService = new FakeCliEnvironmentService();

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var report = await CommandLineActions.CreateHealthReportAsync(
                CancellationToken.None,
                paths,
                cliEnvironmentService);

            var commands = cliEnvironmentService.LastCommands;
            Assert.Contains("CLI environment", report, StringComparison.Ordinal);
            Assert.NotNull(commands);
            Assert.Equal(@"C:\Tools\codex.cmd", commands.Single(command => command.CommandName == "codex").CommandOverride);
            Assert.Equal(@"C:\Tools\claude.cmd", commands.Single(command => command.CommandName == "claude").CommandOverride);
            Assert.Null(commands.Single(command => command.CommandName == "gh").CommandOverride);
            Assert.Null(commands.Single(command => command.CommandName == "git").CommandOverride);
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
    public async Task CreateHealthReportAsync_IncludesSavedUpdateStatus()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Updates.CheckOnStartup = true;
        config.Updates.MinimumCheckIntervalHours = 3;
        config.Updates.DownloadAutomatically = true;
        config.Updates.InstallAutomatically = true;
        config.Updates.LastCheckedAt = new DateTimeOffset(2026, 7, 8, 9, 30, 0, TimeSpan.FromHours(9));
        config.Updates.LastStatus = "InstallLaunched";
        config.Updates.LastCurrentVersion = "0.1.4";
        config.Updates.LastLatestVersion = "0.1.5";
        config.Updates.LastReleasePageUrl = "https://example.test/releases/v0.1.5";
        config.Updates.LastInstallLaunchedVersion = "0.1.5";
        config.Updates.LastPackagePath = @"C:\Updates\WinAIUsageBar-0.1.5-win-x64.zip";
        config.Updates.LastInstallScriptPath = @"C:\Updates\install-1\apply-update.ps1";
        config.Updates.LastInstallResultPath = @"C:\Updates\install-1\install-result.json";
        config.Updates.LastInstallResultStatus = "Succeeded";
        config.Updates.LastInstallResultMessage = "Update installed successfully.";
        config.Updates.LastInstallResultCompletedAt = new DateTimeOffset(2026, 7, 8, 9, 32, 0, TimeSpan.FromHours(9));
        config.Updates.LastMessage = "Install script launched.";

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var report = await CommandLineActions.CreateHealthReportAsync(
                CancellationToken.None,
                paths,
                new FakeCliEnvironmentService());

            Assert.Contains("Updates", report, StringComparison.Ordinal);
            Assert.Contains("Startup interval: at most every 3 hour(s)", report, StringComparison.Ordinal);
            Assert.Contains("Automatic download: On", report, StringComparison.Ordinal);
            Assert.Contains("Automatic install launch: On", report, StringComparison.Ordinal);
            Assert.Contains("Last checked: 2026-07-08 09:30:00 +09:00", report, StringComparison.Ordinal);
            Assert.Contains("Status: InstallLaunched", report, StringComparison.Ordinal);
            Assert.Contains("Current version: 0.1.4", report, StringComparison.Ordinal);
            Assert.Contains("Latest version: 0.1.5", report, StringComparison.Ordinal);
            Assert.Contains("Release page: https://example.test/releases/v0.1.5", report, StringComparison.Ordinal);
            Assert.Contains("Last launched install: 0.1.5", report, StringComparison.Ordinal);
            Assert.Contains(@"Package path: C:\Updates\WinAIUsageBar-0.1.5-win-x64.zip", report, StringComparison.Ordinal);
            Assert.Contains(@"Install script: C:\Updates\install-1\apply-update.ps1", report, StringComparison.Ordinal);
            Assert.Contains(@"Install result: C:\Updates\install-1\install-result.json", report, StringComparison.Ordinal);
            Assert.Contains("Install result status: Succeeded", report, StringComparison.Ordinal);
            Assert.Contains("Install result completed: 2026-07-08 09:32:00 +09:00", report, StringComparison.Ordinal);
            Assert.Contains("Install result message: Update installed successfully.", report, StringComparison.Ordinal);
            Assert.Contains("Message: Install script launched.", report, StringComparison.Ordinal);
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
    public async Task CreateHealthReportAsync_ReconcilesPendingInstallResult()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        config.Updates.LastInstallScriptPath = Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1");
        config.Updates.LastInstallResultPath = resultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, """
        {
          "status": "Succeeded",
          "message": "Installed with token-secret-123",
          "completedAtUtc": "2026-07-08T00:32:00Z"
        }
        """);

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);

            var report = await CommandLineActions.CreateHealthReportAsync(
                CancellationToken.None,
                paths,
                new FakeCliEnvironmentService());
            var reloaded = await configStore.LoadAsync(CancellationToken.None);

            Assert.Contains("Install result status: Succeeded", report, StringComparison.Ordinal);
            Assert.Contains("Install result message: Installed with [REDACTED]", report, StringComparison.Ordinal);
            Assert.DoesNotContain("token-secret-123", report, StringComparison.Ordinal);
            Assert.Equal("Succeeded", reloaded.Updates.LastInstallResultStatus);
            Assert.Equal("Installed with [REDACTED]", reloaded.Updates.LastInstallResultMessage);
            Assert.Equal(
                new DateTimeOffset(2026, 7, 8, 0, 32, 0, TimeSpan.Zero),
                reloaded.Updates.LastInstallResultCompletedAt);
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
        var paths = TestPaths();
        var updateCheck = new FakeUpdateCheckService(new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.2",
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
                128),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-setup.exe",
                new Uri("https://example.test/setup.exe"),
                4096),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-setup.exe.sha256",
                new Uri("https://example.test/setup.exe.sha256"),
                128)));

        try
        {
            var result = await CommandLineActions.CheckForUpdatesAsync(
                new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0+local"),
                updateCheck,
                CancellationToken.None,
                currentVersionOverride: "0.1.2",
                paths);
            var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("0.1.2", updateCheck.LastCurrentVersion);
            Assert.Contains("Update check", result.Output, StringComparison.Ordinal);
            Assert.Contains("Status: UpdateAvailable", result.Output, StringComparison.Ordinal);
            Assert.Contains("Current version: 0.1.2", result.Output, StringComparison.Ordinal);
            Assert.Contains("Latest version: 0.2.0", result.Output, StringComparison.Ordinal);
            Assert.Contains("Update available: yes", result.Output, StringComparison.Ordinal);
            Assert.Contains("WinAIUsageBar-0.2.0-win-x64.zip", result.Output, StringComparison.Ordinal);
            Assert.Contains("package.zip.sha256", result.Output, StringComparison.Ordinal);
            Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
            Assert.Equal("https://example.test/releases/v0.2.0", config.Updates.LastReleasePageUrl);
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", config.Updates.LastPackageAssetName);
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", config.Updates.LastPackageChecksumAssetName);
            Assert.Equal("WinAIUsageBar-0.2.0-setup.exe", config.Updates.LastInstallerAssetName);
            Assert.Equal("WinAIUsageBar-0.2.0-setup.exe.sha256", config.Updates.LastInstallerChecksumAssetName);
            Assert.Null(config.Updates.LastStatus);
            Assert.Null(config.Updates.LastCurrentVersion);
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
        var updateCheck = new FakeUpdateCheckService(AvailableUpdate());
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "downloaded",
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"),
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('a', 64)));

        var result = await CommandLineActions.DownloadUpdateAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            updateCheck,
            downloader,
            paths,
            CancellationToken.None);
        var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("0.1.0", updateCheck.LastCurrentVersion);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Contains("Update download", result.Output, StringComparison.Ordinal);
        Assert.Contains("Download status: Downloaded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Package path:", result.Output, StringComparison.Ordinal);
        Assert.Equal("Downloaded", config.Updates.LastStatus);
        Assert.Equal("downloaded", config.Updates.LastMessage);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", config.Updates.LastPackageAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", config.Updates.LastPackageChecksumAssetName);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"), config.Updates.LastPackagePath);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"), config.Updates.LastPackageChecksumPath);
        Assert.NotNull(config.Updates.LastCheckedAt);
    }

    [Fact]
    public async Task DownloadUpdateAsync_DoesNotPersistDogfoodDownloadPaths()
    {
        var paths = TestPaths();
        var updateCheck = new FakeUpdateCheckService(AvailableUpdate());
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "downloaded",
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"),
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('a', 64)));

        var result = await CommandLineActions.DownloadUpdateAsync(
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            updateCheck,
            downloader,
            paths,
            CancellationToken.None,
            currentVersionOverride: "0.1.2");
        var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("0.1.2", updateCheck.LastCurrentVersion);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Null(config.Updates.LastStatus);
        Assert.Null(config.Updates.LastMessage);
        Assert.Null(config.Updates.LastCurrentVersion);
        Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", config.Updates.LastPackageAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", config.Updates.LastPackageChecksumAssetName);
        Assert.Null(config.Updates.LastPackagePath);
        Assert.Null(config.Updates.LastPackageChecksumPath);
        Assert.NotNull(config.Updates.LastCheckedAt);
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
        var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Contains("Download status: Skipped", result.Output, StringComparison.Ordinal);
        Assert.Equal("NoUpdate", config.Updates.LastStatus);
        Assert.Equal("The current app version is up to date.", config.Updates.LastMessage);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Null(config.Updates.LastPackagePath);
        Assert.Null(config.Updates.LastPackageChecksumPath);
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
        var config = await new JsonAppConfigStore(paths).LoadAsync(CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Contains("Download status: ChecksumMismatch", result.Output, StringComparison.Ordinal);
        Assert.Equal("DownloadFailed", config.Updates.LastStatus);
        Assert.Equal("hash mismatch", config.Updates.LastMessage);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Null(config.Updates.LastPackagePath);
        Assert.Null(config.Updates.LastPackageChecksumPath);
    }

    [Fact]
    public async Task PrepareUpdateInstallAsync_UsesDefaultInstallDirectoryWhenNotProvided()
    {
        var service = new FakeUpdateInstallPreparationService(new UpdateInstallPreparationResult(
            UpdateInstallPreparationStatus.Prepared,
            "prepared",
            ScriptPath: @"C:\Updates\apply-update.ps1",
            Command: @"powershell -ExecutionPolicy Bypass -File C:\Updates\apply-update.ps1",
            PackagePath: @"C:\Updates\update.zip",
            InstallDirectory: @"C:\App",
            StagingDirectory: @"C:\Updates\staging",
            BackupDirectory: @"C:\Updates\backup")
        {
            ResultPath = @"C:\Updates\install-result.json"
        });

        var result = await CommandLineActions.PrepareUpdateInstallAsync(
            new CommandLinePrepareUpdateInstallOptions(
                @"C:\Updates\update.zip",
                InstallDirectory: null,
                RestartAfterInstall: true),
            service,
            defaultInstallDirectory: @"C:\App",
            processIdToWait: 4321,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, service.PrepareCount);
        Assert.Equal(@"C:\Updates\update.zip", service.LastRequest?.PackagePath);
        Assert.Equal(@"C:\App", service.LastRequest?.InstallDirectory);
        Assert.Equal(4321, service.LastRequest?.ProcessIdToWait);
        Assert.True(service.LastRequest?.RestartAfterInstall);
        Assert.Contains("Status: Prepared", result.Output, StringComparison.Ordinal);
        Assert.Contains("Script: C:\\Updates\\apply-update.ps1", result.Output, StringComparison.Ordinal);
        Assert.Contains("Result: C:\\Updates\\install-result.json", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareUpdateInstallAsync_ReturnsNonZeroWhenPreparationFails()
    {
        var service = new FakeUpdateInstallPreparationService(new UpdateInstallPreparationResult(
            UpdateInstallPreparationStatus.InvalidPackage,
            "bad package",
            ScriptPath: null,
            Command: null,
            PackagePath: null,
            InstallDirectory: null,
            StagingDirectory: null,
            BackupDirectory: null));

        var result = await CommandLineActions.PrepareUpdateInstallAsync(
            new CommandLinePrepareUpdateInstallOptions(
                @"C:\Updates\bad.zip",
                InstallDirectory: @"C:\App",
                RestartAfterInstall: false),
            service,
            defaultInstallDirectory: @"C:\Default",
            processIdToWait: 0,
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status: InvalidPackage", result.Output, StringComparison.Ordinal);
        Assert.Contains("bad package", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaunchPreparedUpdateAsync_FormatsLaunchResult()
    {
        var service = new FakeUpdateInstallLaunchService(new UpdateInstallLaunchResult(
            UpdateInstallLaunchStatus.Launched,
            "launched",
            ScriptPath: @"C:\Updates\install-1\apply-update.ps1",
            Command: @"powershell -NoProfile -ExecutionPolicy Bypass -File C:\Updates\install-1\apply-update.ps1",
            ProcessId: 4321));

        var result = await CommandLineActions.LaunchPreparedUpdateAsync(
            new CommandLineLaunchPreparedUpdateOptions(@"C:\Updates\install-1\apply-update.ps1"),
            service,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, service.LaunchCount);
        Assert.Equal(@"C:\Updates\install-1\apply-update.ps1", service.LastRequest?.ScriptPath);
        Assert.Contains("Update install launch", result.Output, StringComparison.Ordinal);
        Assert.Contains("Status: Launched", result.Output, StringComparison.Ordinal);
        Assert.Contains("Process ID: 4321", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaunchPreparedUpdateAsync_ReturnsNonZeroWhenLaunchFails()
    {
        var service = new FakeUpdateInstallLaunchService(new UpdateInstallLaunchResult(
            UpdateInstallLaunchStatus.InvalidScript,
            "bad script",
            ScriptPath: null,
            Command: null,
            ProcessId: null));

        var result = await CommandLineActions.LaunchPreparedUpdateAsync(
            new CommandLineLaunchPreparedUpdateOptions(@"C:\Updates\not-update.ps1"),
            service,
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status: InvalidScript", result.Output, StringComparison.Ordinal);
        Assert.Contains("bad script", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstallLatestUpdateAsync_FormatsSkippedResult()
    {
        var service = new FakeLatestUpdateInstallService(new LatestUpdateInstallResult(
            LatestUpdateInstallStatus.SkippedNoUpdate,
            "up to date",
            new ReleaseUpdateCheckResult(
                UpdateCheckStatus.UpToDate,
                CurrentVersion: "0.1.0",
                LatestVersion: "0.1.0",
                "The current app version is up to date.",
                IsUpdateAvailable: false,
                ReleasePageUrl: null,
                Package: null,
                Checksum: null),
            Download: null,
            Preparation: null,
            Launch: null));

        var result = await CommandLineActions.InstallLatestUpdateAsync(
            new CommandLineInstallLatestUpdateOptions(
                RestartAfterInstall: true,
                CurrentVersionOverride: "0.1.2"),
            service,
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            installDirectory: @"C:\App",
            processIdToWait: 777,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, service.InstallCount);
        Assert.Equal("0.1.2", service.LastRequest?.CurrentVersion);
        Assert.Equal(@"C:\App", service.LastRequest?.InstallDirectory);
        Assert.Equal(777, service.LastRequest?.ProcessIdToWait);
        Assert.True(service.LastRequest?.RestartAfterInstall);
        Assert.Contains("Latest update install", result.Output, StringComparison.Ordinal);
        Assert.Contains("Status: SkippedNoUpdate", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstallLatestUpdateAsync_ReturnsNonZeroForFailures()
    {
        var service = new FakeLatestUpdateInstallService(new LatestUpdateInstallResult(
            LatestUpdateInstallStatus.DownloadFailed,
            "hash mismatch",
            UpdateCheck: null,
            Download: null,
            Preparation: null,
            Launch: null));

        var result = await CommandLineActions.InstallLatestUpdateAsync(
            new CommandLineInstallLatestUpdateOptions(RestartAfterInstall: false),
            service,
            new AppInfo("WinAI Usage Bar", "0.1.0.0", "0.1.0"),
            installDirectory: @"C:\App",
            processIdToWait: 777,
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status: DownloadFailed", result.Output, StringComparison.Ordinal);
        Assert.Contains("hash mismatch", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunStartupUpdateAsync_FormatsDisabledResultWithCurrentVersion()
    {
        var service = new FakeStartupUpdateService(new StartupUpdateResult(
            StartupUpdateStatus.Disabled,
            "Startup update check is disabled.",
            LatestVersion: null,
            PackagePath: null,
            InstallScriptPath: null));

        var result = await CommandLineActions.RunStartupUpdateAsync(
            service,
            new AppInfo("WinAI Usage Bar", "0.1.4.0", "0.1.4"),
            installDirectory: @"C:\App",
            processIdToWait: 1234,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, service.RunCount);
        Assert.Equal("0.1.4", service.LastRequest?.CurrentVersion);
        Assert.Equal(@"C:\App", service.LastRequest?.InstallDirectory);
        Assert.Equal(1234, service.LastRequest?.ProcessIdToWait);
        Assert.Contains("Startup update check", result.Output, StringComparison.Ordinal);
        Assert.Contains("Status: Disabled", result.Output, StringComparison.Ordinal);
        Assert.Contains("Current version: 0.1.4", result.Output, StringComparison.Ordinal);
        Assert.Contains("Latest version: n/a", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunStartupUpdateAsync_FormatsInstallLaunchedResult()
    {
        var service = new FakeStartupUpdateService(new StartupUpdateResult(
            StartupUpdateStatus.InstallLaunched,
            "Update package downloaded, verified, prepared, and launched for install.",
            LatestVersion: "0.2.0",
            PackagePath: @"C:\Updates\WinAIUsageBar-0.2.0-win-x64.zip",
            InstallScriptPath: @"C:\Updates\install-1\apply-update.ps1")
        {
            InstallResultPath = @"C:\Updates\install-1\install-result.json"
        });

        var result = await CommandLineActions.RunStartupUpdateAsync(
            service,
            new AppInfo("WinAI Usage Bar", "0.1.4.0", "0.1.4"),
            installDirectory: @"C:\App",
            processIdToWait: 1234,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: InstallLaunched", result.Output, StringComparison.Ordinal);
        Assert.Contains("Latest version: 0.2.0", result.Output, StringComparison.Ordinal);
        Assert.Contains(@"Package path: C:\Updates\WinAIUsageBar-0.2.0-win-x64.zip", result.Output, StringComparison.Ordinal);
        Assert.Contains(@"Install script: C:\Updates\install-1\apply-update.ps1", result.Output, StringComparison.Ordinal);
        Assert.Contains(@"Install result: C:\Updates\install-1\install-result.json", result.Output, StringComparison.Ordinal);
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
        public IReadOnlyList<CliCommandCheck>? LastCommands { get; private set; }

        public Task<CliEnvironmentReport> GetReportAsync(
            IReadOnlyList<CliCommandCheck> commands,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCommands = commands;
            var statuses = commands
                .Select(command => new CliCommandStatus(
                    command.CommandName,
                    IsFound: command.CommandOverride is not null,
                    Paths: command.CommandOverride is null ? [] : [command.CommandOverride],
                    CanStart: command.CommandOverride is null ? null : true,
                    ExitCode: command.CommandOverride is null ? null : 0,
                    TimedOut: false,
                    StatusMessage: command.CommandOverride is null ? "Not found on PATH." : "ok",
                    LaunchTarget: command.CommandOverride,
                    UsesCommandProcessor: false,
                    UsesConfiguredOverride: command.CommandOverride is not null))
                .ToList();
            return Task.FromResult(new CliEnvironmentReport(statuses));
        }
    }

    private sealed class FakeUpdateCheckService(ReleaseUpdateCheckResult result) : IReleaseUpdateCheckService
    {
        public string? LastCurrentVersion { get; private set; }

        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCurrentVersion = currentVersion;
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

    private sealed class FakeUpdateInstallPreparationService(
        UpdateInstallPreparationResult result) : IUpdateInstallPreparationService
    {
        public int PrepareCount { get; private set; }

        public UpdateInstallPreparationRequest? LastRequest { get; private set; }

        public Task<UpdateInstallPreparationResult> PrepareAsync(
            UpdateInstallPreparationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUpdateInstallLaunchService(
        UpdateInstallLaunchResult result) : IUpdateInstallLaunchService
    {
        public int LaunchCount { get; private set; }

        public UpdateInstallLaunchRequest? LastRequest { get; private set; }

        public Task<UpdateInstallLaunchResult> LaunchAsync(
            UpdateInstallLaunchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeLatestUpdateInstallService(
        LatestUpdateInstallResult result) : ILatestUpdateInstallService
    {
        public int InstallCount { get; private set; }

        public LatestUpdateInstallRequest? LastRequest { get; private set; }

        public Task<LatestUpdateInstallResult> InstallLatestAsync(
            LatestUpdateInstallRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeStartupUpdateService(StartupUpdateResult result) : IStartupUpdateService
    {
        public int RunCount { get; private set; }

        public StartupUpdateRequest? LastRequest { get; private set; }

        public Task<StartupUpdateResult> RunAsync(
            StartupUpdateRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
