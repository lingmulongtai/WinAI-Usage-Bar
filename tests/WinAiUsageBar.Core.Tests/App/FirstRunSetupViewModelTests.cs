using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class FirstRunSetupViewModelTests
{
    [Fact]
    public void IsVisible_ReturnsTrueUntilFirstRunIsComplete()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new FirstRunSetupViewModel(config, ProviderDescriptors.All);

        Assert.True(viewModel.IsVisible);

        config.Onboarding.HasCompletedFirstRun = true;
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void ProviderLines_ShowSourceStateWithoutSecretNames()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.OfficialApi;
        gemini.ApiKey.SecretName = "gemini-api-key";

        var viewModel = new FirstRunSetupViewModel(config, [ProviderDescriptors.Get(ProviderId.Gemini)]);

        var line = Assert.Single(viewModel.ProviderLines);
        Assert.Contains("Gemini: enabled, OfficialApi", line, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini-api-key", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecklistItems_ShowDefaultSetupProgress()
    {
        var config = AppConfig.CreateDefault();
        var viewModel = new FirstRunSetupViewModel(config, ProviderDescriptors.All);

        var chooseProviders = viewModel.ChecklistItems.Single(item => item.Title == "Choose providers");
        var sourceModes = viewModel.ChecklistItems.Single(item => item.Title == "Choose supported source modes");
        var apiReferences = viewModel.ChecklistItems.Single(item => item.Title == "Prepare API references");

        Assert.True(chooseProviders.IsComplete);
        Assert.True(sourceModes.IsComplete);
        Assert.True(apiReferences.IsComplete);
        Assert.Contains("2 provider", chooseProviders.StateText, StringComparison.Ordinal);
        Assert.Contains("No API-backed providers", apiReferences.StateText, StringComparison.Ordinal);
        Assert.Equal("Open Providers", chooseProviders.ActionButtonText);
        Assert.Equal("Providers", chooseProviders.ActionNavigationTag);
        Assert.Equal("Providers", sourceModes.ActionNavigationTag);
        Assert.Equal("Providers", apiReferences.ActionNavigationTag);
    }

    [Fact]
    public void ChecklistItems_FlagUnsupportedSourceModes()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.LocalAppServer;
        var viewModel = new FirstRunSetupViewModel(config, [ProviderDescriptors.Get(ProviderId.Gemini)]);

        var sourceModes = viewModel.ChecklistItems.Single(item => item.Title == "Choose supported source modes");

        Assert.False(sourceModes.IsComplete);
        Assert.Contains("1 enabled provider", sourceModes.StateText, StringComparison.Ordinal);
        Assert.Contains("Manual mode", sourceModes.ActionText, StringComparison.Ordinal);
        Assert.Equal("Open Providers", sourceModes.ActionButtonText);
        Assert.Equal("Providers", sourceModes.ActionNavigationTag);
    }

    [Fact]
    public void ChecklistItems_FlagApiProvidersMissingCredentialReferencesWithoutLeakingNames()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.OfficialApi;
        gemini.ApiKey.SecretName = "gemini-api-key";
        var copilot = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.GitHubCopilot));
        copilot.IsEnabled = true;
        copilot.SourceKind = DataSourceKind.OfficialApi;
        copilot.GitHubCopilot.Organization = "my-org";

        var viewModel = new FirstRunSetupViewModel(config, ProviderDescriptors.All);

        var apiReferences = viewModel.ChecklistItems.Single(item => item.Title == "Prepare API references");
        var checklistText = string.Join(
            Environment.NewLine,
            viewModel.ChecklistItems.SelectMany(item => new[] { item.Title, item.StateText, item.ActionText }));

        Assert.False(apiReferences.IsComplete);
        Assert.Contains("1 API-backed provider", apiReferences.StateText, StringComparison.Ordinal);
        Assert.Equal("Open Privacy & Data", apiReferences.ActionButtonText);
        Assert.Equal("Privacy & Data", apiReferences.ActionNavigationTag);
        Assert.DoesNotContain("gemini-api-key", checklistText, StringComparison.Ordinal);
        Assert.DoesNotContain("my-org", checklistText, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecklistItems_CompleteApiReferencesWithoutDisplayingSecretReferences()
    {
        var config = AppConfig.CreateDefault();
        var copilot = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.GitHubCopilot));
        copilot.IsEnabled = true;
        copilot.SourceKind = DataSourceKind.OfficialApi;
        copilot.GitHubCopilot.EnterpriseSlug = "my-enterprise";
        copilot.GitHubCopilot.PatSecretName = "github-copilot-pat";

        var viewModel = new FirstRunSetupViewModel(config, [ProviderDescriptors.Get(ProviderId.GitHubCopilot)]);

        var apiReferences = viewModel.ChecklistItems.Single(item => item.Title == "Prepare API references");
        var checklistText = string.Join(
            Environment.NewLine,
            viewModel.ChecklistItems.SelectMany(item => new[] { item.Title, item.StateText, item.ActionText }));

        Assert.True(apiReferences.IsComplete);
        Assert.Contains("required non-secret references", apiReferences.StateText, StringComparison.Ordinal);
        Assert.Equal("Open Providers", apiReferences.ActionButtonText);
        Assert.Equal("Providers", apiReferences.ActionNavigationTag);
        Assert.DoesNotContain("github-copilot-pat", checklistText, StringComparison.Ordinal);
        Assert.DoesNotContain("my-enterprise", checklistText, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderSetupDecisions_ExplainProviderSourceChoices()
    {
        var config = AppConfig.CreateDefault();
        var codex = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));
        codex.IsEnabled = true;
        codex.SourceKind = DataSourceKind.LocalAppServer;
        var claude = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Claude));
        claude.IsEnabled = true;
        claude.SourceKind = DataSourceKind.Cli;
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.Manual;

        var viewModel = new FirstRunSetupViewModel(
            config,
            [
                ProviderDescriptors.Get(ProviderId.Codex),
                ProviderDescriptors.Get(ProviderId.Claude),
                ProviderDescriptors.Get(ProviderId.Gemini),
                ProviderDescriptors.Get(ProviderId.OpenCodeZen)
            ]);

        AssertDecision(
            viewModel,
            "Codex",
            "Local app-server setup",
            "signed-in local provider command",
            "Providers");
        AssertDecision(
            viewModel,
            "Claude",
            "CLI setup",
            "launchable provider command",
            "Providers");
        AssertDecision(
            viewModel,
            "Gemini",
            "Manual ready",
            "Manual fallback is ready",
            "Providers");
        AssertDecision(
            viewModel,
            "OpenCode Zen",
            "Disabled",
            "Leave this disabled",
            "Providers");
    }

    [Fact]
    public void ProviderSetupDecisions_FlagUnsupportedAndMockSources()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.LocalAppServer;
        var codex = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Codex));
        codex.IsEnabled = true;
        codex.SourceKind = DataSourceKind.Mock;

        var viewModel = new FirstRunSetupViewModel(
            config,
            [ProviderDescriptors.Get(ProviderId.Gemini), ProviderDescriptors.Get(ProviderId.Codex)]);

        AssertDecision(
            viewModel,
            "Gemini",
            "Needs attention",
            "Choose a supported source mode",
            "Providers");
        AssertDecision(
            viewModel,
            "Codex",
            "Mock only",
            "UI checks only",
            "Providers");
    }

    [Fact]
    public void ProviderSetupDecisions_RouteMissingApiReferencesToPrivacyWithoutLeakingNames()
    {
        var config = AppConfig.CreateDefault();
        var gemini = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini));
        gemini.IsEnabled = true;
        gemini.SourceKind = DataSourceKind.OfficialApi;
        gemini.ApiKey.SecretName = "gemini-api-key";
        var copilot = config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.GitHubCopilot));
        copilot.IsEnabled = true;
        copilot.SourceKind = DataSourceKind.OfficialApi;
        copilot.GitHubCopilot.Organization = "my-org";

        var viewModel = new FirstRunSetupViewModel(
            config,
            [ProviderDescriptors.Get(ProviderId.Gemini), ProviderDescriptors.Get(ProviderId.GitHubCopilot)]);
        var text = string.Join(
            Environment.NewLine,
            viewModel.ProviderSetupDecisions.SelectMany(decision => new[]
            {
                decision.ProviderName,
                decision.StateText,
                decision.RecommendationText,
                decision.ActionButtonText
            }));

        AssertDecision(
            viewModel,
            "Gemini",
            "API references ready",
            "non-secret references configured",
            "Providers");
        AssertDecision(
            viewModel,
            "GitHub Copilot",
            "Needs API references",
            "Save the secret value",
            "Privacy & Data");
        Assert.DoesNotContain("gemini-api-key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("my-org", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret name", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkComplete_RecordsCompletionState()
    {
        var now = new DateTimeOffset(2026, 7, 8, 13, 0, 0, TimeSpan.Zero);
        var config = AppConfig.CreateDefault();
        var viewModel = new FirstRunSetupViewModel(
            config,
            ProviderDescriptors.All,
            nowProvider: () => now);

        viewModel.MarkComplete();

        Assert.True(config.Onboarding.HasCompletedFirstRun);
        Assert.Equal(now, config.Onboarding.CompletedAt);
    }

    private static void AssertDecision(
        FirstRunSetupViewModel viewModel,
        string providerName,
        string stateText,
        string recommendationText,
        string actionNavigationTag)
    {
        var decision = viewModel.ProviderSetupDecisions.Single(decision => decision.ProviderName == providerName);
        Assert.Equal(stateText, decision.StateText);
        Assert.Contains(recommendationText, decision.RecommendationText, StringComparison.Ordinal);
        Assert.Equal(actionNavigationTag, decision.ActionNavigationTag);
    }
}
