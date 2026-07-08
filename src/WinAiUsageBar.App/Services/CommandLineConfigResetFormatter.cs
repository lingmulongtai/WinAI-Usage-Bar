using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigResetFormatter
{
    public static string Format(ConfigResetResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Reset ? "Config reset: reset" : "Config reset: not reset");
        builder.AppendLine($"Rollback backup: {result.RollbackBackupPath}");
        builder.AppendLine($"Config version: {result.ConfigVersion}");
        builder.AppendLine($"Providers: {result.EnabledProviderCount} enabled / {result.ProviderCount} configured");

        foreach (var warning in result.Warnings)
        {
            builder.AppendLine($"Warning: {warning}");
        }

        return builder.ToString().TrimEnd();
    }
}
