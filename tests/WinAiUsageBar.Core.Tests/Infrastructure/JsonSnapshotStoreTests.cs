using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class JsonSnapshotStoreTests
{
    [Fact]
    public async Task SaveAsync_SanitizesSnapshotsBeforeWritingAndLoading()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var store = new JsonSnapshotStore(paths);
        var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var secretValue = "sample-store-secret";

        try
        {
            await store.SaveAsync(
                [SecretSnapshot(ProviderId.Codex, now, secretValue)],
                CancellationToken.None);

            var text = await File.ReadAllTextAsync(paths.SnapshotsPath);
            var loaded = await store.LoadAsync(CancellationToken.None);
            var snapshot = Assert.Single(loaded).Value;

            Assert.DoesNotContain(secretValue, text, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
            AssertSanitizedSnapshot(snapshot, secretValue);
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
    public async Task AppendHistoryAsync_SanitizesSnapshotsBeforeWritingAndRetentionRewrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var store = new JsonSnapshotStore(paths, () => now);
        var secretValue = "sample-history-secret";

        try
        {
            paths.EnsureCreated();
            var rawLegacyLine = JsonSerializer.Serialize(
                SecretSnapshot(ProviderId.Codex, now.AddMinutes(-5), secretValue),
                JsonInfrastructureOptions.CreateNdjson());
            await File.WriteAllTextAsync(paths.HistoryPath, rawLegacyLine + Environment.NewLine);

            await store.AppendHistoryAsync(
                [SecretSnapshot(ProviderId.ChatGPT, now, secretValue)],
                new HistoryRetentionSettings { MaxDays = 30, MaxBytes = 1_000_000 },
                CancellationToken.None);

            var text = await File.ReadAllTextAsync(paths.HistoryPath);
            var lines = await File.ReadAllLinesAsync(paths.HistoryPath);

            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain(secretValue, text, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
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

    private static UsageSnapshot SecretSnapshot(
        ProviderId providerId,
        DateTimeOffset updatedAt,
        string secretValue)
    {
        return new UsageSnapshot(
            providerId,
            $"Provider token={secretValue}",
            ProviderHealth.Warning,
            new ProviderIdentity(
                Email: $"person access_token={secretValue}",
                AccountName: $"account token={secretValue}",
                PlanName: $"plan cookie={secretValue}",
                Organization: $"org api_key={secretValue}"),
            new UsageWindow(
                $"Primary token={secretValue}",
                85,
                15,
                null,
                $"primary reset cookie={secretValue}",
                $"requests secret_name={secretValue}",
                85,
                100),
            SecondaryWindow: new UsageWindow(
                $"Secondary access_token={secretValue}",
                10,
                90,
                null,
                $"secondary reset api_key={secretValue}",
                $"tokens pat_secret_name={secretValue}",
                10,
                100),
            Credits: new ProviderCredits(1m, $"USD token={secretValue}", null, null),
            DataSourceKind.Manual,
            updatedAt,
            $"status authorization: bearer {secretValue}",
            $"error cookie={secretValue}");
    }

    private static void AssertSanitizedSnapshot(UsageSnapshot snapshot, string secretValue)
    {
        var text = string.Join(
            Environment.NewLine,
            snapshot.DisplayName,
            snapshot.Identity?.Email,
            snapshot.Identity?.AccountName,
            snapshot.Identity?.PlanName,
            snapshot.Identity?.Organization,
            snapshot.PrimaryWindow?.Label,
            snapshot.PrimaryWindow?.ResetDescription,
            snapshot.PrimaryWindow?.Unit,
            snapshot.SecondaryWindow?.Label,
            snapshot.SecondaryWindow?.ResetDescription,
            snapshot.SecondaryWindow?.Unit,
            snapshot.Credits?.Currency,
            snapshot.StatusMessage,
            snapshot.ErrorMessage);

        Assert.DoesNotContain(secretValue, text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
    }
}
