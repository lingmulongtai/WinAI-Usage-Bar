using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.ViewModels;

public sealed class ProviderCardViewModel(UsageSnapshot snapshot)
{
    public ProviderId ProviderId { get; } = snapshot.ProviderId;

    public string DisplayName { get; } = snapshot.DisplayName;

    public string HealthText { get; } = snapshot.Health.ToString();

    public double ProgressValue { get; } = snapshot.PrimaryWindow?.UsedPercent ?? 0;

    public string PercentText { get; } = snapshot.PrimaryWindow?.RemainingPercent is double remaining
        ? $"{remaining:0.#}% left"
        : "Usage unknown";

    public string ResetText { get; } = snapshot.PrimaryWindow?.ResetsAt is DateTimeOffset resetsAt
        ? resetsAt > DateTimeOffset.Now
            ? $"Resets in {FormatRelative(resetsAt - DateTimeOffset.Now)}"
            : "Reset time passed"
        : snapshot.PrimaryWindow?.ResetDescription ?? "Reset unknown";

    public string CreditsLine { get; } = FormatCredits(snapshot.Credits);

    public string SourceText { get; } = snapshot.SourceKind.ToString();

    public string UpdatedText { get; } = $"Updated {FormatAgo(DateTimeOffset.Now - snapshot.UpdatedAt)} ago";

    public string? ErrorMessage { get; } = snapshot.ErrorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string StatusMessage { get; } = snapshot.StatusMessage ?? string.Empty;

    public UsageSnapshot Snapshot { get; } = snapshot;

    private static string FormatCredits(ProviderCredits? credits)
    {
        if (credits is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (credits.Balance is decimal balance)
        {
            parts.Add($"{balance:0.##} {credits.Currency ?? "credits"}");
        }

        if (credits.MonthToDateCost is decimal cost)
        {
            parts.Add($"MTD {cost:0.##} {credits.Currency ?? "USD"}");
        }

        if (credits.TokensLast31Days is long tokens)
        {
            parts.Add($"{tokens:N0} tokens");
        }

        return string.Join(" / ", parts);
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

    private static string FormatRelative(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return "under 1m";
        }

        if (duration.TotalHours < 1)
        {
            return $"{Math.Ceiling(duration.TotalMinutes):0}m";
        }

        return $"{Math.Floor(duration.TotalHours):0}h {duration.Minutes:0}m";
    }
}
