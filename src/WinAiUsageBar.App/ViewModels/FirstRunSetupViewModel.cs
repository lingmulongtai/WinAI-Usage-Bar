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

    public IReadOnlyList<FirstRunProviderSetupDecision> ProviderSetupDecisions
    {
        get
        {
            return descriptors
                .Select(CreateProviderSetupDecision)
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

    private FirstRunProviderSetupDecision CreateProviderSetupDecision(ProviderDescriptor descriptor)
    {
        var provider = config.Providers.FirstOrDefault(provider => provider.ProviderId == descriptor.Id)
            ?? ProviderConfig.CreateDefault(descriptor);

        if (!provider.IsEnabled)
        {
            return Decision(
                descriptor,
                "Disabled",
                "Leave this disabled, or enable it in Providers if you want it on usage surfaces.",
                "Open Providers",
                "Providers");
        }

        if (!descriptor.SupportedSources.Contains(provider.SourceKind))
        {
            return Decision(
                descriptor,
                "Needs attention",
                $"Choose a supported source mode: {string.Join(", ", descriptor.SupportedSources)}. Manual is the safest fallback.",
                "Open Providers",
                "Providers");
        }

        return provider.SourceKind switch
        {
            DataSourceKind.Manual => Decision(
                descriptor,
                "Manual ready",
                "Manual fallback is ready. Enter usage values by hand, then switch to an automatic source later if one becomes reliable.",
                "Open Providers",
                "Providers"),
            DataSourceKind.Mock => Decision(
                descriptor,
                "Mock only",
                "Mock data is useful for UI checks only. Switch to Manual or a real source before relying on this provider.",
                "Open Providers",
                "Providers"),
            DataSourceKind.Cli => Decision(
                descriptor,
                "CLI setup",
                "CLI refresh needs a launchable provider command. Use a command override if PATH discovery is unreliable.",
                "Open Providers",
                "Providers"),
            DataSourceKind.LocalAppServer => Decision(
                descriptor,
                "Local app-server setup",
                "Local app-server refresh uses the signed-in local provider command and falls back to visible errors when startup or auth fails.",
                "Open Providers",
                "Providers"),
            DataSourceKind.OfficialApi when NeedsApiReferences(provider) => Decision(
                descriptor,
                "Needs API references",
                "Save the secret value in Privacy & Data first, then configure only non-secret references in Providers.",
                "Open Privacy & Data",
                "Privacy & Data"),
            DataSourceKind.OfficialApi => Decision(
                descriptor,
                "API references ready",
                "API mode has the required non-secret references configured. Refresh can test the provider without exposing values.",
                "Open Providers",
                "Providers"),
            _ => Decision(
                descriptor,
                "Needs attention",
                "Choose Manual mode until this source has explicit setup guidance.",
                "Open Providers",
                "Providers")
        };
    }

    private static FirstRunProviderSetupDecision Decision(
        ProviderDescriptor descriptor,
        string stateText,
        string recommendationText,
        string actionButtonText,
        string actionNavigationTag)
    {
        return new FirstRunProviderSetupDecision(
            descriptor.DisplayName,
            stateText,
            recommendationText,
            actionButtonText,
            actionNavigationTag);
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

public sealed record FirstRunProviderSetupDecision(
    string ProviderName,
    string StateText,
    string RecommendationText,
    string ActionButtonText,
    string ActionNavigationTag);
