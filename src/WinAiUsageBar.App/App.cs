using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

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
        try
        {
            host = await AppHost.CreateAsync(DispatcherQueue.GetForCurrentThread(), CancellationToken.None);
            await host.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await LogStartupFailureAsync(ex);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _ = LogUnhandledExceptionAsync(e.Exception);
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

    private Task LogUnhandledExceptionAsync(Exception exception)
    {
        var diagnosticsLog = host?.DiagnosticsLog
            ?? new FileAppDiagnosticsLog(AppDataPaths.CreateDefault());

        return diagnosticsLog.ErrorAsync(
            "Unhandled WinUI exception.",
            exception,
            CancellationToken.None);
    }

    private static Task LogStartupFailureAsync(Exception exception)
    {
        var diagnosticsLog = new FileAppDiagnosticsLog(AppDataPaths.CreateDefault());
        return diagnosticsLog.ErrorAsync(
            "Application startup failed.",
            exception,
            CancellationToken.None);
    }
}
