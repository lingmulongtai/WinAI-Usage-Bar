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
    private string creditBalanceText = provider.Manual.CreditBalance?.ToString("0.##") ?? string.Empty;
    private string monthToDateCostText = provider.Manual.MonthToDateCost?.ToString("0.##") ?? string.Empty;
    private string notesText = provider.Manual.Notes ?? string.Empty;

    public string DisplayName { get; } = descriptor.DisplayName;

    public string HelperText { get; } = descriptor.SupportsLogin
        ? "Automatic sources use placeholders unless an official or local integration is available."
        : "Manual source is available.";

    public IReadOnlyList<string> SupportedSourceNames { get; } = descriptor.SupportedSources
        .Select(source => source.ToString())
        .ToList();

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public string SourceKindText
    {
        get => sourceKindText;
        set => SetProperty(ref sourceKindText, value);
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

    public string CreditBalanceText
    {
        get => creditBalanceText;
        set => SetProperty(ref creditBalanceText, value);
    }

    public string MonthToDateCostText
    {
        get => monthToDateCostText;
        set => SetProperty(ref monthToDateCostText, value);
    }

    public string NotesText
    {
        get => notesText;
        set => SetProperty(ref notesText, value);
    }

    public ProviderSettingsEditorValidationResult Validate()
    {
        var manualResult = ManualUsageInputValidator.Parse(
            provider.Manual,
            new ManualUsageInput(
                UsedPercentText,
                RemainingPercentText,
                ResetDateTimeText,
                CreditBalanceText,
                MonthToDateCostText,
                NotesText));
        var errors = manualResult.Errors.ToList();

        var sourceKind = ParseSourceKind(errors);
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
