using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.App.Windows;

public sealed class MainWindow : Window
{
    private readonly AppHost host;
    private readonly NavigationView navigationView = new()
    {
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact
    };

    public MainWindow(AppHost host)
    {
        this.host = host;
        Title = "WinAI Usage Bar Settings";
        Content = navigationView;
        WindowHelpers.Resize(this, 920, 640);

        navigationView.MenuItems.Add(CreateItem("Overview", Symbol.Home));
        navigationView.MenuItems.Add(CreateItem("Providers", Symbol.AllApps));
        navigationView.MenuItems.Add(CreateItem("Appearance", Symbol.View));
        navigationView.MenuItems.Add(CreateItem("Refresh", Symbol.Refresh));
        navigationView.MenuItems.Add(CreateItem("Privacy & Data", Symbol.Setting));
        navigationView.MenuItems.Add(CreateItem("About", Symbol.Help));
        navigationView.SelectionChanged += OnSelectionChanged;

        host.ViewModel.Providers.CollectionChanged += OnProvidersChanged;
        Closed += (_, _) =>
        {
            host.ViewModel.Providers.CollectionChanged -= OnProvidersChanged;
            host.OnSettingsClosed();
        };

        navigationView.SelectedItem = navigationView.MenuItems[0];
        _ = NavigateAsync("Overview");
    }

    private static NavigationViewItem CreateItem(string text, Symbol symbol)
    {
        return new NavigationViewItem
        {
            Content = text,
            Tag = text,
            Icon = new SymbolIcon(symbol)
        };
    }

