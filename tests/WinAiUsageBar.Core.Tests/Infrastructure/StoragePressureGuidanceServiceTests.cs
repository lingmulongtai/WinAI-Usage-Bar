using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Diagnostics;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class StoragePressureGuidanceServiceTests
{
    [Fact]
    public void CreateGuidance_ReturnsOkGuidanceForSmallLocalFiles()
    {
        var service = new StoragePressureGuidanceService();
        var summary = Summary(
            historyBytes: 100_000,
            historyMaxBytes: 5_000_000,
            backupCount: 1,
            backupBytes: 2_048,
            exportCount: 1,
            exportBytes: 4_096,
            logBytes: 4_096);

        var guidance = service.CreateGuidance(summary);

        Assert.All(guidance, item => Assert.Equal(StoragePressureLevel.Ok, item.Level));
        Assert.Contains(guidance, item => item.Title == "Retained history"
            && item.Recommendation.Contains("No action needed", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateGuidance_WarnsWhenHistoryApproachesRetentionLimit()
    {
        var service = new StoragePressureGuidanceService();
        var summary = Summary(
            historyBytes: 8_000_000,
            historyMaxBytes: 10_000_000,
            backupCount: 1,
            backupBytes: 2_048,
            exportCount: 1,
            exportBytes: 4_096,
            logBytes: 4_096);

        var history = service.CreateGuidance(summary).Single(item => item.Title == "Retained history");

        Assert.Equal(StoragePressureLevel.Watch, history.Level);
        Assert.Contains("history.ndjson uses", history.Detail, StringComparison.Ordinal);
        Assert.Contains("Review history retention", history.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateGuidance_FlagsHighHistoryPressure()
    {
        var service = new StoragePressureGuidanceService();
        var summary = Summary(
            historyBytes: 9_500_000,
            historyMaxBytes: 10_000_000,
            backupCount: 1,
            backupBytes: 2_048,
            exportCount: 1,
            exportBytes: 4_096,
            logBytes: 4_096);

        var history = service.CreateGuidance(summary).Single(item => item.Title == "Retained history");

        Assert.Equal(StoragePressureLevel.High, history.Level);
        Assert.Contains("Use Clear History soon", history.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateGuidance_FlagsBackupAndLogPressureWithoutSecretPaths()
    {
        var service = new StoragePressureGuidanceService();
        var summary = Summary(
            historyBytes: 100_000,
            historyMaxBytes: 10_000_000,
            backupCount: 51,
            backupBytes: 120_000_000,
            exportCount: 51,
            exportBytes: 240_000_000,
            logBytes: 30_000_000);

        var guidance = service.CreateGuidance(summary);
        var backups = guidance.Single(item => item.Title == "Config backups");
        var exports = guidance.Single(item => item.Title == "Diagnostics exports");
        var log = guidance.Single(item => item.Title == "Diagnostics log");
        var visibleText = string.Join(
            Environment.NewLine,
            guidance.SelectMany(item => new[] { item.Title, item.Detail, item.Recommendation }));

        Assert.Equal(StoragePressureLevel.High, backups.Level);
        Assert.Equal(StoragePressureLevel.High, exports.Level);
        Assert.Equal(StoragePressureLevel.High, log.Level);
        Assert.Contains("archive or delete older config backups", backups.Recommendation, StringComparison.Ordinal);
        Assert.Contains("delete older diagnostics exports", exports.Recommendation, StringComparison.Ordinal);
        Assert.Contains("prune the old diagnostics log", log.Recommendation, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets", visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", visibleText, StringComparison.OrdinalIgnoreCase);
    }

    private static DiagnosticsSummary Summary(
        long historyBytes,
        long historyMaxBytes,
        int backupCount,
        long backupBytes,
        int exportCount,
        long exportBytes,
        long logBytes)
    {
        var root = @"C:\Users\test\AppData\Roaming\WinAiUsageBar";
        var now = new DateTimeOffset(2026, 7, 8, 22, 30, 0, TimeSpan.Zero);
        return new DiagnosticsSummary(
            RootDirectory: root,
            ConfigPath: Path.Combine(root, "config.json"),
            SnapshotsPath: Path.Combine(root, "snapshots.json"),
            HistoryPath: Path.Combine(root, "history.ndjson"),
            DiagnosticsLogPath: Path.Combine(root, "diagnostics.log"),
            DiagnosticsExportsDirectory: Path.Combine(root, "diagnostics-exports"),
            ConfigBackupsDirectory: Path.Combine(root, "config-backups"),
            ConfigVersion: 1,
            ConfiguredProviderCount: 7,
            EnabledProviderCount: 2,
            RefreshInterval: RefreshIntervalKind.FiveMinutes,
            NotificationsEnabled: true,
            CachedSnapshotCount: 2,
            LatestSnapshotUpdatedAt: now,
            ConfigBackupCount: backupCount,
            LatestConfigBackupPath: backupCount == 0 ? null : Path.Combine(root, "config-backups", "config-backup.json"),
            LatestConfigBackupCreatedAt: backupCount == 0 ? null : now,
            ConfigBackupTotalBytes: backupBytes,
            DiagnosticsExportCount: exportCount,
            LatestDiagnosticsExportPath: exportCount == 0 ? null : Path.Combine(root, "diagnostics-exports", "diagnostics-export.txt"),
            LatestDiagnosticsExportCreatedAt: exportCount == 0 ? null : now,
            DiagnosticsExportTotalBytes: exportBytes,
            HistoryRetentionMaxDays: 30,
            HistoryRetentionMaxBytes: historyMaxBytes,
            ConfigFile: new DiagnosticsFileSummary(
                Path.Combine(root, "config.json"),
                Exists: true,
                SizeBytes: 2_048,
                LastWriteTime: now),
            SnapshotsFile: new DiagnosticsFileSummary(
                Path.Combine(root, "snapshots.json"),
                Exists: true,
                SizeBytes: 1_024,
                LastWriteTime: now),
            HistoryFile: new DiagnosticsFileSummary(
                Path.Combine(root, "history.ndjson"),
                Exists: historyBytes > 0,
                SizeBytes: historyBytes,
                LastWriteTime: historyBytes > 0 ? now : null),
            DiagnosticsLogFile: new DiagnosticsFileSummary(
                Path.Combine(root, "diagnostics.log"),
                Exists: logBytes > 0,
                SizeBytes: logBytes,
                LastWriteTime: logBytes > 0 ? now : null));
    }
}
