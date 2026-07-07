using System.Collections.Specialized;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.App.Windows;

public sealed class CompactUsageWindow : Window
{
    private readonly AppHost host;
    private readonly StackPanel cardsPanel = new() { Spacing = 0 };

    public CompactUsageWindow(AppHost host)
    {
        this.host = host;
        Title = "WinAI Usage Bar";
        Content = BuildContent();
        WindowHelpers.Resize(this, 360, 520);

        host.ViewModel.Providers.CollectionChanged += OnProvidersChanged;
        Closed += (_, _) =>
        {
            host.ViewModel.Providers.CollectionChanged -= OnProvidersChanged;
            host.OnCompactClosed();
        };

        RenderCards();
        _ = ApplyConfiguredThemeAsync();
    }

    private UIElement BuildContent()
    {
        var root = new Grid
        {
            Padding = new Thickness(12),
            RowSpacing = 10
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid
        {
            ColumnSpacing = 8
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = "Usage",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });

        var refreshButton = UiFactory.IconButton(Symbol.Refresh, "Refresh now");
        refreshButton.Click += (_, _) => _ = host.RefreshNowAsync(CancellationToken.None);
        Grid.SetColumn(refreshButton, 1);
        header.Children.Add(refreshButton);

        var settingsButton = UiFactory.IconButton(Symbol.Setting, "Settings");
        settingsButton.Click += (_, _) => host.ShowSettings();
        Grid.SetColumn(settingsButton, 2);
        header.Children.Add(settingsButton);

        root.Children.Add(header);

        var scroll = new ScrollViewer
        {
            Content = cardsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        return root;
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderCards();
    }

    private void RenderCards()
    {
        cardsPanel.Children.Clear();

        if (host.ViewModel.Providers.Count == 0)
        {
            cardsPanel.Children.Add(UiFactory.Text("No enabled providers", 14));
            return;
        }

        foreach (var provider in host.ViewModel.Providers)
        {
            cardsPanel.Children.Add(UiFactory.ProviderCard(provider));
        }
    }

    private async Task ApplyConfiguredThemeAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        ThemeHelper.Apply(Content, config.Appearance.Theme);
    }
}
