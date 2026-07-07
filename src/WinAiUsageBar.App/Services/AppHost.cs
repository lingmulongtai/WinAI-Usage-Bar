using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.App.Windows;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
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
        TrayIconService trayIconService)
    {
        this.dispatcherQueue = dispatcherQueue;
        Paths = paths;
        ConfigStore = configStore;
        this.refreshService = refreshService;
        this.widgetPlacementStore = widgetPlacementStore;
        this.trayIconService = trayIconService;
    }

    public ShellViewModel ViewModel { get; } = new();

    public AppDataPaths Paths { get; }

    public IAppConfigStore ConfigStore { get; }

    public static async Task<AppHost> CreateAsync(DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
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
            tray);

        await host.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await refreshService.StartAsync(cancellationToken).ConfigureAwait(false);
        _ = RefreshNowAsync(CancellationToken.None);

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
        await refreshService.RefreshNowAsync(cancellationToken).ConfigureAwait(false);
    }

    public void ShowCompactPanel()
    {
        compactUsageWindow ??= new CompactUsageWindow(this);
        compactUsageWindow.Activate();
    }

    public void ShowSettings()
    {
        mainWindow ??= new MainWindow(this);
        mainWindow.Activate();
    }

    public void ShowWidget()
    {
        widgetWindow ??= new WidgetWindow(this, widgetPlacementStore);
        widgetWindow.Activate();
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
        trayIconService.RefreshNowRequested += (_, _) => _ = RefreshNowAsync(CancellationToken.None);
        trayIconService.ExitRequested += (_, _) => dispatcherQueue.TryEnqueue(() => Application.Current.Exit());

        await refreshService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        ApplySnapshots(refreshService.CurrentSnapshots);
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
}
