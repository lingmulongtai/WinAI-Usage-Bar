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
            HistoryMaxBytesText = "1",
            CheckUpdatesOnStartup = false,
            DownloadUpdatesAutomatically = true,
            InstallUpdatesAutomatically = false
        };

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(RefreshIntervalKind.OneMinute, config.Refresh.Interval);
        Assert.False(config.Notifications.IsEnabled);
        Assert.Equal(HistoryRetentionSettings.MaxDaysLimit, config.HistoryRetention.MaxDays);
        Assert.Equal(HistoryRetentionSettings.MinBytes, config.HistoryRetention.MaxBytes);
        Assert.False(config.Updates.CheckOnStartup);
        Assert.True(config.Updates.DownloadAutomatically);
        Assert.False(config.Updates.InstallAutomatically);
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

    [Fact]
    public void TryApply_RejectsAutomaticInstallWithoutAutomaticDownload()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new RefreshSettingsPageViewModel(config)
        {
            DownloadUpdatesAutomatically = false,
            InstallUpdatesAutomatically = true
        };

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("download", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Constructor_FormatsLastStartupUpdateStatus()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.LastStatus = "Downloaded";
        config.Updates.LastLatestVersion = "0.2.0";
        config.Updates.LastMessage = "Update package downloaded and verified.";
        config.Updates.LastCheckedAt = new DateTimeOffset(2026, 7, 8, 9, 30, 0, TimeSpan.FromHours(9));

        var viewModel = new RefreshSettingsPageViewModel(config);

        Assert.Contains("Downloaded", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("0.2.0", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Update package downloaded", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("2026-07-08", viewModel.UpdateStatusText, StringComparison.Ordinal);
    }
}
