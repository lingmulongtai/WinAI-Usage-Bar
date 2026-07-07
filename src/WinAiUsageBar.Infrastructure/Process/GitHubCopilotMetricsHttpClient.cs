using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed class GitHubCopilotMetricsHttpClient(
    HttpClient httpClient,
    Uri? baseUri = null) : IGitHubCopilotMetricsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri baseUri = baseUri ?? new Uri("https://api.github.com/");

    public async Task<GitHubCopilotMetricsFetchResult> FetchLatestReportAsync(
        GitHubCopilotMetricsRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(request));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Token);
        message.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        message.Headers.UserAgent.ParseAdd("WinAIUsageBar/1.0");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return GitHubCopilotMetricsFetchResult.Failure(
                "GitHub Copilot metrics report is not available yet.",
                ProviderHealth.Warning,
                "GitHub Copilot metrics returned 204.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var safeReason = DiagnosticRedactor.Redact(response.ReasonPhrase);
            return GitHubCopilotMetricsFetchResult.Failure(
                response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                    ? "GitHub Copilot metrics require organization or enterprise permissions."
                    : $"GitHub Copilot metrics request failed with {(int)response.StatusCode}.",
                response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                    ? ProviderHealth.AuthRequired
                    : ProviderHealth.Error,
                $"GitHub Copilot metrics HTTP {(int)response.StatusCode} {safeReason}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GitHubCopilotMetricsResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var report = new GitHubCopilotMetricsReport(
            ParseDay(payload?.ReportStartDay),
            ParseDay(payload?.ReportEndDay),
            ParseDay(payload?.ReportDay),
            payload?.DownloadLinks?.Count ?? 0);

        return GitHubCopilotMetricsFetchResult.FromReport(
            report,
            $"GitHub Copilot metrics report returned {report.DownloadLinkCount} download link(s).");
    }

    private Uri BuildUri(GitHubCopilotMetricsRequest request)
    {
        var escapedSlug = Uri.EscapeDataString(request.Slug);
        var path = request.Scope == GitHubCopilotMetricsScope.Organization
            ? $"orgs/{escapedSlug}/copilot/metrics/reports/organization-28-day/latest"
            : $"enterprises/{escapedSlug}/copilot/metrics/reports/enterprise-28-day/latest";
        return new Uri(baseUri, path);
    }

    private static DateOnly? ParseDay(string? value)
    {
        return DateOnly.TryParse(value, out var day) ? day : null;
    }

    private sealed record GitHubCopilotMetricsResponse(
        [property: JsonPropertyName("download_links")] IReadOnlyList<string>? DownloadLinks,
        [property: JsonPropertyName("report_start_day")] string? ReportStartDay,
        [property: JsonPropertyName("report_end_day")] string? ReportEndDay,
        [property: JsonPropertyName("report_day")] string? ReportDay);
}
