using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineRefreshReportFormatterTests
{
    [Fact]
    public void Format_IncludesSafeSnapshotSummary()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.FromHours(9));
        var snapshot = new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Warning,
            Identity: new ProviderIdentity("person@example.com", "person", "Plus", null),
            PrimaryWindow: new UsageWindow(
                "Codex usage",
                UsedPercent: 82.5,
                RemainingPercent: 17.5,
                ResetsAt: generatedAt.AddHours(2),
                ResetDescription: "reset token=reset-secret",
                Unit: "%",
                Used: 82.5,
                Limit: 100),
            SecondaryWindow: null,
            Credits: new ProviderCredits(12.34m, "USD", 5.67m, 8901),
            DataSourceKind.LocalAppServer,
            UpdatedAt: generatedAt.AddMinutes(-1),
            StatusMessage: "Loaded from Codex app-server with access_token=status-secret",
            ErrorMessage: "Low quota but cookie=error-secret");

        var report = CommandLineRefreshReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            [snapshot],
            generatedAt);

        Assert.Contains("WinAI Usage Bar 1.2.3", report, StringComparison.Ordinal);
        Assert.Contains("Refresh once generated: 2026-07-08 07:30:00 +09:00", report, StringComparison.Ordinal);
        Assert.Contains("Snapshots: 1", report, StringComparison.Ordinal);
        Assert.Contains("Codex", report, StringComparison.Ordinal);
        Assert.Contains("Health: Warning", report, StringComparison.Ordinal);
        Assert.Contains("Source: LocalAppServer", report, StringComparison.Ordinal);
        Assert.Contains("Remaining: 17.5%", report, StringComparison.Ordinal);
        Assert.Contains("Reset: 2026-07-08 09:30:00 +09:00", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Secondary:", report, StringComparison.Ordinal);
        Assert.Contains("Credits: 12.34 USD, month 5.67, tokens31d 8901", report, StringComparison.Ordinal);
        Assert.Contains("Repair:", report, StringComparison.Ordinal);
        Assert.Contains("Review the status message", report, StringComparison.Ordinal);
        Assert.Contains("local provider command can start", report, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", report, StringComparison.Ordinal);
        Assert.DoesNotContain("status-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("error-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("person@example.com", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesSafeSecondaryWindowSummary()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.FromHours(9));
        var snapshot = new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Ok,
            Identity: null,
            PrimaryWindow: new UsageWindow(
                "Codex usage",
                UsedPercent: 40,
                RemainingPercent: 60,
                ResetsAt: null,
                ResetDescription: "primary",
                Unit: "%",
                Used: 40,
                Limit: 100),
            SecondaryWindow: new UsageWindow(
                "Codex rate token=label-secret",
                UsedPercent: 77.75,
                RemainingPercent: 22.25,
                ResetsAt: null,
                ResetDescription: "reset cookie=reset-secret",
                Unit: "%",
                Used: 77.75,
                Limit: 100),
            Credits: null,
            DataSourceKind.LocalAppServer,
            UpdatedAt: generatedAt,
            StatusMessage: "Loaded",
            ErrorMessage: null);

        var report = CommandLineRefreshReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            [snapshot],
            generatedAt);

        Assert.Contains("Secondary:", report, StringComparison.Ordinal);
        Assert.Contains("remaining 22.25%", report, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", report, StringComparison.Ordinal);
        Assert.DoesNotContain("label-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("reset-secret", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesUnsupportedLocalAppServerRepairGuidance()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.FromHours(9));
        var snapshot = Snapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Unsupported,
            DataSourceKind.LocalAppServer,
            "Codex CLI could not start from WindowsApps with token=start-secret",
            "cookie=error-secret",
            generatedAt);

        var report = CommandLineRefreshReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            [snapshot],
            generatedAt);

        Assert.Contains("Repair:", report, StringComparison.Ordinal);
        Assert.Contains("Switch to Manual mode", report, StringComparison.Ordinal);
        Assert.Contains("local provider command can start", report, StringComparison.Ordinal);
        Assert.Contains("App Execution Alias", report, StringComparison.Ordinal);
        Assert.Contains("CLI command override", report, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", report, StringComparison.Ordinal);
        Assert.DoesNotContain("start-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("error-secret", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesCopilotAuthRepairGuidanceWithoutConfiguredScopeOrSecretNames()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.FromHours(9));
        var snapshot = Snapshot(
            ProviderId.GitHubCopilot,
            "GitHub Copilot",
            ProviderHealth.AuthRequired,
            DataSourceKind.OfficialApi,
            "missing github-copilot-pat for my-org",
            "token=sample-token-value",
            generatedAt);

        var report = CommandLineRefreshReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            [snapshot],
            generatedAt);

        Assert.Contains("Repair:", report, StringComparison.Ordinal);
        Assert.Contains("Reconnect credentials", report, StringComparison.Ordinal);
        Assert.Contains("PAT secret reference", report, StringComparison.Ordinal);
        Assert.DoesNotContain("github-copilot-pat", report, StringComparison.Ordinal);
        Assert.DoesNotContain("my-org", report, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-token-value", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExplainsEmptySnapshotSet()
    {
        var report = CommandLineRefreshReportFormatter.Format(
            new AppInfo("WinAI Usage Bar", "1.2.3.0", "1.2.3"),
            [],
            new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.FromHours(9)));

        Assert.Contains("Snapshots: 0", report, StringComparison.Ordinal);
        Assert.Contains("No enabled provider snapshots were produced.", report, StringComparison.Ordinal);
    }

    private static UsageSnapshot Snapshot(
        ProviderId providerId,
        string displayName,
        ProviderHealth health,
        DataSourceKind sourceKind,
        string? statusMessage,
        string? errorMessage,
        DateTimeOffset updatedAt)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            health,
            Identity: new ProviderIdentity("person@example.com", "person", "Plus", "my-org"),
            PrimaryWindow: null,
            SecondaryWindow: null,
            Credits: null,
            sourceKind,
            updatedAt,
            statusMessage,
            errorMessage);
    }
}
