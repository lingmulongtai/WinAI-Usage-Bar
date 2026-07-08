using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface ILatestUpdateInstallService
{
    Task<LatestUpdateInstallResult> InstallLatestAsync(
        LatestUpdateInstallRequest request,
        CancellationToken cancellationToken);
}

public sealed record LatestUpdateInstallRequest(
    string CurrentVersion,
    string InstallDirectory,
    int ProcessIdToWait,
    bool RestartAfterInstall);

public sealed record LatestUpdateInstallResult(
    LatestUpdateInstallStatus Status,
    string Message,
    ReleaseUpdateCheckResult? UpdateCheck,
    UpdateDownloadResult? Download,
    UpdateInstallPreparationResult? Preparation,
    UpdateInstallLaunchResult? Launch);

public enum LatestUpdateInstallStatus
{
    SkippedNoUpdate,
    Launched,
    UpdateCheckFailed,
    DownloadFailed,
    PreparationFailed,
    LaunchFailed,
    Error
}

public sealed class LatestUpdateInstallService(
    IReleaseUpdateCheckService updateCheck,
    IUpdatePackageDownloader downloader,
    IUpdateInstallPreparationService preparation,
    IUpdateInstallLaunchService launcher,
    AppDataPaths paths) : ILatestUpdateInstallService
{
    public async Task<LatestUpdateInstallResult> InstallLatestAsync(
        LatestUpdateInstallRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var update = await updateCheck.CheckAsync(request.CurrentVersion, cancellationToken)
                .ConfigureAwait(false);

            if (!update.IsUpdateAvailable
                && update.Status is UpdateCheckStatus.UpToDate or UpdateCheckStatus.NoRelease)
            {
                return new LatestUpdateInstallResult(
                    LatestUpdateInstallStatus.SkippedNoUpdate,
                    update.Message,
                    update,
                    Download: null,
                    Preparation: null,
                    Launch: null);
            }

            if (!update.IsUpdateAvailable)
            {
                return Failed(
                    LatestUpdateInstallStatus.UpdateCheckFailed,
                    update.Message,
                    update);
            }

            if (update.Package is null || update.Checksum is null)
            {
                return Failed(
                    LatestUpdateInstallStatus.UpdateCheckFailed,
                    "Update check reported an update but did not include both package and checksum assets.",
                    update);
            }

            var download = await downloader.DownloadAndVerifyAsync(
                update.Package,
                update.Checksum,
                paths.UpdatesDirectory,
                cancellationToken).ConfigureAwait(false);
            if (download.Status is not UpdateDownloadStatus.Downloaded || string.IsNullOrWhiteSpace(download.PackagePath))
            {
                return Failed(
                    LatestUpdateInstallStatus.DownloadFailed,
                    download.Message,
                    update,
                    download);
            }

            var prepared = await preparation.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    download.PackagePath,
                    request.InstallDirectory,
                    request.ProcessIdToWait,
                    request.RestartAfterInstall),
                cancellationToken).ConfigureAwait(false);
            if (prepared.Status is not UpdateInstallPreparationStatus.Prepared || string.IsNullOrWhiteSpace(prepared.ScriptPath))
            {
                return Failed(
                    LatestUpdateInstallStatus.PreparationFailed,
                    prepared.Message,
                    update,
                    download,
                    prepared);
            }

            var launch = await launcher.LaunchAsync(
                new UpdateInstallLaunchRequest(prepared.ScriptPath),
                cancellationToken).ConfigureAwait(false);
            if (launch.Status is not UpdateInstallLaunchStatus.Launched)
            {
                return Failed(
                    LatestUpdateInstallStatus.LaunchFailed,
                    launch.Message,
                    update,
                    download,
                    prepared,
                    launch);
            }

            return new LatestUpdateInstallResult(
                LatestUpdateInstallStatus.Launched,
                "Latest update install script launched.",
                update,
                download,
                prepared,
                launch);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(
                LatestUpdateInstallStatus.Error,
                $"Latest update install failed: {ex.Message}",
                updateCheck: null);
        }
    }

    private static LatestUpdateInstallResult Failed(
        LatestUpdateInstallStatus status,
        string message,
        ReleaseUpdateCheckResult? updateCheck,
        UpdateDownloadResult? download = null,
        UpdateInstallPreparationResult? preparation = null,
        UpdateInstallLaunchResult? launch = null)
    {
        return new LatestUpdateInstallResult(
            status,
            message,
            updateCheck,
            download,
            preparation,
            launch);
    }
}
