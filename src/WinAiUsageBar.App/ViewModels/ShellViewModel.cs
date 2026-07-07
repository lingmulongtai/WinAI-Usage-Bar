using System.Collections.ObjectModel;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private string statusText = "Ready";

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public void ApplySnapshots(IEnumerable<UsageSnapshot> snapshots)
    {
        Providers.Clear();
        foreach (var snapshot in snapshots.OrderBy(snapshot => snapshot.DisplayName))
        {
            Providers.Add(new ProviderCardViewModel(snapshot));
        }

        StatusText = Providers.Count == 0
            ? "No enabled providers"
            : $"{Providers.Count} provider(s) updated";
    }

    public string BuildTrayTooltip()
    {
        var lowest = Providers
            .Where(provider => provider.Snapshot.PrimaryWindow?.RemainingPercent is not null)
            .OrderBy(provider => provider.Snapshot.PrimaryWindow?.RemainingPercent)
            .FirstOrDefault();

        if (lowest is null)
        {
            return "WinAI Usage Bar";
        }

        return $"{lowest.DisplayName} {lowest.PercentText} / {lowest.ResetText}";
    }
}
