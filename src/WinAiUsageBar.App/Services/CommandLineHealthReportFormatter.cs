using System.Globalization;
using System.Text;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineHealthReportFormatter
{
    public static string Format(
        AppInfo appInfo,
        DiagnosticsSummary diagnostics,
        HistorySummary history,
        DateTimeOffset generatedAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{appInfo.ProductName} {appInfo.InformationalVersion}");
        builder.AppendLine($"Generated: {FormatDate(generatedAt)}");
        builder.AppendLine();
        builder.AppendLine("Storage");
        builder.AppendLine($"  Root: {diagnostics.RootDirectory}");
        builder.AppendLine($"  Config: {diagnostics.ConfigPath}");
        builder.AppendLine($"  Snapshots: {diagnostics.SnapshotsPath}");
        builder.AppendLine($"  History: {diagnostics.HistoryPath}");
        builder.AppendLine($"  Diagnostics log: {diagnostics.DiagnosticsLogPath}");
        builder.AppendLine($"  Diagnostics exports: {diagnostics.DiagnosticsExportsDirectory}");
        builder.AppendLine();
        builder.AppendLine("Configuration");
        builder.AppendLine($"  Config version: {diagnostics.ConfigVersion}");
        builder.AppendLine($"  Providers: {diagnostics.EnabledProviderCount} enabled / {diagnostics.ConfiguredProviderCount} configured");
        builder.AppendLine($"  Refresh interval: {diagnostics.RefreshInterval}");
        builder.AppendLine($"  Notifications: {(diagnostics.NotificationsEnabled ? "On" : "Off")}");
        builder.AppendLine($"  History retention: {diagnostics.HistoryRetentionMaxDays} day(s), {FormatBytes(diagnostics.HistoryRetentionMaxBytes)} max");
        builder.AppendLine();
        builder.AppendLine("Snapshots");
        builder.AppendLine($"  Cached snapshots: {diagnostics.CachedSnapshotCount}");
        builder.AppendLine($"  Latest snapshot: {FormatDate(diagnostics.LatestSnapshotUpdatedAt)}");
        builder.AppendLine();
        builder.AppendLine("History");
        builder.AppendLine($"  Entries: {history.TotalEntries}");
        builder.AppendLine($"  Invalid lines: {history.InvalidLines}");
        builder.AppendLine($"  Range: {FormatDate(history.EarliestUpdatedAt)} to {FormatDate(history.LatestUpdatedAt)}");
        builder.AppendLine($"  Providers with history: {history.Providers.Count}");

        foreach (var provider in history.Providers)
        {
            builder.AppendLine(
                $"    {provider.DisplayName}: {provider.EntryCount} entries, latest {provider.LatestHealth}, remaining {FormatPercent(provider.LatestRemainingPercent)}, source {provider.LatestSourceKind}");
        }

        builder.AppendLine();
        builder.AppendLine("Files");
        AppendFile(builder, "config.json", diagnostics.ConfigFile);
        AppendFile(builder, "snapshots.json", diagnostics.SnapshotsFile);
        AppendFile(builder, "history.ndjson", diagnostics.HistoryFile);
        AppendFile(builder, "diagnostics.log", diagnostics.DiagnosticsLogFile);

        return builder.ToString().TrimEnd();
    }

    private static void AppendFile(StringBuilder builder, string label, DiagnosticsFileSummary file)
    {
        var status = file.Exists
            ? $"{FormatBytes(file.SizeBytes)}, modified {FormatDate(file.LastWriteTime)}"
            : "Missing";

        builder.AppendLine($"  {label}: {status}");
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "n/a";
    }

    private static string FormatPercent(double? value)
    {
        return value is null
            ? "n/a"
            : $"{value.Value:0.##}%";
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
                return $"{value:0.##} {unit}";
            }

            value /= 1024d;
        }

        return $"{bytes} B";
    }
}
