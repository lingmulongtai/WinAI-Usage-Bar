using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class ReleaseUpdateCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_ReturnsUpdateAvailableWithPackageAndChecksumAssets()
    {
        var release = Release(
            "v0.2.0",
            assets:
            [
                Asset("WinAIUsageBar-0.2.0-win-x64.zip", "https://example.test/WinAIUsageBar-0.2.0-win-x64.zip", 2048),
                Asset("WinAIUsageBar-0.2.0-win-x64.zip.sha256", "https://example.test/WinAIUsageBar-0.2.0-win-x64.zip.sha256", 128)
            ]);
        var service = new ReleaseUpdateCheckService(new FakeLatestReleaseClient(LatestReleaseResult.Found(release)));

        var result = await service.CheckAsync("0.1.0+local", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("0.1.0", result.CurrentVersion);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", result.Package?.Name);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", result.Checksum?.Name);
        Assert.Contains("newer GitHub release", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUpToDateWhenLatestIsNotNewer()
    {
        var release = Release(
            "v0.1.0",
            assets:
            [
                Asset("WinAIUsageBar-0.1.0-win-x64.zip", "https://example.test/WinAIUsageBar-0.1.0-win-x64.zip", 2048),
                Asset("WinAIUsageBar-0.1.0-win-x64.zip.sha256", "https://example.test/WinAIUsageBar-0.1.0-win-x64.zip.sha256", 128)
            ]);
        var service = new ReleaseUpdateCheckService(new FakeLatestReleaseClient(LatestReleaseResult.Found(release)));

        var result = await service.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.False(result.IsUpdateAvailable);
        Assert.Contains("up to date", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_ReturnsMissingAssetsWhenZipOrChecksumIsAbsent()
    {
        var release = Release(
            "v0.2.0",
            assets:
            [
                Asset("WinAIUsageBar-0.2.0-win-x64.zip", "https://example.test/WinAIUsageBar-0.2.0-win-x64.zip", 2048)
            ]);
        var service = new ReleaseUpdateCheckService(new FakeLatestReleaseClient(LatestReleaseResult.Found(release)));

        var result = await service.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.MissingAssets, result.Status);
        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.Package);
        Assert.Null(result.Checksum);
        Assert.Contains(".zip.sha256", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNoReleaseWhenLatestReleaseIsMissing()
    {
        var service = new ReleaseUpdateCheckService(
            new FakeLatestReleaseClient(LatestReleaseResult.NotFound("No release yet.")));

        var result = await service.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.NoRelease, result.Status);
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("No release yet.", result.Message);
    }

    [Fact]
    public async Task CheckAsync_ReturnsInvalidReleaseForUnparseableVersions()
    {
        var release = Release("release-now", assets: []);
        var service = new ReleaseUpdateCheckService(new FakeLatestReleaseClient(LatestReleaseResult.Found(release)));

        var result = await service.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.InvalidRelease, result.Status);
        Assert.False(result.IsUpdateAvailable);
        Assert.Contains("could not be parsed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MapsGitHubLatestReleaseJson()
    {
        var release = GitHubReleaseJsonParser.Parse(
            """
            {
              "tag_name": "v0.2.0",
              "name": "WinAI Usage Bar v0.2.0",
              "draft": false,
              "prerelease": false,
              "html_url": "https://github.com/lingmulongtai/WinAI-Usage-Bar/releases/tag/v0.2.0",
              "assets": [
                {
                  "name": "WinAIUsageBar-0.2.0-win-x64.zip",
                  "browser_download_url": "https://github.com/download.zip",
                  "size": 123
                },
                {
                  "name": "ignored.txt",
                  "browser_download_url": "",
                  "size": 4
                }
              ]
            }
            """);

        Assert.Equal("v0.2.0", release.TagName);
        Assert.False(release.IsDraft);
        Assert.False(release.IsPrerelease);
        Assert.Equal("WinAI Usage Bar v0.2.0", release.Name);
        Assert.Single(release.Assets);
        Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", release.Assets[0].Name);
        Assert.Equal(123, release.Assets[0].SizeBytes);
    }

    private static GitHubReleaseMetadata Release(
        string tagName,
        IReadOnlyList<GitHubReleaseAsset> assets)
    {
        return new GitHubReleaseMetadata(
            tagName,
            tagName,
            IsDraft: false,
            IsPrerelease: false,
            new Uri($"https://example.test/releases/{tagName}"),
            assets);
    }

    private static GitHubReleaseAsset Asset(
        string name,
        string downloadUrl,
        long? sizeBytes)
    {
        return new GitHubReleaseAsset(name, new Uri(downloadUrl), sizeBytes);
    }

    private sealed class FakeLatestReleaseClient(LatestReleaseResult result) : IGitHubLatestReleaseClient
    {
        public Task<LatestReleaseResult> GetLatestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
