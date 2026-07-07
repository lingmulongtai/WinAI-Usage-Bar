using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class JsonConfigStoreTests
{
    [Fact]
    public async Task JsonAppConfigStore_LoadSave_RoundTripsProviderSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var store = new JsonAppConfigStore(paths);

        var config = await store.LoadAsync(CancellationToken.None);
        var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        codex.IsEnabled = true;
        codex.SourceKind = DataSourceKind.Manual;
        codex.Manual.RemainingPercent = 52;

        await store.SaveAsync(config, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);
        var reloadedCodex = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);

        Assert.True(reloadedCodex.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, reloadedCodex.SourceKind);
        Assert.Equal(52, reloadedCodex.Manual.RemainingPercent);

        Directory.Delete(root, recursive: true);
    }
}
