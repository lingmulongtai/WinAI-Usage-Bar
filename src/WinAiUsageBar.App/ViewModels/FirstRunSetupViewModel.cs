using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.ViewModels;

public sealed class FirstRunSetupViewModel(
    AppConfig config,
    IReadOnlyList<ProviderDescriptor> descriptors,
    Func<DateTimeOffset>? nowProvider = null)
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public bool IsVisible => !config.Onboarding.HasCompletedFirstRun;

    public string SummaryText
    {
        get
        {
            var enabled = config.Providers.Count(provider => provider.IsEnabled);
            var automatic = config.Providers.Count(provider =>
                provider.IsEnabled && provider.SourceKind != DataSourceKind.Manual);
            var manual = config.Providers.Count(provider =>
                provider.IsEnabled && provider.SourceKind == DataSourceKind.Manual);
            return $"{enabled} provider(s) enabled / {automatic} automatic / {manual} manual";
        }
    }

    public IReadOnlyList<string> ProviderLines
    {
        get
        {
            return descriptors
                .Select(descriptor =>
                {
                    var provider = config.Providers.FirstOrDefault(provider => provider.ProviderId == descriptor.Id);
                    var isEnabled = provider?.IsEnabled ?? descriptor.IsEnabledByDefault;
                    var sourceKind = provider?.SourceKind ?? DataSourceKind.Manual;
                    var state = isEnabled ? "enabled" : "disabled";
                    return $"{descriptor.DisplayName}: {state}, {sourceKind}";
                })
                .ToList();
        }
    }

    public void MarkComplete()
    {
        config.Onboarding.HasCompletedFirstRun = true;
        config.Onboarding.CompletedAt = nowProvider();
    }
}
