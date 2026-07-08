using System.Globalization;
using System.Text;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.App.Services;

public static class CommandLineRefreshReportFormatter
{
    public static string Format(
        AppInfo appInfo,
        IReadOnlyList<UsageSnapshot> snapshots,
        DateTimeOffset generatedAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{appInfo.ProductName} {appInfo.InformationalVersion}");
        builder.AppendLine($"Refresh once generated: {FormatDate(generatedAt)}");
        builder.AppendLine($"Snapshots: {snapshots.Count}");

        if (snapshots.Count == 0)
        {
            builder.AppendLine("No enabled provider snapshots were produced.");
            return builder.ToString().TrimEnd();
        }

        foreach (var snapshot in snapshots.OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.AppendLine($"{snapshot.DisplayName}");
            builder.AppendLine($"  Health: {snapshot.Health}");
            builder.AppendLine($"  Source: {snapshot.SourceKind}");
            builder.AppendLine($"  Updated: {FormatDate(snapshot.UpdatedAt)}");
            builder.AppendLine($"  Remaining: {FormatPercent(snapshot.PrimaryWindow?.RemainingPercent)}");
            builder.AppendLine($"  Reset: {FormatReset(snapshot.PrimaryWindow)}");
            AppendSecondaryWindow(builder, snapshot.SecondaryWindow);
            builder.AppendLine($"  Credits: {FormatCredits(snapshot.Credits)}");
            AppendSnapshotMessages(builder, snapshot);
            AppendRepairLines(builder, snapshot);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendSafeLine(StringBuilder builder, string label, string? value)
    {
        var safe = SafeText(value);
        if (safe is not null)
        {
            builder.AppendLine($"  {label}: {safe}");
        }
    }

    private static void AppendSnapshotMessages(StringBuilder builder, UsageSnapshot snapshot)
    {
        if (snapshot.Health == ProviderHealth.AuthRequired)
        {
            builder.AppendLine("  Status: Auth required; details are omitted from the CLI report.");
            if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
            {
                builder.AppendLine("  Error: Auth error details are omitted from the CLI report.");
            }

            return;
        }

        AppendSafeLine(builder, "Status", snapshot.StatusMessage);
        AppendSafeLine(builder, "Error", snapshot.ErrorMessage);
    }

    private static void AppendSecondaryWindow(StringBuilder builder, UsageWindow? window)
    {
        if (window is null)
        {
            return;
        }

        var label = SafeText(window.Label) ?? "usage window";
        builder.AppendLine(
            $"  Secondary: {label}; remaining {FormatPercent(window.RemainingPercent)}; reset {FormatReset(window)}");
    }

    private static void AppendRepairLines(StringBuilder builder, UsageSnapshot snapshot)
    {
        var repairLines = ProviderRepairGuidanceService.BuildRepairLines(snapshot);
        if (repairLines.Count == 0)
        {
            return;
        }

        builder.AppendLine("  Repair:");
        foreach (var line in repairLines)
        {
            var safe = SafeText(line);
            if (safe is not null)
            {
                builder.AppendLine($"    - {safe}");
            }
        }
    }

    private static string FormatReset(UsageWindow? window)
    {
        if (window is null)
        {
            return "n/a";
        }

        if (window.ResetsAt is not null)
        {
            return FormatDate(window.ResetsAt);
        }

        return SafeText(window.ResetDescription) ?? "n/a";
    }

    private static string FormatCredits(ProviderCredits? credits)
    {
        if (credits is null)
        {
            return "n/a";
        }

        var parts = new List<string>();
        if (credits.Balance is not null)
        {
            var currency = SafeText(credits.Currency);
            parts.Add(currency is null
                ? credits.Balance.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : $"{credits.Balance.Value.ToString("0.##", CultureInfo.InvariantCulture)} {currency}");
        }

        if (credits.MonthToDateCost is not null)
        {
            parts.Add($"month {credits.MonthToDateCost.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        if (credits.TokensLast31Days is not null)
        {
            parts.Add($"tokens31d {credits.TokensLast31Days.Value}");
        }

        return parts.Count == 0 ? "n/a" : string.Join(", ", parts);
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

    private static string? SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var oneLine = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        var redacted = DiagnosticRedactor.Redact(oneLine);
        return redacted.Length <= 240 ? redacted : $"{redacted[..240]}...";
    }
}
