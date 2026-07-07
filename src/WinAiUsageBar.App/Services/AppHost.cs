using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.App.Windows;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Services;

public sealed class AppHost : IAsyncDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly TrayIconService trayIconService;
    private readonly UsageRefreshService refreshService;
    private readonly WidgetPlacementStore widgetPlacementStore;
    private MainWindow? mainWindow;
    private CompactUsageWindow? compactUsageWindow;
    private WidgetWindow? widgetWindow;

    private AppHost(
        DispatcherQueue dispatcherQueue,
        AppDataPaths paths,
        IAppConfigStore configStore,
        UsageRefreshService refreshService,
        WidgetPlacementStore widgetPlacementStore,
        TrayIconService trayIconService,
        IAppDiagnosticsLog diagnosticsLog)
    {
        this.dispatcherQueue = dispatcherQueue;
        Paths = paths;
        ConfigStore = configStore;
        this.refreshService = refreshService;
        this.widgetPlacementStore = widgetPlacementStore;
        this.trayIconService = trayIconService;
        DiagnosticsLog = diagnosticsLog;
    }

    public ShellViewModel ViewModel { get; } = new();

    public AppDataPaths Paths { get; }

    public IAppConfigStore ConfigStore { get; }

    public IAppDiagnosticsLog DiagnosticsLog { get; }

    public static async Task<AppHost> CreateAsync(DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var diagnosticsLog = new FileAppDiagnosticsLog(paths);
        await diagnosticsLog.InfoAsync("Starting WinAI Usage Bar.", cancellationToken).ConfigureAwait(false);

        var configStore = new JsonAppConfigStore(paths);
        var snapshotStore = new JsonSnapshotStore(paths);
        var commandProbe = new CliCommandProbe();
        var codexClient = new CodexAppServerClient();
        var registry = new ProviderRegistry(commandProbe, codexClient);
        var notifications = new NoOpAppNotificationService();
        var refreshService = new UsageRefreshService(configStore, snapshotStore, registry, paths, notifications);
        var widgetPlacementStore = new WidgetPlacementStore(configStore);
        var tray = new TrayIconService();

        var host = new AppHost(
            dispatcherQueue,
            paths,
            configStore,
            refreshService,
            widgetPlacementStore,
            tray,
            diagnosticsLog);

        await host.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return host;
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
            dispatcherQueue.TryEnqueue(ShowWidget);
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

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        refreshService.SnapshotsChanged += OnSnapshotsChanged;
        trayIconService.ShowRequested += (_, _) => dispatcherQueue.TryEnqueue(ShowCompactPanel);
        trayIconService.ShowWidgetRequested += (_, _) => dispatcherQueue.TryEnqueue(ShowWidget);
        trayIconService.SettingsRequested += (_, _) => dispatcherQueue.TryEnqueue(ShowSettings);
        trayIconService.RefreshNowRequested += (_, _) => RunLoggedInBackground(
            () => RefreshNowAsync(CancellationToken.None),
            "Tray refresh command failed.");
        trayIconService.ExitRequested += (_, _) => dispatcherQueue.TryEnqueue(() => Application.Current.Exit());

        await refreshService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        ApplySnapshots(refreshService.CurrentSnapshots);
        await DiagnosticsLog.InfoAsync("WinAI Usage Bar initialized.", cancellationToken).ConfigureAwait(false);
    }

    private void OnSnapshotsChanged(object? sender, IReadOnlyList<UsageSnapshot> snapshots)
    {
        dispatcherQueue.TryEnqueue(() => ApplySnapshots(snapshots));
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
