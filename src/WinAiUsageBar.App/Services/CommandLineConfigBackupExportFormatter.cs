using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigBackupExportFormatter
{
    public static string Format(ConfigBackupResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Config backup export");
        builder.AppendLine($"Path: {result.Path}");
        CommandLinePathMetadataFormatter.AppendFileName(builder, "File name", result.Path);
        CommandLinePathMetadataFormatter.AppendConfigBackupRelativePath(builder, "Relative path", result.Path);
        builder.AppendLine($"Created: {result.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        return builder.ToString().TrimEnd();
    }
}
