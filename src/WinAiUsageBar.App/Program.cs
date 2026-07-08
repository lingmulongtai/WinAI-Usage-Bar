using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var commandLineResult = CommandLineHandler.TryHandleAsync(
            args,
            Console.Out,
            Console.Error,
            SmokeTestRunner.RunAsync,
            CommandLineActions.ExportDiagnosticsAsync,
            CommandLineActions.CreateHealthReportAsync,
            CommandLineActions.CreateProviderCatalog,
            CommandLineActions.ValidateConfigBackupAsync,
            CommandLineActions.RestoreConfigBackupAsync,
            AppInfoProvider.Get,
            CancellationToken.None,
            refreshOnce: CommandLineActions.RefreshOnceAsync,
            setProviderCliOverride: CommandLineActions.SetProviderCliOverrideAsync,
            clearProviderCliOverride: CommandLineActions.ClearProviderCliOverrideAsync,
            pruneSupportArtifacts: CommandLineActions.PruneSupportArtifactsAsync,
            checkForUpdates: CommandLineActions.CheckForUpdatesAsync,
            downloadUpdate: CommandLineActions.DownloadUpdateAsync,
            prepareUpdateInstall: CommandLineActions.PrepareUpdateInstallAsync,
            launchPreparedUpdate: CommandLineActions.LaunchPreparedUpdateAsync,
            installLatestUpdate: CommandLineActions.InstallLatestUpdateAsync,
            exportConfigBackup: CommandLineActions.ExportConfigBackupAsync,
            runStartupUpdate: CommandLineActions.RunStartupUpdateAsync,
            restoreLatestConfigBackup: CommandLineActions.RestoreLatestConfigBackupAsync)
            .GetAwaiter()
            .GetResult();

        if (commandLineResult.Handled)
        {
            Environment.ExitCode = commandLineResult.ExitCode;
            return;
        }

        using var singleInstance = new SingleInstanceGuard("Local\\WinAIUsageBar");
        if (!singleInstance.TryAcquire())
        {
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
