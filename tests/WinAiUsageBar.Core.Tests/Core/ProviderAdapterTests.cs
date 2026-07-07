using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Core.Providers.Manual;
using WinAiUsageBar.Core.Providers.Mock;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class ProviderAdapterTests
{
    [Fact]
    public async Task ManualProviderAdapter_ReturnsManualSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var adapter = new ManualProviderAdapter(descriptor);
        var context = new ProviderFetchContext(
            new ProviderConfig
            {
                ProviderId = ProviderId.Gemini,
                SourceKind = DataSourceKind.Manual,
                Manual = new ManualUsageSettings
                {
                    RemainingPercent = 44,
                    Notes = "Gemini manual"
                }
            },
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.Manual, result.Snapshot?.SourceKind);
        Assert.Equal(44, result.Snapshot?.PrimaryWindow?.RemainingPercent);
    }

    [Fact]
    public async Task MockProviderAdapter_ReturnsRealisticSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new MockProviderAdapter(descriptor);
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            new DateTimeOffset(2026, 7, 8, 0, 5, 0, TimeSpan.Zero),
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.Mock, result.Snapshot?.SourceKind);
        Assert.InRange(result.Snapshot?.PrimaryWindow?.RemainingPercent ?? -1, 0, 100);
    }
}
