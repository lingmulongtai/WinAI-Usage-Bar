using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
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
}
