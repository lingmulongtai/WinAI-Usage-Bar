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
    int ConfigVersion,
    int ConfiguredProviderCount,
    int EnabledProviderCount,
    RefreshIntervalKind RefreshInterval,
    bool NotificationsEnabled,
    int CachedSnapshotCount,
    DateTimeOffset? LatestSnapshotUpdatedAt,
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

        return new DiagnosticsSummary(
            paths.RootDirectory,
            paths.ConfigPath,
            paths.SnapshotsPath,
            paths.HistoryPath,
            paths.DiagnosticsLogPath,
            paths.DiagnosticsExportsDirectory,
            config.Version,
            config.Providers.Count,
            config.Providers.Count(provider => provider.IsEnabled),
            config.Refresh.Interval,
            config.Notifications.IsEnabled,
            snapshots.Count,
            latestSnapshot,
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
}
