namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface IRecoveryGuidanceService
{
    IReadOnlyList<RecoveryGuidanceItem> CreateGuidance(DiagnosticsSummary summary);
}

public enum RecoveryActionKind
{
    ExportConfigBackup,
    RestoreLatestConfigBackup,
    ResetConfigToDefaults,
    ExportDiagnostics
}

public sealed record RecoveryGuidanceItem(
    RecoveryActionKind ActionKind,
    string Title,
    bool IsAvailable,
    string Recommendation,
    string SafetyNote);

public sealed class RecoveryGuidanceService : IRecoveryGuidanceService
{
    public IReadOnlyList<RecoveryGuidanceItem> CreateGuidance(DiagnosticsSummary summary)
    {
        var hasConfig = summary.ConfigFile.Exists;
        var hasBackup = summary.ConfigBackupCount > 0
            && !string.IsNullOrWhiteSpace(summary.LatestConfigBackupPath);

        return
        [
            new RecoveryGuidanceItem(
                RecoveryActionKind.ExportConfigBackup,
                "Export a config backup",
                hasConfig,
                hasConfig
                    ? "Use this before changing provider, widget, startup, refresh, or recovery settings."
                    : "No config file exists yet. Open settings once, save the setup you want, then export a backup.",
                "Backups include config settings only and do not copy files under secrets/."),
            new RecoveryGuidanceItem(
                RecoveryActionKind.RestoreLatestConfigBackup,
                "Restore the latest backup",
                hasBackup,
                hasBackup
                    ? "Use this when the current settings were changed by mistake and the latest app-created backup is the desired state."
                    : "No app-created config backup is available yet. Export a backup after the app is in a known good state.",
                "Restore validates the backup, creates a rollback backup first, and leaves secrets/ unchanged."),
            new RecoveryGuidanceItem(
                RecoveryActionKind.ResetConfigToDefaults,
                "Reset config to defaults",
                true,
                "Use this when settings are badly broken or you want a clean setup and are ready to reconfigure providers.",
                "Reset creates a rollback backup first and leaves saved secret files in secrets/ unchanged."),
            new RecoveryGuidanceItem(
                RecoveryActionKind.ExportDiagnostics,
                "Export diagnostics",
                true,
                "Use this before asking for help or comparing local state after a recovery action.",
                "Diagnostics exports redact common secret shapes and never include files under secrets/.")
        ];
    }
}
