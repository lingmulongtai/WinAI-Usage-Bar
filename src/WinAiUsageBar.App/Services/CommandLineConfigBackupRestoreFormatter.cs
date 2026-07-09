using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigBackupRestoreFormatter
{
    public static string Format(ConfigBackupRestoreResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Restored
            ? "Config backup restore: restored"
            : "Config backup restore: not restored");
        builder.AppendLine($"Path: {result.Path}");
        CommandLinePathMetadataFormatter.AppendFileName(builder, "File name", result.Path);

        if (result.Restored)
        {
            builder.AppendLine($"Rollback backup: {result.RollbackBackupPath}");
            CommandLinePathMetadataFormatter.AppendFileName(builder, "Rollback backup file", result.RollbackBackupPath);
            CommandLinePathMetadataFormatter.AppendConfigBackupRelativePath(
                builder,
                "Rollback relative path",
                result.RollbackBackupPath);
            builder.AppendLine($"Config version: {result.ConfigVersion}");
            builder.AppendLine($"Providers: {result.EnabledProviderCount} enabled / {result.ProviderCount} configured");
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
