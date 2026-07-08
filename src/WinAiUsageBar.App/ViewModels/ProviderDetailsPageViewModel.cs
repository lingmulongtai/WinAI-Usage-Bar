using WinAiUsageBar.Core.Models;
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
        DisplayName = snapshot.DisplayName;
        SummaryLines =
        [
            $"Provider ID: {snapshot.ProviderId}",
            $"Health: {snapshot.Health}",
            $"Source: {snapshot.SourceKind}",
            $"Updated: {FormatAgo(nowProvider() - snapshot.UpdatedAt)} ago"
        ];
        IdentityLines = BuildIdentityLines(snapshot.Identity);
        UsageLines = BuildUsageLines(snapshot.PrimaryWindow, snapshot.SecondaryWindow);
        CreditLines = BuildCreditLines(snapshot.Credits);
        RepairLines = BuildRepairLines(snapshot);
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

    private static IReadOnlyList<string> BuildRepairLines(UsageSnapshot snapshot)
    {
        var lines = new List<string>();
        switch (snapshot.Health)
        {
            case ProviderHealth.Ok:
                return [];
            case ProviderHealth.Warning:
                lines.Add("Review the status message, then refresh again before changing provider settings.");
                break;
            case ProviderHealth.AuthRequired:
                lines.Add("Reconnect credentials from Privacy & Data, then confirm the provider stores only a secret-name reference.");
                break;
            case ProviderHealth.Unsupported:
                lines.Add("Switch to Manual mode if this source is not available on this machine yet.");
                break;
            case ProviderHealth.Error:
                lines.Add("Refresh again. If the error repeats, export diagnostics before changing provider settings.");
                break;
            case ProviderHealth.Unknown:
                lines.Add("Refresh now to load the first snapshot, then review provider settings if the state stays unknown.");
                break;
        }

        lines.Add(SourceRepairLine(snapshot.SourceKind));
        if (snapshot.ProviderId == ProviderId.GitHubCopilot
            && snapshot.SourceKind == DataSourceKind.OfficialApi
            && snapshot.Health == ProviderHealth.AuthRequired)
        {
            lines.Add("For GitHub Copilot metrics, confirm organization or enterprise mode and a PAT secret reference in provider settings.");
        }

        return lines;
    }

    private static string SourceRepairLine(DataSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DataSourceKind.Cli => "For CLI sources, confirm the command is installed, starts from PATH, and appears in the health report.",
            DataSourceKind.LocalAppServer => "For local app-server sources, confirm the local provider command can start and the account is signed in.",
            DataSourceKind.OfficialApi => "For API sources, confirm required scope settings and secret references without pasting secret values into config.",
            DataSourceKind.Manual => "For Manual mode, update the manual values in Providers and refresh.",
            DataSourceKind.Mock => "For Mock mode, switch to Manual or a real source before relying on the data.",
            DataSourceKind.LocalFile => "For local-file sources, confirm the configured file path exists and does not contain secrets.",
            _ => "Review provider settings and refresh."
        };
    }

    private static string Safe(string? value)
    {
        return DiagnosticRedactor.Redact(value).Trim();
    }

    private static string FormatUnit(string? unit)
    {
        var safe = Safe(unit);
        return string.IsNullOrWhiteSpace(safe) ? "units" : safe;
    }

    private static string FormatAgo(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 60)
        {
            return "just now";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{Math.Floor(elapsed.TotalMinutes):0}m";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"{Math.Floor(elapsed.TotalHours):0}h";
        }

        return $"{Math.Floor(elapsed.TotalDays):0}d";
    }
}
