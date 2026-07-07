using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.App.ViewModels;

public sealed class ProviderSettingsPageViewModel
{
    public ProviderSettingsPageViewModel(
        AppConfig config,
        IEnumerable<ProviderDescriptor> descriptors)
    {
        Config = config;
        Editors = descriptors
            .Select(descriptor => new ProviderSettingsEditorViewModel(
                descriptor,
                config.GetOrCreateProvider(descriptor)))
            .ToList();
    }

    public AppConfig Config { get; }

    public IReadOnlyList<ProviderSettingsEditorViewModel> Editors { get; }

    public ProviderSettingsApplyResult TryApply()
    {
        var editorResults = Editors
            .Select(editor => editor.Validate())
            .ToList();
        var errors = editorResults
            .SelectMany(result => result.Errors.Select(error => $"{result.Editor.DisplayName}: {error}"))
            .ToList();
        var warnings = editorResults
            .SelectMany(result => result.Warnings.Select(warning => $"{result.Editor.DisplayName}: {warning}"))
            .ToList();

        var result = new ProviderSettingsApplyResult(editorResults, errors, warnings);
        if (!result.IsValid)
        {
            return result;
        }

        foreach (var editorResult in editorResults)
        {
            editorResult.Editor.Apply(editorResult);
        }

        return result;
    }
}

