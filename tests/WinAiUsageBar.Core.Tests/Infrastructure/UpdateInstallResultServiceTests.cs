using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Updates;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class UpdateInstallResultServiceTests
{
    [Fact]
    public async Task RefreshAsync_UpdatesConfigFromSafeResultFileWithRedaction()
    {
        var paths = TestPaths();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = resultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, """
        {
          "status": "Succeeded token-secret-123",
          "message": "Installed with access_token=raw-secret token-secret-456",
          "completedAtUtc": "2026-07-08T12:34:56Z",
          "validationStatus": "Passed token-validation-secret",
          "validationExitCode": 0,
          "installDirectory": "C:\\Tools\\WinAIUsageBar"
        }
        """);

        try
        {
            var service = new UpdateInstallResultService(paths);

            var result = await service.RefreshAsync(config, CancellationToken.None);

            Assert.Equal(UpdateInstallResultRefreshStatus.Updated, result.Status);
            Assert.Equal(Path.GetFullPath(resultPath), result.ResultPath);
            Assert.Equal("Succeeded [REDACTED]", config.Updates.LastInstallResultStatus);
            Assert.Equal("Installed with access_token=[REDACTED] [REDACTED]", config.Updates.LastInstallResultMessage);
            Assert.Equal("Passed [REDACTED]", config.Updates.LastInstallValidationStatus);
            Assert.Equal(0, config.Updates.LastInstallValidationExitCode);
            Assert.Equal("Passed [REDACTED]", result.ValidationStatus);
            Assert.Equal(0, result.ValidationExitCode);
            Assert.Equal(
                new DateTimeOffset(2026, 7, 8, 12, 34, 56, TimeSpan.Zero),
                config.Updates.LastInstallResultCompletedAt);
            Assert.DoesNotContain("raw-secret", config.Updates.LastInstallResultMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("token-secret", config.Updates.LastInstallResultMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("validation-secret", config.Updates.LastInstallValidationStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_IgnoresUnsafeResultPath()
    {
        var paths = TestPaths();
        var outsideRoot = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var outsidePath = Path.Combine(outsideRoot, "install-result.json");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = outsidePath;
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(outsidePath, """{"status":"Succeeded","message":"outside"}""");

        try
        {
            var service = new UpdateInstallResultService(paths);

            var result = await service.RefreshAsync(config, CancellationToken.None);

            Assert.Equal(UpdateInstallResultRefreshStatus.UnsafePath, result.Status);
            Assert.Null(config.Updates.LastInstallResultStatus);
            Assert.Null(config.Updates.LastInstallResultMessage);
            Assert.Null(config.Updates.LastInstallResultCompletedAt);
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_ReportsInvalidJsonWithoutChangingConfig()
    {
        var paths = TestPaths();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = resultPath;
        config.Updates.LastInstallResultStatus = "Previous";
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, "{ this is not json");

        try
        {
            var service = new UpdateInstallResultService(paths);

            var result = await service.RefreshAsync(config, CancellationToken.None);

            Assert.Equal(UpdateInstallResultRefreshStatus.InvalidJson, result.Status);
            Assert.Equal("Previous", config.Updates.LastInstallResultStatus);
            Assert.Null(config.Updates.LastInstallResultMessage);
            Assert.Null(config.Updates.LastInstallResultCompletedAt);
        }
        finally
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_ReportsMissingFileWithoutChangingConfig()
    {
        var paths = TestPaths();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = resultPath;

        var service = new UpdateInstallResultService(paths);

        var result = await service.RefreshAsync(config, CancellationToken.None);

        Assert.Equal(UpdateInstallResultRefreshStatus.Missing, result.Status);
        Assert.Null(config.Updates.LastInstallResultStatus);
        Assert.Null(config.Updates.LastInstallResultMessage);
        Assert.Null(config.Updates.LastInstallResultCompletedAt);
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }
}
