using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.App.Windows;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App;

public sealed class App : Application
{
    private AppHost? host;
    private Window? uiLaunchSmokeWindow;

    public static UiLaunchSmokeOptions? UiLaunchSmokeOptions { get; set; }

    public App()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var appHost = await AppHost.CreateAsync(DispatcherQueue.GetForCurrentThread(), CancellationToken.None);
            host = appHost;
            await appHost.StartAsync(CancellationToken.None);
            if (UiLaunchSmokeOptions is { } options)
            {
                await RunUiLaunchSmokeAsync(appHost, options);
            }
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

    private async Task RunUiLaunchSmokeAsync(
        AppHost appHost,
        UiLaunchSmokeOptions options)
    {
        var smokeWindow = new Window
        {
            Title = "WinAI Usage Bar UI Smoke",
            Content = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "WinAI Usage Bar UI smoke",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        uiLaunchSmokeWindow = smokeWindow;
        WindowHelpers.Resize(smokeWindow, 360, 160);
        smokeWindow.Activate();
        await appHost.DiagnosticsLog.InfoAsync(
            "UI launch smoke activated a minimal WinUI window.",
            CancellationToken.None);
        _ = CompleteUiLaunchSmokeAsync(appHost, options.HoldDuration);
    }

    private static async Task CompleteUiLaunchSmokeAsync(
        AppHost host,
        TimeSpan holdDuration)
    {
        await Task.Delay(holdDuration, CancellationToken.None).ConfigureAwait(false);
        await host.DiagnosticsLog.InfoAsync(
            "UI launch smoke completed.",
            CancellationToken.None).ConfigureAwait(false);
        Environment.Exit(0);
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
