using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Core.Providers.GitHubCopilot;
using WinAiUsageBar.Core.Providers.Manual;
using WinAiUsageBar.Core.Providers.Mock;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void GetDescriptors_ReturnsAllExpectedProviders()
    {
        var registry = new ProviderRegistry();

        var descriptors = registry.GetDescriptors();

        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.ChatGPT);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.Codex);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.Gemini);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.Claude);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.ClaudeCode);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.OpenCodeZen);
        Assert.Contains(descriptors, descriptor => descriptor.Id == ProviderId.GitHubCopilot);
    }

    [Fact]
    public void CreateAdapter_UsesManualAndMockSources()
    {
        var registry = new ProviderRegistry();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);

        var manual = registry.CreateAdapter(descriptor, new ProviderConfig
        {
            ProviderId = ProviderId.Codex,
            SourceKind = DataSourceKind.Manual
        });

        var mock = registry.CreateAdapter(descriptor, new ProviderConfig
        {
            ProviderId = ProviderId.Codex,
            SourceKind = DataSourceKind.Mock
        });

        Assert.IsType<ManualProviderAdapter>(manual);
        Assert.IsType<MockProviderAdapter>(mock);
    }

    [Fact]
    public void CreateAdapter_UsesGitHubCopilotMetricsAdapterWhenServicesExist()
    {
        var registry = new ProviderRegistry(
            secretResolver: new NullSecretResolver(),
            gitHubCopilotMetricsClient: new NullGitHubCopilotMetricsClient());
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);

        var adapter = registry.CreateAdapter(descriptor, new ProviderConfig
        {
            ProviderId = ProviderId.GitHubCopilot,
            SourceKind = DataSourceKind.OfficialApi
        });

        Assert.IsType<GitHubCopilotMetricsProviderAdapter>(adapter);
    }

    private sealed class NullSecretResolver : ISecretResolver
    {
        public Task<string?> ResolveSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class NullGitHubCopilotMetricsClient : IGitHubCopilotMetricsClient
    {
        public Task<GitHubCopilotMetricsFetchResult> FetchLatestReportAsync(
            GitHubCopilotMetricsRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GitHubCopilotMetricsFetchResult.Failure("not used"));
        }
    }
}
