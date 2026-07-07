namespace WinAiUsageBar.Infrastructure.Storage;

public interface IDataMaintenanceService
{
    Task<DataMaintenanceResult> ClearSnapshotsAsync(CancellationToken cancellationToken);

    Task<DataMaintenanceResult> ClearHistoryAsync(CancellationToken cancellationToken);
}

public sealed record DataMaintenanceResult(
    string Path,
    bool Deleted,
    DateTimeOffset ClearedAt);

public sealed class DataMaintenanceService(
    AppDataPaths paths,
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
