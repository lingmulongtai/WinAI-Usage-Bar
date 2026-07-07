using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.App.ViewModels;

public sealed class WidgetSettingsPageViewModel
{
    private readonly WidgetSettings settings;

    public WidgetSettingsPageViewModel(
        WidgetSettings settings,
        IEnumerable<ProviderDescriptor> descriptors)
    {
        this.settings = settings;
        ShowOnStartup = settings.ShowOnStartup;
        TopMost = settings.TopMost;
        ProviderOptions = descriptors
            .Select(descriptor => new WidgetProviderOptionViewModel(
                descriptor.Id,
                descriptor.DisplayName,
                settings.ProviderIds.Contains(descriptor.Id)))
            .ToList();
    }

    public bool ShowOnStartup { get; set; }

    public bool TopMost { get; set; }

    public IReadOnlyList<WidgetProviderOptionViewModel> ProviderOptions { get; }

    public WidgetSettingsApplyResult TryApply()
    {
        var selected = ProviderOptions
            .Where(option => option.IsSelected)
            .Select(option => option.ProviderId)
            .Distinct()
            .ToList();
        var errors = new List<string>();

        if (selected.Count == 0)
        {
            errors.Add("Select at least one widget provider.");
        }

        if (selected.Count > 3)
        {
            errors.Add("Select no more than three widget providers.");
        }

        if (errors.Count > 0)
        {
            return new WidgetSettingsApplyResult(IsValid: false, errors);
        }

        settings.ShowOnStartup = ShowOnStartup;
        settings.TopMost = TopMost;
        settings.ProviderIds = selected;
        return new WidgetSettingsApplyResult(IsValid: true, []);
    }
}

public sealed class WidgetProviderOptionViewModel(
    ProviderId providerId,
    string displayName,
    bool isSelected) : ObservableObject
{
    private bool isSelected = isSelected;

    public ProviderId ProviderId { get; } = providerId;

    public string DisplayName { get; } = displayName;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

public sealed record WidgetSettingsApplyResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
