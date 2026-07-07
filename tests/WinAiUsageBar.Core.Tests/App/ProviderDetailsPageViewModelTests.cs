using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ProviderDetailsPageViewModelTests
{
    [Fact]
    public void Constructor_ReturnsEmptyStateWhenSnapshotsAreMissing()
    {
        var viewModel = new ProviderDetailsPageViewModel([]);

        Assert.False(viewModel.HasProviders);
        Assert.Empty(viewModel.Providers);
        Assert.Contains("No provider snapshots", viewModel.EmptyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_FormatsProviderDetails()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderDetailsPageViewModel(
            [Snapshot(now.AddMinutes(-5))],
            nowProvider: () => now);

        var provider = Assert.Single(viewModel.Providers);
        Assert.Equal("Codex", provider.DisplayName);
        Assert.Contains("Provider ID: Codex", provider.SummaryLines);
        Assert.Contains("Health: Warning", provider.SummaryLines);
        Assert.Contains("Source: LocalAppServer", provider.SummaryLines);
        Assert.Contains("Updated: 5m ago", provider.SummaryLines);
        Assert.Contains("Email: user@example.com", provider.IdentityLines);
        Assert.Contains("Plan: Plus", provider.IdentityLines);
        Assert.Contains("Primary remaining: 75%", provider.UsageLines);
        Assert.Contains("Primary amount: 25 / 100 requests", provider.UsageLines);
        Assert.Contains("Secondary remaining: 90%", provider.UsageLines);
        Assert.Contains("Balance: 12.34 USD", provider.CreditLines);
        Assert.Contains("Tokens last 31 days: 42,000", provider.CreditLines);
    }

    [Fact]
    public void Constructor_RedactsStatusAndErrorText()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: "authorization: bearer sk-live123456789",
            errorMessage: "token=ghp_123456789abcdef");

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);

        Assert.Contains("[REDACTED]", provider.StatusText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", provider.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live123456789", provider.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_123456789abcdef", provider.ErrorText, StringComparison.Ordinal);
    }

    private static UsageSnapshot Snapshot(DateTimeOffset updatedAt)
    {
        return Snapshot(
            updatedAt,
            statusMessage: "Using local app-server data.",
            errorMessage: null);
    }

    private static UsageSnapshot Snapshot(
        DateTimeOffset updatedAt,
        string? statusMessage,
        string? errorMessage)
    {
        return new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Warning,
            new ProviderIdentity(
                "user@example.com",
                "Primary account",
                "Plus",
                "Example Org"),
            new UsageWindow(
                "Primary",
                UsedPercent: 25,
                RemainingPercent: 75,
                ResetsAt: new DateTimeOffset(2026, 7, 9, 0, 0, 0, TimeSpan.Zero),
                ResetDescription: null,
                Unit: "requests",
                Used: 25,
                Limit: 100),
            new UsageWindow(
                "Secondary",
                UsedPercent: 10,
                RemainingPercent: 90,
                ResetsAt: null,
                ResetDescription: "daily reset",
                Unit: "%",
                Used: null,
                Limit: null),
            new ProviderCredits(
                Balance: 12.34m,
                Currency: "USD",
                MonthToDateCost: 5.67m,
                TokensLast31Days: 42000),
            DataSourceKind.LocalAppServer,
            updatedAt,
            statusMessage,
            errorMessage);
    }
}
