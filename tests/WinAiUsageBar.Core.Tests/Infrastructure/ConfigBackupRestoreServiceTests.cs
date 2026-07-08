using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class ConfigBackupRestoreServiceTests
{
    [Fact]
    public async Task RestoreAsync_ValidatesBacksUpCurrentConfigAndRestoresMigratedBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var current = AppConfig.CreateDefault();
        current.Appearance.Theme = "Dark";
        var backup = AppConfig.CreateDefault();
        backup.Appearance.Theme = "Light";
        backup.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled = true;
        var backupPath = Path.Combine(root, "incoming-config-backup.json");
        var now = new DateTimeOffset(2026, 7, 8, 20, 0, 0, TimeSpan.Zero);
        var service = new ConfigBackupRestoreService(paths, configStore, nowProvider: () => now);

        try
        {
            await configStore.SaveAsync(current, CancellationToken.None);
            await WriteConfigAsync(backupPath, backup);
            await File.WriteAllTextAsync(Path.Combine(paths.SecretsDirectory, "gemini-secret"), "secret-value");

            var result = await service.RestoreAsync(backupPath, CancellationToken.None);
            var restored = await configStore.LoadAsync(CancellationToken.None);
            var rollbackContent = await File.ReadAllTextAsync(result.RollbackBackupPath!);

            Assert.True(result.Restored);
            Assert.Equal(Path.GetFullPath(backupPath), result.Path);
            Assert.Equal("config-backup-before-restore-20260708-200000.json", Path.GetFileName(result.RollbackBackupPath));
            Assert.Equal("Light", restored.Appearance.Theme);
            Assert.True(restored.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled);
            Assert.Contains("\"theme\": \"Dark\"", rollbackContent, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(paths.SecretsDirectory, "gemini-secret")));
            Assert.Equal("secret-value", await File.ReadAllTextAsync(Path.Combine(paths.SecretsDirectory, "gemini-secret")));
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
    public async Task RestoreAsync_DoesNotChangeConfigWhenBackupIsInvalid()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var current = AppConfig.CreateDefault();
        current.Appearance.Theme = "Dark";
        var invalidPath = Path.Combine(root, "invalid.json");
        var service = new ConfigBackupRestoreService(paths, configStore);

        try
        {
            await configStore.SaveAsync(current, CancellationToken.None);
            await File.WriteAllTextAsync(invalidPath, "{ nope");

            var result = await service.RestoreAsync(invalidPath, CancellationToken.None);
            var after = await configStore.LoadAsync(CancellationToken.None);

            Assert.False(result.Restored);
            Assert.Null(result.RollbackBackupPath);
            Assert.Equal("Dark", after.Appearance.Theme);
            Assert.Empty(Directory.GetFiles(paths.ConfigBackupsDirectory, "config-backup-before-restore-*.json"));
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
    public async Task RestoreAsync_UsesUniqueRollbackNameWhenSameSecondBackupExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var backup = AppConfig.CreateDefault();
        backup.Appearance.Theme = "Light";
        var backupPath = Path.Combine(root, "incoming-config-backup.json");
        var now = new DateTimeOffset(2026, 7, 8, 20, 30, 0, TimeSpan.Zero);
        var service = new ConfigBackupRestoreService(paths, configStore, nowProvider: () => now);

        try
        {
            paths.EnsureCreated();
            await configStore.SaveAsync(AppConfig.CreateDefault(), CancellationToken.None);
            await WriteConfigAsync(backupPath, backup);
            await File.WriteAllTextAsync(
                Path.Combine(paths.ConfigBackupsDirectory, "config-backup-before-restore-20260708-203000.json"),
                "existing rollback");

            var result = await service.RestoreAsync(backupPath, CancellationToken.None);

            Assert.True(result.Restored);
            Assert.Equal("config-backup-before-restore-20260708-203000-1.json", Path.GetFileName(result.RollbackBackupPath));
            Assert.True(File.Exists(result.RollbackBackupPath!));
            Assert.Empty(Directory.GetFiles(paths.ConfigBackupsDirectory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task WriteConfigAsync(string path, AppConfig config)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            config,
            JsonInfrastructureOptions.CreateIndented());
    }
}
