using System.Globalization;
using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineSupportArtifactPruneFormatter
{
    public static string Format(
        DataPruneResult configBackups,
        DataPruneResult diagnosticsExports,
        DataPruneResult crashReports)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Support artifact pruning");
        builder.AppendLine($"Keep newest: {configBackups.KeepNewest}");
        builder.AppendLine();
        AppendResult(builder, "Config backups", configBackups);
        builder.AppendLine();
        AppendResult(builder, "Diagnostics exports", diagnosticsExports);
        builder.AppendLine();
        AppendResult(builder, "Crash reports", crashReports);
        return builder.ToString().TrimEnd();
    }

    private static void AppendResult(
        StringBuilder builder,
        string title,
        DataPruneResult result)
    {
        builder.AppendLine(title);
        builder.AppendLine($"  Directory: {result.DirectoryPath}");
        builder.AppendLine($"  Pattern: {result.SearchPattern}");
        builder.AppendLine($"  Matched: {result.MatchedCount}");
        builder.AppendLine($"  Kept: {result.KeptCount}");
        builder.AppendLine($"  Deleted: {result.DeletedCount}");
        builder.AppendLine($"  Freed: {FormatBytes(result.DeletedBytes)}");
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
