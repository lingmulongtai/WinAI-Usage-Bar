using System.Text.Json;
using System.Text;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface ISnapshotStore
{
    Task<IReadOnlyDictionary<ProviderId, UsageSnapshot>> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(IEnumerable<UsageSnapshot> snapshots, CancellationToken cancellationToken);

    Task AppendHistoryAsync(
        IEnumerable<UsageSnapshot> snapshots,
        HistoryRetentionSettings retention,
        CancellationToken cancellationToken);
}

public sealed class JsonSnapshotStore(AppDataPaths paths, Func<DateTimeOffset>? nowProvider = null) : ISnapshotStore
{
    private readonly JsonSerializerOptions indentedOptions = JsonInfrastructureOptions.CreateIndented();
    private readonly JsonSerializerOptions ndjsonOptions = JsonInfrastructureOptions.CreateNdjson();
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<IReadOnlyDictionary<ProviderId, UsageSnapshot>> LoadAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();

        if (!File.Exists(paths.SnapshotsPath))
        {
            return new Dictionary<ProviderId, UsageSnapshot>();
        }

        await using var stream = File.OpenRead(paths.SnapshotsPath);
        var snapshots = await JsonSerializer.DeserializeAsync<List<UsageSnapshot>>(stream, indentedOptions, cancellationToken).ConfigureAwait(false)
            ?? [];

        return snapshots
            .GroupBy(snapshot => snapshot.ProviderId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(snapshot => snapshot.UpdatedAt).First());
    }

    public async Task SaveAsync(IEnumerable<UsageSnapshot> snapshots, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var ordered = snapshots.OrderBy(snapshot => snapshot.ProviderId).ToList();
        var tempPath = $"{paths.SnapshotsPath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, ordered, indentedOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, paths.SnapshotsPath, overwrite: true);
    }

    public async Task AppendHistoryAsync(
        IEnumerable<UsageSnapshot> snapshots,
        HistoryRetentionSettings retention,
        CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        await using (var stream = new FileStream(paths.HistoryPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        await using (var writer = new StreamWriter(stream))
        {
            foreach (var snapshot in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = JsonSerializer.Serialize(snapshot, ndjsonOptions);
                await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        await ApplyRetentionAsync(retention, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyRetentionAsync(
        HistoryRetentionSettings retention,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.HistoryPath))
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(paths.HistoryPath, cancellationToken).ConfigureAwait(false);
        var cutoff = nowProvider().AddDays(-retention.MaxDays);
        var kept = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryParseSnapshot(line, out var snapshot) || snapshot.UpdatedAt >= cutoff)
            {
                kept.Add(line);
            }
        }

        TrimToMaxBytes(kept, retention.MaxBytes);

        var tempPath = $"{paths.HistoryPath}.tmp";
        await File.WriteAllLinesAsync(tempPath, kept, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, paths.HistoryPath, overwrite: true);
    }

    private bool TryParseSnapshot(string line, out UsageSnapshot snapshot)
    {
        try
        {
            snapshot = JsonSerializer.Deserialize<UsageSnapshot>(line, ndjsonOptions)!;
            return snapshot is not null;
        }
        catch (JsonException)
        {
            snapshot = default!;
            return false;
        }
    }

    private static void TrimToMaxBytes(List<string> lines, long maxBytes)
    {
        while (lines.Count > 0 && EstimateUtf8Size(lines) > maxBytes)
        {
            lines.RemoveAt(0);
        }
    }

    private static long EstimateUtf8Size(IEnumerable<string> lines)
    {
        var total = 0L;
        foreach (var line in lines)
        {
            total += Encoding.UTF8.GetByteCount(line);
            total += Environment.NewLine.Length;
        }

        return total;
    }
}
