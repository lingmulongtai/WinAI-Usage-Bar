namespace WinAiUsageBar.Infrastructure.Updates;

public sealed record GitHubReleaseAsset(
    string Name,
    Uri DownloadUrl,
    long? SizeBytes);

public sealed record GitHubReleaseMetadata(
    string TagName,
    string? Name,
    bool IsDraft,
    bool IsPrerelease,
    Uri? ReleasePageUrl,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record LatestReleaseResult(
    GitHubReleaseMetadata? Release,
    LatestReleaseStatus Status,
    string? ErrorMessage)
{
    public static LatestReleaseResult Found(GitHubReleaseMetadata release)
    {
        return new LatestReleaseResult(release, LatestReleaseStatus.Found, null);
    }

    public static LatestReleaseResult NotFound(string message)
    {
        return new LatestReleaseResult(null, LatestReleaseStatus.NotFound, message);
    }

    public static LatestReleaseResult Failed(string message)
    {
        return new LatestReleaseResult(null, LatestReleaseStatus.Error, message);
    }
}

public enum LatestReleaseStatus
{
    Found,
    NotFound,
    Error
}
