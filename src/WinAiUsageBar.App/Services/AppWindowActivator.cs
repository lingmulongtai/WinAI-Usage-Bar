using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Windows;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Services;

public interface IAppWindowActivator : IDisposable
{
    void ShowCompactPanel(AppHost host);

    void ShowSettings(AppHost host);

    void ShowWidget(AppHost host);

    void OnSettingsClosed();

    void OnCompactClosed();

    void OnWidgetClosed();
}

public interface IApplicationExitService
{
    void Exit();
}

public sealed class WinUiWindowActivator(
    WidgetPlacementStore widgetPlacementStore,
    ICompactPanelPlacementService compactPanelPlacementService) : IAppWindowActivator
{
    private MainWindow? mainWindow;
    private CompactUsageWindow? compactUsageWindow;
    private WidgetWindow? widgetWindow;

    public void ShowCompactPanel(AppHost host)
    {
        compactUsageWindow ??= new CompactUsageWindow(host);
        var placement = compactPanelPlacementService.Calculate(360, 520);
        WindowHelpers.MoveAndResize(
            compactUsageWindow,
            Convert.ToInt32(placement.Left),
            Convert.ToInt32(placement.Top),
            Convert.ToInt32(placement.Width),
            Convert.ToInt32(placement.Height));
        compactUsageWindow.Activate();
    }

    public void ShowSettings(AppHost host)
    {
        mainWindow ??= new MainWindow(host);
        mainWindow.Activate();
    }

    public void ShowWidget(AppHost host)
    {
        widgetWindow ??= new WidgetWindow(host, widgetPlacementStore);
        widgetWindow.Activate();
    }

    public void OnSettingsClosed()
    {
        mainWindow = null;
    }

    public void OnCompactClosed()
    {
        compactUsageWindow = null;
    }

    public void OnWidgetClosed()
    {
        widgetWindow = null;
    }

    public void Dispose()
    {
        mainWindow = null;
        compactUsageWindow = null;
        widgetWindow = null;
    }
}

public sealed class WinUiApplicationExitService : IApplicationExitService
{
    public void Exit()
    {
        Application.Current.Exit();
    }
}
