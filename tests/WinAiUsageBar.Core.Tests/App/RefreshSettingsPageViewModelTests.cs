using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class RefreshSettingsPageViewModelTests
{
    [Fact]
    public void TryApply_RejectsInvalidNumbers()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new RefreshSettingsPageViewModel(config)
        {
            HistoryMaxDaysText = "many",
            HistoryMaxBytesText = "huge"
        };

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("History max days", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("History max bytes", StringComparison.Ordinal));
    }

    [Fact]
    public void TryApply_ClampsRetentionAndAppliesSettings()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new RefreshSettingsPageViewModel(config)
        {
            IntervalText = RefreshIntervalKind.OneMinute.ToString(),
            NotificationsEnabled = false,
            HistoryMaxDaysText = "99999",
            HistoryMaxBytesText = "1"
        };

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(RefreshIntervalKind.OneMinute, config.Refresh.Interval);
        Assert.False(config.Notifications.IsEnabled);
        Assert.Equal(HistoryRetentionSettings.MaxDaysLimit, config.HistoryRetention.MaxDays);
        Assert.Equal(HistoryRetentionSettings.MinBytes, config.HistoryRetention.MaxBytes);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void TryApply_RejectsInvalidInterval()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new RefreshSettingsPageViewModel(config)
        {
            IntervalText = "Soon"
        };

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("interval", StringComparison.OrdinalIgnoreCase));
    }
}
