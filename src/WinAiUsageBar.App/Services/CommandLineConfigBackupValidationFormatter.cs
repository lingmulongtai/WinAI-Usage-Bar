using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigBackupValidationFormatter
{
    public static string Format(ConfigBackupValidationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.IsValid
            ? "Config backup validation: valid"
            : "Config backup validation: invalid");
        builder.AppendLine($"Path: {result.Path}");

        if (result.IsValid)
        {
            builder.AppendLine($"Config version: {result.ConfigVersion}");
            builder.AppendLine($"Providers: {result.EnabledProviderCount} enabled / {result.ProviderCount} configured");
            builder.AppendLine($"Defaulted providers after migration: {result.DefaultedProviderCount}");
        }

        foreach (var warning in result.Warnings)
        {
            builder.AppendLine($"Warning: {warning}");
        }

        foreach (var error in result.Errors)
        {
            builder.AppendLine($"Error: {error}");
        }

        return builder.ToString().TrimEnd();
    }
}
