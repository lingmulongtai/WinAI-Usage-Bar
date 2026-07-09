using WinAiUsageBar.Core.Models;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.App.ViewModels;

public sealed class ProviderDetailsPageViewModel
{
    public ProviderDetailsPageViewModel(
        IEnumerable<UsageSnapshot> snapshots,
        Func<DateTimeOffset>? nowProvider = null)
    {
        var getNow = nowProvider ?? (() => DateTimeOffset.Now);
        Providers = snapshots
            .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(snapshot => new ProviderDetailsRowViewModel(snapshot, getNow))
            .ToList();
    }

    public IReadOnlyList<ProviderDetailsRowViewModel> Providers { get; }

    public bool HasProviders => Providers.Count > 0;

    public string EmptyText => "No provider snapshots are loaded yet.";
}

public sealed class ProviderDetailsRowViewModel
{
    public ProviderDetailsRowViewModel(
        UsageSnapshot snapshot,
        Func<DateTimeOffset> nowProvider)
    {
        var timestamp = ProviderSnapshotTimestampFormatter.Format(snapshot.UpdatedAt, nowProvider());
        DisplayName = snapshot.DisplayName;
        SummaryLines = BuildSummaryLines(snapshot, timestamp);
        IdentityLines = BuildIdentityLines(snapshot.Identity);
        UsageLines = BuildUsageLines(snapshot.PrimaryWindow, snapshot.SecondaryWindow);
        CreditLines = BuildCreditLines(snapshot.Credits);
        RepairLines = ProviderRepairGuidanceService.BuildRepairLines(snapshot);
        StatusText = Safe(snapshot.StatusMessage);
        ErrorText = Safe(snapshot.ErrorMessage);
    }

    public string DisplayName { get; }

    public IReadOnlyList<string> SummaryLines { get; }

    public IReadOnlyList<string> IdentityLines { get; }

    public IReadOnlyList<string> UsageLines { get; }

    public IReadOnlyList<string> CreditLines { get; }

    public IReadOnlyList<string> RepairLines { get; }

    public bool HasRepairLines => RepairLines.Count > 0;

    public string StatusText { get; }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText)
        && !string.Equals(StatusText, ErrorText, StringComparison.Ordinal);

    public string ErrorText { get; }

    public bool HasErrorText => !string.IsNullOrWhiteSpace(ErrorText);

    private static IReadOnlyList<string> BuildSummaryLines(
        UsageSnapshot snapshot,
        ProviderSnapshotTimestampText timestamp)
    {
        var lines = new List<string>
        {
            $"Provider ID: {snapshot.ProviderId}",
            $"Health: {snapshot.Health}",
            $"Source: {snapshot.SourceKind}",
            $"Updated: {timestamp.DisplayText}"
        };

        if (timestamp.HasWarning)
        {
            lines.Add(timestamp.WarningText!);
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildIdentityLines(ProviderIdentity? identity)
    {
        if (identity is null)
        {
            return ["Identity: unknown"];
        }

        var lines = new List<string>();
        AddIfPresent(lines, "Email", identity.Email);
        AddIfPresent(lines, "Account", identity.AccountName);
        AddIfPresent(lines, "Plan", identity.PlanName);
        AddIfPresent(lines, "Organization", identity.Organization);
        return lines.Count == 0 ? ["Identity: unknown"] : lines;
    }

    private static IReadOnlyList<string> BuildUsageLines(
        UsageWindow? primaryWindow,
        UsageWindow? secondaryWindow)
    {
        var lines = new List<string>();
        AddWindow(lines, primaryWindow, "Primary");
        AddWindow(lines, secondaryWindow, "Secondary");
        return lines.Count == 0 ? ["Usage window: unknown"] : lines;
    }

    private static IReadOnlyList<string> BuildCreditLines(ProviderCredits? credits)
    {
        if (credits is null)
        {
            return ["Credits: unknown"];
        }

        var currency = string.IsNullOrWhiteSpace(credits.Currency) ? "credits" : Safe(credits.Currency);
        var lines = new List<string>();
        if (credits.Balance is decimal balance)
        {
            lines.Add($"Balance: {balance:0.##} {currency}");
        }

        if (credits.MonthToDateCost is decimal cost)
        {
            lines.Add($"Month to date: {cost:0.##} {currency}");
        }

        if (credits.TokensLast31Days is long tokens)
        {
            lines.Add($"Tokens last 31 days: {tokens:N0}");
        }

        return lines.Count == 0 ? ["Credits: unknown"] : lines;
    }

    private static void AddWindow(List<string> lines, UsageWindow? window, string fallbackLabel)
    {
        if (window is null)
        {
            return;
        }

        var label = string.IsNullOrWhiteSpace(window.Label) ? fallbackLabel : Safe(window.Label);
        if (window.UsedPercent is double usedPercent)
        {
            lines.Add($"{label} used: {usedPercent:0.#}%");
        }

        if (window.RemainingPercent is double remainingPercent)
        {
            lines.Add($"{label} remaining: {remainingPercent:0.#}%");
        }

        if (window.Used is double used && window.Limit is double limit)
        {
            lines.Add($"{label} amount: {used:0.##} / {limit:0.##} {FormatUnit(window.Unit)}");
        }
        else if (window.Used is double usedOnly)
        {
            lines.Add($"{label} used amount: {usedOnly:0.##} {FormatUnit(window.Unit)}");
        }

        if (window.ResetsAt is DateTimeOffset resetsAt)
        {
            lines.Add($"{label} resets at: {resetsAt:yyyy-MM-dd HH:mm zzz}");
        }
        else
        {
            AddIfPresent(lines, $"{label} reset", window.ResetDescription);
        }
    }

    private static void AddIfPresent(List<string> lines, string label, string? value)
    {
        var safe = Safe(value);
        if (!string.IsNullOrWhiteSpace(safe))
        {
            lines.Add($"{label}: {safe}");
        }
    }

    private static string Safe(string? value)
    {
        return DiagnosticRedactor.RedactForDisplay(value).Trim();
    }

    private static string FormatUnit(string? unit)
    {
        var safe = Safe(unit);
        return string.IsNullOrWhiteSpace(safe) ? "units" : safe;
    }

}
