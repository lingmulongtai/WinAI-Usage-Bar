using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class ConfigBackupCatalogServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsNewestTopLevelMatchedBackupsWithoutReadingSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var secretPath = Path.Combine(paths.SecretsDirectory, "config-backup-20260708-090000.json");
        await File.WriteAllTextAsync(secretPath, "secret");
        var unrelatedPath = Path.Combine(paths.ConfigBackupsDirectory, "manual-note.json");
        await File.WriteAllTextAsync(unrelatedPath, "keep me");
        var nestedDirectory = Path.Combine(paths.ConfigBackupsDirectory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedBackupPath = Path.Combine(nestedDirectory, "config-backup-20260708-140000.json");
        await File.WriteAllTextAsync(nestedBackupPath, "nested");
        var olderBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-100000.json",
            "older",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var newestBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-before-restore-20260708-120000.json",
            "newest",
            new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
        var service = new ConfigBackupCatalogService(paths);

        try
        {
            var result = await service.ListAsync(1, CancellationToken.None);

            Assert.Equal(paths.ConfigBackupsDirectory, result.DirectoryPath);
            Assert.Equal("config-backup-*.json", result.SearchPattern);
            Assert.Equal(1, result.Limit);
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(
                new FileInfo(olderBackup).Length + new FileInfo(newestBackup).Length,
                result.TotalBytes);
            var backup = Assert.Single(result.Backups);
            Assert.Equal(newestBackup, backup.Path);
            Assert.Equal(Path.GetFileName(newestBackup), backup.FileName);
            Assert.Equal(new FileInfo(newestBackup).Length, backup.SizeBytes);
            Assert.True(File.Exists(olderBackup));
            Assert.True(File.Exists(newestBackup));
            Assert.True(File.Exists(unrelatedPath));
            Assert.True(File.Exists(nestedBackupPath));
            Assert.True(File.Exists(secretPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListAsync_RejectsUnsupportedLimits(int limit)
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var service = new ConfigBackupCatalogService(paths);

        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.ListAsync(limit, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<string> WriteTimestampedFileAsync(
        string directory,
        string fileName,
        string content,
        DateTime lastWriteTimeUtc)
    {
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        return path;
    }
}
