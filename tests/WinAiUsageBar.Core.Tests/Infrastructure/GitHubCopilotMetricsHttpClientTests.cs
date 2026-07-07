using System.Net;
using System.Text;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Process;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class GitHubCopilotMetricsHttpClientTests
{
    [Fact]
    public async Task FetchLatestReportAsync_UsesOrganizationEndpointAndParsesReport()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "download_links": [
                    "https://example.com/report-1.ndjson",
                    "https://example.com/report-2.ndjson"
                  ],
                  "report_start_day": "2026-06-01",
                  "report_end_day": "2026-06-28"
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        var client = new GitHubCopilotMetricsHttpClient(
            new HttpClient(handler),
            new Uri("https://api.github.test/"));

        var result = await client.FetchLatestReportAsync(
            new GitHubCopilotMetricsRequest(
                GitHubCopilotMetricsScope.Organization,
                "octo-org",
                "ghp_secret_token"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new DateOnly(2026, 6, 1), result.Report?.ReportStartDay);
        Assert.Equal(new DateOnly(2026, 6, 28), result.Report?.ReportEndDay);
        Assert.Equal(2, result.Report?.DownloadLinkCount);
        Assert.Equal("https://api.github.test/orgs/octo-org/copilot/metrics/reports/organization-28-day/latest", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("https://example.com/report-1.ndjson", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchLatestReportAsync_UsesEnterpriseEndpoint()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"download_links":[],"report_start_day":"2026-06-01","report_end_day":"2026-06-28"}""",
                Encoding.UTF8,
                "application/json")
        });
        var client = new GitHubCopilotMetricsHttpClient(
            new HttpClient(handler),
            new Uri("https://api.github.test/"));

        await client.FetchLatestReportAsync(
            new GitHubCopilotMetricsRequest(
                GitHubCopilotMetricsScope.Enterprise,
                "octo-enterprise",
                "token"),
            CancellationToken.None);

        Assert.Equal("https://api.github.test/enterprises/octo-enterprise/copilot/metrics/reports/enterprise-28-day/latest", handler.RequestUri?.ToString());
    }

    [Fact]
    public async Task FetchLatestReportAsync_MapsPermissionErrorsWithoutLeakingToken()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "Forbidden token=ghp_secret_token"
        });
        var client = new GitHubCopilotMetricsHttpClient(
            new HttpClient(handler),
            new Uri("https://api.github.test/"));

        var result = await client.FetchLatestReportAsync(
            new GitHubCopilotMetricsRequest(
                GitHubCopilotMetricsScope.Organization,
                "octo-org",
                "ghp_secret_token"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.AuthRequired, result.Health);
        Assert.Contains("permissions", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("ghp_secret_token", StringComparison.Ordinal));
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? ApiVersion { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            ApiVersion = request.Headers.TryGetValues("X-GitHub-Api-Version", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(response);
        }
    }
}
