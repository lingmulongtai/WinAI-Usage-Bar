namespace WinAiUsageBar.Infrastructure.Updates;

public interface IReleaseUpdateCheckService
{
    Task<ReleaseUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken);
}

public sealed record UpdatePackageAsset(
    string Name,
    Uri DownloadUrl,
    long? SizeBytes);

public sealed record ReleaseUpdateCheckResult(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? LatestVersion,
    string Message,
    bool IsUpdateAvailable,
    Uri? ReleasePageUrl,
    UpdatePackageAsset? Package,
    UpdatePackageAsset? Checksum);

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    NoRelease,
    MissingAssets,
    InvalidRelease,
    Error
}

public sealed class ReleaseUpdateCheckService(
    IGitHubLatestReleaseClient releaseClient,
    string packagePrefix = "WinAIUsageBar",
    string runtime = "win-x64") : IReleaseUpdateCheckService
{
    public async Task<ReleaseUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        var currentVersionText = NormalizeVersionText(currentVersion);
        if (!TryParseVersion(currentVersionText, out var parsedCurrentVersion))
        {
            return Result(
                UpdateCheckStatus.InvalidRelease,
                currentVersionText,
                latestVersion: null,
                "Current app version could not be parsed for update comparison.",
                isUpdateAvailable: false);
        }

        var latestRelease = await releaseClient.GetLatestAsync(cancellationToken).ConfigureAwait(false);
        if (latestRelease.Status == LatestReleaseStatus.NotFound)
        {
            return Result(
                UpdateCheckStatus.NoRelease,
                currentVersionText,
                latestVersion: null,
                latestRelease.ErrorMessage ?? "No published GitHub release was found.",
                isUpdateAvailable: false);
        }

        if (latestRelease.Status == LatestReleaseStatus.Error || latestRelease.Release is null)
        {
            return Result(
                UpdateCheckStatus.Error,
                currentVersionText,
                latestVersion: null,
                latestRelease.ErrorMessage ?? "GitHub release check failed.",
                isUpdateAvailable: false);
        }

        var release = latestRelease.Release;
        if (release.IsDraft || release.IsPrerelease)
        {
            return Result(
                UpdateCheckStatus.NoRelease,
                currentVersionText,
                latestVersion: release.TagName,
                "Latest GitHub release is a draft or prerelease.",
                isUpdateAvailable: false,
                release.ReleasePageUrl);
        }

        var latestVersionText = NormalizeVersionText(release.TagName);
        if (!TryParseVersion(latestVersionText, out var parsedLatestVersion))
        {
            return Result(
                UpdateCheckStatus.InvalidRelease,
                currentVersionText,
                latestVersionText,
                $"Latest release tag '{release.TagName}' could not be parsed.",
                isUpdateAvailable: false,
                release.ReleasePageUrl);
        }

        var expectedZipName = $"{packagePrefix}-{latestVersionText}-{runtime}.zip";
        var expectedChecksumName = $"{expectedZipName}.sha256";
        var package = FindAsset(release.Assets, expectedZipName);
        var checksum = FindAsset(release.Assets, expectedChecksumName);
        var isNewer = parsedLatestVersion > parsedCurrentVersion;

        if (package is null || checksum is null)
        {
            return Result(
                UpdateCheckStatus.MissingAssets,
                currentVersionText,
                latestVersionText,
                $"Latest release is missing {expectedZipName} or {expectedChecksumName}.",
                isUpdateAvailable: false,
                release.ReleasePageUrl,
                package,
                checksum);
        }

        return Result(
            isNewer ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
            currentVersionText,
            latestVersionText,
            isNewer ? "A newer GitHub release is available." : "The current app version is up to date.",
            isNewer,
            release.ReleasePageUrl,
            package,
            checksum);
    }

    private static ReleaseUpdateCheckResult Result(
        UpdateCheckStatus status,
        string currentVersion,
        string? latestVersion,
        string message,
        bool isUpdateAvailable,
        Uri? releasePageUrl = null,
        UpdatePackageAsset? package = null,
        UpdatePackageAsset? checksum = null)
    {
        return new ReleaseUpdateCheckResult(
            status,
            currentVersion,
            latestVersion,
            message,
            isUpdateAvailable,
            releasePageUrl,
            package,
            checksum);
    }

    private static UpdatePackageAsset? FindAsset(
        IEnumerable<GitHubReleaseAsset> assets,
        string expectedName)
    {
        var asset = assets.FirstOrDefault(item =>
            string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));
        return asset is null
            ? null
            : new UpdatePackageAsset(asset.Name, asset.DownloadUrl, asset.SizeBytes);
    }

    public static string NormalizeVersionText(string version)
    {
        var normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var buildIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (buildIndex >= 0)
        {
            normalized = normalized[..buildIndex];
        }

        var prereleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        return normalized;
    }

    private static bool TryParseVersion(string version, out Version parsedVersion)
    {
        if (Version.TryParse(version, out var value) && value is not null)
        {
            parsedVersion = value;
            return true;
        }

        parsedVersion = new Version(0, 0);
        return false;
    }
}
