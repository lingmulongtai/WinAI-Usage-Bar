using System.Globalization;
using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigBackupCatalogFormatter
{
    public static string Format(ConfigBackupCatalogResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Config backups");
        builder.AppendLine($"Directory: {result.DirectoryPath}");
        builder.AppendLine($"Pattern: {result.SearchPattern}");
        builder.AppendLine($"Matched: {result.TotalCount}");
        builder.AppendLine($"Listed: {result.Backups.Count}");
        builder.AppendLine($"Limit: {result.Limit}");
        builder.AppendLine($"Total size: {FormatBytes(result.TotalBytes)}");

        if (result.Backups.Count == 0)
        {
            builder.AppendLine("No config backups are available.");
            return builder.ToString().TrimEnd();
        }

        for (var index = 0; index < result.Backups.Count; index++)
        {
            var backup = result.Backups[index];
            builder.AppendLine();
            builder.AppendLine($"{index + 1}. {backup.FileName}");
            builder.AppendLine($"  Path: {backup.Path}");
            builder.AppendLine($"  Size: {FormatBytes(backup.SizeBytes)}");
            builder.AppendLine($"  Created: {backup.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
            builder.AppendLine($"  Modified: {backup.ModifiedAt:yyyy-MM-dd HH:mm:ss zzz}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var units = new[] { "KB", "MB", "GB" };
        var value = bytes / 1024d;
        foreach (var unit in units)
        {
            if (value < 1024d || unit == units[^1])
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", value, unit);
            }

            value /= 1024d;
        }

        return $"{bytes} B";
    }
}
