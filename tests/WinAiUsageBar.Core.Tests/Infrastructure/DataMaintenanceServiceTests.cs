using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
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
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths), () => now);

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
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));

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

    [Fact]
    public async Task ExportConfigBackupAsync_WritesConfigOnlyWithoutSecretValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).ApiKey.SecretName = "gemini-secret-ref";
        paths.EnsureCreated();
        await File.WriteAllTextAsync(Path.Combine(paths.SecretsDirectory, "gemini-secret-ref"), "actual-secret-value");
        await configStore.SaveAsync(config, CancellationToken.None);
        var now = new DateTimeOffset(2026, 7, 8, 19, 0, 0, TimeSpan.Zero);
        var service = new DataMaintenanceService(paths, configStore, () => now);

        try
        {
            var result = await service.ExportConfigBackupAsync(CancellationToken.None);
            var backupContent = await File.ReadAllTextAsync(result.Path);

            Assert.Equal(now, result.CreatedAt);
            Assert.Equal(paths.ConfigBackupsDirectory, Path.GetDirectoryName(result.Path));
            Assert.Equal("config-backup-20260708-190000.json", Path.GetFileName(result.Path));
            Assert.True(File.Exists(result.Path));
            Assert.Contains("gemini-secret-ref", backupContent, StringComparison.Ordinal);
            Assert.DoesNotContain("actual-secret-value", backupContent, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(paths.ConfigBackupsDirectory));
            Assert.True(File.Exists(Path.Combine(paths.SecretsDirectory, "gemini-secret-ref")));
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
    public async Task ExportConfigBackupAsync_CreatesDefaultConfigWhenMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var service = new DataMaintenanceService(paths, configStore);

        try
        {
            var result = await service.ExportConfigBackupAsync(CancellationToken.None);

            Assert.True(File.Exists(paths.ConfigPath));
            Assert.True(File.Exists(result.Path));
            Assert.Equal(paths.ConfigBackupsDirectory, Path.GetDirectoryName(result.Path));
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
