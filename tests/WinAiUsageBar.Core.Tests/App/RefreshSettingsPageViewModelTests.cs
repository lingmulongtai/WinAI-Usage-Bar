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
            HistoryMaxBytesText = "huge",
            UpdateCheckIntervalHoursText = "often"
        };

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("History max days", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("History max bytes", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Startup update interval", StringComparison.Ordinal));
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
            UpdateCheckIntervalHoursText = "999",
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
        Assert.Equal(UpdateSettings.MaxCheckIntervalHours, config.Updates.MinimumCheckIntervalHours);
        Assert.True(config.Updates.DownloadAutomatically);
        Assert.False(config.Updates.InstallAutomatically);
        Assert.Equal(3, result.Warnings.Count);
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
        config.Updates.LastCurrentVersion = "0.1.9";
        config.Updates.LastLatestVersion = "0.2.0";
        config.Updates.LastMessage = "Update package downloaded and verified.";
        config.Updates.LastInstallLaunchedVersion = "0.1.9";
        config.Updates.LastPackagePath = @"C:\Users\test\AppData\Roaming\WinAiUsageBar\updates\WinAIUsageBar-0.2.0-win-x64.zip";
        config.Updates.LastInstallScriptPath = @"C:\Users\test\AppData\Roaming\WinAiUsageBar\updates\install-1\apply-update.ps1";
        config.Updates.LastInstallResultPath = @"C:\Users\test\AppData\Roaming\WinAiUsageBar\updates\install-1\install-result.json";
        config.Updates.LastInstallResultStatus = "Succeeded";
        config.Updates.LastInstallResultMessage = "Update installed successfully.";
        config.Updates.LastInstallResultCompletedAt = new DateTimeOffset(2026, 7, 8, 9, 35, 0, TimeSpan.FromHours(9));
        config.Updates.LastCheckedAt = new DateTimeOffset(2026, 7, 8, 9, 30, 0, TimeSpan.FromHours(9));

        var viewModel = new RefreshSettingsPageViewModel(config);

        Assert.Contains("Downloaded", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Current version: 0.1.9", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("0.2.0", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("0.1.9", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Package path:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("WinAIUsageBar-0.2.0-win-x64.zip", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Install script:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("apply-update.ps1", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Install result:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("install-result.json", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Install result status: Succeeded", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Install result completed: 2026-07-08 09:35:00 +09:00", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Install result message: Update installed successfully.", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Update package downloaded", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("2026-07-08", viewModel.UpdateStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RedactsSensitiveStartupUpdateStatus()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.LastStatus = "Downloaded token=sk-secret-value";
        config.Updates.LastCurrentVersion = "0.1.9 access_token=current-secret";
        config.Updates.LastLatestVersion = "0.2.0 cookie=session-secret";
        config.Updates.LastMessage = "Authorization: Bearer bearer-secret and ghp_1234567890";
        config.Updates.LastInstallLaunchedVersion = "0.2.0 patSecretName=install-secret";
        config.Updates.LastPackagePath = @"C:\Updates\token=sk-secret-value\WinAIUsageBar.zip";
        config.Updates.LastInstallScriptPath = @"C:\Updates\secret=script-secret\apply-update.ps1";
        config.Updates.LastInstallResultPath = @"C:\Updates\secret=result-secret\install-result.json";
        config.Updates.LastInstallResultStatus = "Failed token=result-status-secret";
        config.Updates.LastInstallResultMessage = "Failed with cookie=result-message-secret";

        var viewModel = new RefreshSettingsPageViewModel(config);

        Assert.Contains("[REDACTED]", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret-value", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("current-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("session-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_1234567890", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("install-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("script-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("result-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("result-status-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("result-message-secret", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("token", viewModel.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", viewModel.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", viewModel.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", viewModel.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_OmitsMissingUpdateArtifactPaths()
    {
        var config = AppConfig.CreateDefault();
        config.Updates.LastStatus = "NoUpdate";
        config.Updates.LastMessage = "The current app version is up to date.";
        config.Updates.LastLatestVersion = "0.1.4";

        var viewModel = new RefreshSettingsPageViewModel(config);

        Assert.Contains("Status: NoUpdate", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Current version:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Package path:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Install script:", viewModel.UpdateStatusText, StringComparison.Ordinal);
        Assert.Contains("Message: The current app version is up to date.", viewModel.UpdateStatusText, StringComparison.Ordinal);
    }
}
