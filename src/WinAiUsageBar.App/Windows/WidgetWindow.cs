using System.Collections.Specialized;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App.Windows;

public sealed class WidgetWindow : Window
{
    private readonly AppHost host;
    private readonly WidgetPlacementStore placementStore;
    private readonly StackPanel cardsPanel = new() { Spacing = 0 };
    private IReadOnlyList<ProviderId> selectedProviderIds = [];

    public WidgetWindow(AppHost host, WidgetPlacementStore placementStore)
    {
        this.host = host;
        this.placementStore = placementStore;
        Title = "WinAI Usage Widget";
        Content = BuildContent();

        host.ViewModel.Providers.CollectionChanged += OnProvidersChanged;
        Closed += OnClosed;

        _ = LoadPlacementAsync();
        RenderCards();
    }

    private UIElement BuildContent()
    {
        var root = new Grid
        {
            Padding = new Thickness(10),
            RowSpacing = 8
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = "WinAI",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });

        var refreshButton = UiFactory.IconButton(Symbol.Refresh, "Refresh now");
        refreshButton.Click += (_, _) => _ = host.RefreshNowAsync(CancellationToken.None);
        Grid.SetColumn(refreshButton, 1);
        header.Children.Add(refreshButton);

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

    private async Task LoadPlacementAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        selectedProviderIds = config.Widget.ProviderIds.Take(3).ToList();
        var placement = await placementStore.LoadAsync(CancellationToken.None);
        WindowHelpers.MoveAndResize(
            this,
            Convert.ToInt32(placement.Left),
            Convert.ToInt32(placement.Top),
            Convert.ToInt32(Math.Max(280, placement.Width)),
            Convert.ToInt32(Math.Max(160, placement.Height)));
        WindowHelpers.SetAlwaysOnTop(this, placement.TopMost);
        RenderCards();
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderCards();
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        host.ViewModel.Providers.CollectionChanged -= OnProvidersChanged;

        var appWindow = WindowHelpers.GetAppWindow(this);
        var presenter = appWindow.Presenter as OverlappedPresenter;
        var placement = new WindowPlacement(
            appWindow.Position.X,
            appWindow.Position.Y,
            appWindow.Size.Width,
            appWindow.Size.Height,
            presenter?.IsAlwaysOnTop ?? false);

        await placementStore.SaveAsync(placement, CancellationToken.None);
        host.OnWidgetClosed();
    }

    private void RenderCards()
    {
        cardsPanel.Children.Clear();

        var providers = host.ViewModel.Providers
            .Where(provider => selectedProviderIds.Count == 0 || selectedProviderIds.Contains(provider.ProviderId))
            .Take(3)
            .ToList();

        if (providers.Count == 0)
        {
            cardsPanel.Children.Add(UiFactory.Text("No widget providers", 13));
            return;
        }

        foreach (var provider in providers)
        {
            cardsPanel.Children.Add(UiFactory.ProviderCard(provider));
        }
    }
}
