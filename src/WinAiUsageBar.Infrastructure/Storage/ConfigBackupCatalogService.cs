namespace WinAiUsageBar.Infrastructure.Storage;

public sealed record ConfigBackupCatalogEntry(
    string Path,
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);

public sealed record ConfigBackupCatalogResult(
    string DirectoryPath,
    string SearchPattern,
    int Limit,
    int TotalCount,
    long TotalBytes,
    IReadOnlyList<ConfigBackupCatalogEntry> Backups);

public sealed class ConfigBackupCatalogService(AppDataPaths paths)
{
    public const string SearchPattern = "config-backup-*.json";

    public Task<ConfigBackupCatalogResult> ListAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();

        var files = Directory
            .EnumerateFiles(paths.ConfigBackupsDirectory, SearchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var backups = files
            .Take(limit)
            .Select(file => new ConfigBackupCatalogEntry(
                file.FullName,
                file.Name,
                file.Length,
                new DateTimeOffset(file.CreationTime),
                new DateTimeOffset(file.LastWriteTime)))
            .ToList();

        return Task.FromResult(new ConfigBackupCatalogResult(
            paths.ConfigBackupsDirectory,
            SearchPattern,
            limit,
            files.Count,
            files.Sum(file => file.Length),
            backups));
    }
}
