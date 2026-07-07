using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.GitHubCopilot;

public sealed class GitHubCopilotMetricsProviderAdapter(
    ProviderDescriptor descriptor,
    ISecretResolver secretResolver,
    IGitHubCopilotMetricsClient metricsClient) : IProviderAdapter
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    public async Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        var settings = context.ProviderConfig.GitHubCopilot;
        var secretName = TrimToNull(settings.PatSecretName);
        if (secretName is null)
        {
            return AuthRequired(context.Now, "GitHub Copilot API mode needs a PAT secret name.");
        }

        var token = await secretResolver.ResolveSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthRequired(context.Now, "GitHub Copilot PAT secret was not found. Save the secret value from Privacy & Data.");
        }

        var scope = ResolveScope(settings.Organization, settings.EnterpriseSlug);
        if (scope is null)
        {
            return AuthRequired(context.Now, "GitHub Copilot API mode needs an organization or enterprise slug.");
        }

        var result = await metricsClient.FetchLatestReportAsync(
            new GitHubCopilotMetricsRequest(scope.Value.Scope, scope.Value.Slug, token),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success || result.Report is null)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                result.Health,
                DataSourceKind.OfficialApi,
                context.Now,
                result.ErrorMessage ?? "GitHub Copilot metrics report could not be fetched.",
                result.Diagnostics.ToArray());
        }

        var report = result.Report;
        var resetDescription = DescribeReportRange(report);
        var snapshot = new UsageSnapshot(
            Descriptor.Id,
            Descriptor.DisplayName,
            ProviderHealth.Ok,
            new ProviderIdentity(
                Email: null,
                AccountName: null,
                PlanName: "Copilot metrics",
                Organization: scope.Value.Slug),
            new UsageWindow(
                "Latest metrics report",
                UsedPercent: null,
                RemainingPercent: null,
                ResetsAt: null,
                resetDescription,
                Unit: "report",
                Used: report.DownloadLinkCount,
                Limit: null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.OfficialApi,
            context.Now,
            $"GitHub Copilot {scope.Value.Scope.ToString().ToLowerInvariant()} metrics report available ({report.DownloadLinkCount} download link(s)).",
            ErrorMessage: null);

        return ProviderFetchResult.FromSnapshot(snapshot, result.Diagnostics.ToArray());
    }

    private ProviderFetchResult AuthRequired(DateTimeOffset now, string message)
    {
        return ProviderFetchResult.Failure(
            Descriptor,
            ProviderHealth.AuthRequired,
            DataSourceKind.OfficialApi,
            now,
            message);
    }

    private static (GitHubCopilotMetricsScope Scope, string Slug)? ResolveScope(
        string? organization,
        string? enterpriseSlug)
    {
        if (TrimToNull(organization) is { } org)
        {
            return (GitHubCopilotMetricsScope.Organization, org);
        }

        if (TrimToNull(enterpriseSlug) is { } enterprise)
        {
            return (GitHubCopilotMetricsScope.Enterprise, enterprise);
        }

        return null;
    }

    private static string DescribeReportRange(GitHubCopilotMetricsReport report)
    {
        if (report.ReportStartDay is not null && report.ReportEndDay is not null)
        {
            return $"{report.ReportStartDay:yyyy-MM-dd} to {report.ReportEndDay:yyyy-MM-dd}";
        }

        if (report.ReportDay is not null)
        {
            return $"{report.ReportDay:yyyy-MM-dd}";
        }

        return "Latest report";
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
