using Microsoft.UI.Dispatching;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Services;

public sealed class AppHost : IAsyncDisposable
{
    private readonly IAppDispatcher dispatcher;
    private readonly ITrayIconService trayIconService;
    private readonly IUsageRefreshService refreshService;
    private readonly IDiagnosticsExportService diagnosticsExportService;
    private readonly IStartupRegistrationService startupRegistrationService;
    private readonly IAppWindowActivator windowActivator;
    private readonly IApplicationExitService exitService;

    public AppHost(IAppDispatcher dispatcher, AppHostServices services)
    {
        this.dispatcher = dispatcher;
        Paths = services.Paths;
        ConfigStore = services.ConfigStore;
        refreshService = services.RefreshService;
        trayIconService = services.TrayIconService;
        DiagnosticsLog = services.DiagnosticsLog;
        diagnosticsExportService = services.DiagnosticsExportService;
        startupRegistrationService = services.StartupRegistrationService;
        windowActivator = services.WindowActivator;
        exitService = services.ExitService;
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
}
