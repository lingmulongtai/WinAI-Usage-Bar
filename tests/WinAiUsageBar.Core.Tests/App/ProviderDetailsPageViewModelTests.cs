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
        Assert.True(provider.HasRepairLines);
        Assert.Contains(provider.RepairLines, line => line.Contains("Review the status message", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_RedactsStatusAndErrorText()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: "authorization: bearer sample-api-key-value",
            errorMessage: "token=sample-token-value");

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);

        Assert.Contains("[REDACTED]", provider.StatusText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", provider.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-api-key-value", provider.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-token-value", provider.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", provider.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", provider.ErrorText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_AddsWarningForFutureSnapshotTimestamp()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderDetailsPageViewModel(
            [Snapshot(now.AddMinutes(4))],
            nowProvider: () => now);

        var provider = Assert.Single(viewModel.Providers);

        Assert.Contains("Updated: in 4m (future timestamp)", provider.SummaryLines);
        Assert.Contains(
            provider.SummaryLines,
            line => line.Contains("in the future", StringComparison.Ordinal)
                && line.Contains("system clock", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_AddsWarningForStaleSnapshotTimestamp()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderDetailsPageViewModel(
            [Snapshot(now.AddMinutes(-45))],
            nowProvider: () => now);

        var provider = Assert.Single(viewModel.Providers);

        Assert.Contains("Updated: 45m ago", provider.SummaryLines);
        Assert.Contains(
            provider.SummaryLines,
            line => line.Contains("cached snapshot is stale", StringComparison.Ordinal)
                && line.Contains("refresh now", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_HidesRepairGuidanceForOkProviders()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: "Provider is healthy.",
            errorMessage: null,
            health: ProviderHealth.Ok,
            sourceKind: DataSourceKind.Manual);

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);

        Assert.False(provider.HasRepairLines);
        Assert.Empty(provider.RepairLines);
    }

    [Fact]
    public void Constructor_AddsAuthRepairGuidanceWithoutLeakingSecretReferences()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: "missing github-copilot-pat for my-org",
            errorMessage: "token=sample-token-value",
            providerId: ProviderId.GitHubCopilot,
            displayName: "GitHub Copilot",
            health: ProviderHealth.AuthRequired,
            sourceKind: DataSourceKind.OfficialApi);

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);
        var repairText = string.Join(Environment.NewLine, provider.RepairLines);

        Assert.True(provider.HasRepairLines);
        Assert.Contains("Reconnect credentials", repairText, StringComparison.Ordinal);
        Assert.Contains("PAT secret reference", repairText, StringComparison.Ordinal);
        Assert.Contains("metrics permissions", repairText, StringComparison.Ordinal);
        Assert.Contains("Manual mode", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain("github-copilot-pat", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain("my-org", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-token-value", repairText, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AddsUnsupportedCliRepairGuidance()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: @"claude command missing at C:\Tools\claude.cmd for my-org",
            errorMessage: "unsupported source token=sample-token-value",
            providerId: ProviderId.ClaudeCode,
            displayName: "Claude Code",
            health: ProviderHealth.Unsupported,
            sourceKind: DataSourceKind.Cli);

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);
        var repairText = string.Join(Environment.NewLine, provider.RepairLines);

        Assert.Contains("Switch to Manual mode", repairText, StringComparison.Ordinal);
        Assert.Contains("command is installed", repairText, StringComparison.Ordinal);
        Assert.Contains("health report", repairText, StringComparison.Ordinal);
        Assert.Contains("readiness placeholder", repairText, StringComparison.Ordinal);
        Assert.Contains("does not run interactive /usage", repairText, StringComparison.Ordinal);
        Assert.Contains("does not scrape private local files", repairText, StringComparison.Ordinal);
        Assert.Contains("launchable claude command", repairText, StringComparison.Ordinal);
        Assert.Contains("provider CLI sign-in flow", repairText, StringComparison.Ordinal);
        Assert.Contains("CLI command override", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Tools\claude.cmd", repairText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("my-org", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-token-value", repairText, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AddsCodexWindowsAppsRepairGuidance()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var snapshot = Snapshot(
            now,
            statusMessage: "Codex CLI was found but Windows could not start it.",
            errorMessage: @"Access is denied for C:\Program Files\WindowsApps\OpenAI.Codex\codex.exe",
            providerId: ProviderId.Codex,
            displayName: "Codex",
            health: ProviderHealth.Unsupported,
            sourceKind: DataSourceKind.LocalAppServer);

        var viewModel = new ProviderDetailsPageViewModel([snapshot], nowProvider: () => now);
        var provider = Assert.Single(viewModel.Providers);
        var repairText = string.Join(Environment.NewLine, provider.RepairLines);

        Assert.Contains("WindowsApps", repairText, StringComparison.Ordinal);
        Assert.Contains("App Execution Alias", repairText, StringComparison.Ordinal);
        Assert.Contains("CLI command override", repairText, StringComparison.Ordinal);
        Assert.Contains("health report", repairText, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Program Files\WindowsApps", repairText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_AddsErrorAndUnknownRepairGuidance()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var errorSnapshot = Snapshot(
            now,
            statusMessage: "api failed",
            errorMessage: "request failed",
            providerId: ProviderId.Gemini,
            displayName: "Gemini",
            health: ProviderHealth.Error,
            sourceKind: DataSourceKind.OfficialApi);
        var unknownSnapshot = Snapshot(
            now,
            statusMessage: null,
            errorMessage: null,
            providerId: ProviderId.OpenCodeZen,
            displayName: "OpenCode Zen",
            health: ProviderHealth.Unknown,
            sourceKind: DataSourceKind.Manual);

        var viewModel = new ProviderDetailsPageViewModel(
            [errorSnapshot, unknownSnapshot],
            nowProvider: () => now);

        var errorProvider = viewModel.Providers.Single(provider => provider.DisplayName == "Gemini");
        var unknownProvider = viewModel.Providers.Single(provider => provider.DisplayName == "OpenCode Zen");

        Assert.Contains(errorProvider.RepairLines, line => line.Contains("export diagnostics", StringComparison.Ordinal));
        Assert.Contains(errorProvider.RepairLines, line => line.Contains("secret references", StringComparison.Ordinal));
        Assert.Contains(unknownProvider.RepairLines, line => line.Contains("Refresh now", StringComparison.Ordinal));
        Assert.Contains(unknownProvider.RepairLines, line => line.Contains("Manual mode", StringComparison.Ordinal));
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
        string? errorMessage,
        ProviderId providerId = ProviderId.Codex,
        string displayName = "Codex",
        ProviderHealth health = ProviderHealth.Warning,
        DataSourceKind sourceKind = DataSourceKind.LocalAppServer)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            health,
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
            sourceKind,
            updatedAt,
            statusMessage,
            errorMessage);
    }
}
