using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineConfigBackupValidationFormatterTests
{
    [Fact]
    public void Format_PrintsValidSummaryWithoutSecretReferences()
    {
        var result = new ConfigBackupValidationResult(
            @"C:\Temp\config-backup.json",
            IsValid: true,
            ConfigVersion: 1,
            ProviderCount: 7,
            EnabledProviderCount: 2,
            DefaultedProviderCount: 1,
            Errors: [],
            Warnings: ["1 missing provider config(s) will be defaulted by migration."]);

        var text = CommandLineConfigBackupValidationFormatter.Format(result);

        Assert.Contains("valid", text, StringComparison.Ordinal);
        Assert.Contains("Providers: 2 enabled / 7 configured", text, StringComparison.Ordinal);
        Assert.Contains("Defaulted providers after migration: 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_PrintsInvalidErrors()
    {
        var result = new ConfigBackupValidationResult(
            @"C:\Temp\missing.json",
            IsValid: false,
            ConfigVersion: null,
            ProviderCount: null,
            EnabledProviderCount: null,
            DefaultedProviderCount: null,
            Errors: ["Backup file was not found."],
            Warnings: []);

        var text = CommandLineConfigBackupValidationFormatter.Format(result);

        Assert.Contains("invalid", text, StringComparison.Ordinal);
        Assert.Contains("Error: Backup file was not found.", text, StringComparison.Ordinal);
    }
}
