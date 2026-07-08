using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineActions
{
    private static readonly CliCommandCheck[] HealthReportCliChecks =
    [
        new("codex", "--version"),
        new("claude", "--version"),
        new("gh", "--version"),
        new("git", "--version")
    ];

    public static async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var service = new DiagnosticsExportService(paths);
        var result = await service.ExportAsync(cancellationToken).ConfigureAwait(false);
        return result.Path;
    }

    public static async Task<string> CreateHealthReportAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var configStore = new JsonAppConfigStore(paths);
        var snapshotStore = new JsonSnapshotStore(paths);
        var diagnosticsService = new DiagnosticsSummaryService(paths, configStore, snapshotStore);
        var historyService = new HistorySummaryService(paths);
        var cliEnvironmentService = new CliEnvironmentService();

        var diagnostics = await diagnosticsService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var history = await historyService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var cliEnvironment = await cliEnvironmentService.GetReportAsync(HealthReportCliChecks, cancellationToken)
            .ConfigureAwait(false);

        return CommandLineHealthReportFormatter.Format(
            AppInfoProvider.Get(),
            diagnostics,
            history,
            DateTimeOffset.Now,
            cliEnvironment);
    }

    public static string CreateProviderCatalog()
    {
        return CommandLineProviderCatalogFormatter.Format(ProviderDescriptors.All);
    }

    public static async Task<string> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var services = AppCompositionRoot.CreateServices(paths, new NoOpAppNotificationService());
        var refreshService = services.RefreshService;

        try
        {
            await refreshService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await refreshService.RefreshNowAsync(cancellationToken).ConfigureAwait(false);
            return CommandLineRefreshReportFormatter.Format(
                AppInfoProvider.Get(),
                refreshService.CurrentSnapshots,
                DateTimeOffset.Now);
        }
        finally
        {
            await refreshService.DisposeAsync().ConfigureAwait(false);
        }
    }

    public static async Task<CommandLineActionResult> ValidateConfigBackupAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var service = new ConfigBackupValidationService();
        var result = await service.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineConfigBackupValidationFormatter.Format(result),
            result.IsValid ? 0 : 1);
    }

    public static async Task<CommandLineActionResult> RestoreConfigBackupAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var configStore = new JsonAppConfigStore(paths);
        var service = new ConfigBackupRestoreService(paths, configStore);
        var result = await service.RestoreAsync(path, cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineConfigBackupRestoreFormatter.Format(result),
            result.Restored ? 0 : 1);
    }
}

public sealed record CommandLineActionResult(
    string Output,
    int ExitCode);
