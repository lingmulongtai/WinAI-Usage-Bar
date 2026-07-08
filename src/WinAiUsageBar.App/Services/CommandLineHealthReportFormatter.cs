using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineHealthReportFormatter
{
    public static string Format(
        AppInfo appInfo,
        DiagnosticsSummary diagnostics,
        HistorySummary history,
        DateTimeOffset generatedAt,
        CliEnvironmentReport? cliEnvironment = null,
        IReadOnlyList<StoragePressureGuidanceItem>? storagePressure = null,
        IReadOnlyList<RecoveryGuidanceItem>? recoveryGuidance = null,
        UpdateSettings? updates = null)
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
        builder.AppendLine($"  Config backups: {diagnostics.ConfigBackupsDirectory}");
        builder.AppendLine();
        builder.AppendLine("Configuration");
        builder.AppendLine($"  Config version: {diagnostics.ConfigVersion}");
        builder.AppendLine($"  Providers: {diagnostics.EnabledProviderCount} enabled / {diagnostics.ConfiguredProviderCount} configured");
        builder.AppendLine($"  Refresh interval: {diagnostics.RefreshInterval}");
        builder.AppendLine($"  Notifications: {(diagnostics.NotificationsEnabled ? "On" : "Off")}");
        builder.AppendLine($"  History retention: {diagnostics.HistoryRetentionMaxDays} day(s), {FormatBytes(diagnostics.HistoryRetentionMaxBytes)} max");
        builder.AppendLine($"  Config backups: {diagnostics.ConfigBackupCount} backup(s), {FormatBytes(diagnostics.ConfigBackupTotalBytes)} total");
        builder.AppendLine($"  Latest config backup: {diagnostics.LatestConfigBackupPath ?? "n/a"}");
        builder.AppendLine($"  Latest config backup time: {FormatDate(diagnostics.LatestConfigBackupCreatedAt)}");
        builder.AppendLine($"  Diagnostics exports: {diagnostics.DiagnosticsExportCount} export(s), {FormatBytes(diagnostics.DiagnosticsExportTotalBytes)} total");
        builder.AppendLine($"  Latest diagnostics export: {diagnostics.LatestDiagnosticsExportPath ?? "n/a"}");
        builder.AppendLine($"  Latest diagnostics export time: {FormatDate(diagnostics.LatestDiagnosticsExportCreatedAt)}");

        if (updates is not null)
        {
            AppendUpdates(builder, updates);
        }

        if (storagePressure is { Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("Storage pressure");
            foreach (var item in storagePressure)
            {
                builder.AppendLine($"  {item.Title}: {item.Level}");
                builder.AppendLine($"    {item.Detail}");
                builder.AppendLine($"    {item.Recommendation}");
            }
        }

        if (recoveryGuidance is { Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("Recovery guidance");
            foreach (var item in recoveryGuidance)
            {
                builder.AppendLine($"  {item.Title}: {(item.IsAvailable ? "Available" : "Not ready")}");
                builder.AppendLine($"    {item.Recommendation}");
            }
        }

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

        if (cliEnvironment is not null)
        {
            builder.AppendLine();
            builder.AppendLine("CLI environment");
            foreach (var command in cliEnvironment.Commands)
            {
                builder.AppendLine($"  {command.CommandName}: {FormatCommandStatus(command)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Files");
        AppendFile(builder, "config.json", diagnostics.ConfigFile);
        AppendFile(builder, "snapshots.json", diagnostics.SnapshotsFile);
        AppendFile(builder, "history.ndjson", diagnostics.HistoryFile);
        AppendFile(builder, "diagnostics.log", diagnostics.DiagnosticsLogFile);

        return builder.ToString().TrimEnd();
    }

    private static void AppendUpdates(StringBuilder builder, UpdateSettings updates)
    {
        var interval = updates.MinimumCheckIntervalHours <= 0
            ? "every startup"
            : $"at most every {updates.MinimumCheckIntervalHours} hour(s)";

        builder.AppendLine();
        builder.AppendLine("Updates");
        builder.AppendLine($"  Check on startup: {(updates.CheckOnStartup ? "On" : "Off")}");
        builder.AppendLine($"  Startup interval: {interval}");
        builder.AppendLine($"  Automatic download: {(updates.DownloadAutomatically ? "On" : "Off")}");
        builder.AppendLine($"  Automatic install launch: {(updates.InstallAutomatically ? "On" : "Off")}");
        builder.AppendLine($"  Last checked: {FormatDate(updates.LastCheckedAt)}");
        builder.AppendLine($"  Status: {SafeValue(updates.LastStatus)}");
        builder.AppendLine($"  Current version: {SafeValue(updates.LastCurrentVersion)}");
        builder.AppendLine($"  Latest version: {SafeValue(updates.LastLatestVersion)}");
        builder.AppendLine($"  Last launched install: {SafeValue(updates.LastInstallLaunchedVersion)}");
        builder.AppendLine($"  Package path: {SafeValue(updates.LastPackagePath)}");
        builder.AppendLine($"  Install script: {SafeValue(updates.LastInstallScriptPath)}");
        builder.AppendLine($"  Message: {SafeValue(updates.LastMessage)}");
    }

    private static void AppendFile(StringBuilder builder, string label, DiagnosticsFileSummary file)
    {
        var status = file.Exists
            ? $"{FormatBytes(file.SizeBytes)}, modified {FormatDate(file.LastWriteTime)}"
            : "Missing";

        builder.AppendLine($"  {label}: {status}");
    }

    private static string FormatCommandStatus(CliCommandStatus command)
    {
        if (!command.IsFound)
        {
            return "not found on PATH";
        }

        var start = command.TimedOut
            ? "startup timed out"
            : command.CanStart == true
                ? "startup ok"
                : "startup failed";
        var exit = command.ExitCode is int exitCode ? $", exit {exitCode}" : string.Empty;
        var path = command.Paths.Count == 0
            ? "path n/a"
            : command.Paths.Count == 1
                ? command.Paths[0]
                : $"{command.Paths[0]} (+{command.Paths.Count - 1} more)";
        if (command.UsesConfiguredOverride)
        {
            path = $"configured override {path}";
        }

        var launch = string.IsNullOrWhiteSpace(command.LaunchTarget)
            ? string.Empty
            : $"; launch {FormatLaunchTarget(command)}";
        var repair = FormatCommandRepairHint(command);

        return $"{start}{exit}; {path}{launch}; {command.StatusMessage}{repair}";
    }

    private static string FormatLaunchTarget(CliCommandStatus command)
    {
        var mode = command.UsesCommandProcessor ? " via command processor" : string.Empty;
        return $"{command.LaunchTarget}{mode}";
    }

    private static string FormatCommandRepairHint(CliCommandStatus command)
    {
        if (command.CanStart != false)
        {
            return string.Empty;
        }

        if (command.TimedOut)
        {
            return "; hint check whether the command opens an interactive prompt or is waiting for login";
        }

        if (command.StatusMessage.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return "; hint check Windows App Execution Aliases, package permissions, reinstall the CLI outside WindowsApps, or set a provider CLI override to a launchable path";
        }

        if (command.UsesCommandProcessor)
        {
            return "; hint verify the command shim works from a normal terminal";
        }

        return "; hint verify the command starts from this working directory and appears before stale PATH entries";
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

    private static string SafeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        var redacted = DiagnosticRedactor.Redact(value);
        return Regex.Replace(
            redacted,
            @"(?i)\b(?:authorization\s*[:=]\s*bearer|api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret(?:[_-]?name)?|pat[_-]?secret(?:[_-]?name)?|cookie)\s*[:=]\s*\[REDACTED\]",
            "[REDACTED]");
    }
}
