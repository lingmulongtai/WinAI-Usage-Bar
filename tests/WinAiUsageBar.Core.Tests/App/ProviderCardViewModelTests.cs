using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ProviderCardViewModelTests
{
    [Fact]
    public void StatusDisplay_ShowsTrimmedNonErrorStatusMessage()
    {
        var viewModel = new ProviderCardViewModel(Snapshot(
            statusMessage: "  Manual values updated  ",
            errorMessage: null));

        Assert.True(viewModel.HasStatusMessage);
        Assert.Equal("Manual values updated", viewModel.StatusText);
    }

    [Fact]
    public void StatusDisplay_DoesNotDuplicateErrorMessage()
    {
        var viewModel = new ProviderCardViewModel(Snapshot(
            statusMessage: "Auth required",
            errorMessage: "Auth required"));

        Assert.False(viewModel.HasStatusMessage);
        Assert.True(viewModel.HasError);
    }

    private static UsageSnapshot Snapshot(string? statusMessage, string? errorMessage)
    {
        return new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            errorMessage is null ? ProviderHealth.Ok : ProviderHealth.AuthRequired,
            Identity: null,
            PrimaryWindow: new UsageWindow("Test", 25, 75, null, "later", "%", null, null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            DateTimeOffset.Now,
            statusMessage,
            errorMessage);
    }
}
