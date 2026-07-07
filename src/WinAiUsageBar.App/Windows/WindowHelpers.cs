using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace WinAiUsageBar.App.Windows;

public static class WindowHelpers
{
    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void Resize(Window window, int width, int height)
    {
        GetAppWindow(window).Resize(new SizeInt32(width, height));
    }

    public static void MoveAndResize(Window window, int left, int top, int width, int height)
    {
        GetAppWindow(window).MoveAndResize(new RectInt32(left, top, width, height));
    }

    public static void SetAlwaysOnTop(Window window, bool isAlwaysOnTop)
    {
        if (GetAppWindow(window).Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isAlwaysOnTop;
        }
    }
}