    private async void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            await NavigateAsync(tag);
        }
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if ((navigationView.SelectedItem as NavigationViewItem)?.Tag as string == "Overview")
        {
            _ = NavigateAsync("Overview");
        }
    }

    private async Task NavigateAsync(string tag)
    {
        navigationView.Content = tag switch
        {
            "Overview" => BuildOverviewPage(),
            "Providers" => await BuildProvidersPageAsync(),
            "Appearance" => await BuildAppearancePageAsync(),
            "Refresh" => await BuildRefreshPageAsync(),
            "Privacy & Data" => BuildPrivacyPage(),
            "About" => BuildAboutPage(),
            _ => BuildOverviewPage()
        };
    }

    private UIElement BuildOverviewPage()
    {
        var panel = PageStack("Overview");
        panel.Children.Add(new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Title = host.ViewModel.StatusText,
            Message = "Provider data is refreshed in the background and cached locally."
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var refreshButton = new Button { Content = "Refresh Now" };
        refreshButton.Click += (_, _) => _ = host.RefreshNowAsync(CancellationToken.None);
        actions.Children.Add(refreshButton);

        var compactButton = new Button { Content = "Show Compact" };
        compactButton.Click += (_, _) => host.ShowCompactPanel();
        actions.Children.Add(compactButton);

        var widgetButton = new Button { Content = "Show Widget" };
        widgetButton.Click += (_, _) => host.ShowWidget();
        actions.Children.Add(widgetButton);

        panel.Children.Add(actions);

        foreach (var provider in host.ViewModel.Providers)
        {
            panel.Children.Add(UiFactory.ProviderCard(provider));
        }

        if (host.ViewModel.Providers.Count == 0)
        {
            panel.Children.Add(UiFactory.Text("No enabled providers", 14));
        }

        return Wrap(panel);
    }

    private async Task<UIElement> BuildProvidersPageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var panel = PageStack("Providers");
        var editors = new List<ProviderEditor>();
        var validationInfo = new InfoBar
        {
            Severity = InfoBarSeverity.Error,
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(validationInfo);

        foreach (var descriptor in ProviderDescriptors.All)
        {
            var provider = config.GetOrCreateProvider(descriptor);
            var editor = CreateProviderEditor(descriptor, provider);
            editors.Add(editor);
            panel.Children.Add(editor.Root);
        }

        var saveButton = new Button { Content = "Save Providers" };
        saveButton.Click += async (_, _) =>
        {
            var validations = editors.Select(editor => editor.Validate()).ToList();
            var errors = validations
                .SelectMany(validation => validation.ManualResult.Errors.Select(
                    error => $"{validation.Editor.DisplayName}: {error}"))
                .ToList();

            if (errors.Count > 0)
            {
                validationInfo.Title = "Invalid provider settings";
                validationInfo.Message = string.Join(Environment.NewLine, errors);
                validationInfo.IsOpen = true;
                return;
            }

            foreach (var validation in validations)
            {
                validation.Editor.Apply(validation.ManualResult.Settings);
            }

            await host.SaveConfigAsync(config, CancellationToken.None);
            await host.RefreshNowAsync(CancellationToken.None);
            navigationView.Content = await BuildProvidersPageAsync();
        };
        panel.Children.Add(saveButton);

        return Wrap(panel);
    }

    private ProviderEditor CreateProviderEditor(ProviderDescriptor descriptor, ProviderConfig provider)
    {
        var root = new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 120, 120, 120))
        };

        var stack = new StackPanel { Spacing = 8 };
        root.Child = stack;

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(UiFactory.Text(descriptor.DisplayName, 16, FontWeights.SemiBold));
        var toggle = new ToggleSwitch { IsOn = provider.IsEnabled, Header = "Enabled" };
        Grid.SetColumn(toggle, 1);
        header.Children.Add(toggle);
        stack.Children.Add(header);

        var sourceCombo = new ComboBox
        {
            Header = "Source",
            MinWidth = 180
        };

        foreach (var source in descriptor.SupportedSources)
        {
            sourceCombo.Items.Add(source.ToString());
        }

        sourceCombo.SelectedItem = provider.SourceKind.ToString();
        stack.Children.Add(sourceCombo);

        var manualGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        manualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        manualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var usedBox = TextBox("Used %", provider.Manual.UsedPercent?.ToString("0.##") ?? string.Empty);
        var remainingBox = TextBox("Remaining %", provider.Manual.RemainingPercent?.ToString("0.##") ?? string.Empty);
        var resetBox = TextBox("Reset datetime", provider.Manual.ResetsAt?.ToString("O") ?? string.Empty);
        var creditsBox = TextBox("Credits", provider.Manual.CreditBalance?.ToString("0.##") ?? string.Empty);
        var costBox = TextBox("Month cost", provider.Manual.MonthToDateCost?.ToString("0.##") ?? string.Empty);
        var notesBox = TextBox("Notes", provider.Manual.Notes ?? string.Empty);

        AddToGrid(manualGrid, usedBox, 0, 0);
        AddToGrid(manualGrid, remainingBox, 0, 1);
        AddToGrid(manualGrid, resetBox, 1, 0);
        AddToGrid(manualGrid, creditsBox, 1, 1);
        AddToGrid(manualGrid, costBox, 2, 0);
        AddToGrid(manualGrid, notesBox, 2, 1);

        stack.Children.Add(manualGrid);

        stack.Children.Add(new TextBlock
        {
            Text = descriptor.SupportsLogin
                ? "Automatic sources use placeholders unless an official or local integration is available."
                : "Manual source is available.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        return new ProviderEditor(
            root,
            descriptor.DisplayName,
            provider,
            toggle,
            sourceCombo,
            usedBox,
            remainingBox,
            resetBox,
            creditsBox,
            costBox,
            notesBox);
    }

    private async Task<UIElement> BuildAppearancePageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var panel = PageStack("Appearance");
        var combo = new ComboBox
        {
            Header = "Theme",
            MinWidth = 200
        };
        combo.Items.Add("System");
        combo.Items.Add("Light");
        combo.Items.Add("Dark");
        combo.SelectedItem = config.Appearance.Theme;
        panel.Children.Add(combo);

        var save = new Button { Content = "Save Appearance" };
        save.Click += async (_, _) =>
        {
            config.Appearance.Theme = combo.SelectedItem?.ToString() ?? "System";
            await host.SaveConfigAsync(config, CancellationToken.None);
        };
        panel.Children.Add(save);

        return Wrap(panel);
    }

    private async Task<UIElement> BuildRefreshPageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var panel = PageStack("Refresh");
        var combo = new ComboBox
        {
            Header = "Interval",
            MinWidth = 220
        };

        foreach (var value in Enum.GetValues<RefreshIntervalKind>())
        {
            combo.Items.Add(value.ToString());
        }

        combo.SelectedItem = config.Refresh.Interval.ToString();
        panel.Children.Add(combo);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var save = new Button { Content = "Save Refresh" };
        save.Click += async (_, _) =>
        {
            if (Enum.TryParse<RefreshIntervalKind>(combo.SelectedItem?.ToString(), out var interval))
            {
                config.Refresh.Interval = interval;
                await host.SaveConfigAsync(config, CancellationToken.None);
            }
        };
        actions.Children.Add(save);

        var refresh = new Button { Content = "Refresh Now" };
        refresh.Click += (_, _) => _ = host.RefreshNowAsync(CancellationToken.None);
        actions.Children.Add(refresh);
        panel.Children.Add(actions);

        return Wrap(panel);
    }

    private UIElement BuildPrivacyPage()
    {
        var panel = PageStack("Privacy & Data");
        panel.Children.Add(new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Title = "Local storage",
            Message = host.Paths.RootDirectory
        });

        panel.Children.Add(UiFactory.Text("Secrets are stored through the secret store abstraction and are not written to config.json.", 14));
        panel.Children.Add(UiFactory.Text("Browser cookie scraping is not implemented in this MVP.", 14));

        var openButton = new Button { Content = "Open Data Folder" };
        openButton.Click += (_, _) =>
        {
            Directory.CreateDirectory(host.Paths.RootDirectory);
            Process.Start(new ProcessStartInfo(host.Paths.RootDirectory) { UseShellExecute = true });
        };
        panel.Children.Add(openButton);

        return Wrap(panel);
    }

    private UIElement BuildAboutPage()
    {
        var panel = PageStack("About");
        panel.Children.Add(UiFactory.Text("WinAI Usage Bar", 22, FontWeights.SemiBold));
        panel.Children.Add(UiFactory.Text("MVP desktop usage monitor for AI providers.", 14));
        panel.Children.Add(UiFactory.Text("Built with C#, WinUI 3, and Windows App SDK.", 14));
        return Wrap(panel);
    }

    private static StackPanel PageStack(string title)
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(24),
            Spacing = 12
        };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });

        return panel;
    }

    private static ScrollViewer Wrap(UIElement element)
    {
        return new ScrollViewer
        {
            Content = element,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static TextBox TextBox(string header, string value)
    {
        return new TextBox
        {
            Header = header,
            Text = value,
            MinWidth = 160
        };
    }

    private static void AddToGrid(Grid grid, FrameworkElement element, int row, int column)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private sealed record ProviderEditor(
        Border Root,
        string DisplayName,
        ProviderConfig Provider,
        ToggleSwitch Toggle,
        ComboBox SourceCombo,
        TextBox UsedBox,
        TextBox RemainingBox,
        TextBox ResetBox,
        TextBox CreditsBox,
        TextBox CostBox,
        TextBox NotesBox)
    {
        public ProviderEditorValidation Validate()
        {
            var input = new ManualUsageInput(
                UsedBox.Text,
                RemainingBox.Text,
                ResetBox.Text,
                CreditsBox.Text,
                CostBox.Text,
                NotesBox.Text);
            var result = ManualUsageInputValidator.Parse(Provider.Manual, input);
            return new ProviderEditorValidation(this, result);
        }

        public void Apply(ManualUsageSettings manualSettings)
        {
            Provider.IsEnabled = Toggle.IsOn;
            if (Enum.TryParse<DataSourceKind>(SourceCombo.SelectedItem?.ToString(), out var sourceKind))
            {
                Provider.SourceKind = sourceKind;
            }

            Provider.Manual = manualSettings;
        }
    }

    private sealed record ProviderEditorValidation(
        ProviderEditor Editor,
        ManualUsageValidationResult ManualResult);
}
