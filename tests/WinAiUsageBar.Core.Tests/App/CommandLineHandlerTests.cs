using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsUnhandledWhenNoArgs()
    {
        var result = await CommandLineHandler.TryHandleAsync(
            [],
            new StringWriter(),
            new StringWriter(),
            _ => Task.FromResult(0),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.False(result.Handled);
        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public async Task TryHandleAsync_WritesHelp(string arg)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            [arg],
            output,
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--version", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--refresh-once", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--provider-catalog", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--validate-config-backup", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--restore-config-backup", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_WritesVersion()
    {
        using var output = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--version"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("WinAI Usage Bar 9.8.7", output.ToString().Trim());
    }

    [Fact]
    public async Task TryHandleAsync_RunsSmokeTest()
    {
        var smokeTestCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--smoke-test"],
            new StringWriter(),
            new StringWriter(),
            _ =>
            {
                smokeTestCount++;
                return Task.FromResult(42);
            },
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(42, result.ExitCode);
        Assert.Equal(1, smokeTestCount);
    }

    [Fact]
    public async Task TryHandleAsync_ExportsDiagnostics()
    {
        using var output = new StringWriter();
        var exportCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--export-diagnostics"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            _ =>
            {
                exportCount++;
                return Task.FromResult(@"C:\Temp\diagnostics-export.txt");
            },
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, exportCount);
        Assert.Contains("Diagnostics exported to C:\\Temp\\diagnostics-export.txt", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_WritesHealthReport()
    {
        using var output = new StringWriter();
        var healthReportCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--health-report"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            _ =>
            {
                healthReportCount++;
                return Task.FromResult("health report body");
            },
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, healthReportCount);
        Assert.Contains("health report body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_RunsRefreshOnce()
    {
        using var output = new StringWriter();
        var refreshCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--refresh-once"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None,
            refreshOnce: cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                refreshCount++;
                return Task.FromResult("refresh report body");
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, refreshCount);
        Assert.Contains("refresh report body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenRefreshOnceIsUnavailable()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--refresh-once"],
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unavailable", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_WritesProviderCatalog()
    {
        using var output = new StringWriter();
        var providerCatalogCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--provider-catalog"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            () =>
            {
                providerCatalogCount++;
                return "provider catalog body";
            },
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, providerCatalogCount);
        Assert.Contains("provider catalog body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ValidatesConfigBackup()
    {
        using var output = new StringWriter();
        var validatedPath = string.Empty;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--validate-config-backup", @"C:\Temp\config-backup.json"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            (path, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                validatedPath = path;
                return Task.FromResult(new CommandLineActionResult("backup valid", 0));
            },
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(@"C:\Temp\config-backup.json", validatedPath);
        Assert.Contains("backup valid", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsValidationExitCode()
    {
        using var output = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--validate-config-backup", @"C:\Temp\missing.json"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            (_, _) => Task.FromResult(new CommandLineActionResult("backup invalid", 1)),
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("backup invalid", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenValidateConfigBackupPathIsMissing()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--validate-config-backup"],
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing path", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_RestoresConfigBackupWhenConfirmed()
    {
        using var output = new StringWriter();
        var restoredPath = string.Empty;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--restore-config-backup", @"C:\Temp\config-backup.json", "--confirm"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            (path, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                restoredPath = path;
                return Task.FromResult(new CommandLineActionResult("restore ok", 0));
            },
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(@"C:\Temp\config-backup.json", restoredPath);
        Assert.Contains("restore ok", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsRestoreExitCode()
    {
        using var output = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--restore-config-backup", @"C:\Temp\missing.json", "--confirm"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            (_, _) => Task.FromResult(new CommandLineActionResult("restore failed", 1)),
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("restore failed", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--restore-config-backup")]
    [InlineData("--restore-config-backup", @"C:\Temp\config-backup.json")]
    [InlineData("--restore-config-backup", @"C:\Temp\config-backup.json", "--force")]
    public async Task TryHandleAsync_RequiresConfirmForRestoreConfigBackup(params string[] args)
    {
        using var error = new StringWriter();
        var restoreCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            (_, _) =>
            {
                restoreCount++;
                return Task.FromResult(new CommandLineActionResult("should not run", 0));
            },
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(0, restoreCount);
        Assert.Contains("requires", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("--confirm", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--wat")]
    [InlineData("--version", "--help")]
    public async Task TryHandleAsync_ReturnsErrorForUnknownArguments(params string[] args)
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(0),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown command-line argument", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("--help", error.ToString(), StringComparison.Ordinal);
    }

    private static AppInfo AppInfo()
    {
        return new AppInfo("WinAI Usage Bar", "9.8.7.0", "9.8.7");
    }

    private static Task<string> ExportDiagnostics(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(@"C:\Temp\diagnostics-export.txt");
    }

    private static Task<string> HealthReport(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("health report");
    }

    private static string ProviderCatalog()
    {
        return "provider catalog";
    }

    private static Task<CommandLineActionResult> ValidateConfigBackup(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CommandLineActionResult($"validated {path}", 0));
    }

    private static Task<CommandLineActionResult> RestoreConfigBackup(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CommandLineActionResult($"restored {path}", 0));
    }
}