public sealed class ProviderSettingsEditorViewModel(
    ProviderDescriptor descriptor,
    ProviderConfig provider) : ObservableObject
{
    private bool isEnabled = provider.IsEnabled;
    private string sourceKindText = provider.SourceKind.ToString();
    private string usedPercentText = provider.Manual.UsedPercent?.ToString("0.##") ?? string.Empty;
    private string remainingPercentText = provider.Manual.RemainingPercent?.ToString("0.##") ?? string.Empty;
    private string resetDateTimeText = provider.Manual.ResetsAt?.ToString("O") ?? string.Empty;
    private string resetDescriptionText = provider.Manual.ResetDescription ?? string.Empty;
    private string creditBalanceText = provider.Manual.CreditBalance?.ToString("0.##") ?? string.Empty;
    private string currencyText = provider.Manual.Currency ?? string.Empty;
    private string monthToDateCostText = provider.Manual.MonthToDateCost?.ToString("0.##") ?? string.Empty;
    private string tokensLast31DaysText = provider.Manual.TokensLast31Days?.ToString() ?? string.Empty;
    private string notesText = provider.Manual.Notes ?? string.Empty;
    private string apiKeySecretNameText = provider.ApiKey.SecretName ?? string.Empty;
    private string gitHubOrganizationText = provider.GitHubCopilot.Organization ?? string.Empty;
    private string gitHubEnterpriseSlugText = provider.GitHubCopilot.EnterpriseSlug ?? string.Empty;
    private string gitHubPatSecretNameText = provider.GitHubCopilot.PatSecretName ?? string.Empty;

    public string DisplayName { get; } = descriptor.DisplayName;

    public string HelperText { get; } = descriptor.SupportsLogin
        ? "Automatic sources use placeholders unless an official or local integration is available."
        : "Manual source is available.";

    public IReadOnlyList<string> SupportedSourceNames { get; } = descriptor.SupportedSources
        .Select(source => source.ToString())
        .ToList();

    public bool HasGitHubCopilotSettings => descriptor.Id == ProviderId.GitHubCopilot;

    public bool HasApiKeySettings => descriptor.Id is ProviderId.Gemini or ProviderId.OpenCodeZen;

    public string ApiKeyStatusText
    {
        get
        {
            if (!HasApiKeySettings)
            {
                return string.Empty;
            }

            return SourceKindText == DataSourceKind.OfficialApi.ToString()
                ? "Store the API key through the secret store and enter only its secret name here. Usage retrieval remains a provider TODO until an official endpoint is selected."
                : "Manual mode can track balance and reset details without storing an API key reference.";
        }
    }

    public string GitHubCopilotStatusText
    {
        get
        {
            if (!HasGitHubCopilotSettings)
            {
                return string.Empty;
            }

            return SourceKindText == DataSourceKind.OfficialApi.ToString()
                ? "Organization or Enterprise metrics require permissions. Store the PAT in the secret store and enter only its secret name here."
                : "Personal Copilot users can stay in Manual mode without organization metrics.";
        }
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public string SourceKindText
    {
        get => sourceKindText;
        set
        {
            if (SetProperty(ref sourceKindText, value))
            {
                OnPropertyChanged(nameof(ApiKeyStatusText));
                OnPropertyChanged(nameof(GitHubCopilotStatusText));
            }
        }
    }

    public string UsedPercentText
    {
        get => usedPercentText;
        set => SetProperty(ref usedPercentText, value);
    }

    public string RemainingPercentText
    {
        get => remainingPercentText;
        set => SetProperty(ref remainingPercentText, value);
    }

    public string ResetDateTimeText
    {
        get => resetDateTimeText;
        set => SetProperty(ref resetDateTimeText, value);
    }

    public string ResetDescriptionText
    {
        get => resetDescriptionText;
        set => SetProperty(ref resetDescriptionText, value);
    }

    public string CreditBalanceText
    {
        get => creditBalanceText;
        set => SetProperty(ref creditBalanceText, value);
    }

    public string CurrencyText
    {
        get => currencyText;
        set => SetProperty(ref currencyText, value);
    }

    public string MonthToDateCostText
    {
        get => monthToDateCostText;
        set => SetProperty(ref monthToDateCostText, value);
    }

    public string TokensLast31DaysText
    {
        get => tokensLast31DaysText;
        set => SetProperty(ref tokensLast31DaysText, value);
    }

    public string NotesText
    {
        get => notesText;
        set => SetProperty(ref notesText, value);
    }

    public string ApiKeySecretNameText
    {
        get => apiKeySecretNameText;
        set => SetProperty(ref apiKeySecretNameText, value);
    }

    public string GitHubOrganizationText
    {
        get => gitHubOrganizationText;
        set => SetProperty(ref gitHubOrganizationText, value);
    }

    public string GitHubEnterpriseSlugText
    {
        get => gitHubEnterpriseSlugText;
        set => SetProperty(ref gitHubEnterpriseSlugText, value);
    }

    public string GitHubPatSecretNameText
    {
        get => gitHubPatSecretNameText;
        set => SetProperty(ref gitHubPatSecretNameText, value);
    }

    public ProviderSettingsEditorValidationResult Validate()
    {
        var manualResult = ManualUsageInputValidator.Parse(
            provider.Manual,
            new ManualUsageInput(
                UsedPercentText,
                RemainingPercentText,
                ResetDateTimeText,
                ResetDescriptionText,
                CreditBalanceText,
                CurrencyText,
                MonthToDateCostText,
                TokensLast31DaysText,
                NotesText));
        var errors = manualResult.Errors.ToList();

        var sourceKind = ParseSourceKind(errors);
        ValidateApiKeySettings(sourceKind, errors);
        ValidateGitHubCopilotSettings(sourceKind, errors);
        return new ProviderSettingsEditorValidationResult(
            this,
            sourceKind,
            manualResult.Settings,
            errors,
            manualResult.Warnings);
    }

    public void Apply(ProviderSettingsEditorValidationResult validation)
    {
        if (!validation.IsValid || validation.SourceKind is null)
        {
            throw new InvalidOperationException("Provider settings cannot be applied before validation succeeds.");
        }

        provider.IsEnabled = IsEnabled;
        provider.SourceKind = validation.SourceKind.Value;
        provider.Manual = validation.ManualSettings;
        provider.ApiKey.SecretName = TrimToNull(ApiKeySecretNameText);
        provider.GitHubCopilot.Organization = TrimToNull(GitHubOrganizationText);
        provider.GitHubCopilot.EnterpriseSlug = TrimToNull(GitHubEnterpriseSlugText);
        provider.GitHubCopilot.PatSecretName = TrimToNull(GitHubPatSecretNameText);
    }

    private DataSourceKind? ParseSourceKind(ICollection<string> errors)
    {
        if (!Enum.TryParse<DataSourceKind>(SourceKindText, out var sourceKind)
            || !descriptor.SupportedSources.Contains(sourceKind))
        {
            errors.Add("Source must be one of the supported values.");
            return null;
        }

        return sourceKind;
    }

    private void ValidateApiKeySettings(
        DataSourceKind? sourceKind,
        ICollection<string> errors)
    {
        if (!HasApiKeySettings || sourceKind != DataSourceKind.OfficialApi)
        {
            return;
        }

        if (TrimToNull(ApiKeySecretNameText) is null)
        {
            errors.Add("API key secret name is required for API mode; store the key in the secret store and keep only the secret name in config.");
        }
    }

    private void ValidateGitHubCopilotSettings(
        DataSourceKind? sourceKind,
        ICollection<string> errors)
    {
        if (!HasGitHubCopilotSettings || sourceKind != DataSourceKind.OfficialApi)
        {
            return;
        }

        if (TrimToNull(GitHubOrganizationText) is null
            && TrimToNull(GitHubEnterpriseSlugText) is null)
        {
            errors.Add("Organization or Enterprise slug is required for Copilot metrics API mode. Use Manual mode for personal-only tracking.");
        }

        if (TrimToNull(GitHubPatSecretNameText) is null)
        {
            errors.Add("PAT secret name is required for Copilot metrics API mode; do not paste the token into config.");
        }
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record ProviderSettingsEditorValidationResult(
    ProviderSettingsEditorViewModel Editor,
    DataSourceKind? SourceKind,
    ManualUsageSettings ManualSettings,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record ProviderSettingsApplyResult(
    IReadOnlyList<ProviderSettingsEditorValidationResult> EditorResults,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
