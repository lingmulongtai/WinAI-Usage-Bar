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

public sealed class WinUiWindowActivator(WidgetPlacementStore widgetPlacementStore) : IAppWindowActivator
{
    private MainWindow? mainWindow;
    private CompactUsageWindow? compactUsageWindow;
    private WidgetWindow? widgetWindow;

    public void ShowCompactPanel(AppHost host)
    {
        compactUsageWindow ??= new CompactUsageWindow(host);
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
