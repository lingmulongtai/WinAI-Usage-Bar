using System.Text.Json;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IHistorySummaryService
{
    Task<HistorySummary> GetSummaryAsync(CancellationToken cancellationToken);
}

public sealed record HistorySummary(
    int TotalEntries,
    int InvalidLines,
    DateTimeOffset? EarliestUpdatedAt,
    DateTimeOffset? LatestUpdatedAt,
    IReadOnlyList<ProviderHistorySummary> Providers);

public sealed record ProviderHistorySummary(
    ProviderId ProviderId,
    string DisplayName,
    int EntryCount,
    DateTimeOffset LatestUpdatedAt,
    ProviderHealth LatestHealth,
    double? LatestRemainingPercent,
    DataSourceKind LatestSourceKind);

public sealed class HistorySummaryService(AppDataPaths paths) : IHistorySummaryService
{
    private readonly JsonSerializerOptions options = JsonInfrastructureOptions.CreateNdjson();

    public async Task<HistorySummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();

        if (!File.Exists(paths.HistoryPath))
        {
            return new HistorySummary(0, 0, null, null, []);
        }

        var snapshots = new List<UsageSnapshot>();
        var invalidLines = 0;
        var lines = await File.ReadAllLinesAsync(paths.HistoryPath, cancellationToken).ConfigureAwait(false);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryParseSnapshot(line, out var snapshot))
            {
                snapshots.Add(snapshot);
            }
            else
            {
                invalidLines++;
            }
        }

        if (snapshots.Count == 0)
        {
            return new HistorySummary(0, invalidLines, null, null, []);
        }

        var providers = snapshots
            .GroupBy(snapshot => snapshot.ProviderId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(snapshot => snapshot.UpdatedAt).First();
                return new ProviderHistorySummary(
                    latest.ProviderId,
                    latest.DisplayName,
                    group.Count(),
                    latest.UpdatedAt,
                    latest.Health,
                    latest.PrimaryWindow?.RemainingPercent,
                    latest.SourceKind);
            })
            .OrderBy(provider => provider.DisplayName)
            .ToList();

        return new HistorySummary(
            snapshots.Count,
            invalidLines,
            snapshots.Min(snapshot => snapshot.UpdatedAt),
            snapshots.Max(snapshot => snapshot.UpdatedAt),
            providers);
    }

    private bool TryParseSnapshot(string line, out UsageSnapshot snapshot)
    {
        try
        {
            snapshot = JsonSerializer.Deserialize<UsageSnapshot>(line, options)!;
            return snapshot is not null;
        }
        catch (JsonException)
        {
            snapshot = default!;
            return false;
        }
    }
}
