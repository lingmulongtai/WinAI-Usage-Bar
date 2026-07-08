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
    string? InstallScriptPath)
{
    public string? InstallResultPath { get; init; }
}

public enum StartupUpdateStatus
{
    Disabled,
    SkippedRecentCheck,
    NoUpdate,
    UpdateAvailable,
    Downloaded,
    InstallAlreadyLaunched,
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
    Func<DateTimeOffset>? nowProvider = null,
    IUpdateInstallResultService? installResultService = null) : IStartupUpdateService
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
    private readonly IUpdateInstallResultService installResultService = installResultService ?? new UpdateInstallResultService(paths);

    public async Task<StartupUpdateResult> RunAsync(
        StartupUpdateRequest request,
        CancellationToken cancellationToken)
    {
        AppConfig? config = null;
        try
        {
            config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            await installResultService.RefreshAsync(config, cancellationToken).ConfigureAwait(false);
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
                    updateCheckedAt: false,
                    installLaunchedVersion: null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (ShouldSkipRecentCheck(config.Updates, out var skipMessage))
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.SkippedRecentCheck,
                    skipMessage,
                    request.CurrentVersion,
                    config.Updates.LastLatestVersion,
                    config.Updates.LastPackagePath,
                    config.Updates.LastInstallScriptPath,
                    updateCheckedAt: false,
                    installLaunchedVersion: null,
                    cancellationToken).ConfigureAwait(false);
            }

            var update = await updateCheck.CheckAsync(request.CurrentVersion, cancellationToken)
                .ConfigureAwait(false);
            SaveInstallerAssetStatus(config.Updates, update);
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
                    cancellationToken).ConfigureAwait(false);
            }

            if (config.Updates.InstallAutomatically
                && string.Equals(
                    config.Updates.LastInstallLaunchedVersion,
                    update.LatestVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                return await SaveResultAsync(
                    config,
                    StartupUpdateStatus.InstallAlreadyLaunched,
                    "Automatic install was already launched for this release version.",
                    update.CurrentVersion,
                    update.LatestVersion,
                    config.Updates.LastPackagePath,
                    config.Updates.LastInstallScriptPath,
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                    updateCheckedAt: true,
                    installLaunchedVersion: null,
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
                updateCheckedAt: true,
                installLaunchedVersion: update.LatestVersion,
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
                updateCheckedAt: true,
                installLaunchedVersion: null,
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
        bool updateCheckedAt,
        string? installLaunchedVersion,
        CancellationToken cancellationToken)
    {
        config.Updates.LastStatus = status.ToString();
        config.Updates.LastMessage = message;
        config.Updates.LastCurrentVersion = currentVersion;
        config.Updates.LastLatestVersion = latestVersion;
        config.Updates.LastPackagePath = packagePath;

        if (!string.IsNullOrWhiteSpace(installScriptPath))
        {
            var previousResultPath = config.Updates.LastInstallResultPath;
            var nextResultPath = GetInstallResultPath(installScriptPath);
            config.Updates.LastInstallScriptPath = installScriptPath;
            config.Updates.LastInstallResultPath = nextResultPath;
            if (!SamePath(previousResultPath, nextResultPath))
            {
                config.Updates.LastInstallResultStatus = null;
                config.Updates.LastInstallResultMessage = null;
                config.Updates.LastInstallResultCompletedAt = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(installLaunchedVersion))
        {
            config.Updates.LastInstallLaunchedVersion = installLaunchedVersion;
        }

        if (updateCheckedAt)
        {
            config.Updates.LastCheckedAt = nowProvider();
        }

        await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);

        return new StartupUpdateResult(
            status,
            message,
            latestVersion,
            packagePath,
            installScriptPath)
        {
            InstallResultPath = config.Updates.LastInstallResultPath
        };
    }

    private static string? GetInstallResultPath(string? installScriptPath)
    {
        if (string.IsNullOrWhiteSpace(installScriptPath))
        {
            return null;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(installScriptPath));
            return string.IsNullOrWhiteSpace(directory)
                ? null
                : Path.Combine(directory, "install-result.json");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right);
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void SaveInstallerAssetStatus(
        UpdateSettings settings,
        ReleaseUpdateCheckResult result)
    {
        settings.LastInstallerAssetName = result.Installer?.Name;
        settings.LastInstallerChecksumAssetName = result.InstallerChecksum?.Name;
    }

    private bool ShouldSkipRecentCheck(UpdateSettings updates, out string message)
    {
        if (updates.MinimumCheckIntervalHours <= 0 || updates.LastCheckedAt is null)
        {
            message = string.Empty;
            return false;
        }

        var minimumInterval = TimeSpan.FromHours(updates.MinimumCheckIntervalHours);
        var elapsed = nowProvider() - updates.LastCheckedAt.Value;
        if (elapsed >= minimumInterval)
        {
            message = string.Empty;
            return false;
        }

        var remaining = minimumInterval - elapsed;
        message = $"Startup update check skipped. Last check is still fresh for about {FormatRemaining(remaining)}.";
        return true;
    }

    private static string FormatRemaining(TimeSpan value)
    {
        if (value.TotalMinutes < 1)
        {
            return "less than 1 minute";
        }

        if (value.TotalHours < 1)
        {
            return $"{Math.Ceiling(value.TotalMinutes)} minutes";
        }

        return $"{Math.Ceiling(value.TotalHours)} hours";
    }
}
