using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class JsonSnapshotStoreTests
{
    [Fact]
    public async Task AppendHistoryAsync_RemovesSnapshotsOlderThanRetentionWindow()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var store = new JsonSnapshotStore(paths, () => now);

        try
        {
            await store.AppendHistoryAsync(
                [
                    Snapshot(ProviderId.Codex, "Old Codex", now.AddDays(-10)),
                    Snapshot(ProviderId.ChatGPT, "Fresh ChatGPT", now.AddDays(-1))
                ],
                new HistoryRetentionSettings { MaxDays = 3, MaxBytes = 1_000_000 },
                CancellationToken.None);

            var text = await File.ReadAllTextAsync(paths.HistoryPath);

            Assert.DoesNotContain("Old Codex", text);
            Assert.Contains("Fresh ChatGPT", text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AppendHistoryAsync_TrimsOldestLinesToMaxBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var store = new JsonSnapshotStore(paths, () => now);
        var snapshots = Enumerable.Range(0, 20)
            .Select(index => Snapshot(ProviderId.Codex, $"Codex {index:D2}", now.AddMinutes(index)))
            .ToList();

        try
        {
            await store.AppendHistoryAsync(
                snapshots,
                new HistoryRetentionSettings { MaxDays = 30, MaxBytes = 2_500 },
                CancellationToken.None);

            var fileInfo = new FileInfo(paths.HistoryPath);
            var lines = await File.ReadAllLinesAsync(paths.HistoryPath);

            Assert.True(fileInfo.Length <= 2_500);
            Assert.NotEmpty(lines);
            Assert.True(lines.Length < snapshots.Count);
            Assert.DoesNotContain(lines, line => line.Contains("Codex 00", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Codex 19", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UsageSnapshot Snapshot(ProviderId providerId, string displayName, DateTimeOffset updatedAt)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            ProviderHealth.Ok,
            Identity: null,
            new UsageWindow("Test", 25, 75, null, "test reset", "%", 25, 100),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            updatedAt,
            "history test",
            ErrorMessage: null);
    }
}
