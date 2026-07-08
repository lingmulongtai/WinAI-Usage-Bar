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

    [Fact]
    public void StatusDisplay_RedactsStatusAndErrorText()
    {
        var secret = "gh" + "p_" + new string('a', 8);
        var viewModel = new ProviderCardViewModel(Snapshot(
            statusMessage: "status token=" + secret,
            errorMessage: "error authorization: bearer " + secret));

        Assert.Contains("[REDACTED]", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDisplay_DeduplicatesAfterRedaction()
    {
        var firstSecret = "gh" + "p_" + new string('a', 8);
        var secondSecret = "gh" + "p_" + new string('b', 8);
        var viewModel = new ProviderCardViewModel(Snapshot(
            statusMessage: "token=" + firstSecret,
            errorMessage: "token=" + secondSecret));

        Assert.Equal("[REDACTED]", viewModel.StatusText);
        Assert.Equal("[REDACTED]", viewModel.ErrorMessage);
        Assert.False(viewModel.HasStatusMessage);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public void TimestampDisplay_DoesNotWarnForRecentSnapshot()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderCardViewModel(
            Snapshot(
                statusMessage: "Manual values updated",
                errorMessage: null,
                updatedAt: now.AddMinutes(-5)),
            nowProvider: () => now);

        Assert.Equal("Updated 5m ago", viewModel.UpdatedText);
        Assert.False(viewModel.HasTimestampWarning);
        Assert.Empty(viewModel.TimestampWarningText);
    }

    [Fact]
    public void TimestampDisplay_WarnsForFutureSnapshot()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderCardViewModel(
            Snapshot(
                statusMessage: "Manual values updated",
                errorMessage: null,
                updatedAt: now.AddMinutes(4)),
            nowProvider: () => now);

        Assert.Equal("Updated in 4m (future timestamp)", viewModel.UpdatedText);
        Assert.True(viewModel.HasTimestampWarning);
        Assert.Contains("system clock", viewModel.TimestampWarningText, StringComparison.Ordinal);
    }

    [Fact]
    public void TimestampDisplay_WarnsForStaleSnapshot()
    {
        var now = new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new ProviderCardViewModel(
            Snapshot(
                statusMessage: "Manual values updated",
                errorMessage: null,
                updatedAt: now.AddMinutes(-45)),
            nowProvider: () => now);

        Assert.Equal("Updated 45m ago", viewModel.UpdatedText);
        Assert.True(viewModel.HasTimestampWarning);
        Assert.Contains("cached snapshot is stale", viewModel.TimestampWarningText, StringComparison.Ordinal);
    }

    private static UsageSnapshot Snapshot(
        string? statusMessage,
        string? errorMessage,
        DateTimeOffset? updatedAt = null)
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
            updatedAt ?? DateTimeOffset.Now,
            statusMessage,
            errorMessage);
    }
}
