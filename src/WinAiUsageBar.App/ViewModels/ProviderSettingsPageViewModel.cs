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
    private string cliCommandPathOverrideText = provider.Cli.CommandPathOverride ?? string.Empty;
    private string gitHubOrganizationText = provider.GitHubCopilot.Organization ?? string.Empty;
    private string gitHubEnterpriseSlugText = provider.GitHubCopilot.EnterpriseSlug ?? string.Empty;
    private string gitHubPatSecretNameText = provider.GitHubCopilot.PatSecretName ?? string.Empty;

    public string DisplayName { get; } = descriptor.DisplayName;

    public string HelperText { get; } = descriptor.SupportsLogin
        ? "Automatic sources use placeholders unless an official or local integration is available."
        : "Manual source is available.";

    public IReadOnlyList<string> SetupGuidanceLines => BuildSetupGuidanceLines();

    public IReadOnlyList<string> SupportedSourceNames { get; } = descriptor.SupportedSources
        .Select(source => source.ToString())
        .ToList();

    public bool HasGitHubCopilotSettings => descriptor.Id == ProviderId.GitHubCopilot;

    public bool HasApiKeySettings => descriptor.Id is ProviderId.Gemini or ProviderId.OpenCodeZen;

    public bool HasCliCommandSettings => descriptor.SupportedSources.Any(source =>
        source is DataSourceKind.Cli or DataSourceKind.LocalAppServer);

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

    public string CliCommandStatusText
    {
        get
        {
            if (!HasCliCommandSettings)
            {
                return string.Empty;
            }

            return CliCommandSettings.NormalizeCommandPathOverride(CliCommandPathOverrideText) is null
                ? "Provider refresh will use normal PATH discovery for CLI or local app-server sources."
                : "Provider refresh will try the configured CLI command override before normal PATH discovery; this guidance does not echo the value.";
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
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
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
                OnPropertyChanged(nameof(SetupGuidanceLines));
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
        set
        {
            if (SetProperty(ref apiKeySecretNameText, value))
            {
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
    }

    public string CliCommandPathOverrideText
    {
        get => cliCommandPathOverrideText;
        set
        {
            if (SetProperty(ref cliCommandPathOverrideText, value))
            {
                OnPropertyChanged(nameof(CliCommandStatusText));
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
    }

    public string GitHubOrganizationText
    {
        get => gitHubOrganizationText;
        set
        {
            if (SetProperty(ref gitHubOrganizationText, value))
            {
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
    }

    public string GitHubEnterpriseSlugText
    {
        get => gitHubEnterpriseSlugText;
        set
        {
            if (SetProperty(ref gitHubEnterpriseSlugText, value))
            {
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
    }

    public string GitHubPatSecretNameText
    {
        get => gitHubPatSecretNameText;
        set
        {
            if (SetProperty(ref gitHubPatSecretNameText, value))
            {
                OnPropertyChanged(nameof(SetupGuidanceLines));
            }
        }
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
        ValidateCliCommandSettings(errors);
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
        provider.Cli.CommandPathOverride = CliCommandSettings.NormalizeCommandPathOverride(CliCommandPathOverrideText);
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

    private IReadOnlyList<string> BuildSetupGuidanceLines()
    {
        var lines = new List<string>
        {
            IsEnabled
                ? $"Enabled provider will refresh from the current {SourceKindText} source."
                : "Disabled providers are not refreshed or shown on usage surfaces until enabled."
        };

        if (!Enum.TryParse<DataSourceKind>(SourceKindText, out var sourceKind)
            || !descriptor.SupportedSources.Contains(sourceKind))
        {
            lines.Add($"Current source is not supported here. Choose one of: {string.Join(", ", SupportedSourceNames)}.");
            return lines;
        }

        lines.Add(GetSourceGuidance(sourceKind));

        if (descriptor.SupportsCredits)
        {
            lines.Add("Credits or costs can appear when the selected source provides them; Manual mode can still track them by hand.");
        }
        else
        {
            lines.Add("This provider is not expected to return credits yet; Manual mode can still track usage values.");
        }

        AddApiKeyGuidance(lines, sourceKind);
        AddCliCommandGuidance(lines, sourceKind);
        AddGitHubCopilotGuidance(lines, sourceKind);
        return lines;
    }

    private void AddCliCommandGuidance(
        ICollection<string> lines,
        DataSourceKind sourceKind)
    {
        if (!HasCliCommandSettings)
        {
            return;
        }

        if (sourceKind is not (DataSourceKind.Cli or DataSourceKind.LocalAppServer))
        {
            lines.Add("CLI command overrides are only used by CLI or local app-server sources.");
            return;
        }

        lines.Add(CliCommandSettings.NormalizeCommandPathOverride(CliCommandPathOverrideText) is null
            ? "CLI or local app-server refresh will use PATH discovery unless a command override is configured."
            : "CLI command override is configured; guidance does not echo the command path.");
    }

    private void ValidateCliCommandSettings(ICollection<string> errors)
    {
        var normalizedOverride = CliCommandSettings.NormalizeCommandPathOverride(CliCommandPathOverrideText);
        if (normalizedOverride is null)
        {
            return;
        }

        if (!HasCliCommandSettings)
        {
            errors.Add("CLI command override is not supported by this provider.");
            return;
        }

        if (CliCommandSettings.HasInvalidCommandPathOverrideQuotes(CliCommandPathOverrideText))
        {
            errors.Add("CLI command override quotes must wrap a single path or command.");
        }

        if (normalizedOverride.Length > 512)
        {
            errors.Add("CLI command override must be 512 characters or fewer.");
        }

        if (normalizedOverride.Contains('\r') || normalizedOverride.Contains('\n'))
        {
            errors.Add("CLI command override must be a single path or command.");
        }

        if (LooksSensitive(normalizedOverride))
        {
            errors.Add("CLI command override must not contain tokens, cookies, or auth values.");
        }
    }

    private static string GetSourceGuidance(DataSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DataSourceKind.Manual => "Manual mode is the safe fallback and does not need secrets or external CLI commands.",
            DataSourceKind.Mock => "Mock mode is for UI and refresh testing only; switch sources before relying on the data.",
            DataSourceKind.Cli => "CLI mode requires the provider command to be installed and discoverable on PATH.",
            DataSourceKind.LocalFile => "Local file mode must read only explicitly supported paths and should never inspect secret files.",
            DataSourceKind.LocalAppServer => "Local app-server mode requires a signed-in local provider command and reports failures without crashing.",
            DataSourceKind.OfficialApi => "API mode stores only secret references in config; save secret values from Privacy & Data.",
            _ => "This source is not recognized yet."
        };
    }

    private void AddApiKeyGuidance(
        ICollection<string> lines,
        DataSourceKind sourceKind)
    {
        if (!HasApiKeySettings)
        {
            return;
        }

        if (sourceKind != DataSourceKind.OfficialApi)
        {
            lines.Add("API key references are only used in API mode.");
            return;
        }

        lines.Add(TrimToNull(ApiKeySecretNameText) is null
            ? "API mode needs an API key secret reference before refresh can work."
            : "API mode has an API key reference configured; this guidance does not echo the reference name or value.");
    }

    private void AddGitHubCopilotGuidance(
        ICollection<string> lines,
        DataSourceKind sourceKind)
    {
        if (!HasGitHubCopilotSettings)
        {
            return;
        }

        if (sourceKind != DataSourceKind.OfficialApi)
        {
            lines.Add("Personal Copilot users can stay in Manual mode without organization metrics.");
            return;
        }

        var hasScope = TrimToNull(GitHubOrganizationText) is not null
            || TrimToNull(GitHubEnterpriseSlugText) is not null;
        var hasPatReference = TrimToNull(GitHubPatSecretNameText) is not null;

        lines.Add((hasScope, hasPatReference) switch
        {
            (false, false) => "Copilot API mode needs an organization or enterprise scope plus a PAT secret reference.",
            (false, true) => "Copilot API mode still needs an organization or enterprise scope; guidance does not echo configured reference values.",
            (true, false) => "Copilot API mode still needs a PAT secret reference; guidance does not echo configured scope values.",
            _ => "Copilot API mode has scope and PAT references configured; this guidance does not echo their names or values."
        });
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

    private static bool LooksSensitive(string value)
    {
        var markers = new[]
        {
            "token=",
            "access_token",
            "authorization:",
            "bearer ",
            "cookie=",
            "api_key",
            "apikey",
            "secret="
        };

        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
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
