using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Configuration;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    public List<ProviderConfig> Providers { get; set; } = [];

    public RefreshSettings Refresh { get; set; } = new();

    public WidgetSettings Widget { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public NotificationSettings Notifications { get; set; } = new();

    public StartupSettings Startup { get; set; } = new();

    public HistoryRetentionSettings HistoryRetention { get; set; } = new();

    public OnboardingSettings Onboarding { get; set; } = new();

    public UpdateSettings Updates { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        var config = new AppConfig();

        foreach (var descriptor in ProviderDescriptors.All)
        {
            config.Providers.Add(ProviderConfig.CreateDefault(descriptor));
        }

        return config;
    }

    public ProviderConfig GetOrCreateProvider(ProviderDescriptor descriptor)
    {
        var provider = Providers.FirstOrDefault(item => item.ProviderId == descriptor.Id);
        if (provider is not null)
        {
            return provider;
        }

        provider = ProviderConfig.CreateDefault(descriptor);
        Providers.Add(provider);
        return provider;
    }
}

public sealed class ProviderConfig
{
    public ProviderId ProviderId { get; set; }

    public bool IsEnabled { get; set; }

    public DataSourceKind SourceKind { get; set; } = DataSourceKind.Manual;

    public ManualUsageSettings Manual { get; set; } = new();

    public ApiKeySettings ApiKey { get; set; } = new();

    public GitHubCopilotSettings GitHubCopilot { get; set; } = new();

    public static ProviderConfig CreateDefault(ProviderDescriptor descriptor)
    {
        return new ProviderConfig
        {
            ProviderId = descriptor.Id,
            IsEnabled = descriptor.IsEnabledByDefault,
            SourceKind = descriptor.Id is ProviderId.Codex or ProviderId.ChatGPT
                ? DataSourceKind.Mock
                : DataSourceKind.Manual,
            Manual = ManualUsageSettings.CreateDefault(descriptor)
        };
    }
}

public sealed class ManualUsageSettings
{
    public double? UsedPercent { get; set; }

    public double? RemainingPercent { get; set; }

    public DateTimeOffset? ResetsAt { get; set; }

    public string? ResetDescription { get; set; }

    public decimal? CreditBalance { get; set; }

    public string? Currency { get; set; } = "USD";

    public decimal? MonthToDateCost { get; set; }

    public long? TokensLast31Days { get; set; }

    public string? Notes { get; set; }

    public static ManualUsageSettings CreateDefault(ProviderDescriptor descriptor)
    {
        return new ManualUsageSettings
        {
            UsedPercent = descriptor.Id == ProviderId.Codex ? 33 : null,
            RemainingPercent = descriptor.Id == ProviderId.Codex ? 67 : null,
            ResetDescription = "Manual mode",
            Notes = "Enter values manually when automatic usage is unavailable."
        };
    }
}

public sealed class ApiKeySettings
{
    public string? SecretName { get; set; }

    public string? LastConnectionStatus { get; set; }
}

public sealed class GitHubCopilotSettings
{
    public string? Organization { get; set; }

    public string? EnterpriseSlug { get; set; }

    public string? PatSecretName { get; set; }
}

public sealed class RefreshSettings
{
    public RefreshIntervalKind Interval { get; set; } = RefreshIntervalKind.FiveMinutes;
}

public enum RefreshIntervalKind
{
    Manual,
    OneMinute,
    TwoMinutes,
    FiveMinutes,
    FifteenMinutes
}

public sealed class WidgetSettings
{
    public bool ShowOnStartup { get; set; }

    public bool TopMost { get; set; }

    public double Left { get; set; } = 80;

    public double Top { get; set; } = 80;

    public double Width { get; set; } = 320;

    public double Height { get; set; } = 220;

    public List<ProviderId> ProviderIds { get; set; } = [ProviderId.Codex, ProviderId.ChatGPT];
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "System";
}

public sealed class NotificationSettings
{
    public bool IsEnabled { get; set; } = true;
}

public sealed class StartupSettings
{
    public bool LaunchOnLogin { get; set; }
}

public sealed class HistoryRetentionSettings
{
    public const int MinDays = 1;
    public const int MaxDaysLimit = 3650;
    public const long MinBytes = 100_000;
    public const long MaxBytesLimit = 500_000_000;

    public int MaxDays { get; set; } = 30;

    public long MaxBytes { get; set; } = 5_000_000;
}

public sealed class OnboardingSettings
{
    public bool HasCompletedFirstRun { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class UpdateSettings
{
    public const int MinCheckIntervalHours = 0;

    public const int MaxCheckIntervalHours = 168;

    public bool CheckOnStartup { get; set; } = true;

    public int MinimumCheckIntervalHours { get; set; } = 24;

    public bool DownloadAutomatically { get; set; }

    public bool InstallAutomatically { get; set; }

    public string? LastStatus { get; set; }

    public string? LastMessage { get; set; }

    public string? LastCurrentVersion { get; set; }

    public string? LastLatestVersion { get; set; }

    public string? LastPackagePath { get; set; }

    public string? LastInstallScriptPath { get; set; }

    public string? LastInstallLaunchedVersion { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
}
