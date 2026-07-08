using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class LatestUpdateInstallServiceTests
{
    [Fact]
    public async Task InstallLatestAsync_SkipsWhenUpToDate()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var launcher = new FakeUpdateInstallLaunchService(Launched(paths));
        var service = new LatestUpdateInstallService(
            new FakeUpdateCheckService(UpToDate()),
            downloader,
            preparation,
            launcher,
            paths);

        var result = await service.InstallLatestAsync(Request(), CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.SkippedNoUpdate, result.Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, result.UpdateCheck?.Status);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task InstallLatestAsync_ReturnsDownloadFailedWhenVerificationFails()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.ChecksumMismatch,
            "hash mismatch",
            PackagePath: null,
            ChecksumPath: null,
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('b', 64)));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var launcher = new FakeUpdateInstallLaunchService(Launched(paths));
        var service = new LatestUpdateInstallService(
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation,
            launcher,
            paths);

        var result = await service.InstallLatestAsync(Request(), CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.DownloadFailed, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task InstallLatestAsync_ReturnsPreparationFailedWhenScriptCannotBePrepared()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(new UpdateInstallPreparationResult(
            UpdateInstallPreparationStatus.InvalidInstallDirectory,
            "bad install",
            ScriptPath: null,
            Command: null,
            PackagePath: null,
            InstallDirectory: null,
            StagingDirectory: null,
            BackupDirectory: null));
        var launcher = new FakeUpdateInstallLaunchService(Launched(paths));
        var service = new LatestUpdateInstallService(
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation,
            launcher,
            paths);

        var result = await service.InstallLatestAsync(Request(restartAfterInstall: true), CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.PreparationFailed, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"), preparation.LastRequest?.PackagePath);
        Assert.Equal(@"C:\App", preparation.LastRequest?.InstallDirectory);
        Assert.Equal(777, preparation.LastRequest?.ProcessIdToWait);
        Assert.True(preparation.LastRequest?.RestartAfterInstall);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task InstallLatestAsync_ReturnsLaunchFailedWhenPreparedScriptCannotLaunch()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var launcher = new FakeUpdateInstallLaunchService(new UpdateInstallLaunchResult(
            UpdateInstallLaunchStatus.InvalidScript,
            "bad script",
            ScriptPath: null,
            Command: null,
            ProcessId: null));
        var service = new LatestUpdateInstallService(
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation,
            launcher,
            paths);

        var result = await service.InstallLatestAsync(Request(), CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.LaunchFailed, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(1, launcher.LaunchCount);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"), launcher.LastRequest?.ScriptPath);
    }

    [Fact]
    public async Task InstallLatestAsync_DownloadsPreparesAndLaunchesUpdate()
    {
        var paths = TestPaths();
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var launcher = new FakeUpdateInstallLaunchService(Launched(paths));
        var service = new LatestUpdateInstallService(
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation,
            launcher,
            paths);

        var result = await service.InstallLatestAsync(Request(), CancellationToken.None);

        Assert.Equal(LatestUpdateInstallStatus.Launched, result.Status);
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.UpdateCheck?.Status);
        Assert.Equal(UpdateDownloadStatus.Downloaded, result.Download?.Status);
        Assert.Equal(UpdateInstallPreparationStatus.Prepared, result.Preparation?.Status);
        Assert.Equal(UpdateInstallLaunchStatus.Launched, result.Launch?.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(1, launcher.LaunchCount);
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }

    private static LatestUpdateInstallRequest Request(bool restartAfterInstall = false)
    {
        return new LatestUpdateInstallRequest(
            CurrentVersion: "0.1.0",
            InstallDirectory: @"C:\App",
            ProcessIdToWait: 777,
            restartAfterInstall);
    }

    private static ReleaseUpdateCheckResult UpToDate()
    {
        return new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpToDate,
            CurrentVersion: "0.1.0",
            LatestVersion: "0.1.0",
            "The current app version is up to date.",
            IsUpdateAvailable: false,
            ReleasePageUrl: new Uri("https://example.test/releases/v0.1.0"),
            Package: null,
            Checksum: null);
    }

    private static ReleaseUpdateCheckResult UpdateAvailable()
    {
        return new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.0",
            LatestVersion: "0.2.0",
            "A newer GitHub release is available.",
            IsUpdateAvailable: true,
            ReleasePageUrl: new Uri("https://example.test/releases/v0.2.0"),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip",
                new Uri("https://example.test/package.zip"),
                2048),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip.sha256",
                new Uri("https://example.test/package.zip.sha256"),
                128));
    }

    private static UpdateDownloadResult Downloaded(AppDataPaths paths)
    {
        return new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "downloaded",
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip.sha256"),
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('a', 64));
    }

    private static UpdateInstallPreparationResult Prepared(AppDataPaths paths)
    {
        return new UpdateInstallPreparationResult(
            UpdateInstallPreparationStatus.Prepared,
            "prepared",
            Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"),
            @"powershell -ExecutionPolicy Bypass -File apply-update.ps1",
            Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"),
            @"C:\App",
            Path.Combine(paths.UpdatesDirectory, "install-1", "staging"),
            Path.Combine(paths.UpdatesDirectory, "install-1", "backup"));
    }

    private static UpdateInstallLaunchResult Launched(AppDataPaths paths)
    {
        return new UpdateInstallLaunchResult(
            UpdateInstallLaunchStatus.Launched,
            "launched",
            Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"),
            @"powershell -NoProfile -ExecutionPolicy Bypass -File apply-update.ps1",
            ProcessId: 4321);
    }

    private sealed class FakeUpdateCheckService(ReleaseUpdateCheckResult result) : IReleaseUpdateCheckService
    {
        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("0.1.0", currentVersion);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUpdatePackageDownloader(UpdateDownloadResult result) : IUpdatePackageDownloader
    {
        public int DownloadCount { get; private set; }

        public Task<UpdateDownloadResult> DownloadAndVerifyAsync(
            UpdatePackageAsset package,
            UpdatePackageAsset checksum,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadCount++;
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip", package.Name);
            Assert.Equal("WinAIUsageBar-0.2.0-win-x64.zip.sha256", checksum.Name);
            Assert.False(string.IsNullOrWhiteSpace(targetDirectory));
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUpdateInstallPreparationService(
        UpdateInstallPreparationResult result) : IUpdateInstallPreparationService
    {
        public int PrepareCount { get; private set; }

        public UpdateInstallPreparationRequest? LastRequest { get; private set; }

        public Task<UpdateInstallPreparationResult> PrepareAsync(
            UpdateInstallPreparationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUpdateInstallLaunchService(
        UpdateInstallLaunchResult result) : IUpdateInstallLaunchService
    {
        public int LaunchCount { get; private set; }

        public UpdateInstallLaunchRequest? LastRequest { get; private set; }

        public Task<UpdateInstallLaunchResult> LaunchAsync(
            UpdateInstallLaunchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
