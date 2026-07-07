using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineConfigBackupRestoreFormatterTests
{
    [Fact]
    public void Format_PrintsRestoreSummary()
    {
        var result = new ConfigBackupRestoreResult(
            @"C:\Temp\incoming.json",
            Restored: true,
            RollbackBackupPath: @"C:\Temp\config-backup-before-restore.json",
            ConfigVersion: 1,
            ProviderCount: 7,
            EnabledProviderCount: 2,
            Errors: [],
            Warnings: []);

        var text = CommandLineConfigBackupRestoreFormatter.Format(result);

        Assert.Contains("restored", text, StringComparison.Ordinal);
        Assert.Contains("Rollback backup: C:\\Temp\\config-backup-before-restore.json", text, StringComparison.Ordinal);
        Assert.Contains("Providers: 2 enabled / 7 configured", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_PrintsRestoreErrors()
    {
        var result = new ConfigBackupRestoreResult(
            @"C:\Temp\invalid.json",
            Restored: false,
            RollbackBackupPath: null,
            ConfigVersion: null,
            ProviderCount: null,
            EnabledProviderCount: null,
            Errors: ["Backup file could not be parsed as WinAI Usage Bar config JSON."],
            Warnings: []);

        var text = CommandLineConfigBackupRestoreFormatter.Format(result);

        Assert.Contains("not restored", text, StringComparison.Ordinal);
        Assert.Contains("Error: Backup file could not be parsed", text, StringComparison.Ordinal);
    }
}
