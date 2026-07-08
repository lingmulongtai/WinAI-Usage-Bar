namespace WinAiUsageBar.Infrastructure.Storage;

public interface IDataMaintenanceService
{
    Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken);

    Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken);

    Task<ConfigBackupResult> ExportConfigBackupAsync(CancellationToken cancellationToken);
}

public sealed record DataMaintenanceResult(
    string Path,
    bool Deleted,
    DateTimeOffset ClearedAt);

public sealed record ConfigBackupResult(
    string Path,
    DateTimeOffset CreatedAt);

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
}
