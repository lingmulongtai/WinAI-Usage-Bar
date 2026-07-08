namespace WinAiUsageBar.App.ViewModels;

public static class ProviderSnapshotTimestampFormatter
{
    // Twice the longest automatic refresh interval keeps one missed tick from looking stale.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FutureTimestampTolerance = TimeSpan.FromSeconds(5);

    public static ProviderSnapshotTimestampText Format(DateTimeOffset updatedAt, DateTimeOffset now)
    {
        var elapsed = now - updatedAt;
        var displayText = elapsed < -FutureTimestampTolerance
            ? $"in {FormatFuture(elapsed)} (future timestamp)"
            : $"{FormatAgo(elapsed)} ago";

        return new ProviderSnapshotTimestampText(
            displayText,
            FormatFreshnessWarning(elapsed));
    }

    private static string FormatAgo(TimeSpan elapsed)
    {
        elapsed = elapsed.Duration();
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

    private static string? FormatFreshnessWarning(TimeSpan elapsed)
    {
        if (elapsed < -FutureTimestampTolerance)
        {
            return $"Timestamp warning: snapshot is {FormatFuture(elapsed)} in the future; check the system clock or refresh again.";
        }

        if (elapsed > StaleAfter)
        {
            return $"Timestamp warning: cached snapshot is stale ({FormatAgo(elapsed)} old); refresh now or inspect provider errors.";
        }

        return null;
    }

    private static string FormatFuture(TimeSpan elapsed)
    {
        var duration = elapsed.Duration();
        return duration.TotalMinutes < 1
            ? "under 1m"
            : FormatAgo(duration);
    }
}

public sealed record ProviderSnapshotTimestampText(string DisplayText, string? WarningText)
{
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);
}
