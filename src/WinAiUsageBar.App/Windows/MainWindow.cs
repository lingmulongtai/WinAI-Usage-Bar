using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.App.ViewModels;
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
        navigationView.MenuItems.Add(CreateItem("Widget", Symbol.PreviewLink));
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
            "Widget" => await BuildWidgetPageAsync(),
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
        var viewModel = new ProviderSettingsPageViewModel(config, ProviderDescriptors.All);
        var panel = PageStack("Providers");
        var editors = new List<ProviderEditor>();
        var validationInfo = new InfoBar
        {
            Severity = InfoBarSeverity.Error,
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(validationInfo);

        foreach (var providerEditor in viewModel.Editors)
        {
            var editor = CreateProviderEditor(providerEditor);
            editors.Add(editor);
            panel.Children.Add(editor.Root);
        }

        var saveButton = new Button { Content = "Save Providers" };
        saveButton.Click += async (_, _) =>
        {
            foreach (var editor in editors)
            {
                editor.SyncToViewModel();
            }

            var result = viewModel.TryApply();
            if (!result.IsValid)
            {
                validationInfo.Title = "Invalid provider settings";
                validationInfo.Message = string.Join(Environment.NewLine, result.Errors);
                validationInfo.IsOpen = true;
                return;
            }

            await host.SaveConfigAsync(config, CancellationToken.None);
            await host.RefreshNowAsync(CancellationToken.None);
            navigationView.Content = await BuildProvidersPageAsync();
        };
        panel.Children.Add(saveButton);

        return Wrap(panel);
    }

    private ProviderEditor CreateProviderEditor(ProviderSettingsEditorViewModel provider)
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

        header.Children.Add(UiFactory.Text(provider.DisplayName, 16, FontWeights.SemiBold));
        var toggle = new ToggleSwitch { IsOn = provider.IsEnabled, Header = "Enabled" };
        Grid.SetColumn(toggle, 1);
        header.Children.Add(toggle);
        stack.Children.Add(header);

        var sourceCombo = new ComboBox
        {
            Header = "Source",
            MinWidth = 180
        };

        foreach (var source in provider.SupportedSourceNames)
        {
            sourceCombo.Items.Add(source);
        }

        sourceCombo.SelectedItem = provider.SourceKindText;
        stack.Children.Add(sourceCombo);

        var manualGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        manualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        manualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var usedBox = TextBox("Used %", provider.UsedPercentText);
        var remainingBox = TextBox("Remaining %", provider.RemainingPercentText);
        var resetBox = TextBox("Reset datetime", provider.ResetDateTimeText);
        var creditsBox = TextBox("Credits", provider.CreditBalanceText);
        var costBox = TextBox("Month cost", provider.MonthToDateCostText);
        var notesBox = TextBox("Notes", provider.NotesText);

        AddToGrid(manualGrid, usedBox, 0, 0);
        AddToGrid(manualGrid, remainingBox, 0, 1);
        AddToGrid(manualGrid, resetBox, 1, 0);
        AddToGrid(manualGrid, creditsBox, 1, 1);
        AddToGrid(manualGrid, costBox, 2, 0);
        AddToGrid(manualGrid, notesBox, 2, 1);

        stack.Children.Add(manualGrid);

        TextBox? apiKeySecretNameBox = null;
        TextBox? gitHubOrganizationBox = null;
        TextBox? gitHubEnterpriseBox = null;
        TextBox? gitHubPatSecretNameBox = null;

        if (provider.HasApiKeySettings)
        {
            apiKeySecretNameBox = TextBox("API key secret name", provider.ApiKeySecretNameText);
            stack.Children.Add(apiKeySecretNameBox);

            stack.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Message = provider.ApiKeyStatusText
            });
        }

        if (provider.HasGitHubCopilotSettings)
        {
            var copilotGrid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            copilotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            copilotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            gitHubOrganizationBox = TextBox("GitHub organization", provider.GitHubOrganizationText);
            gitHubEnterpriseBox = TextBox("Enterprise slug", provider.GitHubEnterpriseSlugText);
            gitHubPatSecretNameBox = TextBox("PAT secret name", provider.GitHubPatSecretNameText);
            AddToGrid(copilotGrid, gitHubOrganizationBox, 0, 0);
            AddToGrid(copilotGrid, gitHubEnterpriseBox, 0, 1);
            AddToGrid(copilotGrid, gitHubPatSecretNameBox, 1, 0);
            stack.Children.Add(copilotGrid);

            stack.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Message = provider.GitHubCopilotStatusText
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = provider.HelperText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        return new ProviderEditor(
            root,
            provider,
            toggle,
            sourceCombo,
            usedBox,
            remainingBox,
            resetBox,
            creditsBox,
            costBox,
            notesBox,
            apiKeySecretNameBox,
            gitHubOrganizationBox,
            gitHubEnterpriseBox,
            gitHubPatSecretNameBox);
    }

    private async Task<UIElement> BuildAppearancePageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var startupStatus = await host.GetStartupRegistrationStatusAsync(CancellationToken.None);
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

        var startupToggle = new ToggleSwitch
        {
            Header = "Start at login",
            IsOn = config.Startup.LaunchOnLogin || startupStatus.IsEnabled,
            IsEnabled = startupStatus.IsSupported
        };
        panel.Children.Add(startupToggle);

        var startupInfo = new InfoBar
        {
            Severity = startupStatus.IsSupported ? InfoBarSeverity.Informational : InfoBarSeverity.Warning,
            IsOpen = true,
            IsClosable = false,
            Title = "Startup",
            Message = startupStatus.StatusMessage
        };
        panel.Children.Add(startupInfo);

        var save = new Button { Content = "Save Appearance" };
        save.Click += async (_, _) =>
        {
            try
            {
                config.Appearance.Theme = combo.SelectedItem?.ToString() ?? "System";
                config.Startup.LaunchOnLogin = startupToggle.IsOn;
                await host.ApplyStartupRegistrationAsync(startupToggle.IsOn, CancellationToken.None);
                await host.SaveConfigAsync(config, CancellationToken.None);
                var nextStatus = await host.GetStartupRegistrationStatusAsync(CancellationToken.None);
                startupInfo.Severity = InfoBarSeverity.Success;
                startupInfo.Title = "Appearance saved";
                startupInfo.Message = nextStatus.StatusMessage;
                startupInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                startupInfo.Severity = InfoBarSeverity.Error;
                startupInfo.Title = "Appearance save failed";
                startupInfo.Message = ex.Message;
                startupInfo.IsOpen = true;
            }
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

    private async Task<UIElement> BuildWidgetPageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var viewModel = new WidgetSettingsPageViewModel(config.Widget, ProviderDescriptors.All);
        var panel = PageStack("Widget");
        var validationInfo = new InfoBar
        {
            Severity = InfoBarSeverity.Error,
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(validationInfo);

        var showOnStartup = new ToggleSwitch
        {
            Header = "Show widget when app starts",
            IsOn = viewModel.ShowOnStartup
        };
        panel.Children.Add(showOnStartup);

        var topMost = new ToggleSwitch
        {
            Header = "Always on top",
            IsOn = viewModel.TopMost
        };
        panel.Children.Add(topMost);

        panel.Children.Add(UiFactory.Text("Widget providers", 16, FontWeights.SemiBold));
        var providerChecks = new List<(WidgetProviderOptionViewModel Option, CheckBox CheckBox)>();
        foreach (var option in viewModel.ProviderOptions)
        {
            var checkBox = new CheckBox
            {
                Content = option.DisplayName,
                IsChecked = option.IsSelected
            };
            providerChecks.Add((option, checkBox));
            panel.Children.Add(checkBox);
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var save = new Button { Content = "Save Widget" };
        save.Click += async (_, _) =>
        {
            viewModel.ShowOnStartup = showOnStartup.IsOn;
            viewModel.TopMost = topMost.IsOn;
            foreach (var (option, checkBox) in providerChecks)
            {
                option.IsSelected = checkBox.IsChecked == true;
            }

            var result = viewModel.TryApply();
            if (!result.IsValid)
            {
                validationInfo.Title = "Invalid widget settings";
                validationInfo.Message = string.Join(Environment.NewLine, result.Errors);
                validationInfo.Severity = InfoBarSeverity.Error;
                validationInfo.IsOpen = true;
                return;
            }

            await host.SaveConfigAsync(config, CancellationToken.None);
            validationInfo.Title = "Widget settings saved";
            validationInfo.Message = "Reopen the widget to apply provider and always-on-top changes.";
            validationInfo.Severity = InfoBarSeverity.Success;
            validationInfo.IsOpen = true;
        };
        actions.Children.Add(save);

        var show = new Button { Content = "Show Widget" };
        show.Click += (_, _) => host.ShowWidget();
        actions.Children.Add(show);
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

        var exportInfo = new InfoBar
        {
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(exportInfo);

        var exportButton = new Button { Content = "Export Diagnostics" };
        exportButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.ExportDiagnosticsAsync(CancellationToken.None);
                exportInfo.Severity = InfoBarSeverity.Success;
                exportInfo.Title = "Diagnostics exported";
                exportInfo.Message = result.Path;
                exportInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exportInfo.Severity = InfoBarSeverity.Error;
                exportInfo.Title = "Diagnostics export failed";
                exportInfo.Message = ex.Message;
                exportInfo.IsOpen = true;
            }
        };
        panel.Children.Add(exportButton);

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
        ProviderSettingsEditorViewModel ViewModel,
        ToggleSwitch Toggle,
        ComboBox SourceCombo,
        TextBox UsedBox,
        TextBox RemainingBox,
        TextBox ResetBox,
        TextBox CreditsBox,
        TextBox CostBox,
        TextBox NotesBox,
        TextBox? ApiKeySecretNameBox,
        TextBox? GitHubOrganizationBox,
        TextBox? GitHubEnterpriseBox,
        TextBox? GitHubPatSecretNameBox)
    {
        public void SyncToViewModel()
        {
            ViewModel.IsEnabled = Toggle.IsOn;
            ViewModel.SourceKindText = SourceCombo.SelectedItem?.ToString() ?? string.Empty;
            ViewModel.UsedPercentText = UsedBox.Text;
            ViewModel.RemainingPercentText = RemainingBox.Text;
            ViewModel.ResetDateTimeText = ResetBox.Text;
            ViewModel.CreditBalanceText = CreditsBox.Text;
            ViewModel.MonthToDateCostText = CostBox.Text;
            ViewModel.NotesText = NotesBox.Text;
            ViewModel.ApiKeySecretNameText = ApiKeySecretNameBox?.Text ?? string.Empty;
            ViewModel.GitHubOrganizationText = GitHubOrganizationBox?.Text ?? string.Empty;
            ViewModel.GitHubEnterpriseSlugText = GitHubEnterpriseBox?.Text ?? string.Empty;
            ViewModel.GitHubPatSecretNameText = GitHubPatSecretNameBox?.Text ?? string.Empty;
        }
    }
}
