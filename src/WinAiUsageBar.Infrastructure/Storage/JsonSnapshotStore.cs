using System.Text.Json;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface ISnapshotStore
{
    Task<IReadOnlyDictionary<ProviderId, UsageSnapshot>> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(IEnumerable<UsageSnapshot> snapshots, CancellationToken cancellationToken);

    Task AppendHistoryAsync(IEnumerable<UsageSnapshot> snapshots, CancellationToken cancellationToken);
}

public sealed class JsonSnapshotStore(AppDataPaths paths) : ISnapshotStore
{
    private readonly JsonSerializerOptions indentedOptions = JsonInfrastructureOptions.CreateIndented();
    private readonly JsonSerializerOptions ndjsonOptions = JsonInfrastructureOptions.CreateNdjson();

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

    public async Task AppendHistoryAsync(IEnumerable<UsageSnapshot> snapshots, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        await using var stream = new FileStream(paths.HistoryPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(snapshot, ndjsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }
}
