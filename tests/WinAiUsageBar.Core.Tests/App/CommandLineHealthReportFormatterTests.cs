using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineHealthReportFormatterTests
{
    [Fact]
    public void Format_IncludesLocalHealthMetadata()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 6, 45, 0, TimeSpan.FromHours(9));
        var latestSnapshot = generatedAt.AddMinutes(-5);
        var diagnostics = new DiagnosticsSummary(
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\snapshots.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\history.ndjson",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics.log",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics-exports",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config-backups",
            ConfigVersion: 7,
            ConfiguredProviderCount: 8,
            EnabledProviderCount: 3,
            RefreshInterval: RefreshIntervalKind.FiveMinutes,
            NotificationsEnabled: true,
            CachedSnapshotCount: 2,
            LatestSnapshotUpdatedAt: latestSnapshot,
            ConfigBackupCount: 2,
            LatestConfigBackupPath: @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config-backups\config-backup-20260708-064000.json",
            LatestConfigBackupCreatedAt: latestSnapshot,
            ConfigBackupTotalBytes: 4096,
            DiagnosticsExportCount: 3,
            LatestDiagnosticsExportPath: @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics-exports\diagnostics-export-20260708-064000.txt",
            LatestDiagnosticsExportCreatedAt: latestSnapshot,
            DiagnosticsExportTotalBytes: 8192,
            HistoryRetentionMaxDays: 30,
            HistoryRetentionMaxBytes: 1_048_576,
            ConfigFile: FileSummary("config.json", true, 2048, latestSnapshot),
            SnapshotsFile: FileSummary("snapshots.json", true, 512, latestSnapshot),
            HistoryFile: FileSummary("history.ndjson", true, 1_048_576, latestSnapshot),
            DiagnosticsLogFile: FileSummary("diagnostics.log", false, 0, null));
        var history = new HistorySummary(
            TotalEntries: 3,
            InvalidLines: 1,
            EarliestUpdatedAt: generatedAt.AddDays(-1),
            LatestUpdatedAt: latestSnapshot,
            Providers:
            [
                new ProviderHistorySummary(
                    ProviderId.Codex,
                    "Codex",
                    EntryCount: 3,
                    LatestUpdatedAt: latestSnapshot,
                    LatestHealth: ProviderHealth.Warning,
                    LatestRemainingPercent: 42.5,
                    LatestSourceKind: DataSourceKind.LocalAppServer)
            ]);
        var cliEnvironment = new CliEnvironmentReport(
        [
            new CliCommandStatus(
                "codex",
                IsFound: true,
                Paths: [@"C:\Tools\codex.exe"],
                CanStart: false,
                ExitCode: null,
                TimedOut: false,
                StatusMessage: "Access is denied.",
                LaunchTarget: @"C:\Tools\codex.exe"),
            new CliCommandStatus(
                "git",
                IsFound: true,
                Paths: [@"C:\Tools\git.exe", @"C:\Other\git.exe"],
                CanStart: true,
                ExitCode: 0,
                TimedOut: false,
                StatusMessage: "git version 2.50.0",
                LaunchTarget: @"C:\Tools\git.exe"),
            new CliCommandStatus(
                "claude",
                IsFound: false,
                Paths: [],
                CanStart: null,
                ExitCode: null,
                TimedOut: false,
                StatusMessage: "Not found on PATH.")
        ]);
        var storagePressure = new StoragePressureGuidanceService().CreateGuidance(diagnostics);
        var recoveryGuidance = new RecoveryGuidanceService().CreateGuidance(diagnostics);

        var report = CommandLineHealthReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            diagnostics,
            history,
            generatedAt,
            cliEnvironment,
            storagePressure,
            recoveryGuidance);

        Assert.Contains("WinAI Usage Bar 1.2.3", report, StringComparison.Ordinal);
        Assert.Contains("Generated: 2026-07-08 06:45:00 +09:00", report, StringComparison.Ordinal);
        Assert.Contains("Providers: 3 enabled / 8 configured", report, StringComparison.Ordinal);
        Assert.Contains(@"Config backups: C:\Users\test\AppData\Roaming\WinAiUsageBar\config-backups", report, StringComparison.Ordinal);
        Assert.Contains("Config backups: 2 backup(s), 4 KB total", report, StringComparison.Ordinal);
        Assert.Contains("Latest config backup time: 2026-07-08 06:40:00 +09:00", report, StringComparison.Ordinal);
        Assert.Contains("Diagnostics exports: 3 export(s), 8 KB total", report, StringComparison.Ordinal);
        Assert.Contains("Latest diagnostics export time: 2026-07-08 06:40:00 +09:00", report, StringComparison.Ordinal);
        Assert.Contains("Storage pressure", report, StringComparison.Ordinal);
        Assert.Contains("Retained history: High", report, StringComparison.Ordinal);
        Assert.Contains("Use Clear History soon", report, StringComparison.Ordinal);
        Assert.Contains("Config backups: Ok", report, StringComparison.Ordinal);
        Assert.Contains("Diagnostics exports: Ok", report, StringComparison.Ordinal);
        Assert.Contains("Diagnostics log: Ok", report, StringComparison.Ordinal);
        Assert.Contains("Recovery guidance", report, StringComparison.Ordinal);
        Assert.Contains("Export a config backup: Available", report, StringComparison.Ordinal);
        Assert.Contains("Restore the latest backup: Available", report, StringComparison.Ordinal);
        Assert.Contains("Reset config to defaults: Available", report, StringComparison.Ordinal);
        Assert.Contains("Export diagnostics: Available", report, StringComparison.Ordinal);
        Assert.Contains("Use this when the current settings were changed by mistake", report, StringComparison.Ordinal);
        Assert.Contains("Cached snapshots: 2", report, StringComparison.Ordinal);
        Assert.Contains("Entries: 3", report, StringComparison.Ordinal);
        Assert.Contains("Codex: 3 entries, latest Warning, remaining 42.5%, source LocalAppServer", report, StringComparison.Ordinal);
        Assert.Contains("CLI environment", report, StringComparison.Ordinal);
        Assert.Contains(@"codex: startup failed; C:\Tools\codex.exe; launch C:\Tools\codex.exe; Access is denied.", report, StringComparison.Ordinal);
        Assert.Contains("hint check Windows App Execution Aliases", report, StringComparison.Ordinal);
        Assert.Contains("provider CLI override", report, StringComparison.Ordinal);
        Assert.Contains(@"git: startup ok, exit 0; C:\Tools\git.exe (+1 more); launch C:\Tools\git.exe; git version 2.50.0", report, StringComparison.Ordinal);
        Assert.Contains("claude: not found on PATH", report, StringComparison.Ordinal);
        Assert.Contains("config.json: 2 KB", report, StringComparison.Ordinal);
        Assert.Contains("diagnostics.log: Missing", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_LabelsConfiguredCliOverride()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 8, 30, 0, TimeSpan.FromHours(9));
        var cliEnvironment = new CliEnvironmentReport(
        [
            new CliCommandStatus(
                "codex",
                IsFound: true,
                Paths: [@"C:\Tools\codex.cmd"],
                CanStart: true,
                ExitCode: 0,
                TimedOut: false,
                StatusMessage: "codex 1.2.3",
                LaunchTarget: @"C:\Tools\codex.cmd",
                UsesCommandProcessor: true,
                UsesConfiguredOverride: true)
        ]);

        var report = CommandLineHealthReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            EmptyDiagnostics(generatedAt),
            EmptyHistory(generatedAt),
            generatedAt,
            cliEnvironment);

        Assert.Contains(
            @"codex: startup ok, exit 0; configured override C:\Tools\codex.cmd; launch C:\Tools\codex.cmd via command processor; codex 1.2.3",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesWindowsAppsOverrideHintForCodexStartupDenial()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 8, 10, 0, TimeSpan.FromHours(9));
        var diagnostics = EmptyDiagnostics(generatedAt);
        var history = EmptyHistory(generatedAt);
        var windowsAppsPath = @"C:\Program Files\WindowsApps\OpenAI.Codex_26.623.19656.0_x64__2p2nqsd0c76g0\app\resources\codex.exe";
        var cliEnvironment = new CliEnvironmentReport(
        [
            new CliCommandStatus(
                "codex",
                IsFound: true,
                Paths: [windowsAppsPath],
                CanStart: false,
                ExitCode: null,
                TimedOut: false,
                StatusMessage: "Access is denied.",
                LaunchTarget: windowsAppsPath)
        ]);

        var report = CommandLineHealthReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            diagnostics,
            history,
            generatedAt,
            cliEnvironment);

        Assert.Contains("codex: startup failed", report, StringComparison.Ordinal);
        Assert.Contains("WindowsApps", report, StringComparison.Ordinal);
        Assert.Contains("Access is denied.", report, StringComparison.Ordinal);
        Assert.Contains("provider CLI override", report, StringComparison.Ordinal);
        Assert.DoesNotContain("token", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
    }

    private static DiagnosticsFileSummary FileSummary(
        string path,
        bool exists,
        long sizeBytes,
        DateTimeOffset? lastWriteTime)
    {
        return new DiagnosticsFileSummary(path, exists, sizeBytes, lastWriteTime);
    }

    private static DiagnosticsSummary EmptyDiagnostics(DateTimeOffset generatedAt)
    {
        return new DiagnosticsSummary(
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\snapshots.json",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\history.ndjson",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics.log",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\diagnostics-exports",
            @"C:\Users\test\AppData\Roaming\WinAiUsageBar\config-backups",
            ConfigVersion: 7,
            ConfiguredProviderCount: 8,
            EnabledProviderCount: 0,
            RefreshInterval: RefreshIntervalKind.Manual,
            NotificationsEnabled: false,
            CachedSnapshotCount: 0,
            LatestSnapshotUpdatedAt: null,
            ConfigBackupCount: 0,
            LatestConfigBackupPath: null,
            LatestConfigBackupCreatedAt: null,
            ConfigBackupTotalBytes: 0,
            DiagnosticsExportCount: 0,
            LatestDiagnosticsExportPath: null,
            LatestDiagnosticsExportCreatedAt: null,
            DiagnosticsExportTotalBytes: 0,
            HistoryRetentionMaxDays: 30,
            HistoryRetentionMaxBytes: 1_048_576,
            ConfigFile: FileSummary("config.json", true, 128, generatedAt),
            SnapshotsFile: FileSummary("snapshots.json", false, 0, null),
            HistoryFile: FileSummary("history.ndjson", false, 0, null),
            DiagnosticsLogFile: FileSummary("diagnostics.log", false, 0, null));
    }

    private static HistorySummary EmptyHistory(DateTimeOffset generatedAt)
    {
        return new HistorySummary(
            TotalEntries: 0,
            InvalidLines: 0,
            EarliestUpdatedAt: null,
            LatestUpdatedAt: null,
            Providers: []);
    }
}
