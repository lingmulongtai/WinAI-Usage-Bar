using WinAiUsageBar.App.ViewModels;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class SecretEditorViewModelTests
{
    [Fact]
    public void ValidateSave_RequiresNameAndValue()
    {
        var viewModel = new SecretEditorViewModel
        {
            SecretNameText = " ",
            SecretValueText = ""
        };

        var result = viewModel.ValidateSave();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSave_TrimsNameAndPreservesValue()
    {
        var viewModel = new SecretEditorViewModel
        {
            SecretNameText = "  gemini-api-key  ",
            SecretValueText = "  secret-value  "
        };

        var result = viewModel.ValidateSave();

        Assert.True(result.IsValid);
        Assert.Equal("gemini-api-key", result.SecretName);
        Assert.Equal("  secret-value  ", result.SecretValue);
    }

    [Fact]
    public void ValidateDelete_RequiresOnlyName()
    {
        var viewModel = new SecretEditorViewModel
        {
            SecretNameText = " github-copilot-pat ",
            SecretValueText = ""
        };

        var result = viewModel.ValidateDelete();

        Assert.True(result.IsValid);
        Assert.Equal("github-copilot-pat", result.SecretName);
        Assert.Null(result.SecretValue);
    }
}
