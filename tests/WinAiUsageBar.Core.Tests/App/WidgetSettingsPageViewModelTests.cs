using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class WidgetSettingsPageViewModelTests
{
    [Fact]
    public void TryApply_RequiresAtLeastOneProvider()
    {
        var settings = new WidgetSettings();
        var viewModel = new WidgetSettingsPageViewModel(settings, ProviderDescriptors.All);
        foreach (var option in viewModel.ProviderOptions)
        {
            option.IsSelected = false;
        }

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("at least one", StringComparison.Ordinal));
    }

    [Fact]
    public void TryApply_RejectsMoreThanThreeProviders()
    {
        var settings = new WidgetSettings();
        var viewModel = new WidgetSettingsPageViewModel(settings, ProviderDescriptors.All);
        foreach (var option in viewModel.ProviderOptions.Take(4))
        {
            option.IsSelected = true;
        }

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("no more than three", StringComparison.Ordinal));
    }

    [Fact]
    public void TryApply_AppliesWidgetSettings()
    {
        var settings = new WidgetSettings
        {
            ProviderIds = []
        };
        var viewModel = new WidgetSettingsPageViewModel(settings, ProviderDescriptors.All);
        viewModel.ShowOnStartup = true;
        viewModel.TopMost = true;
        foreach (var option in viewModel.ProviderOptions)
        {
            option.IsSelected = option.ProviderId is ProviderId.Codex or ProviderId.Gemini;
        }

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.True(settings.ShowOnStartup);
        Assert.True(settings.TopMost);
        Assert.Equal([ProviderId.Codex, ProviderId.Gemini], settings.ProviderIds);
    }
}
