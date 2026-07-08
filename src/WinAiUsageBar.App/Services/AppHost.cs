using Microsoft.UI.Dispatching;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Updates;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Services;

public sealed class AppHost : IAsyncDisposable
{
    private readonly IAppDispatcher dispatcher;
    private readonly ITrayIconService trayIconService;
    private readonly IUsageRefreshService refreshService;
    private readonly IDiagnosticsExportService diagnosticsExportService;
    private readonly IDiagnosticsSummaryService diagnosticsSummaryService;
    private readonly IHistorySummaryService historySummaryService;
    private readonly IDataMaintenanceService dataMaintenanceService;
    private readonly IConfigBackupValidationService configBackupValidationService;
    private readonly IConfigBackupRestoreService configBackupRestoreService;
    private readonly IConfigResetService configResetService;
    private readonly ISecretStore secretStore;
    private readonly IStartupRegistrationService startupRegistrationService;
    private readonly IAppWindowActivator windowActivator;
    private readonly IApplicationExitService exitService;
    private readonly IStartupUpdateService startupUpdateService;
    private readonly IReleaseUpdateCheckService updateCheckService;

    public AppHost(IAppDispatcher dispatcher, AppHostServices services)
    {
        this.dispatcher = dispatcher;
        Paths = services.Paths;
        ConfigStore = services.ConfigStore;
        refreshService = services.RefreshService;
        trayIconService = services.TrayIconService;
        DiagnosticsLog = services.DiagnosticsLog;
        diagnosticsExportService = services.DiagnosticsExportService;
        diagnosticsSummaryService = services.DiagnosticsSummaryService;
        historySummaryService = services.HistorySummaryService;
        dataMaintenanceService = services.DataMaintenanceService;
        configBackupValidationService = services.ConfigBackupValidationService ?? new ConfigBackupValidationService();
        configBackupRestoreService = services.ConfigBackupRestoreService
            ?? new ConfigBackupRestoreService(Paths, ConfigStore, configBackupValidationService);
        configResetService = services.ConfigResetService ?? new ConfigResetService(Paths, ConfigStore);
        secretStore = services.SecretStore;
        startupRegistrationService = services.StartupRegistrationService;
        windowActivator = services.WindowActivator;
        exitService = services.ExitService;
        startupUpdateService = services.StartupUpdateService ?? new NoOpStartupUpdateService();
        updateCheckService = services.UpdateCheckService ?? new NoOpReleaseUpdateCheckService();
    }

    public ShellViewModel ViewModel { get; } = new();

    public AppDataPaths Paths { get; }

    public IAppConfigStore ConfigStore { get; }

    public IAppDiagnosticsLog DiagnosticsLog { get; }

