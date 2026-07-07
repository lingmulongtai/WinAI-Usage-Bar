using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class FirstRunSetupViewModelTests
{
    [Fact]
    public void IsVisible_ReturnsTrueUntilFirstRunIsComplete()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new FirstRunSetupViewModel(config, ProviderDescriptors.All);

        Assert.True(viewModel.IsVisible);

        config.Onboarding.HasCompletedFirstRun = true;
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void ProviderLines_ShowSourceStateWithoutSecretNames()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.OfficialApi;
        gemini.ApiKey.SecretName = "gemini-api-key";

        var viewModel = new FirstRunSetupViewModel(config, [ProviderDescriptors.Get(ProviderId.Gemini)]);

        var line = Assert.Single(viewModel.ProviderLines);
        Assert.Contains("Gemini: enabled, OfficialApi", line, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini-api-key", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkComplete_RecordsCompletionState()
    {
        var now = new DateTimeOffset(2026, 7, 8, 13, 0, 0, TimeSpan.Zero);
        var config = AppConfig.CreateDefault();
        var viewModel = new FirstRunSetupViewModel(
            config,
            ProviderDescriptors.All,
            nowProvider: () => now);

        viewModel.MarkComplete();

        Assert.True(config.Onboarding.HasCompletedFirstRun);
        Assert.Equal(now, config.Onboarding.CompletedAt);
    }
}
