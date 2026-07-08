using System.Text.Json;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class HistorySummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsEmptySummaryWhenHistoryFileIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var service = new HistorySummaryService(paths);

        try
        {
            var summary = await service.GetSummaryAsync(CancellationToken.None);

            Assert.Equal(0, summary.TotalEntries);
            Assert.Equal(0, summary.InvalidLines);
            Assert.Empty(summary.Providers);
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
    public async Task GetSummaryAsync_SummarizesValidHistoryWithoutRawMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var service = new HistorySummaryService(paths);
        var jsonOptions = JsonInfrastructureOptions.CreateNdjson();
        var oldCodex = Snapshot(
            ProviderId.Codex,
            "Codex",
            new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
            ProviderHealth.Ok,
            remainingPercent: 80,
            "super-secret status");
        var latestCodex = Snapshot(
            ProviderId.Codex,
            "Codex",
            new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero),
            ProviderHealth.Warning,
            remainingPercent: 12,
            "another hidden status");
        var chatGpt = Snapshot(
            ProviderId.ChatGPT,
            "ChatGPT",
            new DateTimeOffset(2026, 7, 8, 11, 0, 0, TimeSpan.Zero),
            ProviderHealth.Ok,
            remainingPercent: 64,
            "token=sample-history-token");

        try
        {
            await File.WriteAllLinesAsync(
                paths.HistoryPath,
                [
                    JsonSerializer.Serialize(oldCodex, jsonOptions),
                    "not json",
                    JsonSerializer.Serialize(latestCodex, jsonOptions),
                    JsonSerializer.Serialize(chatGpt, jsonOptions)
                ]);

            var summary = await service.GetSummaryAsync(CancellationToken.None);
            var viewModel = new HistorySummaryViewModel(summary);
            var visibleText = string.Join(
                Environment.NewLine,
                viewModel.Providers.SelectMany(provider => new[]
                {
                    provider.DisplayName,
                    provider.EntryText,
                    provider.LatestText,
                    provider.HealthText,
                    provider.RemainingText,
                    provider.SourceText
                }));
            var codex = summary.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);

            Assert.Equal(3, summary.TotalEntries);
            Assert.Equal(1, summary.InvalidLines);
            Assert.Equal(oldCodex.UpdatedAt, summary.EarliestUpdatedAt);
            Assert.Equal(chatGpt.UpdatedAt, summary.LatestUpdatedAt);
            Assert.Equal(2, codex.EntryCount);
            Assert.Equal(ProviderHealth.Warning, codex.LatestHealth);
            Assert.Equal(12, codex.LatestRemainingPercent);
            Assert.Contains("3 retained history", viewModel.SummaryText);
            Assert.Contains("1 invalid history", viewModel.InvalidLineText);
            Assert.DoesNotContain("super-secret", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("sample-history-token", visibleText, StringComparison.Ordinal);
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
    public async Task GetSummaryAsync_SanitizesLegacyHistorySnapshotsBeforeAggregation()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var service = new HistorySummaryService(paths);
        var jsonOptions = JsonInfrastructureOptions.CreateNdjson();
        var updatedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var secretValue = "sample-legacy-secret";
        var snapshot = new UsageSnapshot(
            ProviderId.Codex,
            $"Codex token={secretValue}",
            ProviderHealth.Warning,
            Identity: new ProviderIdentity(
                $"person access_token={secretValue}",
                $"account cookie={secretValue}",
                null,
                null),
            PrimaryWindow: new UsageWindow(
                $"Primary token={secretValue}",
                88,
                12,
                null,
                $"reset cookie={secretValue}",
                $"requests secret_name={secretValue}",
                88,
                100),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            updatedAt,
            $"status authorization: bearer {secretValue}",
            $"error cookie={secretValue}");

        try
        {
            await File.WriteAllTextAsync(
                paths.HistoryPath,
                JsonSerializer.Serialize(snapshot, jsonOptions) + Environment.NewLine);

            var summary = await service.GetSummaryAsync(CancellationToken.None);
            var provider = Assert.Single(summary.Providers);
            var viewModel = new HistorySummaryViewModel(summary);
            var visibleText = string.Join(
                Environment.NewLine,
                viewModel.Providers.SelectMany(row => new[]
                {
                    row.DisplayName,
                    row.EntryText,
                    row.LatestText,
                    row.HealthText,
                    row.RemainingText,
                    row.SourceText
                }));

            Assert.Equal("Codex token=[REDACTED]", provider.DisplayName);
            Assert.DoesNotContain(secretValue, visibleText, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", visibleText, StringComparison.Ordinal);
            Assert.Equal(12, provider.LatestRemainingPercent);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UsageSnapshot Snapshot(
        ProviderId providerId,
        string displayName,
        DateTimeOffset updatedAt,
        ProviderHealth health,
        double remainingPercent,
        string statusMessage)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            health,
            Identity: new ProviderIdentity("secret@example.test", null, null, null),
            PrimaryWindow: new UsageWindow("Test", 100 - remainingPercent, remainingPercent, null, "reset", "%", null, null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            updatedAt,
            statusMessage,
            ErrorMessage: "raw error should not be displayed");
    }
}