    public static async Task<AppHost> CreateAsync(DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
    {
        return await AppCompositionRoot.CreateHostAsync(dispatcherQueue, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await refreshService.StartAsync(cancellationToken).ConfigureAwait(false);
        RunLoggedInBackground(
            () => RefreshNowAsync(CancellationToken.None),
            "Initial refresh failed.");

        var config = await ConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (config.Startup.LaunchOnLogin)
        {
            await ApplyStartupRegistrationAsync(isEnabled: true, cancellationToken).ConfigureAwait(false);
        }

        if (config.Widget.ShowOnStartup)
        {
            dispatcher.TryEnqueue(ShowWidget);
        }

        RunLoggedInBackground(
            () => RunStartupUpdateAsync(CancellationToken.None),
            "Startup update check failed.");
    }

    public async Task<AppConfig> LoadConfigAsync(CancellationToken cancellationToken)
    {
        return await ConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveConfigAsync(AppConfig config, CancellationToken cancellationToken)
    {
        await ConfigStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        try
        {
            await refreshService.RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Refresh failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task RestartRefreshScheduleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await refreshService.RestartAsync(cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync("Refresh schedule restarted.", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Refresh schedule restart failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ReleaseUpdateCheckResult> CheckForUpdatesNowAsync(CancellationToken cancellationToken)
    {
        try
        {
            var appInfo = AppInfoProvider.Get();
            var result = await updateCheckService.CheckAsync(appInfo.InformationalVersion, cancellationToken)
                .ConfigureAwait(false);
            await SaveUpdateCheckStatusAsync(result, cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync(
                $"Manual update check result: {result.Status} - {result.Message}",
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Manual update check failed.", ex, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DiagnosticsExportResult> ExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await diagnosticsExportService.ExportAsync(cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync($"Diagnostics exported to {result.Path}.", cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Diagnostics export failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DiagnosticsSummary> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken)
    {
        return await diagnosticsSummaryService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HistorySummary> GetHistorySummaryAsync(CancellationToken cancellationToken)
    {
        return await historySummaryService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken)
    {
        var result = await dataMaintenanceService.ClearSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        await DiagnosticsLog.InfoAsync("Snapshot cache cleared.", cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken)
    {
        var result = await dataMaintenanceService.ClearHistoryAsync(cancellationToken).ConfigureAwait(false);
        await DiagnosticsLog.InfoAsync("History file cleared.", cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<ConfigBackupResult> ExportConfigBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await dataMaintenanceService.ExportConfigBackupAsync(cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync($"Config backup exported to {result.Path}.", cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Config backup export failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DataPruneResult> PruneConfigBackupsAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dataMaintenanceService.PruneConfigBackupsAsync(keepNewest, cancellationToken)
                .ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync(
                $"Config backups pruned: {result.DeletedCount} deleted, {result.KeptCount} kept.",
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Config backup pruning failed.", ex, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DataPruneResult> PruneDiagnosticsExportsAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dataMaintenanceService.PruneDiagnosticsExportsAsync(keepNewest, cancellationToken)
                .ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync(
                $"Diagnostics exports pruned: {result.DeletedCount} deleted, {result.KeptCount} kept.",
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Diagnostics export pruning failed.", ex, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ConfigBackupValidationResult> ValidateLatestConfigBackupAsync(CancellationToken cancellationToken)
    {
        var path = await GetLatestConfigBackupPathAsync(cancellationToken).ConfigureAwait(false);
        if (path is null)
        {
            return new ConfigBackupValidationResult(
                string.Empty,
                IsValid: false,
                ConfigVersion: null,
                ProviderCount: null,
                EnabledProviderCount: null,
                DefaultedProviderCount: null,
                Errors: ["No config backup is available."],
                Warnings: []);
        }

        try
        {
            var result = await configBackupValidationService.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync(
                result.IsValid
                    ? $"Config backup validated: {result.Path}."
                    : $"Config backup validation failed: {result.Path}.",
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Config backup validation failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ConfigBackupRestoreResult> RestoreLatestConfigBackupAsync(CancellationToken cancellationToken)
    {
        var path = await GetLatestConfigBackupPathAsync(cancellationToken).ConfigureAwait(false);
        if (path is null)
        {
            return new ConfigBackupRestoreResult(
                string.Empty,
                Restored: false,
                RollbackBackupPath: null,
                ConfigVersion: null,
                ProviderCount: null,
                EnabledProviderCount: null,
                Errors: ["No config backup is available."],
                Warnings: []);
        }

        try
        {
            var result = await configBackupRestoreService.RestoreAsync(path, cancellationToken).ConfigureAwait(false);
            if (result.Restored)
            {
                await DiagnosticsLog.InfoAsync(
                    $"Config backup restored from {result.Path}; rollback saved to {result.RollbackBackupPath}.",
                    cancellationToken).ConfigureAwait(false);
                try
                {
                    await refreshService.RestartAsync(cancellationToken).ConfigureAwait(false);
                    await DiagnosticsLog.InfoAsync("Refresh schedule restarted after config restore.", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await DiagnosticsLog.ErrorAsync(
                        "Config backup restored, but refresh schedule restart failed.",
                        ex,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            else
            {
                await DiagnosticsLog.InfoAsync($"Config backup restore did not apply: {result.Path}.", cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Config backup restore failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ConfigResetResult> ResetConfigToDefaultsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await configResetService.ResetToDefaultsAsync(cancellationToken).ConfigureAwait(false);
            if (result.Reset)
            {
                await DiagnosticsLog.InfoAsync(
                    $"Config reset to defaults; rollback saved to {result.RollbackBackupPath}.",
                    cancellationToken).ConfigureAwait(false);
                try
                {
                    await refreshService.RestartAsync(cancellationToken).ConfigureAwait(false);
                    await DiagnosticsLog.InfoAsync("Refresh schedule restarted after config reset.", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await DiagnosticsLog.ErrorAsync(
                        "Config reset completed, but refresh schedule restart failed.",
                        ex,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Config reset failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> HasSecretAsync(string name, CancellationToken cancellationToken)
    {
        return await secretStore.HasSecretAsync(RequireSecretName(name), cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken)
    {
        var secretName = RequireSecretName(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Secret value is required.", nameof(value));
        }

        await secretStore.SetSecretAsync(secretName, value, cancellationToken).ConfigureAwait(false);
        await DiagnosticsLog.InfoAsync("Secret value saved.", cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken)
    {
        await secretStore.DeleteSecretAsync(RequireSecretName(name), cancellationToken).ConfigureAwait(false);
        await DiagnosticsLog.InfoAsync("Secret value deleted.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<StartupRegistrationStatus> GetStartupRegistrationStatusAsync(CancellationToken cancellationToken)
    {
        return await startupRegistrationService.GetStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyStartupRegistrationAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        try
        {
            await startupRegistrationService.SetEnabledAsync(isEnabled, cancellationToken).ConfigureAwait(false);
            await DiagnosticsLog.InfoAsync(
                isEnabled ? "Startup registration enabled." : "Startup registration disabled.",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DiagnosticsLog.ErrorAsync("Startup registration failed.", ex, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public void ShowCompactPanel()
    {
        try
        {
            windowActivator.ShowCompactPanel(this);
        }
        catch (Exception ex)
        {
            RunLoggedInBackground(
                () => DiagnosticsLog.ErrorAsync("Compact panel failed to open.", ex, CancellationToken.None),
                "Diagnostic logging failed.");
            throw;
        }
    }

    public void ShowSettings()
    {
        try
        {
            windowActivator.ShowSettings(this);
        }
        catch (Exception ex)
        {
            RunLoggedInBackground(
                () => DiagnosticsLog.ErrorAsync("Settings window failed to open.", ex, CancellationToken.None),
                "Diagnostic logging failed.");
            throw;
        }
    }

    public void ShowWidget()
    {
        try
        {
            windowActivator.ShowWidget(this);
        }
        catch (Exception ex)
        {
            RunLoggedInBackground(
                () => DiagnosticsLog.ErrorAsync("Widget window failed to open.", ex, CancellationToken.None),
                "Diagnostic logging failed.");
            throw;
        }
    }

    internal void OnSettingsClosed()
    {
        windowActivator.OnSettingsClosed();
    }

    internal void OnCompactClosed()
    {
        windowActivator.OnCompactClosed();
    }

    internal void OnWidgetClosed()
    {
        windowActivator.OnWidgetClosed();
    }

    public async ValueTask DisposeAsync()
    {
        trayIconService.Dispose();
        windowActivator.Dispose();
        await refreshService.DisposeAsync().ConfigureAwait(false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        refreshService.SnapshotsChanged += OnSnapshotsChanged;
        trayIconService.ShowRequested += (_, _) => dispatcher.TryEnqueue(ShowCompactPanel);
        trayIconService.ShowWidgetRequested += (_, _) => dispatcher.TryEnqueue(ShowWidget);
        trayIconService.SettingsRequested += (_, _) => dispatcher.TryEnqueue(ShowSettings);
        trayIconService.RefreshNowRequested += (_, _) => RunLoggedInBackground(
            () => RefreshNowAsync(CancellationToken.None),
            "Tray refresh command failed.");
        trayIconService.ExitRequested += (_, _) => dispatcher.TryEnqueue(exitService.Exit);

        await refreshService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        ApplySnapshots(refreshService.CurrentSnapshots);
        await DiagnosticsLog.InfoAsync("WinAI Usage Bar initialized.", cancellationToken).ConfigureAwait(false);
    }

    private void OnSnapshotsChanged(object? sender, IReadOnlyList<UsageSnapshot> snapshots)
    {
        dispatcher.TryEnqueue(() => ApplySnapshots(snapshots));
    }

    private void ApplySnapshots(IReadOnlyList<UsageSnapshot> snapshots)
    {
        ViewModel.ApplySnapshots(snapshots);
        trayIconService.UpdateTooltip(ViewModel.BuildTrayTooltip());
    }

    private void RunLoggedInBackground(Func<Task> action, string failureMessage)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DiagnosticsLog.ErrorAsync(failureMessage, ex, CancellationToken.None).ConfigureAwait(false);
            }
        });
    }

    private async Task<string?> GetLatestConfigBackupPathAsync(CancellationToken cancellationToken)
    {
        var summary = await diagnosticsSummaryService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(summary.LatestConfigBackupPath)
            ? null
            : summary.LatestConfigBackupPath;
    }

    private async Task RunStartupUpdateAsync(CancellationToken cancellationToken)
    {
        var appInfo = AppInfoProvider.Get();
        var result = await startupUpdateService.RunAsync(
            new StartupUpdateRequest(
                appInfo.InformationalVersion,
                AppContext.BaseDirectory,
                Environment.ProcessId),
            cancellationToken).ConfigureAwait(false);
        await DiagnosticsLog.InfoAsync(
            $"Startup update result: {result.Status} - {result.Message}",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveUpdateCheckStatusAsync(
        ReleaseUpdateCheckResult result,
        CancellationToken cancellationToken)
    {
        var config = await ConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        config.Updates.LastStatus = result.IsUpdateAvailable
            ? StartupUpdateStatus.UpdateAvailable.ToString()
            : result.Status is UpdateCheckStatus.UpToDate or UpdateCheckStatus.NoRelease
                ? StartupUpdateStatus.NoUpdate.ToString()
                : StartupUpdateStatus.UpdateCheckFailed.ToString();
        config.Updates.LastMessage = result.Message;
        config.Updates.LastCurrentVersion = result.CurrentVersion;
        config.Updates.LastLatestVersion = result.LatestVersion;
        config.Updates.LastCheckedAt = DateTimeOffset.Now;
        await ConfigStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private static string RequireSecretName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Secret name is required.", nameof(name));
        }

        return name.Trim();
    }

    private sealed class NoOpStartupUpdateService : IStartupUpdateService
    {
        public Task<StartupUpdateResult> RunAsync(
            StartupUpdateRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new StartupUpdateResult(
                StartupUpdateStatus.Disabled,
                "Startup update service is unavailable.",
                LatestVersion: null,
                PackagePath: null,
                InstallScriptPath: null));
        }
    }

    private sealed class NoOpReleaseUpdateCheckService : IReleaseUpdateCheckService
    {
        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ReleaseUpdateCheckResult(
                UpdateCheckStatus.Error,
                currentVersion,
                LatestVersion: null,
                "Update check service is unavailable.",
                IsUpdateAvailable: false,
                ReleasePageUrl: null,
                Package: null,
                Checksum: null));
        }
    }
}
