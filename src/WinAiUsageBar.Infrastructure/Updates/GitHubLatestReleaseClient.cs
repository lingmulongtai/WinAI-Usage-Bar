using System.Net;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IGitHubLatestReleaseClient
{
    Task<LatestReleaseResult> GetLatestAsync(CancellationToken cancellationToken);
}

public sealed class GitHubLatestReleaseClient(
    HttpClient? httpClient = null,
    string owner = "lingmulongtai",
    string repository = "WinAI-Usage-Bar") : IGitHubLatestReleaseClient
{
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();
    private readonly Uri latestReleaseUri =
        new($"https://api.github.com/repos/{owner}/{repository}/releases/latest");

    public async Task<LatestReleaseResult> GetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, latestReleaseUri);
            request.Headers.UserAgent.ParseAdd("WinAIUsageBar/0.1");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return LatestReleaseResult.NotFound("No published GitHub release was found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return LatestReleaseResult.Failed(
                    $"GitHub release request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return LatestReleaseResult.Found(GitHubReleaseJsonParser.Parse(json));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return LatestReleaseResult.Failed($"GitHub release request failed: {ex.Message}");
        }
    }
}
