using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.App;

public sealed class App : Application
{
    private AppHost? host;

    public App()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        host = await AppHost.CreateAsync(DispatcherQueue.GetForCurrentThread(), CancellationToken.None);
        await host.StartAsync(CancellationToken.None);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (host is null)
        {
            return;
        }

        host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
