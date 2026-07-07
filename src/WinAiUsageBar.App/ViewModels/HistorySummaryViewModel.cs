using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.ViewModels;

public sealed class HistorySummaryViewModel
{
    public HistorySummaryViewModel(HistorySummary summary)
    {
        TotalEntries = summary.TotalEntries;
        InvalidLines = summary.InvalidLines;
        SummaryText = summary.TotalEntries == 0
            ? "No retained history entries yet."
            : $"{summary.TotalEntries} retained history entry(s) from {FormatTimestamp(summary.EarliestUpdatedAt)} to {FormatTimestamp(summary.LatestUpdatedAt)}.";
        InvalidLineText = summary.InvalidLines == 0
            ? "No invalid history lines found."
            : $"{summary.InvalidLines} invalid history line(s) were ignored.";
        Providers = summary.Providers
            .Select(provider => new ProviderHistoryRowViewModel(provider))
            .ToList();
    }

    public int TotalEntries { get; }

    public int InvalidLines { get; }

    public string SummaryText { get; }

    public string InvalidLineText { get; }

    public IReadOnlyList<ProviderHistoryRowViewModel> Providers { get; }

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        return value is null
            ? "unknown"
            : value.Value.ToString("yyyy-MM-dd HH:mm:ss zzz");
    }
}

public sealed class ProviderHistoryRowViewModel
{
    public ProviderHistoryRowViewModel(ProviderHistorySummary summary)
    {
        DisplayName = summary.DisplayName;
        EntryText = $"{summary.EntryCount} entry(s)";
        LatestText = $"Latest: {summary.LatestUpdatedAt:yyyy-MM-dd HH:mm:ss zzz}";
        HealthText = $"Health: {summary.LatestHealth}";
        RemainingText = summary.LatestRemainingPercent is double remaining
            ? $"Remaining: {remaining:0.#}%"
            : "Remaining: unknown";
        SourceText = $"Source: {summary.LatestSourceKind}";
    }

    public string DisplayName { get; }

    public string EntryText { get; }

    public string LatestText { get; }

    public string HealthText { get; }

    public string RemainingText { get; }

    public string SourceText { get; }
}
