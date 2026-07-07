using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Abstractions;

public interface ICommandProbe
{
    Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken);
}

public interface ICodexAppServerClient
{
    Task<CodexAppServerData> FetchAccountUsageAsync(CancellationToken cancellationToken);
}

public interface ISecretResolver
{
    Task<string?> ResolveSecretAsync(string name, CancellationToken cancellationToken);
}

public interface IGitHubCopilotMetricsClient
{
    Task<GitHubCopilotMetricsFetchResult> FetchLatestReportAsync(
        GitHubCopilotMetricsRequest request,
        CancellationToken cancellationToken);
}

public sealed record CodexAppServerData(
    string? AccountJson,
    string? RateLimitsJson,
    string? UsageJson,
    IReadOnlyList<string> Diagnostics);

public enum GitHubCopilotMetricsScope
{
    Organization,
    Enterprise
}

public sealed record GitHubCopilotMetricsRequest(
    GitHubCopilotMetricsScope Scope,
    string Slug,
    string Token);

public sealed record GitHubCopilotMetricsReport(
    DateOnly? ReportStartDay,
    DateOnly? ReportEndDay,
    DateOnly? ReportDay,
    int DownloadLinkCount);

public sealed record GitHubCopilotMetricsFetchResult(
    bool Success,
    GitHubCopilotMetricsReport? Report,
    ProviderHealth Health,
    string? ErrorMessage,
    IReadOnlyList<string> Diagnostics)
{
    public static GitHubCopilotMetricsFetchResult FromReport(
        GitHubCopilotMetricsReport report,
        params string[] diagnostics)
    {
        return new GitHubCopilotMetricsFetchResult(true, report, ProviderHealth.Ok, null, diagnostics);
    }

    public static GitHubCopilotMetricsFetchResult Failure(
        string message,
        ProviderHealth health = ProviderHealth.Error,
        params string[] diagnostics)
    {
        return new GitHubCopilotMetricsFetchResult(false, null, health, message, diagnostics);
    }
}
