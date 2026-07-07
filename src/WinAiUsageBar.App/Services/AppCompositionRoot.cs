using Microsoft.UI.Dispatching;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Services;

public static class AppCompositionRoot
{
    public static async Task<AppHost> CreateHostAsync(
        DispatcherQueue dispatcherQueue,
        CancellationToken cancellationToken)
    {
        var services = CreateServices(AppDataPaths.CreateDefault());
        await services.DiagnosticsLog.InfoAsync("Starting WinAI Usage Bar.", cancellationToken).ConfigureAwait(false);

        var host = new AppHost(new DispatcherQueueAppDispatcher(dispatcherQueue), services);
        await host.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    public static AppHostServices CreateServices(AppDataPaths paths)
    {
        var diagnosticsLog = new FileAppDiagnosticsLog(paths);
        var configStore = new JsonAppConfigStore(paths);
        var snapshotStore = new JsonSnapshotStore(paths);
        var commandProbe = new CliCommandProbe();
        var codexClient = new CodexAppServerClient();
        var registry = new ProviderRegistry(commandProbe, codexClient);
        var notifications = new WindowsAppNotificationService(new WindowsAppNotificationTransport());
        var refreshService = new UsageRefreshService(configStore, snapshotStore, registry, paths, notifications);
        var widgetPlacementStore = new WidgetPlacementStore(configStore);
        var compactPlacementService = new CompactPanelPlacementService();
        var tray = new TrayIconService();
        var windowActivator = new WinUiWindowActivator(widgetPlacementStore, compactPlacementService);
        var exitService = new WinUiApplicationExitService();

        return new AppHostServices(
            paths,
            configStore,
            refreshService,
            tray,
            diagnosticsLog,
            windowActivator,
            exitService);
    }
}

public sealed record AppHostServices(
    AppDataPaths Paths,
    IAppConfigStore ConfigStore,
    IUsageRefreshService RefreshService,
    ITrayIconService TrayIconService,
    IAppDiagnosticsLog DiagnosticsLog,
    IAppWindowActivator WindowActivator,
    IApplicationExitService ExitService);

public interface IAppDispatcher
{
    bool TryEnqueue(Action action);
}

public sealed class DispatcherQueueAppDispatcher(DispatcherQueue dispatcherQueue) : IAppDispatcher
{
    public bool TryEnqueue(Action action)
    {
        return dispatcherQueue.TryEnqueue(() => action());
    }
}
