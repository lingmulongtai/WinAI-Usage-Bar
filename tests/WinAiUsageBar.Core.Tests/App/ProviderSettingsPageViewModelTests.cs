using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ProviderSettingsPageViewModelTests
{
    [Fact]
    public void TryApply_DoesNotMutateConfigWhenManualInputIsInvalid()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.Manual.UsedPercent = 33;
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.UsedPercentText = "not a number";

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.StartsWith("Codex:", StringComparison.Ordinal));
        Assert.Equal(33, provider.Manual.UsedPercent);
    }

    [Fact]
    public void TryApply_NormalizesValidManualInputAndAppliesProviderState()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();

        editor.IsEnabled = false;
        editor.SourceKindText = DataSourceKind.Manual.ToString();
        editor.UsedPercentText = "120";
        editor.RemainingPercentText = "";
        editor.CreditBalanceText = "12.345";
        editor.MonthToDateCostText = "6.789";
        editor.NotesText = "  checked  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.False(provider.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, provider.SourceKind);
        Assert.Equal(100, provider.Manual.UsedPercent);
        Assert.Equal(12.35m, provider.Manual.CreditBalance);
        Assert.Equal(6.79m, provider.Manual.MonthToDateCost);
        Assert.Equal("checked", provider.Manual.Notes);
        Assert.Contains(result.Warnings, warning => warning.Contains("clamped", StringComparison.Ordinal));
    }

    [Fact]
    public void TryApply_RejectsUnsupportedSourceKind()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Source must be one of the supported values.", StringComparison.Ordinal));
        Assert.NotEqual(DataSourceKind.LocalAppServer, provider.SourceKind);
    }
}
