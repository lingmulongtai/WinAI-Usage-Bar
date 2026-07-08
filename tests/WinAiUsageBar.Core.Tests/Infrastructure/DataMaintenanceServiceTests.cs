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

    [Fact]
    public async Task ExportConfigBackupAsync_UsesUniqueNameWhenBackupAlreadyExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var now = new DateTimeOffset(2026, 7, 8, 19, 30, 0, TimeSpan.Zero);
        var service = new DataMaintenanceService(paths, configStore, () => now);

        try
        {
            await configStore.SaveAsync(AppConfig.CreateDefault(), CancellationToken.None);
            var first = await service.ExportConfigBackupAsync(CancellationToken.None);
            var second = await service.ExportConfigBackupAsync(CancellationToken.None);

            Assert.Equal("config-backup-20260708-193000.json", Path.GetFileName(first.Path));
            Assert.Equal("config-backup-20260708-193000-1.json", Path.GetFileName(second.Path));
            Assert.True(File.Exists(first.Path));
            Assert.True(File.Exists(second.Path));
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

    [Fact]
    public async Task PruneConfigBackupsAsync_DeletesOnlyOldMatchedBackupFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigPath, "config");
        var secretPath = Path.Combine(paths.SecretsDirectory, "config-backup-20260708-090000.json");
        await File.WriteAllTextAsync(secretPath, "secret");
        var unrelatedPath = Path.Combine(paths.ConfigBackupsDirectory, "manual-note.json");
        await File.WriteAllTextAsync(unrelatedPath, "keep me");
        var oldBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-100000.json",
            "old backup",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var olderRollback = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-before-reset-20260708-110000.json",
            "older rollback",
            new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));
        var newestRollback = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-before-restore-20260708-120000.json",
            "newest rollback",
            new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
        var newestBackup = await WriteTimestampedFileAsync(
            paths.ConfigBackupsDirectory,
            "config-backup-20260708-130000.json",
            "newest backup",
            new DateTime(2026, 7, 8, 13, 0, 0, DateTimeKind.Utc));
        var deletedBytes = new FileInfo(oldBackup).Length + new FileInfo(olderRollback).Length;
        var now = new DateTimeOffset(2026, 7, 8, 21, 0, 0, TimeSpan.Zero);
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths), () => now);

        try
        {
            var result = await service.PruneConfigBackupsAsync(2, CancellationToken.None);

            Assert.Equal(paths.ConfigBackupsDirectory, result.DirectoryPath);
            Assert.Equal("config-backup-*.json", result.SearchPattern);
            Assert.Equal(2, result.KeepNewest);
            Assert.Equal(4, result.MatchedCount);
            Assert.Equal(2, result.KeptCount);
            Assert.Equal(2, result.DeletedCount);
            Assert.Equal(deletedBytes, result.DeletedBytes);
            Assert.Equal(now, result.PrunedAt);
            Assert.False(File.Exists(oldBackup));
            Assert.False(File.Exists(olderRollback));
            Assert.True(File.Exists(newestRollback));
            Assert.True(File.Exists(newestBackup));
            Assert.True(File.Exists(unrelatedPath));
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
    public async Task PruneDiagnosticsExportsAsync_DeletesOnlyOldMatchedExportFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var unrelatedPath = Path.Combine(paths.DiagnosticsExportsDirectory, "diagnostics-export-note.log");
        await File.WriteAllTextAsync(unrelatedPath, "keep me");
        var oldExport = await WriteTimestampedFileAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-20260708-100000.txt",
            "old export",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var newestExport = await WriteTimestampedFileAsync(
            paths.DiagnosticsExportsDirectory,
            "diagnostics-export-20260708-110000.txt",
            "newest export",
            new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));
        var deletedBytes = new FileInfo(oldExport).Length;
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));

        try
        {
            var result = await service.PruneDiagnosticsExportsAsync(1, CancellationToken.None);

            Assert.Equal(paths.DiagnosticsExportsDirectory, result.DirectoryPath);
            Assert.Equal("diagnostics-export-*.txt", result.SearchPattern);
            Assert.Equal(2, result.MatchedCount);
            Assert.Equal(1, result.KeptCount);
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(deletedBytes, result.DeletedBytes);
            Assert.False(File.Exists(oldExport));
            Assert.True(File.Exists(newestExport));
            Assert.True(File.Exists(unrelatedPath));
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
    public async Task PruneCrashReportsAsync_DeletesOnlyOldGeneratedCrashReports()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var unrelatedPath = Path.Combine(paths.CrashReportsDirectory, "crash-report-note.json");
        await File.WriteAllTextAsync(unrelatedPath, "keep me");
        var malformedNamePath = Path.Combine(paths.CrashReportsDirectory, "crash-report-20260708-090000-not-a-guid.json");
        await File.WriteAllTextAsync(malformedNamePath, "keep me too");
        var oldReport = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260708-100000-11111111111111111111111111111111.json",
            "old crash report",
            new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc));
        var newestReport = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260708-110000-22222222222222222222222222222222.json",
            "new crash report",
            new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));
        var nestedDirectory = Path.Combine(paths.CrashReportsDirectory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedReport = Path.Combine(nestedDirectory, "crash-report-20260708-120000-33333333333333333333333333333333.json");
        await File.WriteAllTextAsync(nestedReport, "nested");
        var deletedBytes = new FileInfo(oldReport).Length;
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));

        try
        {
            var result = await service.PruneCrashReportsAsync(1, CancellationToken.None);

            Assert.Equal(paths.CrashReportsDirectory, result.DirectoryPath);
            Assert.Equal("crash-report-*.json", result.SearchPattern);
            Assert.Equal(2, result.MatchedCount);
            Assert.Equal(1, result.KeptCount);
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(deletedBytes, result.DeletedBytes);
            Assert.False(File.Exists(oldReport));
            Assert.True(File.Exists(newestReport));
            Assert.True(File.Exists(unrelatedPath));
            Assert.True(File.Exists(malformedNamePath));
            Assert.True(File.Exists(nestedReport));
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
    public async Task PruneConfigBackupsAsync_RequiresAtLeastOneKeptFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));

        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.PruneConfigBackupsAsync(0, CancellationToken.None));
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
