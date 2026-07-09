using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineConfigResetFormatterTests
{
    [Fact]
    public void Format_PrintsAsciiRollbackMetadataForNonAsciiDirectoryPath()
    {
        var fileName = "config-backup-before-reset-20260709-211016.json";
        var rollbackPath = Path.Combine(@"C:\Temp", "\u5b66\u6821\u6cd5\u4eba", "config-backups", fileName);
        var result = new ConfigResetResult(
            Reset: true,
            rollbackPath,
            ConfigVersion: 1,
            ProviderCount: 7,
            EnabledProviderCount: 2,
            Warnings: ["Saved secrets were not deleted."]);

        var text = CommandLineConfigResetFormatter.Format(result);

        Assert.Contains($"Rollback backup: {rollbackPath}", text, StringComparison.Ordinal);
        Assert.Contains($"Rollback backup file: {fileName}", text, StringComparison.Ordinal);
        Assert.Contains($"Rollback relative path: {Path.Combine("config-backups", fileName)}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", text, StringComparison.OrdinalIgnoreCase);
    }
}
