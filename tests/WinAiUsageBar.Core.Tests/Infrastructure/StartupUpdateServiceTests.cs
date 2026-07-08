using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class StartupUpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 9, 30, 0, TimeSpan.FromHours(9));

    [Fact]
    public async Task RunAsync_SkipsWhenStartupCheckIsDisabled()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.CheckOnStartup = false;
        var store = new InMemoryConfigStore(config);
        var updateCheck = new FakeUpdateCheckService(UpdateAvailable());
        var service = CreateService(store, updateCheck);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.Disabled, result.Status);
        Assert.Equal(0, updateCheck.CheckCount);
        Assert.Equal("Disabled", config.Updates.LastStatus);
        Assert.Null(config.Updates.LastCheckedAt);
    }

    [Fact]
    public async Task RunAsync_SkipsRecentChecksWithoutCallingGitHub()
    {
        var lastChecked = Now.AddHours(-2);
        var config = AppConfig.CreateDefault();
        config.Updates.MinimumCheckIntervalHours = 24;
        config.Updates.LastCheckedAt = lastChecked;
        config.Updates.LastLatestVersion = "0.1.0";
        var store = new InMemoryConfigStore(config);
        var updateCheck = new FakeUpdateCheckService(UpdateAvailable());
        var service = CreateService(store, updateCheck);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.SkippedRecentCheck, result.Status);
        Assert.Equal(0, updateCheck.CheckCount);
        Assert.Equal("SkippedRecentCheck", config.Updates.LastStatus);
        Assert.Equal(lastChecked, config.Updates.LastCheckedAt);
        Assert.Contains("still fresh", config.Updates.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReconcilesExistingInstallResultBeforeSkippingRecentCheck()
    {
        var paths = TestPaths();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        var lastChecked = Now.AddHours(-2);
        var config = AppConfig.CreateDefault();
        config.Updates.MinimumCheckIntervalHours = 24;
        config.Updates.LastCheckedAt = lastChecked;
        config.Updates.LastLatestVersion = "0.2.0";
        config.Updates.LastInstallScriptPath = Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1");
        config.Updates.LastInstallResultPath = resultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, """
        {
          "status": "Succeeded",
          "message": "Installed with token-secret-123",
          "completedAtUtc": "2026-07-08T00:30:00Z"
        }
        """);
        var store = new InMemoryConfigStore(config);
        var updateCheck = new FakeUpdateCheckService(UpdateAvailable());
        var service = CreateService(store, updateCheck, paths: paths);

        try
        {
            var result = await service.RunAsync(Request(), CancellationToken.None);

            Assert.Equal(StartupUpdateStatus.SkippedRecentCheck, result.Status);
            Assert.Equal(0, updateCheck.CheckCount);
            Assert.Equal("Succeeded", config.Updates.LastInstallResultStatus);
            Assert.Equal("Installed with [REDACTED]", config.Updates.LastInstallResultMessage);
            Assert.Equal(
                new DateTimeOffset(2026, 7, 8, 0, 30, 0, TimeSpan.Zero),
                config.Updates.LastInstallResultCompletedAt);
            Assert.Equal(resultPath, config.Updates.LastInstallResultPath);
        }
        finally
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RecordsNoUpdateWhenLatestIsCurrent()
    {
        var config = AppConfig.CreateDefault();
        var store = new InMemoryConfigStore(config);
        var updateCheck = new FakeUpdateCheckService(UpToDate());
        var downloader = new FakeUpdatePackageDownloader(Downloaded(TestPaths()));
        var service = CreateService(store, updateCheck, downloader);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.NoUpdate, result.Status);
        Assert.Equal(1, updateCheck.CheckCount);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Equal("NoUpdate", config.Updates.LastStatus);
        Assert.Equal("0.1.0", config.Updates.LastCurrentVersion);
        Assert.Equal("0.1.0", config.Updates.LastLatestVersion);
    }

    [Fact]
    public async Task RunAsync_RecordsAvailableWhenAutomaticDownloadIsDisabled()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.DownloadAutomatically = false;
        var store = new InMemoryConfigStore(config);
        var downloader = new FakeUpdatePackageDownloader(Downloaded(TestPaths()));
        var service = CreateService(store, new FakeUpdateCheckService(UpdateAvailable()), downloader);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Equal("UpdateAvailable", config.Updates.LastStatus);
        Assert.Equal("0.2.0", config.Updates.LastLatestVersion);
        Assert.Null(config.Updates.LastPackagePath);
    }

    [Fact]
    public async Task RunAsync_DownloadsWhenAutomaticInstallIsDisabled()
    {
        var paths = TestPaths();
        var config = AppConfig.CreateDefault();
        config.Updates.DownloadAutomatically = true;
        config.Updates.InstallAutomatically = false;
        var store = new InMemoryConfigStore(config);
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var service = CreateService(
            store,
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.Downloaded, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "WinAIUsageBar-0.2.0-win-x64.zip"), result.PackagePath);
        Assert.Equal(result.PackagePath, config.Updates.LastPackagePath);
        Assert.Null(config.Updates.LastInstallScriptPath);
    }

    [Fact]
    public async Task RunAsync_DownloadsPreparesAndLaunchesWhenAutomaticInstallIsEnabled()
    {
        var paths = TestPaths();
        var config = AppConfig.CreateDefault();
        config.Updates.DownloadAutomatically = true;
        config.Updates.InstallAutomatically = true;
        var store = new InMemoryConfigStore(config);
        var downloader = new FakeUpdatePackageDownloader(Downloaded(paths));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(paths));
        var launcher = new FakeUpdateInstallLaunchService(Launched(paths));
        var service = CreateService(
            store,
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader,
            preparation,
            launcher);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.InstallLaunched, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(1, preparation.PrepareCount);
        Assert.Equal(1, launcher.LaunchCount);
        Assert.Equal(@"C:\App", preparation.LastRequest?.InstallDirectory);
        Assert.Equal(777, preparation.LastRequest?.ProcessIdToWait);
        Assert.True(preparation.LastRequest?.RestartAfterInstall);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1"), result.InstallScriptPath);
        Assert.Equal(result.InstallScriptPath, config.Updates.LastInstallScriptPath);
        Assert.Equal(Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json"), result.InstallResultPath);
        Assert.Equal(result.InstallResultPath, config.Updates.LastInstallResultPath);
        Assert.Equal("0.2.0", config.Updates.LastInstallLaunchedVersion);
    }

    [Fact]
    public async Task RunAsync_DoesNotLaunchSameInstallVersionTwice()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.DownloadAutomatically = true;
        config.Updates.InstallAutomatically = true;
        config.Updates.LastInstallLaunchedVersion = "0.2.0";
        config.Updates.LastPackagePath = @"C:\Updates\package.zip";
        config.Updates.LastInstallScriptPath = @"C:\Updates\install\apply-update.ps1";
        var store = new InMemoryConfigStore(config);
        var updateCheck = new FakeUpdateCheckService(UpdateAvailable());
        var downloader = new FakeUpdatePackageDownloader(Downloaded(TestPaths()));
        var preparation = new FakeUpdateInstallPreparationService(Prepared(TestPaths()));
        var launcher = new FakeUpdateInstallLaunchService(Launched(TestPaths()));
        var service = CreateService(
            store,
            updateCheck,
            downloader,
            preparation,
            launcher);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.InstallAlreadyLaunched, result.Status);
        Assert.Equal(1, updateCheck.CheckCount);
        Assert.Equal(0, downloader.DownloadCount);
        Assert.Equal(0, preparation.PrepareCount);
        Assert.Equal(0, launcher.LaunchCount);
        Assert.Equal("InstallAlreadyLaunched", config.Updates.LastStatus);
        Assert.Equal("0.2.0", config.Updates.LastInstallLaunchedVersion);
        Assert.Equal(Now, config.Updates.LastCheckedAt);
        Assert.Equal(@"C:\Updates\package.zip", result.PackagePath);
        Assert.Equal(@"C:\Updates\install\apply-update.ps1", result.InstallScriptPath);
        Assert.Equal(@"C:\Updates\install\install-result.json", result.InstallResultPath);
    }

    [Fact]
    public async Task RunAsync_RecordsDownloadFailure()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.DownloadAutomatically = true;
        var store = new InMemoryConfigStore(config);
        var downloader = new FakeUpdatePackageDownloader(new UpdateDownloadResult(
            UpdateDownloadStatus.ChecksumMismatch,
            "hash mismatch",
            PackagePath: null,
            ChecksumPath: null,
            ExpectedSha256: new string('a', 64),
            ActualSha256: new string('b', 64)));
        var service = CreateService(
            store,
            new FakeUpdateCheckService(UpdateAvailable()),
            downloader);

        var result = await service.RunAsync(Request(), CancellationToken.None);

        Assert.Equal(StartupUpdateStatus.DownloadFailed, result.Status);
        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal("DownloadFailed", config.Updates.LastStatus);
        Assert.Equal("hash mismatch", config.Updates.LastMessage);
    }

    private static StartupUpdateService CreateService(
        IAppConfigStore store,
        IReleaseUpdateCheckService? updateCheck = null,
        IUpdatePackageDownloader? downloader = null,
        IUpdateInstallPreparationService? preparation = null,
        IUpdateInstallLaunchService? launcher = null,
        AppDataPaths? paths = null)
    {
        paths ??= TestPaths();
        return new StartupUpdateService(
            store,
            updateCheck ?? new FakeUpdateCheckService(UpToDate()),
            downloader ?? new FakeUpdatePackageDownloader(Downloaded(paths)),
            preparation ?? new FakeUpdateInstallPreparationService(Prepared(paths)),
            launcher ?? new FakeUpdateInstallLaunchService(Launched(paths)),
            paths,
            () => Now);
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }

    private static StartupUpdateRequest Request()
    {
        return new StartupUpdateRequest(
            CurrentVersion: "0.1.0",
            InstallDirectory: @"C:\App",
            ProcessIdToWait: 777);
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

    private sealed class InMemoryConfigStore(AppConfig config) : IAppConfigStore
    {
        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(config);
        }

        public Task SaveAsync(AppConfig nextConfig, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            config = nextConfig;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUpdateCheckService(ReleaseUpdateCheckResult result) : IReleaseUpdateCheckService
    {
        public int CheckCount { get; private set; }

        public Task<ReleaseUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCount++;
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
