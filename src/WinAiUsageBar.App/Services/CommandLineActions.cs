using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineActions
{
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

        var diagnostics = await diagnosticsService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var history = await historyService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);

        return CommandLineHealthReportFormatter.Format(
            AppInfoProvider.Get(),
            diagnostics,
            history,
            DateTimeOffset.Now);
    }

    public static string CreateProviderCatalog()
    {
        return CommandLineProviderCatalogFormatter.Format(ProviderDescriptors.All);
    }
}
