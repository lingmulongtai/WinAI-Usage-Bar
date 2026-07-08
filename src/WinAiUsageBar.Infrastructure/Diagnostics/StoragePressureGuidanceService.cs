namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface IStoragePressureGuidanceService
{
    IReadOnlyList<StoragePressureGuidanceItem> CreateGuidance(DiagnosticsSummary summary);
}

public enum StoragePressureLevel
{
    Ok,
    Watch,
    High
}

public sealed record StoragePressureGuidanceItem(
    string Title,
    StoragePressureLevel Level,
    string Detail,
    string Recommendation);

public sealed class StoragePressureGuidanceService : IStoragePressureGuidanceService
{
    private const long WatchLogBytes = 5_000_000;
    private const long HighLogBytes = 25_000_000;
    private const long WatchBackupBytes = 25_000_000;
    private const long HighBackupBytes = 100_000_000;
    private const int WatchBackupCount = 25;
    private const int HighBackupCount = 50;
    private const long WatchExportBytes = 50_000_000;
    private const long HighExportBytes = 200_000_000;
    private const int WatchExportCount = 20;
    private const int HighExportCount = 50;

    public IReadOnlyList<StoragePressureGuidanceItem> CreateGuidance(DiagnosticsSummary summary)
    {
        return
        [
            HistoryGuidance(summary),
            BackupGuidance(summary),
            DiagnosticsExportGuidance(summary),
            DiagnosticsLogGuidance(summary)
        ];
    }

    private static StoragePressureGuidanceItem HistoryGuidance(DiagnosticsSummary summary)
    {
        var size = summary.HistoryFile.Exists ? summary.HistoryFile.SizeBytes : 0;
        var maxBytes = Math.Max(summary.HistoryRetentionMaxBytes, 1);
        var ratio = size / (double)maxBytes;
        var level = ratio >= 0.9d
            ? StoragePressureLevel.High
            : ratio >= 0.75d
                ? StoragePressureLevel.Watch
                : StoragePressureLevel.Ok;

        var recommendation = level switch
        {
            StoragePressureLevel.High => "Use Clear History soon, or lower history retention before the file hits the configured limit.",
            StoragePressureLevel.Watch => "Review history retention and clear old history after exporting diagnostics if you need a support bundle.",
            _ => "No action needed. Keep the current retention unless you want less local history."
        };

        return new StoragePressureGuidanceItem(
            "Retained history",
            level,
            $"history.ndjson uses {FormatBytes(size)} of {FormatBytes(maxBytes)}.",
            recommendation);
    }

    private static StoragePressureGuidanceItem BackupGuidance(DiagnosticsSummary summary)
    {
        var level = summary.ConfigBackupTotalBytes >= HighBackupBytes || summary.ConfigBackupCount >= HighBackupCount
            ? StoragePressureLevel.High
            : summary.ConfigBackupTotalBytes >= WatchBackupBytes || summary.ConfigBackupCount >= WatchBackupCount
                ? StoragePressureLevel.Watch
                : StoragePressureLevel.Ok;

        var recommendation = level switch
        {
            StoragePressureLevel.High => "Use Prune Old Backups or --prune-support-artifacts, then archive older config backups if you need custom retention.",
            StoragePressureLevel.Watch => "Review config-backups/ during maintenance and prune older backups after confirming a known-good backup exists.",
            _ => "No action needed. Keep at least one known-good config backup before changing settings."
        };

        return new StoragePressureGuidanceItem(
            "Config backups",
            level,
            $"{summary.ConfigBackupCount} backup file(s) use {FormatBytes(summary.ConfigBackupTotalBytes)}.",
            recommendation);
    }

    private static StoragePressureGuidanceItem DiagnosticsLogGuidance(DiagnosticsSummary summary)
    {
        var size = summary.DiagnosticsLogFile.Exists ? summary.DiagnosticsLogFile.SizeBytes : 0;
        var level = size >= HighLogBytes
            ? StoragePressureLevel.High
            : size >= WatchLogBytes
                ? StoragePressureLevel.Watch
                : StoragePressureLevel.Ok;

        var recommendation = level switch
        {
            StoragePressureLevel.High => "Export diagnostics if you need the log, then close the app and prune the old diagnostics log from the data folder.",
            StoragePressureLevel.Watch => "If you are no longer investigating a problem, review the diagnostics log during maintenance.",
            _ => "No action needed. Diagnostics logging is within the normal MVP range."
        };

        return new StoragePressureGuidanceItem(
            "Diagnostics log",
            level,
            $"diagnostics.log uses {FormatBytes(size)}.",
            recommendation);
    }

    private static StoragePressureGuidanceItem DiagnosticsExportGuidance(DiagnosticsSummary summary)
    {
        var level = summary.DiagnosticsExportTotalBytes >= HighExportBytes || summary.DiagnosticsExportCount >= HighExportCount
            ? StoragePressureLevel.High
            : summary.DiagnosticsExportTotalBytes >= WatchExportBytes || summary.DiagnosticsExportCount >= WatchExportCount
                ? StoragePressureLevel.Watch
                : StoragePressureLevel.Ok;

        var recommendation = level switch
        {
            StoragePressureLevel.High => "Use Prune Old Diagnostics Exports or --prune-support-artifacts, then archive needed support bundles if required.",
            StoragePressureLevel.Watch => "Review diagnostics-exports/ during maintenance and prune bundles that are not tied to active investigations.",
            _ => "No action needed. Diagnostics exports are within the normal MVP range."
        };

        return new StoragePressureGuidanceItem(
            "Diagnostics exports",
            level,
            $"{summary.DiagnosticsExportCount} export file(s) use {FormatBytes(summary.DiagnosticsExportTotalBytes)}.",
            recommendation);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var units = new[] { "KB", "MB", "GB" };
        var value = bytes / 1024d;
        foreach (var unit in units)
        {
            if (value < 1024d || unit == units[^1])
            {
                return $"{value:0.##} {unit}";
            }

            value /= 1024d;
        }

        return $"{bytes} B";
    }
}
