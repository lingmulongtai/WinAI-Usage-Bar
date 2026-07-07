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
        var exportPath = Path.Combine(
            paths.ConfigBackupsDirectory,
            $"config-backup-{createdAt:yyyyMMdd-HHmmss}.json");

        var tempPath = $"{exportPath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                stream,
                config,
                JsonInfrastructureOptions.CreateIndented(),
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, exportPath, overwrite: true);
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
