using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Diagnostics;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class RecoveryGuidanceServiceTests
{
    [Fact]
    public void CreateGuidance_DisablesRestoreWhenNoBackupExists()
    {
        var service = new RecoveryGuidanceService();
        var summary = Summary(configExists: true, backupCount: 0, latestBackupPath: null);

        var guidance = service.CreateGuidance(summary);
        var restore = guidance.Single(item => item.ActionKind == RecoveryActionKind.RestoreLatestConfigBackup);

        Assert.False(restore.IsAvailable);
        Assert.Contains("No app-created config backup", restore.Recommendation, StringComparison.Ordinal);
        Assert.Contains("leaves secrets/ unchanged", restore.SafetyNote, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateGuidance_EnablesBackupAndRestoreWhenConfigAndBackupExist()
    {
        var service = new RecoveryGuidanceService();
        var summary = Summary(
            configExists: true,
            backupCount: 2,
            latestBackupPath: @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config-backups\config-backup.json");

        var guidance = service.CreateGuidance(summary);
        var backup = guidance.Single(item => item.ActionKind == RecoveryActionKind.ExportConfigBackup);
        var restore = guidance.Single(item => item.ActionKind == RecoveryActionKind.RestoreLatestConfigBackup);

        Assert.True(backup.IsAvailable);
        Assert.True(restore.IsAvailable);
        Assert.Contains("desired state", restore.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not copy files under secrets/", backup.SafetyNote, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateGuidance_KeepsResetAndDiagnosticsAvailableWithoutConfig()
    {
        var service = new RecoveryGuidanceService();
        var summary = Summary(configExists: false, backupCount: 0, latestBackupPath: null);

        var guidance = service.CreateGuidance(summary);
        var backup = guidance.Single(item => item.ActionKind == RecoveryActionKind.ExportConfigBackup);
        var reset = guidance.Single(item => item.ActionKind == RecoveryActionKind.ResetConfigToDefaults);
        var diagnostics = guidance.Single(item => item.ActionKind == RecoveryActionKind.ExportDiagnostics);

        Assert.False(backup.IsAvailable);
        Assert.True(reset.IsAvailable);
        Assert.True(diagnostics.IsAvailable);
        Assert.Contains("rollback backup", reset.SafetyNote, StringComparison.Ordinal);
        Assert.Contains("never include files under secrets/", diagnostics.SafetyNote, StringComparison.Ordinal);
    }

    private static DiagnosticsSummary Summary(
        bool configExists,
        int backupCount,
        string? latestBackupPath)
    {
        var root = @"C:\Users\test\AppData\Roaming\WinAiUsageBar";
        var now = new DateTimeOffset(2026, 7, 8, 22, 0, 0, TimeSpan.Zero);
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
            EnabledProviderCount: 1,
            RefreshInterval: RefreshIntervalKind.FiveMinutes,
            NotificationsEnabled: true,
            CachedSnapshotCount: 1,
            LatestSnapshotUpdatedAt: now,
            ConfigBackupCount: backupCount,
            LatestConfigBackupPath: latestBackupPath,
            LatestConfigBackupCreatedAt: latestBackupPath is null ? null : now,
            ConfigBackupTotalBytes: latestBackupPath is null ? 0 : 1024,
            HistoryRetentionMaxDays: 30,
            HistoryRetentionMaxBytes: 5_000_000,
            ConfigFile: new DiagnosticsFileSummary(
                Path.Combine(root, "config.json"),
                configExists,
                configExists ? 2048 : 0,
                configExists ? now : null),
            SnapshotsFile: new DiagnosticsFileSummary(
                Path.Combine(root, "snapshots.json"),
                Exists: true,
                SizeBytes: 512,
                LastWriteTime: now),
            HistoryFile: new DiagnosticsFileSummary(
                Path.Combine(root, "history.ndjson"),
                Exists: true,
                SizeBytes: 4096,
                LastWriteTime: now),
            DiagnosticsLogFile: new DiagnosticsFileSummary(
                Path.Combine(root, "diagnostics.log"),
                Exists: false,
                SizeBytes: 0,
                LastWriteTime: null));
    }
}
