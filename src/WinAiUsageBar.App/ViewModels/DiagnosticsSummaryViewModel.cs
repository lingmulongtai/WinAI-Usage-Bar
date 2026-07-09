using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.App.ViewModels;

public sealed class DiagnosticsSummaryViewModel
{
    public DiagnosticsSummaryViewModel(
        DiagnosticsSummary summary,
        IReadOnlyList<CrashReportFile>? recentCrashReports = null)
    {
        RootDirectory = summary.RootDirectory;
        ConfigPath = summary.ConfigPath;
        SnapshotsPath = summary.SnapshotsPath;
        HistoryPath = summary.HistoryPath;
        DiagnosticsLogPath = summary.DiagnosticsLogPath;
        DiagnosticsExportsDirectory = summary.DiagnosticsExportsDirectory;
        CrashReportsDirectory = summary.CrashReportsDirectory
            ?? Path.Combine(summary.RootDirectory, "crash-reports");
        ConfigBackupsDirectory = summary.ConfigBackupsDirectory;
        ConfigText = $"Config v{summary.ConfigVersion} / {summary.EnabledProviderCount} of {summary.ConfiguredProviderCount} providers enabled";
        RefreshText = $"Refresh: {summary.RefreshInterval} / Notifications: {(summary.NotificationsEnabled ? "On" : "Off")}";
        SnapshotText = summary.LatestSnapshotUpdatedAt is null
            ? $"{summary.CachedSnapshotCount} cached snapshot(s)"
            : $"{summary.CachedSnapshotCount} cached snapshot(s) / latest {summary.LatestSnapshotUpdatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        ConfigBackupText = summary.LatestConfigBackupCreatedAt is null
            ? $"{summary.ConfigBackupCount} config backup(s)"
            : $"{summary.ConfigBackupCount} config backup(s), {FormatBytes(summary.ConfigBackupTotalBytes)} total / latest {summary.LatestConfigBackupCreatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        LatestConfigBackupPath = summary.LatestConfigBackupPath;
        DiagnosticsExportText = summary.LatestDiagnosticsExportCreatedAt is null
            ? $"{summary.DiagnosticsExportCount} diagnostics export(s)"
            : $"{summary.DiagnosticsExportCount} diagnostics export(s), {FormatBytes(summary.DiagnosticsExportTotalBytes)} total / latest {summary.LatestDiagnosticsExportCreatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        LatestDiagnosticsExportPath = summary.LatestDiagnosticsExportPath;
        CrashReportText = summary.LatestCrashReportCreatedAt is null
            ? $"{summary.CrashReportCount} crash report(s)"
            : $"{summary.CrashReportCount} crash report(s), {FormatBytes(summary.CrashReportTotalBytes)} total / latest {summary.LatestCrashReportCreatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        LatestCrashReportPath = summary.LatestCrashReportPath;
        HistoryText = $"History retention: {summary.HistoryRetentionMaxDays} day(s), {FormatBytes(summary.HistoryRetentionMaxBytes)} max";
        Files =
        [
            new DiagnosticsFileStatusViewModel("config.json", summary.ConfigFile),
            new DiagnosticsFileStatusViewModel("snapshots.json", summary.SnapshotsFile),
            new DiagnosticsFileStatusViewModel("history.ndjson", summary.HistoryFile),
            new DiagnosticsFileStatusViewModel("diagnostics.log", summary.DiagnosticsLogFile)
        ];
        RecentCrashReports = (recentCrashReports ?? [])
            .Select(report => new CrashReportMetadataViewModel(report))
            .ToList();
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string SnapshotsPath { get; }

    public string HistoryPath { get; }

    public string DiagnosticsLogPath { get; }

    public string DiagnosticsExportsDirectory { get; }

    public string CrashReportsDirectory { get; }

    public string ConfigBackupsDirectory { get; }

    public string ConfigText { get; }

    public string RefreshText { get; }

    public string SnapshotText { get; }

    public string ConfigBackupText { get; }

    public string? LatestConfigBackupPath { get; }

    public string DiagnosticsExportText { get; }

    public string? LatestDiagnosticsExportPath { get; }

    public string CrashReportText { get; }

    public string? LatestCrashReportPath { get; }

    public string HistoryText { get; }

    public IReadOnlyList<DiagnosticsFileStatusViewModel> Files { get; }

    public IReadOnlyList<CrashReportMetadataViewModel> RecentCrashReports { get; }

    public IReadOnlyList<string> OverviewLines =>
    [
        ConfigText,
        RefreshText,
        SnapshotText,
        ConfigBackupText,
        DiagnosticsExportText,
        CrashReportText,
        HistoryText,
        $"Config: {ConfigPath}",
        $"Snapshots: {SnapshotsPath}",
        $"History: {HistoryPath}",
        $"Diagnostics log: {DiagnosticsLogPath}",
        $"Exports: {DiagnosticsExportsDirectory}",
        $"Crash reports: {CrashReportsDirectory}",
        $"Config backups: {ConfigBackupsDirectory}",
        LatestConfigBackupPath is null ? "Latest config backup: n/a" : $"Latest config backup: {LatestConfigBackupPath}",
        LatestDiagnosticsExportPath is null ? "Latest diagnostics export: n/a" : $"Latest diagnostics export: {LatestDiagnosticsExportPath}",
        LatestCrashReportPath is null ? "Latest crash report: n/a" : $"Latest crash report: {LatestCrashReportPath}"
    ];

    public static string FormatBytes(long bytes)
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

public sealed class DiagnosticsFileStatusViewModel
{
    public DiagnosticsFileStatusViewModel(string label, DiagnosticsFileSummary file)
    {
        Label = label;
        Path = file.Path;
        StatusText = file.Exists
            ? $"{DiagnosticsSummaryViewModel.FormatBytes(file.SizeBytes)} / modified {file.LastWriteTime:yyyy-MM-dd HH:mm:ss zzz}"
            : "Missing";
    }

    public string Label { get; }

    public string Path { get; }

    public string StatusText { get; }
}

public sealed class CrashReportMetadataViewModel
{
    public CrashReportMetadataViewModel(CrashReportFile report)
    {
        FileName = System.IO.Path.GetFileName(report.Path) ?? report.Path;
        Path = report.Path;
        TimestampText = report.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz");
        Source = string.IsNullOrWhiteSpace(report.Source) ? "Unknown" : report.Source;
        ExceptionType = string.IsNullOrWhiteSpace(report.ExceptionType) ? "Unknown" : report.ExceptionType;
        AppVersionText = string.IsNullOrWhiteSpace(report.AppVersion) ? "n/a" : report.AppVersion;
        SizeText = DiagnosticsSummaryViewModel.FormatBytes(report.SizeBytes);
        StatusText = report.MetadataAvailable ? "Metadata parsed" : report.MetadataStatus;
        SummaryText = $"{TimestampText} / {Source} / {ExceptionType} / app {AppVersionText} / {SizeText} / {StatusText}";
    }

    public string FileName { get; }

    public string Path { get; }

    public string TimestampText { get; }

    public string Source { get; }

    public string ExceptionType { get; }

    public string AppVersionText { get; }

    public string SizeText { get; }

    public string StatusText { get; }

    public string SummaryText { get; }
}

public sealed class CrashReportDetailViewModel
{
    public CrashReportDetailViewModel(CrashReportDetail detail)
    {
        FileName = string.IsNullOrWhiteSpace(detail.FileName) ? "n/a" : detail.FileName;
        StatusText = SafeDisplay(detail.StatusMessage);
        IsAvailable = detail.Status is CrashReportDetailStatus.Available;
        MessageText = string.IsNullOrWhiteSpace(detail.MessagePreview)
            ? "No redacted message preview is available."
            : SafeDisplay(detail.MessagePreview);
        HasMessage = !string.IsNullOrWhiteSpace(detail.MessagePreview);

        var lines = new List<string>
        {
            $"File: {FileName}",
            $"Status: {detail.Status}",
            detail.CreatedAt is null
                ? "Created: n/a"
                : $"Created: {detail.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"Source: {SafeDisplay(detail.Source)}",
            $"Exception: {SafeDisplay(detail.ExceptionType)}",
            $"App version: {SafeDisplay(string.IsNullOrWhiteSpace(detail.AppVersion) ? "n/a" : detail.AppVersion)}",
            $"Size: {DiagnosticsSummaryViewModel.FormatBytes(detail.SizeBytes)}"
        };

        if (detail.MessageTruncated)
        {
            lines.Add("Message preview: truncated");
        }

        MetadataLines = lines;
    }

    public string FileName { get; }

    public bool IsAvailable { get; }

    public string StatusText { get; }

    public bool HasMessage { get; }

    public string MessageText { get; }

    public IReadOnlyList<string> MetadataLines { get; }

    private static string SafeDisplay(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? "n/a"
            : DiagnosticRedactor.RedactForDisplay(text);
    }
}
