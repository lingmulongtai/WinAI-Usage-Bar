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
}
