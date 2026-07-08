using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IStartupUpdateService
{
    Task<StartupUpdateResult> RunAsync(
        StartupUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record StartupUpdateRequest(
    string CurrentVersion,
    string InstallDirectory,
    int ProcessIdToWait);

public sealed record StartupUpdateResult(
    StartupUpdateStatus Status,
    string Message,
    string? LatestVersion,
    string? PackagePath,
    string? InstallScriptPath);

public enum StartupUpdateStatus
{
    Disabled,
    NoUpdate,
    UpdateAvailable,
    Downloaded,
    InstallLaunched,
    UpdateCheckFailed,
    DownloadFailed,
    PreparationFailed,
    LaunchFailed,
    Error
}

public sealed class StartupUpdateService(
    IAppConfigStore configStore,
    IReleaseUpdateCheckService updateCheck,
    IUpdatePackageDownloader downloader,
    IUpdateInstallPreparationService preparation,
    IUpdateInstallLaunchService launcher,
    AppDataPaths paths,
    Func<DateTimeOffset>? nowProvider = null) : IStartupUpdateService
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<StartupUpdateResult> RunAsync(
        StartupUpdateRequest request,
        CancellationToken cancellationToken)
    {
        AppConfig? config = null;
        try
        {
            config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!config.Updates.CheckOnStartup)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.Disabled,
                    "Startup update check is disabled.",
                    request.CurrentVersion,
                    latestVersion: null,
                    packagePath: null,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            var update = await updateCheck.CheckAsync(request.CurrentVersion, cancellationToken)
                .ConfigureAwait(false);
            if (!update.IsUpdateAvailable
                && update.Status is UpdateCheckStatus.UpToDate or UpdateCheckStatus.NoRelease)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.NoUpdate,
                    update.Message,
                    update.CurrentVersion,
                    update.LatestVersion,
                    packagePath: null,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!update.IsUpdateAvailable || update.Package is null || update.Checksum is null)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.UpdateCheckFailed,
                    update.Message,
                    update.CurrentVersion,
                    update.LatestVersion,
                    packagePath: null,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!config.Updates.DownloadAutomatically)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.UpdateAvailable,
                    "A newer update is available. Automatic download is disabled.",
                    update.CurrentVersion,
                    update.LatestVersion,
                    packagePath: null,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            var download = await downloader.DownloadAndVerifyAsync(
                update.Package,
                update.Checksum,
                paths.UpdatesDirectory,
                cancellationToken).ConfigureAwait(false);
            if (download.Status is not UpdateDownloadStatus.Downloaded || string.IsNullOrWhiteSpace(download.PackagePath))
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.DownloadFailed,
                    download.Message,
                    update.CurrentVersion,
                    update.LatestVersion,
                    packagePath: null,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!config.Updates.InstallAutomatically)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.Downloaded,
                    "Update package downloaded and verified. Automatic install is disabled.",
                    update.CurrentVersion,
                    update.LatestVersion,
                    download.PackagePath,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            var prepared = await preparation.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    download.PackagePath,
                    request.InstallDirectory,
                    request.ProcessIdToWait,
                    RestartAfterInstall: true),
                cancellationToken).ConfigureAwait(false);
            if (prepared.Status is not UpdateInstallPreparationStatus.Prepared
                || string.IsNullOrWhiteSpace(prepared.ScriptPath))
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.PreparationFailed,
                    prepared.Message,
                    update.CurrentVersion,
                    update.LatestVersion,
                    download.PackagePath,
                    installScriptPath: null,
                    cancellationToken).ConfigureAwait(false);
            }

            var launch = await launcher.LaunchAsync(
                new UpdateInstallLaunchRequest(prepared.ScriptPath),
                cancellationToken).ConfigureAwait(false);
            if (launch.Status is not UpdateInstallLaunchStatus.Launched)
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.LaunchFailed,
                    launch.Message,
                    update.CurrentVersion,
                    update.LatestVersion,
                    download.PackagePath,
                    prepared.ScriptPath,
                    cancellationToken).ConfigureAwait(false);
            }

            return await SaveResultAsync(
                config,
                StartupUpdateStatus.InstallLaunched,
                "Update package downloaded, verified, prepared, and launched for install.",
                update.CurrentVersion,
                update.LatestVersion,
                download.PackagePath,
                prepared.ScriptPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (config is null)
            {
                return new StartupUpdateResult(
                    StartupUpdateStatus.Error,
                    $"Startup update check failed: {ex.Message}",
                    LatestVersion: null,
                    PackagePath: null,
                    InstallScriptPath: null);
            }

            return await SaveResultAsync(
                config,
                StartupUpdateStatus.Error,
                $"Startup update check failed: {ex.Message}",
                request.CurrentVersion,
                latestVersion: null,
                packagePath: null,
                installScriptPath: null,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<StartupUpdateResult> SaveResultAsync(
        AppConfig config,
        StartupUpdateStatus status,
        string message,
        string? currentVersion,
        string? latestVersion,
        string? packagePath,
        string? installScriptPath,
        CancellationToken cancellationToken)
    {
        config.Updates.LastStatus = status.ToString();
        config.Updates.LastMessage = message;
        config.Updates.LastCurrentVersion = currentVersion;
        config.Updates.LastLatestVersion = latestVersion;
        config.Updates.LastPackagePath = packagePath;
        config.Updates.LastInstallScriptPath = installScriptPath;
        config.Updates.LastCheckedAt = nowProvider();

        await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);

        return new StartupUpdateResult(
            status,
            message,
            latestVersion,
            packagePath,
            installScriptPath);
    }
}
