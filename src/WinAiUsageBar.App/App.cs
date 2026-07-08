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

        return LogAndReportAsync(
            diagnosticsLog,
            "WinUI unhandled exception",
            "Unhandled WinUI exception.",
            exception);
    }

    private static Task LogStartupFailureAsync(Exception exception)
    {
        var diagnosticsLog = new FileAppDiagnosticsLog(AppDataPaths.CreateDefault());
        return LogAndReportAsync(
            diagnosticsLog,
            "startup",
            "Application startup failed.",
            exception);
    }

    private static async Task LogAndReportAsync(
        IAppDiagnosticsLog diagnosticsLog,
        string reportSource,
        string logMessage,
        Exception exception)
    {
        await WriteCrashReportBestEffortAsync(reportSource, exception).ConfigureAwait(false);
        await diagnosticsLog.ErrorAsync(
            logMessage,
            exception,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WriteCrashReportBestEffortAsync(
        string source,
        Exception exception)
    {
        try
        {
            var paths = AppDataPaths.CreateDefault();
            var crashReports = new CrashReportService(paths);
            await crashReports.WriteAsync(
                new CrashReportRequest(
                    source,
                    exception,
                    AppVersion: AppInfoProvider.Get().InformationalVersion),
                CancellationToken.None).ConfigureAwait(false);
            await crashReports.PruneAsync(keepNewest: 20, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Crash reporting must never hide the original app failure.
        }
    }

}
