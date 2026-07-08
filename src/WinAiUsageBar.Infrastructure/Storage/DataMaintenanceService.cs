using WinAiUsageBar.Infrastructure.Diagnostics;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IDataMaintenanceService
{
    Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken);

    Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken);

    Task<ConfigBackupResult> ExportConfigBackupAsync(CancellationToken cancellationToken);

    Task<DataPruneResult> PruneConfigBackupsAsync(int keepNewest, CancellationToken cancellationToken);

    Task<DataPruneResult> PruneDiagnosticsExportsAsync(int keepNewest, CancellationToken cancellationToken);

    Task<DataPruneResult> PruneCrashReportsAsync(int keepNewest, CancellationToken cancellationToken);
}

public sealed record DataMaintenanceResult(
    string Path,
    bool Deleted,
    DateTimeOffset ClearedAt);

public sealed record ConfigBackupResult(
    string Path,
    DateTimeOffset CreatedAt);

public sealed record DataPruneResult(
    string DirectoryPath,
    string SearchPattern,
    int KeepNewest,
    int MatchedCount,
    int KeptCount,
    int DeletedCount,
    long DeletedBytes,
    DateTimeOffset PrunedAt);

public sealed class DataMaintenanceService(
    AppDataPaths paths,
    IAppConfigStore configStore,
    Func<DateTimeOffset>? nowProvider = null) : IDataMaintenanceService
{
    private readonly ConfigBackupFileWriter backupWriter =
        new(JsonInfrastructureOptions.CreateIndented());
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken)
    {
        return DeleteKnownFileAsync(paths.SnapshotsPath, cancellationToken);
    }

    public Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken)
    {
        return DeleteKnownFileAsync(paths.HistoryPath, cancellationToken);
    }

    public async Task<ConfigBackupResult> ExportConfigBackupAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var createdAt = nowProvider();
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var exportPath = await backupWriter.WriteAsync(
            paths,
            $"config-backup-{createdAt:yyyyMMdd-HHmmss}",
            config,
            cancellationToken).ConfigureAwait(false);
        return new ConfigBackupResult(exportPath, createdAt);
    }

    public Task<DataPruneResult> PruneConfigBackupsAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        return PruneFileSetAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-*.json",
            keepNewest,
            cancellationToken,
            fileNamePredicate: null);
    }

    public Task<DataPruneResult> PruneDiagnosticsExportsAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        return PruneFileSetAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-*.txt",
            keepNewest,
            cancellationToken,
            fileNamePredicate: null);
    }

    public Task<DataPruneResult> PruneCrashReportsAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        return PruneFileSetAsync(
            paths.CrashReportsDirectory,
            CrashReportService.GeneratedReportSearchPattern,
            keepNewest,
            cancellationToken,
            CrashReportService.IsGeneratedCrashReportFileName);
    }

    private Task<DataMaintenanceResult> DeleteKnownFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();
        var deleted = false;

        if (File.Exists(path))
        {
            File.Delete(path);
            deleted = true;
        }

        return Task.FromResult(new DataMaintenanceResult(
            path,
            deleted,
            nowProvider()));
    }

    private Task<DataPruneResult> PruneFileSetAsync(
        string directory,
        string searchPattern,
        int keepNewest,
        CancellationToken cancellationToken,
        Func<string, bool>? fileNamePredicate)
    {
        if (keepNewest < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keepNewest), keepNewest, "At least one file must be kept.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();

        var files = Directory
            .EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists
                && (fileNamePredicate is null || fileNamePredicate(file.Name)))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var deleteCandidates = files.Skip(keepNewest).ToList();
        long deletedBytes = 0;

        foreach (var file in deleteCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            deletedBytes += file.Length;
            file.Delete();
        }

        var deletedCount = deleteCandidates.Count;
        return Task.FromResult(new DataPruneResult(
            directory,
            searchPattern,
            keepNewest,
            files.Count,
            files.Count - deletedCount,
            deletedCount,
            deletedBytes,
            nowProvider()));
    }
}
