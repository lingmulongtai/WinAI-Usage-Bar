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

    public IReadOnlyList<FirstRunSetupChecklistItem> ChecklistItems
    {
        get
        {
            var enabled = GetEnabledProviders().ToList();
            var unsupportedSources = enabled.Count(provider =>
            {
                var descriptor = descriptors.FirstOrDefault(descriptor => descriptor.Id == provider.ProviderId);
                return descriptor is null || !descriptor.SupportedSources.Contains(provider.SourceKind);
            });
            var apiProviders = enabled.Count(provider => provider.SourceKind == DataSourceKind.OfficialApi);
            var missingApiReferences = enabled.Count(NeedsApiReferences);

            return
            [
                new FirstRunSetupChecklistItem(
                    "Choose providers",
                    enabled.Count > 0,
                    enabled.Count > 0
                        ? $"{enabled.Count} provider(s) enabled."
                        : "No providers are enabled yet.",
                    "Open Providers and enable the AI services you want to track.",
                    "Open Providers",
                    "Providers"),
                new FirstRunSetupChecklistItem(
                    "Choose supported source modes",
                    enabled.Count > 0 && unsupportedSources == 0,
                    unsupportedSources == 0
                        ? "Enabled providers use supported source modes."
                        : $"{unsupportedSources} enabled provider(s) need a supported source mode.",
                    "Use Manual mode when an automatic source is not ready.",
                    "Open Providers",
                    "Providers"),
                new FirstRunSetupChecklistItem(
                    "Prepare API references",
                    missingApiReferences == 0,
                    apiProviders == 0
                        ? "No API-backed providers are enabled."
                        : missingApiReferences == 0
                            ? "API-backed providers have the required non-secret references."
                            : $"{missingApiReferences} API-backed provider(s) need credential or scope references.",
                    "Save secret values from Privacy & Data, then store only secret names in provider settings.",
                    missingApiReferences == 0 ? "Open Providers" : "Open Privacy & Data",
                    missingApiReferences == 0 ? "Providers" : "Privacy & Data")
            ];
        }
    }

    public void MarkComplete()
    {
        config.Onboarding.HasCompletedFirstRun = true;
        config.Onboarding.CompletedAt = nowProvider();
    }

    private IEnumerable<ProviderConfig> GetEnabledProviders()
    {
        return descriptors
            .Select(descriptor =>
                config.Providers.FirstOrDefault(provider => provider.ProviderId == descriptor.Id)
                ?? ProviderConfig.CreateDefault(descriptor))
            .Where(provider => provider.IsEnabled);
    }

    private static bool NeedsApiReferences(ProviderConfig provider)
    {
        if (provider.SourceKind != DataSourceKind.OfficialApi)
        {
            return false;
        }

        if (provider.ProviderId == ProviderId.GitHubCopilot)
        {
            var hasScope = !string.IsNullOrWhiteSpace(provider.GitHubCopilot.Organization)
                || !string.IsNullOrWhiteSpace(provider.GitHubCopilot.EnterpriseSlug);
            return !hasScope || string.IsNullOrWhiteSpace(provider.GitHubCopilot.PatSecretName);
        }

        return string.IsNullOrWhiteSpace(provider.ApiKey.SecretName);
    }
}

public sealed record FirstRunSetupChecklistItem(
    string Title,
    bool IsComplete,
    string StateText,
    string ActionText,
    string ActionButtonText,
    string ActionNavigationTag);
