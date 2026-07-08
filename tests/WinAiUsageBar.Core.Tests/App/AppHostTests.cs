using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Updates;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class AppHostTests
{
    [Fact]
    public async Task InitializeAsync_UsesInjectedServicesWithoutRealTrayOrProviders()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([Snapshot(ProviderId.Codex, "Codex", 75)]);
        var tray = new FakeTrayIconService();
        var diagnostics = new RecordingDiagnosticsLog();
        var windowActivator = new FakeWindowActivator();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                windowActivator,
                new FakeExitService()));

        await host.InitializeAsync(CancellationToken.None);
        refreshService.Emit([Snapshot(ProviderId.ChatGPT, "ChatGPT", 64)]);

        Assert.True(refreshService.InitializeCalled);
        Assert.Single(host.ViewModel.Providers);
        Assert.Equal(ProviderId.ChatGPT, host.ViewModel.Providers[0].ProviderId);
        Assert.Contains("ChatGPT", tray.Tooltip, StringComparison.Ordinal);
        Assert.Contains("WinAI Usage Bar initialized.", diagnostics.InfoMessages);

        await host.DisposeAsync();

        Assert.True(tray.Disposed);
        Assert.True(refreshService.Disposed);
        Assert.True(windowActivator.Disposed);
    }

    [Fact]
    public async Task TrayCommands_RouteToInjectedWindowAndExitServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([]);
        var tray = new FakeTrayIconService();
        var windowActivator = new FakeWindowActivator();
        var exitService = new FakeExitService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                windowActivator,
                exitService));

        await host.InitializeAsync(CancellationToken.None);

        tray.RaiseShow();
        tray.RaiseSettings();
        tray.RaiseShowWidget();
        tray.RaiseExit();

        Assert.Equal(1, windowActivator.ShowCompactCount);
        Assert.Equal(1, windowActivator.ShowSettingsCount);
        Assert.Equal(1, windowActivator.ShowWidgetCount);
        Assert.Equal(1, exitService.ExitCount);
    }

    [Fact]
    public async Task TrayRefreshCommand_RunsRefreshThroughInjectedService()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([]);
        var tray = new FakeTrayIconService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.InitializeAsync(CancellationToken.None);
        tray.RaiseRefreshNow();

        await refreshService.RefreshObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, refreshService.RefreshCount);
    }

    [Fact]
    public async Task RestartRefreshScheduleAsync_UsesInjectedRefreshService()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var refreshService = new FakeRefreshService([]);
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                refreshService,
                new FakeTrayIconService(),
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.RestartRefreshScheduleAsync(CancellationToken.None);

        Assert.Equal(1, refreshService.RestartCount);
    }

    [Fact]
    public async Task StartAsync_AppliesStartupRegistrationWhenConfigRequestsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var config = AppConfig.CreateDefault();
        config.Startup.LaunchOnLogin = true;
        var startup = new FakeStartupRegistrationService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(config),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                startup,
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.StartAsync(CancellationToken.None);

        Assert.True(startup.LastSetEnabled);
    }

    [Fact]
    public async Task StartAsync_RunsStartupUpdateServiceInBackground()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var diagnostics = new RecordingDiagnosticsLog();
        var startupUpdate = new FakeStartupUpdateService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                StartupUpdateService: startupUpdate));

        await host.StartAsync(CancellationToken.None);
        await startupUpdate.Observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForAsync(() => diagnostics.InfoMessages.Any(message => message.StartsWith(
            "Startup update result: NoUpdate - ",
            StringComparison.Ordinal)));

        Assert.Equal(1, startupUpdate.RunCount);
        Assert.False(string.IsNullOrWhiteSpace(startupUpdate.LastRequest?.CurrentVersion));
        Assert.False(string.IsNullOrWhiteSpace(startupUpdate.LastRequest?.InstallDirectory));
        Assert.True(startupUpdate.LastRequest?.ProcessIdToWait > 0);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith(
            "Startup update result: NoUpdate - ",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckForUpdatesNowAsync_RecordsUpdateStatusAndLogsResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var config = AppConfig.CreateDefault();
        var diagnostics = new RecordingDiagnosticsLog();
        var updateCheck = new FakeReleaseUpdateCheckService(AvailableUpdateCheck());
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(config),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                UpdateCheckService: updateCheck));

        var result = await host.CheckForUpdatesNowAsync(CancellationToken.None);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(1, updateCheck.CheckCount);
        Assert.Equal("UpdateAvailable", config.Updates.LastStatus);
        Assert.Equal("A newer GitHub release is available.", config.Updates.LastMessage);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
        Assert.Equal("https://example.test/releases/v0.2.0", config.Updates.LastReleasePageUrl);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", config.Updates.LastPackageAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", config.Updates.LastPackageChecksumAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-setup.exe", config.Updates.LastInstallerAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-setup.exe.sha256", config.Updates.LastInstallerChecksumAssetName);
        Assert.NotNull(config.Updates.LastCheckedAt);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith(
            "Manual update check result: UpdateAvailable - ",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallLatestUpdateNowAsync_RecordsInstallStatusAndLogsResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var config = AppConfig.CreateDefault();
        var diagnostics = new RecordingDiagnosticsLog();
        var latestInstall = new FakeLatestUpdateInstallService(LaunchedLatestUpdate(paths));
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(config),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                LatestUpdateInstallService: latestInstall));

        var result = await host.InstallLatestUpdateNowAsync(
            restartAfterInstall: true,
            CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.Launched, result.Status);
        Assert.Equal(1, latestInstall.InstallCount);
        Assert.True(latestInstall.LastRequest?.RestartAfterInstall);
        Assert.False(string.IsNullOrWhiteSpace(latestInstall.LastRequest?.CurrentVersion));
        Assert.False(string.IsNullOrWhiteSpace(latestInstall.LastRequest?.InstallDirectory));
        Assert.True(latestInstall.LastRequest?.ProcessIdToWait > 0);
        Assert.Equal("InstallLaunched", config.Updates.LastStatus);
        Assert.Equal("Latest update install script launched.", config.Updates.LastMessage);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
        Assert.Equal("https://example.test/releases/v0.2.0", config.Updates.LastReleasePageUrl);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", config.Updates.LastPackageAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", config.Updates.LastPackageChecksumAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-setup.exe", config.Updates.LastInstallerAssetName);
        Assert.Equal("WinAIUsageBar-0.2.0-setup.exe.sha256", config.Updates.LastInstallerChecksumAssetName);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"), config.Updates.LastPackagePath);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"), config.Updates.LastPackageChecksumPath);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"), config.Updates.LastInstallScriptPath);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json"), config.Updates.LastInstallResultPath);
        Assert.Equal("0.2.0", config.Updates.LastInstallLaunchedVersion);
        Assert.NotNull(config.Updates.LastCheckedAt);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith(
            "Manual latest update install result: Launched - ",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task SecretMethods_UseInjectedSecretStoreWithoutLoggingValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var secrets = new FakeSecretStore();
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                secrets,
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.SetSecretAsync("  gemini-api-key  ", "sample-test-secret", CancellationToken.None);
        var existsAfterSave = await host.HasSecretAsync("gemini-api-key", CancellationToken.None);
        await host.DeleteSecretAsync("gemini-api-key", CancellationToken.None);
        var existsAfterDelete = await host.HasSecretAsync("gemini-api-key", CancellationToken.None);

        Assert.True(existsAfterSave);
        Assert.False(existsAfterDelete);
        Assert.DoesNotContain(diagnostics.InfoMessages, message => message.Contains("sample-test-secret", StringComparison.Ordinal));
        Assert.Equal("gemini-api-key", secrets.LastSetName);
    }

    [Fact]
    public async Task DataMaintenanceMethods_UseInjectedService()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var maintenance = new FakeDataMaintenanceService(paths);
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                maintenance,
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService()));

        var snapshotsResult = await host.ClearSnapshotsAsync(CancellationToken.None);
        var historyResult = await host.ClearHistoryAsync(CancellationToken.None);
        var backupResult = await host.ExportConfigBackupAsync(CancellationToken.None);
        var pruneBackupsResult = await host.PruneConfigBackupsAsync(5, CancellationToken.None);
        var pruneExportsResult = await host.PruneDiagnosticsExportsAsync(5, CancellationToken.None);
        var pruneCrashReportsResult = await host.PruneCrashReportsAsync(5, CancellationToken.None);

        Assert.Equal(1, maintenance.ClearSnapshotsCount);
        Assert.Equal(1, maintenance.ClearHistoryCount);
        Assert.Equal(1, maintenance.ExportConfigBackupCount);
        Assert.Equal(1, maintenance.PruneConfigBackupsCount);
        Assert.Equal(1, maintenance.PruneDiagnosticsExportsCount);
        Assert.Equal(1, maintenance.PruneCrashReportsCount);
        Assert.Equal(paths.SnapshotsPath, snapshotsResult.Path);
        Assert.Equal(paths.HistoryPath, historyResult.Path);
        Assert.Equal(Path.Combine(paths.ConfigBackupsDirectory, "config-backup.json"), backupResult.Path);
        Assert.Equal(paths.ConfigBackupsDirectory, pruneBackupsResult.DirectoryPath);
        Assert.Equal(paths.DiagnosticsExportsDirectory, pruneExportsResult.DirectoryPath);
        Assert.Equal(paths.CrashReportsDirectory, pruneCrashReportsResult.DirectoryPath);
        Assert.Contains("Snapshot cache cleared.", diagnostics.InfoMessages);
        Assert.Contains("History file cleared.", diagnostics.InfoMessages);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith("Config backup exported to ", StringComparison.Ordinal));
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith("Config backups pruned: ", StringComparison.Ordinal));
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith("Diagnostics exports pruned: ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfigBackupRecoveryMethods_UseLatestBackupAndRestartAfterRestore()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var latestBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "config-backup.json");
        var validation = new FakeConfigBackupValidationService();
        var restore = new FakeConfigBackupRestoreService(paths);
        var refresh = new FakeRefreshService([]);
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                refresh,
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths, latestBackupPath),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                validation,
                restore));

        var validationResult = await host.ValidateLatestConfigBackupAsync(CancellationToken.None);
        var restoreResult = await host.RestoreLatestConfigBackupAsync(CancellationToken.None);

        Assert.True(validationResult.IsValid);
        Assert.True(restoreResult.Restored);
        Assert.Equal(latestBackupPath, validation.LastPath);
        Assert.Equal(latestBackupPath, restore.LastPath);
        Assert.Equal(1, refresh.RestartCount);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith("Config backup validated: ", StringComparison.Ordinal));
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith("Config backup restored from ", StringComparison.Ordinal));
        Assert.Contains("config-backup-before-restore.json", restoreResult.RollbackBackupPath);
    }

    [Fact]
    public async Task ConfigBackupRecoveryMethods_ReturnErrorsWhenNoBackupExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var validation = new FakeConfigBackupValidationService();
        var restore = new FakeConfigBackupRestoreService(paths);
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                validation,
                restore));

        var validationResult = await host.ValidateLatestConfigBackupAsync(CancellationToken.None);
        var restoreResult = await host.RestoreLatestConfigBackupAsync(CancellationToken.None);

        Assert.False(validationResult.IsValid);
        Assert.False(restoreResult.Restored);
        Assert.Contains("No config backup", validationResult.Errors.Single(), StringComparison.Ordinal);
        Assert.Contains("No config backup", restoreResult.Errors.Single(), StringComparison.Ordinal);
        Assert.Equal(0, validation.CallCount);
        Assert.Equal(0, restore.CallCount);
    }

    [Fact]
    public async Task RestoreLatestConfigBackupAsync_ReturnsRestoredWhenRefreshRestartFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var latestBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "config-backup.json");
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([], throwOnRestart: true),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths, latestBackupPath),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                new FakeConfigBackupValidationService(),
                new FakeConfigBackupRestoreService(paths)));

        var restoreResult = await host.RestoreLatestConfigBackupAsync(CancellationToken.None);

        Assert.True(restoreResult.Restored);
        Assert.Contains(diagnostics.ErrorMessages, message => message.StartsWith(
            "Config backup restored, but refresh schedule restart failed.",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetConfigToDefaultsAsync_UsesResetServiceAndRestartsRefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var reset = new FakeConfigResetService(paths);
        var refresh = new FakeRefreshService([]);
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                refresh,
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                new FakeConfigBackupValidationService(),
                new FakeConfigBackupRestoreService(paths),
                reset));

        var result = await host.ResetConfigToDefaultsAsync(CancellationToken.None);

        Assert.True(result.Reset);
        Assert.Equal(1, reset.CallCount);
        Assert.Equal(1, refresh.RestartCount);
        Assert.Contains(diagnostics.InfoMessages, message => message.StartsWith(
            "Config reset to defaults; rollback saved to ",
            StringComparison.Ordinal));
        Assert.Contains("Refresh schedule restarted after config reset.", diagnostics.InfoMessages);
    }

    [Fact]
    public async Task ResetConfigToDefaultsAsync_ReturnsResetWhenRefreshRestartFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var reset = new FakeConfigResetService(paths);
        var diagnostics = new RecordingDiagnosticsLog();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(AppConfig.CreateDefault()),
                new FakeRefreshService([], throwOnRestart: true),
                new FakeTrayIconService(),
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeDiagnosticsSummaryService(paths),
                new FakeHistorySummaryService(),
                new FakeDataMaintenanceService(paths),
                new FakeSecretStore(),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService(),
                new FakeConfigBackupValidationService(),
                new FakeConfigBackupRestoreService(paths),
                reset));

        var result = await host.ResetConfigToDefaultsAsync(CancellationToken.None);

        Assert.True(result.Reset);
        Assert.Equal(1, reset.CallCount);
        Assert.Contains(diagnostics.ErrorMessages, message => message.StartsWith(
            "Config reset completed, but refresh schedule restart failed.",
            StringComparison.Ordinal));
    }

    private static UsageSnapshot Snapshot(ProviderId providerId, string displayName, double remainingPercent)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            ProviderHealth.Ok,
            Identity: null,
            new UsageWindow("Test", 100 - remainingPercent, remainingPercent, null, "reset later", "%", null, null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Mock,
            DateTimeOffset.Now,
            "test snapshot",
            ErrorMessage: null);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static ReleaseUpdateCheckResult AvailableUpdateCheck()
    {
        return new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.0",
            LatestVersion: "0.2.0",
            "A newer GitHub release is available.",
            IsUpdateAvailable: true,
            ReleasePageUrl: new Uri("https://example.test/releases/v0.2.0"),
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
                128));
    }

    private static LatestUpdateInstallResult LaunchedLatestUpdate(AppDataPaths paths)
    {
        return new LatestUpdateInstallResult(
            LatestUpdateInstallStatus.Launched,
            "Latest update install script launched.",
            AvailableUpdateCheck(),
            new UpdateDownloadResult(
                UpdateDownloadStatus.Downloaded,
                "downloaded",
                Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
                Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"),
                ExpectedSha256: new string('a', 64),
                ActualSha256: new string('a', 64)),
            new UpdateInstallPreparationResult(
                UpdateInstallPreparationStatus.Prepared,
                "prepared",
                Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"),
                @"powershell -ExecutionPolicy Bypass -File apply-update.ps1",
                Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
                AppContext.BaseDirectory,
                Path.Combine(paths.UpdatesDirectory, "install-1", "staging"),
                Path.Combine(paths.UpdatesDirectory, "install-1", "backup"))
            {
                ResultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json")
            },
            new UpdateInstallLaunchResult(
                UpdateInstallLaunchStatus.Launched,
                "launched",
                Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"),
                @"powershell -NoProfile -ExecutionPolicy Bypass -File apply-update.ps1",
                ProcessId: 4321));
    }

    private sealed class ImmediateDispatcher : IAppDispatcher
    {
        public bool TryEnqueue(Action action)
        {
            action();
            return true;
        }
    }

    private sealed class InMemoryConfigStore(AppConfig config) : IAppConfigStore
    {
        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(config);
        }

        public Task SaveAsync(AppConfig nextConfig, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            config = nextConfig;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshService(
        IReadOnlyList<UsageSnapshot> currentSnapshots,
        bool throwOnRestart = false) : IUsageRefreshService
    {
        public event EventHandler<IReadOnlyList<UsageSnapshot>>? SnapshotsChanged;

        public IReadOnlyList<UsageSnapshot> CurrentSnapshots { get; private set; } = currentSnapshots;

        public bool InitializeCalled { get; private set; }

        public bool Disposed { get; private set; }

        public int RefreshCount { get; private set; }

        public int RestartCount { get; private set; }

        public TaskCompletionSource RefreshObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCalled = true;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RestartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestartCount++;
            if (throwOnRestart)
            {
                throw new InvalidOperationException("restart failed");
            }

            return Task.CompletedTask;
        }

        public Task RefreshNowAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            RefreshObserved.TrySetResult();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public void Emit(IReadOnlyList<UsageSnapshot> snapshots)
        {
            CurrentSnapshots = snapshots;
            SnapshotsChanged?.Invoke(this, snapshots);
        }
    }

    private sealed class FakeTrayIconService : ITrayIconService
    {
        public event EventHandler? ShowRequested;

        public event EventHandler? ShowWidgetRequested;

        public event EventHandler? RefreshNowRequested;

        public event EventHandler? SettingsRequested;

        public event EventHandler? ExitRequested;

        public string Tooltip { get; private set; } = string.Empty;

        public bool Disposed { get; private set; }

        public void UpdateTooltip(string tooltip)
        {
            Tooltip = tooltip;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void RaiseShow()
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseShowWidget()
        {
            ShowWidgetRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseRefreshNow()
        {
            RefreshNowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSettings()
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseExit()
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingDiagnosticsLog : IAppDiagnosticsLog
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> ErrorMessages { get; } = [];

        public Task InfoAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InfoMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ErrorMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> secrets = [];

        public string? LastSetName { get; private set; }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSetName = name;
            secrets[name] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(secrets.GetValueOrDefault(name));
        }

        public Task<bool> HasSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(secrets.ContainsKey(name));
        }

        public Task DeleteSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            secrets.Remove(name);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiagnosticsExportService(AppDataPaths paths) : IDiagnosticsExportService
    {
        public Task<DiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DiagnosticsExportResult(
                Path.Combine(paths.RootDirectory, "diagnostics-export.txt"),
                DateTimeOffset.Now,
                ["test"]));
        }
    }

    private sealed class FakeDiagnosticsSummaryService(AppDataPaths paths, string? latestConfigBackupPath = null) : IDiagnosticsSummaryService
    {
        public Task<DiagnosticsSummary> GetSummaryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset? latestConfigBackupCreatedAt = latestConfigBackupPath is null
                ? null
                : DateTimeOffset.Now;
            return Task.FromResult(new DiagnosticsSummary(
                paths.RootDirectory,
                paths.ConfigPath,
                paths.SnapshotsPath,
                paths.HistoryPath,
                paths.DiagnosticsLogPath,
                paths.DiagnosticsExportsDirectory,
                paths.ConfigBackupsDirectory,
                ConfigVersion: 1,
                ConfiguredProviderCount: 7,
                EnabledProviderCount: 2,
                RefreshIntervalKind.FiveMinutes,
                NotificationsEnabled: true,
                CachedSnapshotCount: 0,
                LatestSnapshotUpdatedAt: null,
                ConfigBackupCount: latestConfigBackupPath is null ? 0 : 1,
                LatestConfigBackupPath: latestConfigBackupPath,
                LatestConfigBackupCreatedAt: latestConfigBackupCreatedAt,
                ConfigBackupTotalBytes: latestConfigBackupPath is null ? 0 : 1024,
                DiagnosticsExportCount: 0,
                LatestDiagnosticsExportPath: null,
                LatestDiagnosticsExportCreatedAt: null,
                DiagnosticsExportTotalBytes: 0,
                HistoryRetentionMaxDays: 30,
                HistoryRetentionMaxBytes: 5_000_000,
                new DiagnosticsFileSummary(paths.ConfigPath, Exists: false, SizeBytes: 0, LastWriteTime: null),
                new DiagnosticsFileSummary(paths.SnapshotsPath, Exists: false, SizeBytes: 0, LastWriteTime: null),
                new DiagnosticsFileSummary(paths.HistoryPath, Exists: false, SizeBytes: 0, LastWriteTime: null),
                new DiagnosticsFileSummary(paths.DiagnosticsLogPath, Exists: false, SizeBytes: 0, LastWriteTime: null)));
        }
    }

    private sealed class FakeConfigBackupValidationService : IConfigBackupValidationService
    {
        public int CallCount { get; private set; }

        public string? LastPath { get; private set; }

        public Task<ConfigBackupValidationResult> ValidateAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastPath = path;
            return Task.FromResult(new ConfigBackupValidationResult(
                path,
                IsValid: true,
                ConfigVersion: 1,
                ProviderCount: 7,
                EnabledProviderCount: 2,
                DefaultedProviderCount: 0,
                Errors: [],
                Warnings: []));
        }
    }

    private sealed class FakeConfigBackupRestoreService(AppDataPaths paths) : IConfigBackupRestoreService
    {
        public int CallCount { get; private set; }

        public string? LastPath { get; private set; }

        public Task<ConfigBackupRestoreResult> RestoreAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastPath = path;
            return Task.FromResult(new ConfigBackupRestoreResult(
                path,
                Restored: true,
                Path.Combine(paths.ConfigBackupsDirectory, "config-backup-before-restore.json"),
                ConfigVersion: 1,
                ProviderCount: 7,
                EnabledProviderCount: 2,
                Errors: [],
                Warnings: []));
        }
    }

    private sealed class FakeConfigResetService(AppDataPaths paths) : IConfigResetService
    {
        public int CallCount { get; private set; }

        public Task<ConfigResetResult> ResetToDefaultsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new ConfigResetResult(
                Reset: true,
                Path.Combine(paths.ConfigBackupsDirectory, "config-backup-before-reset.json"),
                ConfigVersion: 1,
                ProviderCount: 7,
                EnabledProviderCount: 2,
                Warnings: ["test warning"]));
        }
    }

    private sealed class FakeStartupUpdateService : IStartupUpdateService
    {
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCount { get; private set; }

        public StartupUpdateRequest? LastRequest { get; private set; }

        public Task<StartupUpdateResult> RunAsync(
            StartupUpdateRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunCount++;
            LastRequest = request;
            Observed.TrySetResult();
            return Task.FromResult(new StartupUpdateResult(
                StartupUpdateStatus.NoUpdate,
                "No update from fake service.",
                LatestVersion: "0.1.0",
                PackagePath: null,
                InstallScriptPath: null));
        }
    }

    private sealed class FakeReleaseUpdateCheckService(ReleaseUpdateCheckResult result) : IReleaseUpdateCheckService
    {
        public int CheckCount { get; private set; }

        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCount++;
            Assert.False(string.IsNullOrWhiteSpace(currentVersion));
            return Task.FromResult(result);
        }
    }

    private sealed class FakeLatestUpdateInstallService(LatestUpdateInstallResult result) : ILatestUpdateInstallService
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

    private sealed class FakeHistorySummaryService : IHistorySummaryService
    {
        public Task<HistorySummary> GetSummaryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HistorySummary(
                TotalEntries: 0,
                InvalidLines: 0,
                EarliestUpdatedAt: null,
                LatestUpdatedAt: null,
                Providers: []));
        }
    }

    private sealed class FakeDataMaintenanceService(AppDataPaths paths) : IDataMaintenanceService
    {
        public int ClearSnapshotsCount { get; private set; }

        public int ClearHistoryCount { get; private set; }

        public int ExportConfigBackupCount { get; private set; }

        public int PruneConfigBackupsCount { get; private set; }

        public int PruneDiagnosticsExportsCount { get; private set; }

        public int PruneCrashReportsCount { get; private set; }

        public Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearSnapshotsCount++;
            return Task.FromResult(new DataMaintenanceResult(
                paths.SnapshotsPath,
                true,
                DateTimeOffset.Now));
        }

        public Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearHistoryCount++;
            return Task.FromResult(new DataMaintenanceResult(
                paths.HistoryPath,
                true,
                DateTimeOffset.Now));
        }

        public Task<ConfigBackupResult> ExportConfigBackupAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportConfigBackupCount++;
            return Task.FromResult(new ConfigBackupResult(
                Path.Combine(paths.ConfigBackupsDirectory, "config-backup.json"),
                DateTimeOffset.Now));
        }

        public Task<DataPruneResult> PruneConfigBackupsAsync(int keepNewest, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruneConfigBackupsCount++;
            return Task.FromResult(new DataPruneResult(
                paths.ConfigBackupsDirectory,
                "config-backup-*.json",
                keepNewest,
                MatchedCount: 6,
                KeptCount: keepNewest,
                DeletedCount: 1,
                DeletedBytes: 1024,
                PrunedAt: DateTimeOffset.Now));
        }

        public Task<DataPruneResult> PruneDiagnosticsExportsAsync(int keepNewest, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruneDiagnosticsExportsCount++;
            return Task.FromResult(new DataPruneResult(
                paths.DiagnosticsExportsDirectory,
                "diagnostics-export-*.txt",
                keepNewest,
                MatchedCount: 6,
                KeptCount: keepNewest,
                DeletedCount: 1,
                DeletedBytes: 2048,
                PrunedAt: DateTimeOffset.Now));
        }

        public Task<DataPruneResult> PruneCrashReportsAsync(int keepNewest, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruneCrashReportsCount++;
            return Task.FromResult(new DataPruneResult(
                paths.CrashReportsDirectory,
                "crash-report-*.json",
                keepNewest,
                MatchedCount: 6,
                KeptCount: keepNewest,
                DeletedCount: 1,
                DeletedBytes: 4096,
                PrunedAt: DateTimeOffset.Now));
        }
    }

    private sealed class FakeStartupRegistrationService : IStartupRegistrationService
    {
        public bool? LastSetEnabled { get; private set; }

        public Task<StartupRegistrationStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new StartupRegistrationStatus(
                IsSupported: true,
                IsEnabled: LastSetEnabled == true,
                Command: null,
                "fake startup status"));
        }

        public Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSetEnabled = isEnabled;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWindowActivator : IAppWindowActivator
    {
        public int ShowCompactCount { get; private set; }

        public int ShowSettingsCount { get; private set; }

        public int ShowWidgetCount { get; private set; }

        public bool Disposed { get; private set; }

        public void ShowCompactPanel(AppHost host)
        {
            ShowCompactCount++;
        }

        public void ShowSettings(AppHost host)
        {
            ShowSettingsCount++;
        }

        public void ShowWidget(AppHost host)
        {
            ShowWidgetCount++;
        }

        public void OnSettingsClosed()
        {
        }

        public void OnCompactClosed()
        {
        }

        public void OnWidgetClosed()
        {
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeExitService : IApplicationExitService
    {
        public int ExitCount { get; private set; }

        public void Exit()
        {
            ExitCount++;
        }
    }
}
