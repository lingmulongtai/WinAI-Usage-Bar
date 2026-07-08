using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface IDiagnosticsSummaryService
{
    Task<DiagnosticsSummary> GetSummaryAsync(CancellationToken cancellationToken);
}

public sealed record DiagnosticsSummary(
    string RootDirectory,
    string ConfigPath,
    string SnapshotsPath,
    string HistoryPath,
    string DiagnosticsLogPath,
    string DiagnosticsExportsDirectory,
    string ConfigBackupsDirectory,
    int ConfigVersion,
    int ConfiguredProviderCount,
    int EnabledProviderCount,
    RefreshIntervalKind RefreshInterval,
    bool NotificationsEnabled,
    int CachedSnapshotCount,
    DateTimeOffset? LatestSnapshotUpdatedAt,
    int ConfigBackupCount,
    string? LatestConfigBackupPath,
    DateTimeOffset? LatestConfigBackupCreatedAt,
    long ConfigBackupTotalBytes,
    int DiagnosticsExportCount,
    string? LatestDiagnosticsExportPath,
    DateTimeOffset? LatestDiagnosticsExportCreatedAt,
    long DiagnosticsExportTotalBytes,
    int HistoryRetentionMaxDays,
    long HistoryRetentionMaxBytes,
    DiagnosticsFileSummary ConfigFile,
    DiagnosticsFileSummary SnapshotsFile,
    DiagnosticsFileSummary HistoryFile,
    DiagnosticsFileSummary DiagnosticsLogFile);

public sealed record DiagnosticsFileSummary(
    string Path,
    bool Exists,
    long SizeBytes,
    DateTimeOffset? LastWriteTime);

public sealed class DiagnosticsSummaryService(
    AppDataPaths paths,
    IAppConfigStore configStore,
    ISnapshotStore snapshotStore) : IDiagnosticsSummaryService
{
    public async Task<DiagnosticsSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = await snapshotStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? latestSnapshot = snapshots.Count == 0
            ? null
            : snapshots.Values.Max(snapshot => snapshot.UpdatedAt);
        var backupSummary = ReadConfigBackups(paths.ConfigBackupsDirectory);
        var exportSummary = ReadDiagnosticsExports(paths.DiagnosticsExportsDirectory);

        return new DiagnosticsSummary(
            paths.RootDirectory,
            paths.ConfigPath,
            paths.SnapshotsPath,
            paths.HistoryPath,
            paths.DiagnosticsLogPath,
            paths.DiagnosticsExportsDirectory,
            paths.ConfigBackupsDirectory,
            config.Version,
            config.Providers.Count,
            config.Providers.Count(provider => provider.IsEnabled),
            config.Refresh.Interval,
            config.Notifications.IsEnabled,
            snapshots.Count,
            latestSnapshot,
            backupSummary.Count,
            backupSummary.LatestPath,
            backupSummary.LatestCreatedAt,
            backupSummary.TotalBytes,
            exportSummary.Count,
            exportSummary.LatestPath,
            exportSummary.LatestCreatedAt,
            exportSummary.TotalBytes,
            config.HistoryRetention.MaxDays,
            config.HistoryRetention.MaxBytes,
            ReadFile(paths.ConfigPath),
            ReadFile(paths.SnapshotsPath),
            ReadFile(paths.HistoryPath),
            ReadFile(paths.DiagnosticsLogPath));
    }

    private static DiagnosticsFileSummary ReadFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return new DiagnosticsFileSummary(path, Exists: false, SizeBytes: 0, LastWriteTime: null);
        }

        return new DiagnosticsFileSummary(
            path,
            Exists: true,
            file.Length,
            new DateTimeOffset(file.LastWriteTime));
    }

    private static ConfigBackupSummary ReadConfigBackups(string directory)
    {
        var summary = ReadDirectorySummary(directory, "config-backup-*.json");
        return new ConfigBackupSummary(
            summary.Count,
            summary.LatestPath,
            summary.LatestCreatedAt,
            summary.TotalBytes);
    }

    private static DiagnosticsExportSummary ReadDiagnosticsExports(string directory)
    {
        var summary = ReadDirectorySummary(directory, "diagnostics-export-*.txt");
        return new DiagnosticsExportSummary(
            summary.Count,
            summary.LatestPath,
            summary.LatestCreatedAt,
            summary.TotalBytes);
    }

    private static FileSetSummary ReadDirectorySummary(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return new FileSetSummary(0, null, null, 0);
        }

        var files = Directory
            .EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .ToList();

        if (files.Count == 0)
        {
            return new FileSetSummary(0, null, null, 0);
        }

        var latest = files
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .First();

        return new FileSetSummary(
            files.Count,
            latest.FullName,
            new DateTimeOffset(latest.LastWriteTime),
            files.Sum(file => file.Length));
    }

    private sealed record ConfigBackupSummary(
        int Count,
        string? LatestPath,
        DateTimeOffset? LatestCreatedAt,
        long TotalBytes);

    private sealed record DiagnosticsExportSummary(
        int Count,
        string? LatestPath,
        DateTimeOffset? LatestCreatedAt,
        long TotalBytes);

    private sealed record FileSetSummary(
        int Count,
        string? LatestPath,
        DateTimeOffset? LatestCreatedAt,
        long TotalBytes);
}
