using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineHealthReportFormatterTests
{
    [Fact]
    public void Format_IncludesLocalHealthMetadata()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 6, 45, 0, TimeSpan.FromHours(9));
        var latestSnapshot = generatedAt.AddMinutes(-5);
        var diagnostics = new DiagnosticsSummary(
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\snapshots.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\history.ndjson",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics.log",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics-exports",
            ConfigVersion: 7,
            ConfiguredProviderCount: 8,
            EnabledProviderCount: 3,
            RefreshInterval: RefreshIntervalKind.FiveMinutes,
            NotificationsEnabled: true,
            CachedSnapshotCount: 2,
            LatestSnapshotUpdatedAt: latestSnapshot,
            HistoryRetentionMaxDays: 30,
            HistoryRetentionMaxBytes: 1_048_576,
            ConfigFile: FileSummary("config.json", true, 2048, latestSnapshot),
            SnapshotsFile: FileSummary("snapshots.json", true, 512, latestSnapshot),
            HistoryFile: FileSummary("history.ndjson", true, 1_048_576, latestSnapshot),
            DiagnosticsLogFile: FileSummary("diagnostics.log", false, 0, null));
        var history = new HistorySummary(
            TotalEntries: 3,
            InvalidLines: 1,
            EarliestUpdatedAt: generatedAt.AddDays(-1),
            LatestUpdatedAt: latestSnapshot,
            Providers:
            [
                new ProviderHistorySummary(
                    ProviderId.Codex,
                    "Codex",
                    EntryCount: 3,
                    LatestUpdatedAt: latestSnapshot,
                    LatestHealth: ProviderHealth.Warning,
                    LatestRemainingPercent: 42.5,
                    LatestSourceKind: DataSourceKind.LocalAppServer)
            ]);

        var report = CommandLineHealthReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            diagnostics,
            history,
            generatedAt);

        Assert.Contains("WinAI Usage Bar 1.2.3", report, StringComparison.Ordinal);
        Assert.Contains("Generated: 2026-07-08 06:45:00 +09:00", report, StringComparison.Ordinal);
        Assert.Contains("Providers: 3 enabled / 8 configured", report, StringComparison.Ordinal);
        Assert.Contains("Cached snapshots: 2", report, StringComparison.Ordinal);
        Assert.Contains("Entries: 3", report, StringComparison.Ordinal);
        Assert.Contains("Codex: 3 entries, latest Warning, remaining 42.5%, source LocalAppServer", report, StringComparison.Ordinal);
        Assert.Contains("config.json: 2 KB", report, StringComparison.Ordinal);
        Assert.Contains("diagnostics.log: Missing", report, StringComparison.Ordinal);
    }

    private static DiagnosticsFileSummary FileSummary(
        string path,
        bool exists,
        long sizeBytes,
        DateTimeOffset? lastWriteTime)
    {
        return new DiagnosticsFileSummary(path, exists, sizeBytes, lastWriteTime);
    }
}
