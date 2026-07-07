using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class DataMaintenanceServiceTests
{
    [Fact]
    public async Task ClearSnapshotsAndHistory_DeleteOnlyCacheFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigPath, "config");
        await File.WriteAllTextAsync(paths.SnapshotsPath, "snapshots");
        await File.WriteAllTextAsync(paths.HistoryPath, "history");
        var secretPath = Path.Combine(paths.SecretsDirectory, "api-secret");
        await File.WriteAllTextAsync(secretPath, "secret");
        var now = new DateTimeOffset(2026, 7, 8, 18, 0, 0, TimeSpan.Zero);
        var service = new DataMaintenanceService(paths, () => now);

        try
        {
            var snapshotsResult = await service.ClearSnapshotsAsync(CancellationToken.None);
            var historyResult = await service.ClearHistoryAsync(CancellationToken.None);

            Assert.True(snapshotsResult.Deleted);
            Assert.True(historyResult.Deleted);
            Assert.Equal(paths.SnapshotsPath, snapshotsResult.Path);
            Assert.Equal(paths.HistoryPath, historyResult.Path);
            Assert.Equal(now, snapshotsResult.ClearedAt);
            Assert.False(File.Exists(paths.SnapshotsPath));
            Assert.False(File.Exists(paths.HistoryPath));
            Assert.True(File.Exists(paths.ConfigPath));
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

    [Fact]
    public async Task ClearSnapshotsAsync_ReturnsNotDeletedWhenFileIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var service = new DataMaintenanceService(paths);

        try
        {
            var result = await service.ClearSnapshotsAsync(CancellationToken.None);

            Assert.False(result.Deleted);
            Assert.Equal(paths.SnapshotsPath, result.Path);
            Assert.True(Directory.Exists(paths.RootDirectory));
            Assert.True(Directory.Exists(paths.SecretsDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
