using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.App.Windows;
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
    private readonly WidgetPlacementStore widgetPlacementStore;
    private MainWindow? mainWindow;
    private CompactUsageWindow? compactUsageWindow;
    private WidgetWindow? widgetWindow;

    public AppHost(IAppDispatcher dispatcher, AppHostServices services)
    {
        this.dispatcher = dispatcher;
        Paths = services.Paths;
        ConfigStore = services.ConfigStore;
        refreshService = services.RefreshService;
        widgetPlacementStore = services.WidgetPlacementStore;
        trayIconService = services.TrayIconService;
        DiagnosticsLog = services.DiagnosticsLog;
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

    public void ShowCompactPanel()
    {
        try
        {
            compactUsageWindow ??= new CompactUsageWindow(this);
            compactUsageWindow.Activate();
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
            mainWindow ??= new MainWindow(this);
            mainWindow.Activate();
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
            widgetWindow ??= new WidgetWindow(this, widgetPlacementStore);
            widgetWindow.Activate();
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
        mainWindow = null;
    }

    internal void OnCompactClosed()
    {
        compactUsageWindow = null;
    }

    internal void OnWidgetClosed()
    {
        widgetWindow = null;
    }

    public async ValueTask DisposeAsync()
    {
        trayIconService.Dispose();
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
        trayIconService.ExitRequested += (_, _) => dispatcher.TryEnqueue(() => Application.Current.Exit());

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
