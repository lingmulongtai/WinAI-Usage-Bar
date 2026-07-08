using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.ViewModels;

public sealed class ProviderCardViewModel
{
    public ProviderCardViewModel(UsageSnapshot snapshot, Func<DateTimeOffset>? nowProvider = null)
    {
        var now = (nowProvider ?? (() => DateTimeOffset.Now))();
        var timestamp = ProviderSnapshotTimestampFormatter.Format(snapshot.UpdatedAt, now);

        ProviderId = snapshot.ProviderId;
        DisplayName = snapshot.DisplayName;
        HealthText = snapshot.Health.ToString();
        ProgressValue = snapshot.PrimaryWindow?.UsedPercent ?? 0;
        PercentText = snapshot.PrimaryWindow?.RemainingPercent is double remaining
            ? $"{remaining:0.#}% left"
            : "Usage unknown";
        ResetText = FormatResetText(snapshot.PrimaryWindow, now);
        CreditsLine = FormatCredits(snapshot.Credits);
        SourceText = snapshot.SourceKind.ToString();
        UpdatedText = $"Updated {timestamp.DisplayText}";
        TimestampWarningText = timestamp.WarningText ?? string.Empty;
        StatusText = (snapshot.StatusMessage ?? string.Empty).Trim();
        ErrorMessage = snapshot.ErrorMessage;
        Snapshot = snapshot;
    }

    public ProviderId ProviderId { get; }

    public string DisplayName { get; }

    public string HealthText { get; }

    public double ProgressValue { get; }

    public string PercentText { get; }

    public string ResetText { get; }

    public string CreditsLine { get; }

    public string SourceText { get; }

    public string UpdatedText { get; }

    public string TimestampWarningText { get; }

    public bool HasTimestampWarning => !string.IsNullOrWhiteSpace(TimestampWarningText);

    public string StatusText { get; }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusText)
        && !string.Equals(StatusText, ErrorMessage, StringComparison.Ordinal);

    public string? ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public UsageSnapshot Snapshot { get; }

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

    private static string FormatResetText(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.ResetsAt is DateTimeOffset resetsAt)
        {
            return resetsAt > now
                ? $"Resets in {FormatRelative(resetsAt - now)}"
                : "Reset time passed";
        }

        return window?.ResetDescription ?? "Reset unknown";
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
