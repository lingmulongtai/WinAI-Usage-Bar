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
        var openProviders = FirstRunProviderSetupAction.Navigate("Open Providers", "Providers");

        if (!provider.IsEnabled)
        {
            return Decision(
                descriptor,
                "Disabled",
                "Leave this disabled, or enable it in Providers if you want it on usage surfaces.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind, excludeCurrent: false)));
        }

        if (!descriptor.SupportedSources.Contains(provider.SourceKind))
        {
            return Decision(
                descriptor,
                "Needs attention",
                $"Choose a supported source mode: {string.Join(", ", descriptor.SupportedSources)}. Manual is the safest fallback.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind, excludeCurrent: false)));
        }

        return provider.SourceKind switch
        {
            DataSourceKind.Manual => Decision(
                descriptor,
                "Manual ready",
                "Manual fallback is ready. Enter usage values by hand, then switch to an automatic source later if one becomes reliable.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind))),
            DataSourceKind.Mock => Decision(
                descriptor,
                "Mock only",
                "Mock data is useful for UI checks only. Switch to Manual or a real source before relying on this provider.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind))),
            DataSourceKind.Cli => Decision(
                descriptor,
                "CLI setup",
                "CLI refresh needs a launchable provider command. Use a command override if PATH discovery is unreliable.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind))),
            DataSourceKind.LocalAppServer => Decision(
                descriptor,
                "Local app-server setup",
                "Local app-server refresh uses the signed-in local provider command and falls back to visible errors when startup or auth fails.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind))),
            DataSourceKind.OfficialApi when NeedsApiReferences(provider) => Decision(
                descriptor,
                "Needs API references",
                "Save the secret value in Privacy & Data first, then configure only non-secret references in Providers.",
                "Open Privacy & Data",
                "Privacy & Data",
                CreateActions(
                    FirstRunProviderSetupAction.Navigate("Open Privacy & Data", "Privacy & Data"),
                    CreateSafeSourceActions(descriptor, provider.SourceKind))),
            DataSourceKind.OfficialApi => Decision(
                descriptor,
                "API references ready",
                "API mode has the required non-secret references configured. Refresh can test the provider without exposing values.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind))),
            _ => Decision(
                descriptor,
                "Needs attention",
                "Choose Manual mode until this source has explicit setup guidance.",
                "Open Providers",
                "Providers",
                CreateActions(openProviders, CreateSafeSourceActions(descriptor, provider.SourceKind)))
        };
    }

    public FirstRunSetupActionResult ApplyAction(FirstRunProviderSetupAction action)
    {
        if (action.Kind is FirstRunSetupActionKind.Navigate)
        {
            return new FirstRunSetupActionResult(
                Applied: false,
                ShouldSave: false,
                NavigationTag: action.NavigationTag,
                Message: "Open the requested setup page.");
        }

        if (action.Kind is not FirstRunSetupActionKind.ApplyProviderSource
            || action.ProviderId is not { } providerId
            || action.SourceKind is not { } sourceKind)
        {
            return new FirstRunSetupActionResult(
                Applied: false,
                ShouldSave: false,
                NavigationTag: "Providers",
                Message: "This setup action is not available.");
        }

        if (!IsSafeInlineSource(sourceKind))
        {
            var navigationTag = sourceKind is DataSourceKind.OfficialApi ? "Privacy & Data" : "Providers";
            return new FirstRunSetupActionResult(
                Applied: false,
                ShouldSave: false,
                NavigationTag: navigationTag,
                Message: "Open Providers or Privacy & Data for this setup choice.");
        }

        var descriptor = descriptors.FirstOrDefault(descriptor => descriptor.Id == providerId);
        if (descriptor is null || !descriptor.SupportedSources.Contains(sourceKind))
        {
            return new FirstRunSetupActionResult(
                Applied: false,
                ShouldSave: false,
                NavigationTag: "Providers",
                Message: "The selected provider does not support this source mode.");
        }

        var provider = config.GetOrCreateProvider(descriptor);
        provider.IsEnabled = true;
        provider.SourceKind = sourceKind;

        return new FirstRunSetupActionResult(
            Applied: true,
            ShouldSave: true,
            NavigationTag: null,
            Message: $"{descriptor.DisplayName} set to {sourceKind}.");
    }

    private static IReadOnlyList<FirstRunProviderSetupAction> CreateActions(
        FirstRunProviderSetupAction primary,
        IReadOnlyList<FirstRunProviderSetupAction> inlineActions)
    {
        return [primary, .. inlineActions];
    }

    private static IReadOnlyList<FirstRunProviderSetupAction> CreateSafeSourceActions(
        ProviderDescriptor descriptor,
        DataSourceKind currentSource,
        bool excludeCurrent = true)
    {
        return descriptor.SupportedSources
            .Where(IsSafeInlineSource)
            .Where(source => !excludeCurrent || source != currentSource)
            .Select(source => FirstRunProviderSetupAction.ApplyProviderSource(
                ButtonTextForSource(source),
                descriptor.Id,
                source))
            .ToList();
    }

    private static bool IsSafeInlineSource(DataSourceKind sourceKind)
    {
        return sourceKind is DataSourceKind.Manual or DataSourceKind.Mock or DataSourceKind.LocalAppServer;
    }

    private static string ButtonTextForSource(DataSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DataSourceKind.Manual => "Use Manual",
            DataSourceKind.Mock => "Use Mock",
            DataSourceKind.LocalAppServer => "Use Local App Server",
            _ => $"Use {sourceKind}"
        };
    }

    private static FirstRunProviderSetupDecision Decision(
        ProviderDescriptor descriptor,
        string stateText,
        string recommendationText,
        string actionButtonText,
        string actionNavigationTag,
        IReadOnlyList<FirstRunProviderSetupAction> actions)
    {
        return new FirstRunProviderSetupDecision(
            descriptor.DisplayName,
            stateText,
            recommendationText,
            actionButtonText,
            actionNavigationTag,
            actions);
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
    string ActionNavigationTag,
    IReadOnlyList<FirstRunProviderSetupAction> Actions);

public enum FirstRunSetupActionKind
{
    Navigate,
    ApplyProviderSource
}

public sealed record FirstRunProviderSetupAction(
    string ButtonText,
    FirstRunSetupActionKind Kind,
    string? NavigationTag,
    ProviderId? ProviderId,
    DataSourceKind? SourceKind)
{
    public static FirstRunProviderSetupAction Navigate(string buttonText, string navigationTag)
    {
        return new FirstRunProviderSetupAction(
            buttonText,
            FirstRunSetupActionKind.Navigate,
            navigationTag,
            ProviderId: null,
            SourceKind: null);
    }

    public static FirstRunProviderSetupAction ApplyProviderSource(
        string buttonText,
        ProviderId providerId,
        DataSourceKind sourceKind)
    {
        return new FirstRunProviderSetupAction(
            buttonText,
            FirstRunSetupActionKind.ApplyProviderSource,
            NavigationTag: null,
            providerId,
            sourceKind);
    }
}

public sealed record FirstRunSetupActionResult(
    bool Applied,
    bool ShouldSave,
    string? NavigationTag,
    string Message);
