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
        var validationOutputPath = Path.Combine(Path.GetDirectoryName(resultPath)!, "validation.out.txt");
        var validationErrorPath = Path.Combine(Path.GetDirectoryName(resultPath)!, "validation.err.txt");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = resultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(validationOutputPath, "validation output");
        await File.WriteAllTextAsync(validationErrorPath, "");
        await File.WriteAllTextAsync(resultPath, """
        {
          "status": "Succeeded token-secret-123",
          "message": "Installed with access_token=raw-secret token-secret-456",
          "completedAtUtc": "2026-07-08T12:34:56Z",
          "validationStatus": "Passed token-validation-secret",
          "validationExitCode": 0,
          "validationOutputPath": "__VALIDATION_OUTPUT__",
          "validationOutputBytes": 999999,
          "validationErrorPath": "__VALIDATION_ERROR__",
          "validationErrorBytes": 999999,
          "installDirectory": "C:\\Tools\\WinAIUsageBar"
        }
        """.Replace("__VALIDATION_OUTPUT__", EscapeJsonPath(validationOutputPath), StringComparison.Ordinal)
            .Replace("__VALIDATION_ERROR__", EscapeJsonPath(validationErrorPath), StringComparison.Ordinal));

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
            Assert.Equal(Path.GetFullPath(validationOutputPath), config.Updates.LastInstallValidationOutputPath);
            Assert.Equal(new FileInfo(validationOutputPath).Length, config.Updates.LastInstallValidationOutputBytes);
            Assert.Equal(Path.GetFullPath(validationErrorPath), config.Updates.LastInstallValidationErrorPath);
            Assert.Equal(0, config.Updates.LastInstallValidationErrorBytes);
            Assert.Equal("Passed [REDACTED]", result.ValidationStatus);
            Assert.Equal(0, result.ValidationExitCode);
            Assert.Equal(Path.GetFullPath(validationOutputPath), result.ValidationOutputPath);
            Assert.Equal(new FileInfo(validationOutputPath).Length, result.ValidationOutputBytes);
            Assert.Equal(Path.GetFullPath(validationErrorPath), result.ValidationErrorPath);
            Assert.Equal(0, result.ValidationErrorBytes);
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
    public async Task RefreshAsync_IgnoresValidationLogsOutsideResultDirectory()
    {
        var paths = TestPaths();
        var resultPath = Path.Combine(paths.UpdatesDirectory, "install-1", "install-result.json");
        var nestedLogPath = Path.Combine(Path.GetDirectoryName(resultPath)!, "nested", "validation.out.txt");
        var wrongNamePath = Path.Combine(Path.GetDirectoryName(resultPath)!, "not-validation.err.txt");
        var config = AppConfig.CreateDefault();
        config.Updates.LastInstallResultPath = resultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, $$"""
        {
          "status": "Succeeded",
          "message": "Installed",
          "validationStatus": "Passed",
          "validationOutputPath": "{{EscapeJsonPath(nestedLogPath)}}",
          "validationOutputBytes": 100,
          "validationErrorPath": "{{EscapeJsonPath(wrongNamePath)}}",
          "validationErrorBytes": 200
        }
        """);

        try
        {
            var service = new UpdateInstallResultService(paths);

            var result = await service.RefreshAsync(config, CancellationToken.None);

            Assert.Equal(UpdateInstallResultRefreshStatus.Updated, result.Status);
            Assert.Equal("Passed", config.Updates.LastInstallValidationStatus);
            Assert.Null(config.Updates.LastInstallValidationOutputPath);
            Assert.Null(config.Updates.LastInstallValidationOutputBytes);
            Assert.Null(config.Updates.LastInstallValidationErrorPath);
            Assert.Null(config.Updates.LastInstallValidationErrorBytes);
            Assert.Null(result.ValidationOutputPath);
            Assert.Null(result.ValidationOutputBytes);
            Assert.Null(result.ValidationErrorPath);
            Assert.Null(result.ValidationErrorBytes);
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

    private static string EscapeJsonPath(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
