using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineActionsTests
{
    [Fact]
    public async Task RefreshOnceAsync_AppliesProviderSourceOverrideWithoutSavingConfig()
    {
        var paths = TestPaths();
        var configStore = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        codex.IsEnabled = false;
        codex.SourceKind = DataSourceKind.Manual;
        var chatGpt = config.Providers.Single(provider => provider.ProviderId == ProviderId.ChatGPT);
        chatGpt.IsEnabled = true;
        chatGpt.SourceKind = DataSourceKind.Mock;
        await configStore.SaveAsync(config, CancellationToken.None);

        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Codex", "Mock"),
            CancellationToken.None,
            paths);

        var reloaded = await configStore.LoadAsync(CancellationToken.None);
        var reloadedCodex = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        var reloadedChatGpt = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.ChatGPT);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Snapshots: 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("Codex", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("ChatGPT", result.Output, StringComparison.Ordinal);
        Assert.False(reloadedCodex.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, reloadedCodex.SourceKind);
        Assert.True(reloadedChatGpt.IsEnabled);
        Assert.Equal(DataSourceKind.Mock, reloadedChatGpt.SourceKind);
    }

    [Fact]
    public async Task RefreshOnceAsync_ReturnsErrorForUnknownProvider()
    {
        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Nope", "Mock"),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown provider", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshOnceAsync_ReturnsErrorForUnsupportedProviderSource()
    {
        var result = await CommandLineActions.RefreshOnceAsync(
            new CommandLineRefreshOnceOptions("Gemini", "LocalAppServer"),
            CancellationToken.None,
            TestPaths());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("does not support source", result.Output, StringComparison.Ordinal);
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }
}
