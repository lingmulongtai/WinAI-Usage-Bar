using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Configuration;

public static class AppConfigMigrations
{
    public const int CurrentVersion = 1;

    public static AppConfig Migrate(AppConfig? config)
    {
        config ??= AppConfig.CreateDefault();
        config.Version = CurrentVersion;
        config.Providers ??= [];
        config.Refresh ??= new RefreshSettings();
        config.Widget ??= new WidgetSettings();
        config.Appearance ??= new AppearanceSettings();
        config.Notifications ??= new NotificationSettings();
        config.Startup ??= new StartupSettings();
        config.HistoryRetention ??= new HistoryRetentionSettings();
        config.Onboarding ??= new OnboardingSettings();
        config.Updates ??= new UpdateSettings();

        NormalizeProviders(config);
        NormalizeWidget(config.Widget);
        NormalizeAppearance(config.Appearance);
        NormalizeHistoryRetention(config.HistoryRetention);
        NormalizeUpdates(config.Updates);

        return config;
    }

    private static void NormalizeProviders(AppConfig config)
    {
        config.Providers = config.Providers
            .Where(provider => Enum.IsDefined(provider.ProviderId))
            .GroupBy(provider => provider.ProviderId)
            .Select(group => group.First())
            .ToList();

        foreach (var descriptor in ProviderDescriptors.All)
        {
            var provider = config.GetOrCreateProvider(descriptor);
            provider.Manual ??= ManualUsageSettings.CreateDefault(descriptor);
            provider.ApiKey ??= new ApiKeySettings();
            provider.Cli ??= new CliCommandSettings();
            provider.GitHubCopilot ??= new GitHubCopilotSettings();

            if (!descriptor.SupportedSources.Contains(provider.SourceKind))
            {
                provider.SourceKind = DataSourceKind.Manual;
            }

            provider.Manual.Currency = string.IsNullOrWhiteSpace(provider.Manual.Currency)
                ? "USD"
                : provider.Manual.Currency;
        }
    }

    private static void NormalizeWidget(WidgetSettings widget)
    {
        widget.Width = Math.Max(280, widget.Width);
        widget.Height = Math.Max(160, widget.Height);
        widget.ProviderIds ??= [];
        widget.ProviderIds = widget.ProviderIds
            .Where(providerId => Enum.IsDefined(providerId))
            .Distinct()
            .Take(3)
            .ToList();

        if (widget.ProviderIds.Count == 0)
        {
            widget.ProviderIds.Add(ProviderId.Codex);
            widget.ProviderIds.Add(ProviderId.ChatGPT);
        }
    }

    private static void NormalizeAppearance(AppearanceSettings appearance)
    {
        appearance.Theme = string.IsNullOrWhiteSpace(appearance.Theme)
            ? "System"
            : appearance.Theme;
    }

    private static void NormalizeHistoryRetention(HistoryRetentionSettings retention)
    {
        retention.MaxDays = Math.Clamp(
            retention.MaxDays,
            HistoryRetentionSettings.MinDays,
            HistoryRetentionSettings.MaxDaysLimit);
        retention.MaxBytes = Math.Clamp(
            retention.MaxBytes,
            HistoryRetentionSettings.MinBytes,
            HistoryRetentionSettings.MaxBytesLimit);
    }

    private static void NormalizeUpdates(UpdateSettings updates)
    {
        updates.MinimumCheckIntervalHours = Math.Clamp(
            updates.MinimumCheckIntervalHours,
            UpdateSettings.MinCheckIntervalHours,
            UpdateSettings.MaxCheckIntervalHours);
        if (!updates.DownloadAutomatically)
        {
            updates.InstallAutomatically = false;
        }

        updates.LastStatus = NullIfWhiteSpace(updates.LastStatus);
        updates.LastMessage = NullIfWhiteSpace(updates.LastMessage);
        updates.LastCurrentVersion = NullIfWhiteSpace(updates.LastCurrentVersion);
        updates.LastLatestVersion = NullIfWhiteSpace(updates.LastLatestVersion);
        updates.LastReleasePageUrl = NullIfWhiteSpace(updates.LastReleasePageUrl);
        updates.LastPackageAssetName = NullIfWhiteSpace(updates.LastPackageAssetName);
        updates.LastPackageChecksumAssetName = NullIfWhiteSpace(updates.LastPackageChecksumAssetName);
        updates.LastPackagePath = NullIfWhiteSpace(updates.LastPackagePath);
        updates.LastInstallerAssetName = NullIfWhiteSpace(updates.LastInstallerAssetName);
        updates.LastInstallerChecksumAssetName = NullIfWhiteSpace(updates.LastInstallerChecksumAssetName);
        updates.LastInstallScriptPath = NullIfWhiteSpace(updates.LastInstallScriptPath);
        updates.LastInstallResultPath = NullIfWhiteSpace(updates.LastInstallResultPath);
        updates.LastInstallResultStatus = NullIfWhiteSpace(updates.LastInstallResultStatus);
        updates.LastInstallResultMessage = NullIfWhiteSpace(updates.LastInstallResultMessage);
        updates.LastInstallLaunchedVersion = NullIfWhiteSpace(updates.LastInstallLaunchedVersion);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
