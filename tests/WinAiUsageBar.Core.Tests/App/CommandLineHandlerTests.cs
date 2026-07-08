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
        Assert.Contains("--provider <ProviderId>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--source <DataSourceKind>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--provider-catalog", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--check-for-updates", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--download-update", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--prepare-update-install", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--prune-support-artifacts", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--keep-newest <N>", output.ToString(), StringComparison.Ordinal);
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
            refreshOnce: (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                refreshCount++;
                Assert.Null(options.ProviderId);
                Assert.Null(options.SourceKind);
                return Task.FromResult(new CommandLineActionResult("refresh report body", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, refreshCount);
        Assert.Contains("refresh report body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_RunsRefreshOnceWithProviderAndSourceOptions()
    {
        using var output = new StringWriter();
        CommandLineRefreshOnceOptions? capturedOptions = null;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--refresh-once", "--provider", "Codex", "--source", "LocalAppServer"],
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
            refreshOnce: (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                capturedOptions = options;
                return Task.FromResult(new CommandLineActionResult("codex local app-server report", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Codex", capturedOptions?.ProviderId);
        Assert.Equal("LocalAppServer", capturedOptions?.SourceKind);
        Assert.Contains("codex local app-server report", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--refresh-once", "--source", "Mock")]
    [InlineData("--refresh-once", "--provider")]
    [InlineData("--refresh-once", "--provider", "Codex", "--source")]
    [InlineData("--refresh-once", "--provider", "Codex", "--provider", "ChatGPT")]
    [InlineData("--refresh-once", "--wat")]
    public async Task TryHandleAsync_ReturnsErrorForInvalidRefreshOnceOptions(params string[] args)
    {
        using var error = new StringWriter();
        var refreshCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None,
            refreshOnce: (_, _) =>
            {
                refreshCount++;
                return Task.FromResult(new CommandLineActionResult("should not run", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(0, refreshCount);
        Assert.Contains("--refresh-once", error.ToString(), StringComparison.Ordinal);
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
    public async Task TryHandleAsync_ChecksForUpdates()
    {
        using var output = new StringWriter();
        var updateCheckCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--check-for-updates"],
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
            checkForUpdates: cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                updateCheckCount++;
                return Task.FromResult(new CommandLineActionResult("update check body", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, updateCheckCount);
        Assert.Contains("update check body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenCheckForUpdatesIsUnavailable()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--check-for-updates"],
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
    public async Task TryHandleAsync_DownloadsUpdate()
    {
        using var output = new StringWriter();
        var downloadUpdateCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--download-update"],
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
            downloadUpdate: cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                downloadUpdateCount++;
                return Task.FromResult(new CommandLineActionResult("download body", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, downloadUpdateCount);
        Assert.Contains("download body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenDownloadUpdateIsUnavailable()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--download-update"],
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
    public async Task TryHandleAsync_PreparesUpdateInstall()
    {
        using var output = new StringWriter();
        CommandLinePrepareUpdateInstallOptions? capturedOptions = null;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--prepare-update-install", "--package", @"C:\Temp\update.zip", "--install-dir", @"C:\App", "--restart-after-install"],
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
            prepareUpdateInstall: (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                capturedOptions = options;
                return Task.FromResult(new CommandLineActionResult("prepare body", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(@"C:\Temp\update.zip", capturedOptions?.PackagePath);
        Assert.Equal(@"C:\App", capturedOptions?.InstallDirectory);
        Assert.True(capturedOptions?.RestartAfterInstall);
        Assert.Contains("prepare body", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--prepare-update-install")]
    [InlineData("--prepare-update-install", "--package")]
    [InlineData("--prepare-update-install", "--package", "")]
    [InlineData("--prepare-update-install", "--package", "a.zip", "--package", "b.zip")]
    [InlineData("--prepare-update-install", "--package", "a.zip", "--install-dir")]
    [InlineData("--prepare-update-install", "--package", "a.zip", "--install-dir", "C:\\A", "--install-dir", "C:\\B")]
    [InlineData("--prepare-update-install", "--package", "a.zip", "--restart-after-install", "--restart-after-install")]
    [InlineData("--prepare-update-install", "--package", "a.zip", "--unknown")]
    public async Task TryHandleAsync_ReturnsErrorForInvalidPrepareUpdateInstallOptions(params string[] args)
    {
        using var error = new StringWriter();
        var prepareCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None,
            prepareUpdateInstall: (_, _) =>
            {
                prepareCount++;
                return Task.FromResult(new CommandLineActionResult("should not run", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(0, prepareCount);
        Assert.Contains("--prepare-update-install", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenPrepareUpdateInstallIsUnavailable()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--prepare-update-install", "--package", @"C:\Temp\update.zip"],
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
    public async Task TryHandleAsync_PrunesSupportArtifactsWithDefaultKeepNewest()
    {
        using var output = new StringWriter();
        CommandLinePruneSupportArtifactsOptions? capturedOptions = null;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--prune-support-artifacts"],
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
            pruneSupportArtifacts: (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                capturedOptions = options;
                return Task.FromResult(new CommandLineActionResult("prune report body", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(5, capturedOptions?.KeepNewest);
        Assert.Contains("prune report body", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_PrunesSupportArtifactsWithKeepNewestOption()
    {
        using var output = new StringWriter();
        CommandLinePruneSupportArtifactsOptions? capturedOptions = null;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--prune-support-artifacts", "--keep-newest", "12"],
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
            pruneSupportArtifacts: (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                capturedOptions = options;
                return Task.FromResult(new CommandLineActionResult("kept twelve", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(12, capturedOptions?.KeepNewest);
        Assert.Contains("kept twelve", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--prune-support-artifacts", "--keep-newest")]
    [InlineData("--prune-support-artifacts", "--keep-newest", "0")]
    [InlineData("--prune-support-artifacts", "--keep-newest", "nope")]
    [InlineData("--prune-support-artifacts", "--keep-newest", "5", "--keep-newest", "6")]
    [InlineData("--prune-support-artifacts", "--unknown")]
    public async Task TryHandleAsync_ReturnsErrorForInvalidPruneSupportArtifactOptions(params string[] args)
    {
        using var error = new StringWriter();
        var pruneCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(1),
            ExportDiagnostics,
            HealthReport,
            ProviderCatalog,
            ValidateConfigBackup,
            RestoreConfigBackup,
            AppInfo,
            CancellationToken.None,
            pruneSupportArtifacts: (_, _) =>
            {
                pruneCount++;
                return Task.FromResult(new CommandLineActionResult("should not run", 0));
            });

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(0, pruneCount);
        Assert.Contains("--prune-support-artifacts", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsErrorWhenPruneSupportArtifactsIsUnavailable()
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--prune-support-artifacts"],
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
