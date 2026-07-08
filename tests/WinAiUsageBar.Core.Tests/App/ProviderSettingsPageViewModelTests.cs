using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ProviderSettingsPageViewModelTests
{
    [Fact]
    public void TryApply_DoesNotMutateConfigWhenManualInputIsInvalid()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.Manual.UsedPercent = 33;
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.UsedPercentText = "not a number";

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.StartsWith("Codex:", StringComparison.Ordinal));
        Assert.Equal(33, provider.Manual.UsedPercent);
    }

    [Fact]
    public void TryApply_NormalizesValidManualInputAndAppliesProviderState()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();

        editor.IsEnabled = false;
        editor.SourceKindText = DataSourceKind.Manual.ToString();
        editor.UsedPercentText = "120";
        editor.RemainingPercentText = "";
        editor.ResetDescriptionText = "  daily reset  ";
        editor.CreditBalanceText = "12.345";
        editor.CurrencyText = " JPY ";
        editor.MonthToDateCostText = "6.789";
        editor.TokensLast31DaysText = "4567";
        editor.NotesText = "  checked  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.False(provider.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, provider.SourceKind);
        Assert.Equal(100, provider.Manual.UsedPercent);
        Assert.Equal("daily reset", provider.Manual.ResetDescription);
        Assert.Equal(12.35m, provider.Manual.CreditBalance);
        Assert.Equal("JPY", provider.Manual.Currency);
        Assert.Equal(6.79m, provider.Manual.MonthToDateCost);
        Assert.Equal(4567, provider.Manual.TokensLast31Days);
        Assert.Equal("checked", provider.Manual.Notes);
        Assert.Contains(result.Warnings, warning => warning.Contains("clamped", StringComparison.Ordinal));
    }

    [Fact]
    public void TryApply_DoesNotMutateConfigWhenManualTokenInputIsInvalid()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.Manual.TokensLast31Days = 123;
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.TokensLast31DaysText = "-1";

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Tokens last 31 days", StringComparison.Ordinal));
        Assert.Equal(123, provider.Manual.TokensLast31Days);
    }

    [Fact]
    public void TryApply_RejectsUnsupportedSourceKind()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Source must be one of the supported values.", StringComparison.Ordinal));
        Assert.NotEqual(DataSourceKind.LocalAppServer, provider.SourceKind);
    }

    [Theory]
    [InlineData(ProviderId.Gemini)]
    [InlineData(ProviderId.OpenCodeZen)]
    public void TryApply_AllowsApiKeyProvidersToStayManualWithoutSecretReference(ProviderId providerId)
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(providerId);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.Manual.ToString();
        editor.ApiKeySecretNameText = "   ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(DataSourceKind.Manual, provider.SourceKind);
        Assert.Null(provider.ApiKey.SecretName);
    }

    [Theory]
    [InlineData(ProviderId.Gemini)]
    [InlineData(ProviderId.OpenCodeZen)]
    public void TryApply_RequiresApiKeySecretReferenceForApiMode(ProviderId providerId)
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(providerId);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("API key secret name is required", StringComparison.Ordinal));
        Assert.NotEqual(DataSourceKind.OfficialApi, provider.SourceKind);
    }

    [Theory]
    [InlineData(ProviderId.Gemini, "gemini-api-key")]
    [InlineData(ProviderId.OpenCodeZen, "opencode-zen-api-key")]
    public void TryApply_AppliesApiKeyProviderSecretReferenceOnly(
        ProviderId providerId,
        string secretName)
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(providerId);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();
        editor.ApiKeySecretNameText = $"  {secretName}  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(DataSourceKind.OfficialApi, provider.SourceKind);
        Assert.Equal(secretName, provider.ApiKey.SecretName);
    }

    [Fact]
    public void TryApply_AppliesCliCommandOverrideForLocalAppServerProvider()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();
        editor.CliCommandPathOverrideText = @"  C:\Tools\codex.cmd  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(DataSourceKind.LocalAppServer, provider.SourceKind);
        Assert.Equal(@"C:\Tools\codex.cmd", provider.Cli.CommandPathOverride);
    }

    [Fact]
    public void TryApply_NormalizesQuotedCliCommandOverrideForLocalAppServerProvider()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();
        editor.CliCommandPathOverrideText = "  \"C:\\Program Files\\Codex\\codex.cmd\"  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(@"C:\Program Files\Codex\codex.cmd", provider.Cli.CommandPathOverride);
    }

    [Fact]
    public void TryApply_RejectsCliCommandOverrideWithPartialQuotes()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.Cli.CommandPathOverride = @"C:\Tools\codex.cmd";
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();
        editor.CliCommandPathOverrideText = "\"C:\\Tools\\codex.cmd\" --verbose";

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("quotes must wrap", StringComparison.Ordinal));
        Assert.Equal(@"C:\Tools\codex.cmd", provider.Cli.CommandPathOverride);
    }

    [Fact]
    public void TryApply_RejectsCliCommandOverrideThatLooksSensitive()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.Cli.CommandPathOverride = @"C:\Tools\codex.cmd";
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.CliCommandPathOverrideText = "codex.exe --token=secret";

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("must not contain tokens", StringComparison.Ordinal));
        Assert.Equal(@"C:\Tools\codex.cmd", provider.Cli.CommandPathOverride);
    }

    [Fact]
    public void TryApply_AllowsGitHubCopilotManualModeWithoutOrganizationMetrics()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.Manual.ToString();

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(DataSourceKind.Manual, provider.SourceKind);
        Assert.Null(provider.GitHubCopilot.Organization);
        Assert.Null(provider.GitHubCopilot.PatSecretName);
    }

    [Fact]
    public void TryApply_RequiresGitHubCopilotOrgOrEnterpriseAndSecretForApiMode()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();

        var result = viewModel.TryApply();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Organization or Enterprise slug is required", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("PAT secret name is required", StringComparison.Ordinal));
        Assert.NotEqual(DataSourceKind.OfficialApi, provider.SourceKind);
    }

    [Fact]
    public void TryApply_AppliesGitHubCopilotApiSettingsAsSecretReferenceOnly()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var provider = config.GetOrCreateProvider(descriptor);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();
        editor.GitHubOrganizationText = "  my-org  ";
        editor.GitHubEnterpriseSlugText = "";
        editor.GitHubPatSecretNameText = "  github-copilot-pat  ";

        var result = viewModel.TryApply();

        Assert.True(result.IsValid);
        Assert.Equal(DataSourceKind.OfficialApi, provider.SourceKind);
        Assert.Equal("my-org", provider.GitHubCopilot.Organization);
        Assert.Null(provider.GitHubCopilot.EnterpriseSlug);
        Assert.Equal("github-copilot-pat", provider.GitHubCopilot.PatSecretName);
    }

    [Fact]
    public void SetupGuidanceLines_ExplainManualFallbackWithoutEchoingApiSecretReference()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var provider = config.GetOrCreateProvider(descriptor);
        provider.ApiKey.SecretName = "gemini-api-key";
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.Manual.ToString();

        var guidance = GuidanceText(editor);

        Assert.Contains("Manual mode is the safe fallback", guidance, StringComparison.Ordinal);
        Assert.Contains("API key references are only used in API mode.", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini-api-key", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupGuidanceLines_ReportUnsupportedSourceBeforeApply()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();

        var guidance = GuidanceText(editor);

        Assert.Contains("Current source is not supported here.", guidance, StringComparison.Ordinal);
        Assert.Contains("Manual, OfficialApi", guidance, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProviderId.Gemini, "gemini-api-key")]
    [InlineData(ProviderId.OpenCodeZen, "opencode-zen-api-key")]
    public void SetupGuidanceLines_GuideApiSecretReferenceWithoutEchoingConfiguredName(
        ProviderId providerId,
        string secretName)
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(providerId);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();
        editor.ApiKeySecretNameText = secretName;

        var guidance = GuidanceText(editor);

        Assert.Contains("API mode has an API key reference configured", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain(secretName, guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupGuidanceLines_GuideCliCommandOverrideWithoutEchoingPath()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.LocalAppServer.ToString();
        editor.CliCommandPathOverrideText = @"C:\Tools\codex.cmd";

        var guidance = GuidanceText(editor);

        Assert.Contains("CLI command override is configured", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Tools\codex.cmd", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupGuidanceLines_GuideMissingGitHubCopilotApiRequirements()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();

        var guidance = GuidanceText(editor);

        Assert.Contains("Copilot API mode needs an organization or enterprise scope plus a PAT secret reference.", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupGuidanceLines_GuideGitHubCopilotApiReferencesWithoutEchoingScopeOrSecret()
    {
        var config = AppConfig.CreateDefault();
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var viewModel = new ProviderSettingsPageViewModel(config, [descriptor]);
        var editor = viewModel.Editors.Single();
        editor.SourceKindText = DataSourceKind.OfficialApi.ToString();
        editor.GitHubOrganizationText = "my-org";
        editor.GitHubEnterpriseSlugText = "my-enterprise";
        editor.GitHubPatSecretNameText = "github-copilot-pat";

        var guidance = GuidanceText(editor);

        Assert.Contains("Copilot API mode has scope and PAT references configured", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("my-org", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("my-enterprise", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("github-copilot-pat", guidance, StringComparison.Ordinal);
    }

    private static string GuidanceText(ProviderSettingsEditorViewModel editor)
    {
        return string.Join('\n', editor.SetupGuidanceLines);
    }
}
