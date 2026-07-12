using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class WidgetPlacementStoreTests
{
    [Fact]
    public async Task SaveBoundsAsync_PreservesConfiguredTopMost()
    {
        var config = new AppConfig
        {
            Widget = new WidgetSettings
            {
                TopMost = true,
                Left = 80,
                Top = 80,
                Width = 320,
                Height = 220
            }
        };
        var store = new InMemoryConfigStore(config);
        var placementStore = new WidgetPlacementStore(store);

        await placementStore.SaveBoundsAsync(
            new WindowPlacement(10, 20, 400, 240, TopMost: false),
            CancellationToken.None);

        Assert.True(store.Config.Widget.TopMost);
        Assert.Equal(10, store.Config.Widget.Left);
        Assert.Equal(20, store.Config.Widget.Top);
        Assert.Equal(400, store.Config.Widget.Width);
        Assert.Equal(240, store.Config.Widget.Height);
    }

    [Fact]
    public async Task SaveBoundsAsync_ClampsMinimumSize()
    {
        var store = new InMemoryConfigStore(new AppConfig());
        var placementStore = new WidgetPlacementStore(store);

        await placementStore.SaveBoundsAsync(
            new WindowPlacement(10, 20, 40, 50, TopMost: false),
            CancellationToken.None);

        Assert.Equal(280, store.Config.Widget.Width);
        Assert.Equal(160, store.Config.Widget.Height);
    }

    [Fact]
    public async Task SaveAsync_UpdatesTopMost()
    {
        var store = new InMemoryConfigStore(new AppConfig());
        var placementStore = new WidgetPlacementStore(store);

        await placementStore.SaveAsync(
            new WindowPlacement(10, 20, 400, 240, TopMost: true),
            CancellationToken.None);

        Assert.True(store.Config.Widget.TopMost);
    }

    private sealed class InMemoryConfigStore(AppConfig config) : IAppConfigStore
    {
        public AppConfig Config { get; private set; } = config;

        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Config);
        }

        public Task SaveAsync(AppConfig config, CancellationToken cancellationToken)
        {
            Config = config;
            return Task.CompletedTask;
        }
    }
}
