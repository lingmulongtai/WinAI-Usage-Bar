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
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Windows;

public sealed class MainWindow : Window
{
    private const int SupportArtifactPruneKeepNewest = 5;

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
        navigationView.MenuItems.Add(CreateItem("Provider Details", Symbol.ContactInfo));
        navigationView.MenuItems.Add(CreateItem("Appearance", Symbol.View));
        navigationView.MenuItems.Add(CreateItem("Widget", Symbol.PreviewLink));
        navigationView.MenuItems.Add(CreateItem("History", Symbol.Calendar));
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
        _ = ApplyConfiguredThemeAsync();
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
        var tag = (navigationView.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag is "Overview" or "Provider Details")
        {
            _ = NavigateAsync(tag);
        }
    }

    private async Task NavigateAsync(string tag)
    {
        navigationView.Content = tag switch
        {
            "Overview" => await BuildOverviewPageAsync(),
            "Providers" => await BuildProvidersPageAsync(),
            "Provider Details" => BuildProviderDetailsPage(),
            "Appearance" => await BuildAppearancePageAsync(),
            "Widget" => await BuildWidgetPageAsync(),
            "History" => await BuildHistoryPageAsync(),
            "Refresh" => await BuildRefreshPageAsync(),
            "Privacy & Data" => await BuildPrivacyPageAsync(),
            "About" => BuildAboutPage(),
            _ => await BuildOverviewPageAsync()
        };
    }

    private async Task<UIElement> BuildOverviewPageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var firstRun = new FirstRunSetupViewModel(config, ProviderDescriptors.All);
        var panel = PageStack("Overview");

        if (firstRun.IsVisible)
        {
            panel.Children.Add(CreateFirstRunSetupPanel(firstRun, config));
        }

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

    private Border CreateFirstRunSetupPanel(
        FirstRunSetupViewModel viewModel,
        AppConfig config)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(UiFactory.Text("First-run setup", 16, FontWeights.SemiBold));
        stack.Children.Add(UiFactory.Text(viewModel.SummaryText, 13));
        stack.Children.Add(UiFactory.Text("Setup checklist", 13, FontWeights.SemiBold));

        foreach (var item in viewModel.ChecklistItems)
        {
            var state = item.IsComplete ? "Done" : "Needs attention";
            stack.Children.Add(UiFactory.Text($"{state}: {item.Title}", 12, FontWeights.SemiBold));
            stack.Children.Add(UiFactory.Text(item.StateText, 12));
            stack.Children.Add(UiFactory.Text(item.ActionText, 12));
            var actionButton = new Button { Content = item.ActionButtonText };
            actionButton.Click += (_, _) => SelectNavigationItem(item.ActionNavigationTag);
            stack.Children.Add(actionButton);
        }

        foreach (var line in viewModel.ProviderLines)
        {
            stack.Children.Add(UiFactory.Text(line, 12));
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var providersButton = new Button { Content = "Open Providers" };
        providersButton.Click += (_, _) => SelectNavigationItem("Providers");
        actions.Children.Add(providersButton);

        var completeButton = new Button { Content = "Mark Setup Complete" };
        completeButton.Click += async (_, _) =>
        {
            viewModel.MarkComplete();
            await host.SaveConfigAsync(config, CancellationToken.None);
            navigationView.Content = await BuildOverviewPageAsync();
        };
        actions.Children.Add(completeButton);
        stack.Children.Add(actions);

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 120, 120, 120)),
            Child = stack
        };
    }

    private void SelectNavigationItem(string tag)
    {
        foreach (var item in navigationView.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                navigationView.SelectedItem = item;
                return;
            }
        }

        _ = NavigateAsync(tag);
    }

    private UIElement BuildProviderDetailsPage()
    {
        var viewModel = new ProviderDetailsPageViewModel(
            host.ViewModel.Providers.Select(provider => provider.Snapshot));
        var panel = PageStack("Provider Details");

        if (!viewModel.HasProviders)
        {
            panel.Children.Add(UiFactory.Text(viewModel.EmptyText, 14));
        }
        else
        {
            foreach (var provider in viewModel.Providers)
            {
                panel.Children.Add(CreateProviderDetailRow(provider));
            }
        }

        return Wrap(panel);
    }

    private static Border CreateProviderDetailRow(ProviderDetailsRowViewModel provider)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(UiFactory.Text(provider.DisplayName, 16, FontWeights.SemiBold));
        AddDetailSection(stack, "Snapshot", provider.SummaryLines);
        AddDetailSection(stack, "Identity", provider.IdentityLines);
        AddDetailSection(stack, "Usage", provider.UsageLines);
        AddDetailSection(stack, "Credits", provider.CreditLines);
        if (provider.HasRepairLines)
        {
            AddDetailSection(stack, "Repair guidance", provider.RepairLines);
        }

        if (provider.HasStatusText)
        {
            stack.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Title = "Status",
                Message = provider.StatusText
            });
        }

        if (provider.HasErrorText)
        {
            stack.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Error,
                IsOpen = true,
                IsClosable = false,
                Title = "Error",
                Message = provider.ErrorText
            });
        }

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 120, 120, 120)),
            Child = stack
        };
    }

    private static void AddDetailSection(
        StackPanel stack,
        string title,
        IReadOnlyList<string> lines)
    {
        stack.Children.Add(UiFactory.Text(title, 13, FontWeights.SemiBold));
        foreach (var line in lines)
        {
            stack.Children.Add(UiFactory.Text(line, 12));
        }
    }

    private static void RefreshProviderSetupGuidance(
        StackPanel guidancePanel,
        ProviderSettingsEditorViewModel provider)
    {
        guidancePanel.Children.Clear();
        AddDetailSection(guidancePanel, "Setup guidance", provider.SetupGuidanceLines);
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

        var guidancePanel = new StackPanel { Spacing = 4 };
        RefreshProviderSetupGuidance(guidancePanel, provider);
        stack.Children.Add(guidancePanel);

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
        var resetDescriptionBox = TextBox("Reset description", provider.ResetDescriptionText);
        var creditsBox = TextBox("Credits", provider.CreditBalanceText);
        var currencyBox = TextBox("Currency/unit", provider.CurrencyText);
        var costBox = TextBox("Month cost", provider.MonthToDateCostText);
        var tokensBox = TextBox("Tokens last 31 days", provider.TokensLast31DaysText);
        var notesBox = TextBox("Notes", provider.NotesText);

        AddToGrid(manualGrid, usedBox, 0, 0);
        AddToGrid(manualGrid, remainingBox, 0, 1);
        AddToGrid(manualGrid, resetBox, 1, 0);
        AddToGrid(manualGrid, resetDescriptionBox, 1, 1);
        AddToGrid(manualGrid, creditsBox, 2, 0);
        AddToGrid(manualGrid, currencyBox, 2, 1);
        AddToGrid(manualGrid, costBox, 3, 0);
        AddToGrid(manualGrid, tokensBox, 3, 1);
        AddToGrid(manualGrid, notesBox, 4, 0);

        stack.Children.Add(manualGrid);

        TextBox? apiKeySecretNameBox = null;
        TextBox? gitHubOrganizationBox = null;
        TextBox? gitHubEnterpriseBox = null;
        TextBox? gitHubPatSecretNameBox = null;
        InfoBar? apiKeyInfo = null;
        InfoBar? gitHubCopilotInfo = null;

        if (provider.HasApiKeySettings)
        {
            apiKeySecretNameBox = TextBox("API key secret name", provider.ApiKeySecretNameText);
            stack.Children.Add(apiKeySecretNameBox);

            apiKeyInfo = new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Message = provider.ApiKeyStatusText
            };
            stack.Children.Add(apiKeyInfo);
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

            gitHubCopilotInfo = new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Message = provider.GitHubCopilotStatusText
            };
            stack.Children.Add(gitHubCopilotInfo);
        }

        stack.Children.Add(new TextBlock
        {
            Text = provider.HelperText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        void RefreshGuidanceFromControls()
        {
            provider.IsEnabled = toggle.IsOn;
            provider.SourceKindText = sourceCombo.SelectedItem?.ToString() ?? string.Empty;
            provider.ApiKeySecretNameText = apiKeySecretNameBox?.Text ?? string.Empty;
            provider.GitHubOrganizationText = gitHubOrganizationBox?.Text ?? string.Empty;
            provider.GitHubEnterpriseSlugText = gitHubEnterpriseBox?.Text ?? string.Empty;
            provider.GitHubPatSecretNameText = gitHubPatSecretNameBox?.Text ?? string.Empty;
            RefreshProviderSetupGuidance(guidancePanel, provider);

            if (apiKeyInfo is not null)
            {
                apiKeyInfo.Message = provider.ApiKeyStatusText;
            }

            if (gitHubCopilotInfo is not null)
            {
                gitHubCopilotInfo.Message = provider.GitHubCopilotStatusText;
            }
        }

        toggle.Toggled += (_, _) => RefreshGuidanceFromControls();
        sourceCombo.SelectionChanged += (_, _) => RefreshGuidanceFromControls();
        if (apiKeySecretNameBox is not null)
        {
            apiKeySecretNameBox.TextChanged += (_, _) => RefreshGuidanceFromControls();
        }

        if (gitHubOrganizationBox is not null)
        {
            gitHubOrganizationBox.TextChanged += (_, _) => RefreshGuidanceFromControls();
        }

        if (gitHubEnterpriseBox is not null)
        {
            gitHubEnterpriseBox.TextChanged += (_, _) => RefreshGuidanceFromControls();
        }

        if (gitHubPatSecretNameBox is not null)
        {
            gitHubPatSecretNameBox.TextChanged += (_, _) => RefreshGuidanceFromControls();
        }

        return new ProviderEditor(
            root,
            provider,
            toggle,
            sourceCombo,
            usedBox,
            remainingBox,
            resetBox,
            resetDescriptionBox,
            creditsBox,
            currencyBox,
            costBox,
            tokensBox,
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
                ApplyTheme(config.Appearance.Theme);
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

    private async Task ApplyConfiguredThemeAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        ApplyTheme(config.Appearance.Theme);
    }

    private void ApplyTheme(string theme)
    {
        ThemeHelper.Apply(navigationView, theme);
    }

    private async Task<UIElement> BuildRefreshPageAsync()
    {
        var config = await host.LoadConfigAsync(CancellationToken.None);
        var viewModel = new RefreshSettingsPageViewModel(config);
        var panel = PageStack("Refresh");
        var validationInfo = new InfoBar
        {
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(validationInfo);

        var combo = new ComboBox
        {
            Header = "Interval",
            MinWidth = 220
        };

        foreach (var value in Enum.GetValues<RefreshIntervalKind>())
        {
            combo.Items.Add(value.ToString());
        }

        combo.SelectedItem = viewModel.IntervalText;
        panel.Children.Add(combo);

        var notifications = new ToggleSwitch
        {
            Header = "Enable notifications",
            IsOn = viewModel.NotificationsEnabled
        };
        panel.Children.Add(notifications);

        panel.Children.Add(new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Title = "Startup updates",
            Message = viewModel.UpdateStatusText
        });

        var checkUpdatesOnStartup = new ToggleSwitch
        {
            Header = "Check for updates on startup",
            IsOn = viewModel.CheckUpdatesOnStartup
        };
        panel.Children.Add(checkUpdatesOnStartup);

        var updateCheckIntervalHoursBox = TextBox(
            "Startup update interval hours",
            viewModel.UpdateCheckIntervalHoursText);
        panel.Children.Add(updateCheckIntervalHoursBox);

        var downloadUpdatesAutomatically = new ToggleSwitch
        {
            Header = "Download verified updates automatically",
            IsOn = viewModel.DownloadUpdatesAutomatically
        };
        panel.Children.Add(downloadUpdatesAutomatically);

        var installUpdatesAutomatically = new ToggleSwitch
        {
            Header = "Launch prepared update install automatically",
            IsOn = viewModel.InstallUpdatesAutomatically
        };
        panel.Children.Add(installUpdatesAutomatically);

        var confirmManualInstall = new CheckBox
        {
            Content = "Confirm manual latest-update install launch"
        };
        panel.Children.Add(confirmManualInstall);

        var retentionGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        retentionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        retentionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var maxDaysBox = TextBox("History max days", viewModel.HistoryMaxDaysText);
        var maxBytesBox = TextBox("History max bytes", viewModel.HistoryMaxBytesText);
        AddToGrid(retentionGrid, maxDaysBox, 0, 0);
        AddToGrid(retentionGrid, maxBytesBox, 0, 1);
        panel.Children.Add(retentionGrid);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var save = new Button { Content = "Save Refresh" };
        save.Click += async (_, _) =>
        {
            viewModel.IntervalText = combo.SelectedItem?.ToString() ?? string.Empty;
            viewModel.NotificationsEnabled = notifications.IsOn;
            viewModel.HistoryMaxDaysText = maxDaysBox.Text;
            viewModel.HistoryMaxBytesText = maxBytesBox.Text;
            viewModel.CheckUpdatesOnStartup = checkUpdatesOnStartup.IsOn;
            viewModel.UpdateCheckIntervalHoursText = updateCheckIntervalHoursBox.Text;
            viewModel.DownloadUpdatesAutomatically = downloadUpdatesAutomatically.IsOn;
            viewModel.InstallUpdatesAutomatically = installUpdatesAutomatically.IsOn;

            var result = viewModel.TryApply();
            if (!result.IsValid)
            {
                validationInfo.Severity = InfoBarSeverity.Error;
                validationInfo.Title = "Invalid refresh settings";
                validationInfo.Message = string.Join(Environment.NewLine, result.Errors);
                validationInfo.IsOpen = true;
                return;
            }

            await host.SaveConfigAsync(config, CancellationToken.None);
            await host.RestartRefreshScheduleAsync(CancellationToken.None);
            validationInfo.Severity = result.Warnings.Count > 0
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;
            validationInfo.Title = "Refresh settings saved";
            validationInfo.Message = result.Warnings.Count > 0
                ? string.Join(Environment.NewLine, result.Warnings)
                : "Refresh, notification, history, and update settings were saved.";
            validationInfo.IsOpen = true;
        };
        actions.Children.Add(save);

        var refresh = new Button { Content = "Refresh Now" };
        refresh.Click += (_, _) => _ = host.RefreshNowAsync(CancellationToken.None);
        actions.Children.Add(refresh);

        var checkUpdates = new Button { Content = "Check for Updates Now" };
        checkUpdates.Click += async (_, _) =>
        {
            try
            {
                var result = await host.CheckForUpdatesNowAsync(CancellationToken.None);
                validationInfo.Severity = SeverityForUpdateCheck(result);
                validationInfo.Title = "Update check complete";
                validationInfo.Message = FormatUpdateCheckResult(result);
                validationInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                validationInfo.Severity = InfoBarSeverity.Error;
                validationInfo.Title = "Update check failed";
                validationInfo.Message = ex.Message;
                validationInfo.IsOpen = true;
            }
        };
        actions.Children.Add(checkUpdates);

        var installLatestUpdate = new Button
        {
            Content = "Install Latest Update Now",
            IsEnabled = false
        };
        confirmManualInstall.Checked += (_, _) => installLatestUpdate.IsEnabled = true;
        confirmManualInstall.Unchecked += (_, _) => installLatestUpdate.IsEnabled = false;
        installLatestUpdate.Click += async (_, _) =>
        {
            try
            {
                var result = await host.InstallLatestUpdateNowAsync(
                    restartAfterInstall: true,
                    CancellationToken.None);
                validationInfo.Severity = SeverityForLatestUpdateInstall(result);
                validationInfo.Title = "Latest update install checked";
                validationInfo.Message = CommandLineLatestUpdateInstallFormatter.Format(result);
                validationInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                validationInfo.Severity = InfoBarSeverity.Error;
                validationInfo.Title = "Latest update install failed";
                validationInfo.Message = ex.Message;
                validationInfo.IsOpen = true;
            }
        };
        actions.Children.Add(installLatestUpdate);
        panel.Children.Add(actions);

        return Wrap(panel);
    }

    private async Task<UIElement> BuildHistoryPageAsync()
    {
        var viewModel = new HistorySummaryViewModel(
            await host.GetHistorySummaryAsync(CancellationToken.None));
        var panel = PageStack("History");
        panel.Children.Add(new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Title = "Retained history",
            Message = viewModel.SummaryText
        });

        panel.Children.Add(new InfoBar
        {
            Severity = viewModel.InvalidLines == 0 ? InfoBarSeverity.Informational : InfoBarSeverity.Warning,
            IsOpen = true,
            IsClosable = false,
            Title = "History integrity",
            Message = viewModel.InvalidLineText
        });

        if (viewModel.Providers.Count == 0)
        {
            panel.Children.Add(UiFactory.Text("No provider history has been retained yet.", 14));
        }
        else
        {
            foreach (var provider in viewModel.Providers)
            {
                panel.Children.Add(CreateHistoryProviderRow(provider));
            }
        }

        return Wrap(panel);
    }

    private static Border CreateHistoryProviderRow(ProviderHistoryRowViewModel provider)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(UiFactory.Text(provider.DisplayName, 15, FontWeights.SemiBold));
        stack.Children.Add(UiFactory.Text(provider.EntryText, 12));
        stack.Children.Add(UiFactory.Text(provider.LatestText, 12));
        stack.Children.Add(UiFactory.Text(provider.HealthText, 12));
        stack.Children.Add(UiFactory.Text(provider.RemainingText, 12));
        stack.Children.Add(UiFactory.Text(provider.SourceText, 12));

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 120, 120, 120)),
            Child = stack
        };
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

    private async Task<UIElement> BuildPrivacyPageAsync()
    {
        var summary = await host.GetDiagnosticsSummaryAsync(CancellationToken.None);
        var diagnosticsSummary = new DiagnosticsSummaryViewModel(summary);
        var recoveryGuidance = new RecoveryGuidanceService().CreateGuidance(summary);
        var storagePressure = new StoragePressureGuidanceService().CreateGuidance(summary);
        var panel = PageStack("Privacy & Data");
        panel.Children.Add(new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsOpen = true,
            IsClosable = false,
            Title = "Local storage",
            Message = diagnosticsSummary.RootDirectory
        });

        panel.Children.Add(UiFactory.Text("Diagnostics summary", 16, FontWeights.SemiBold));
        foreach (var line in diagnosticsSummary.OverviewLines)
        {
            panel.Children.Add(UiFactory.Text(line, 12));
        }

        panel.Children.Add(UiFactory.Text("Tracked files", 16, FontWeights.SemiBold));
        foreach (var file in diagnosticsSummary.Files)
        {
            panel.Children.Add(UiFactory.Text($"{file.Label}: {file.StatusText}", 12));
        }

        panel.Children.Add(UiFactory.Text("Storage pressure", 16, FontWeights.SemiBold));
        foreach (var item in storagePressure)
        {
            panel.Children.Add(UiFactory.Text(
                $"{item.Title} - {item.Level}",
                13,
                FontWeights.SemiBold));
            panel.Children.Add(UiFactory.Text(item.Detail, 12));
            panel.Children.Add(UiFactory.Text(item.Recommendation, 12));
        }

        panel.Children.Add(UiFactory.Text("Recovery guidance", 16, FontWeights.SemiBold));
        foreach (var item in recoveryGuidance)
        {
            panel.Children.Add(UiFactory.Text(
                $"{item.Title} - {(item.IsAvailable ? "Available" : "Not ready")}",
                13,
                FontWeights.SemiBold));
            panel.Children.Add(UiFactory.Text(item.Recommendation, 12));
            panel.Children.Add(UiFactory.Text(item.SafetyNote, 12));
        }

        panel.Children.Add(UiFactory.Text("Secrets are stored through the secret store abstraction and are not written to config.json.", 14));
        panel.Children.Add(UiFactory.Text("Browser cookie scraping is not implemented in this MVP.", 14));

        var secretEditor = new SecretEditorViewModel();
        var secretInfo = new InfoBar
        {
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(secretInfo);

        var secretGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        secretGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        secretGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var secretNameBox = TextBox("Secret name", string.Empty);
        var secretValueBox = new PasswordBox
        {
            Header = "Secret value",
            MinWidth = 160
        };
        AddToGrid(secretGrid, secretNameBox, 0, 0);
        AddToGrid(secretGrid, secretValueBox, 0, 1);
        panel.Children.Add(secretGrid);

        var secretActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var saveSecretButton = new Button { Content = "Save Secret" };
        saveSecretButton.Click += async (_, _) =>
        {
            secretEditor.SecretNameText = secretNameBox.Text;
            secretEditor.SecretValueText = secretValueBox.Password;
            var result = secretEditor.ValidateSave();
            if (!result.IsValid || result.SecretName is null || result.SecretValue is null)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Invalid secret";
                secretInfo.Message = string.Join(Environment.NewLine, result.Errors);
                secretInfo.IsOpen = true;
                return;
            }

            try
            {
                await host.SetSecretAsync(result.SecretName, result.SecretValue, CancellationToken.None);
                secretValueBox.Password = string.Empty;
                secretInfo.Severity = InfoBarSeverity.Success;
                secretInfo.Title = "Secret saved";
                secretInfo.Message = "The secret value was stored in the Windows user-protected secret store.";
                secretInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Secret save failed";
                secretInfo.Message = ex.Message;
                secretInfo.IsOpen = true;
            }
        };
        secretActions.Children.Add(saveSecretButton);

        var checkSecretButton = new Button { Content = "Check Secret" };
        checkSecretButton.Click += async (_, _) =>
        {
            secretEditor.SecretNameText = secretNameBox.Text;
            var result = secretEditor.ValidateDelete();
            if (!result.IsValid || result.SecretName is null)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Invalid secret";
                secretInfo.Message = string.Join(Environment.NewLine, result.Errors);
                secretInfo.IsOpen = true;
                return;
            }

            try
            {
                var exists = await host.HasSecretAsync(result.SecretName, CancellationToken.None);
                secretInfo.Severity = exists ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
                secretInfo.Title = exists ? "Secret found" : "Secret missing";
                secretInfo.Message = exists
                    ? "A value exists for that secret name."
                    : "No value exists for that secret name.";
                secretInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Secret check failed";
                secretInfo.Message = ex.Message;
                secretInfo.IsOpen = true;
            }
        };
        secretActions.Children.Add(checkSecretButton);

        var deleteSecretButton = new Button { Content = "Delete Secret" };
        deleteSecretButton.Click += async (_, _) =>
        {
            secretEditor.SecretNameText = secretNameBox.Text;
            var result = secretEditor.ValidateDelete();
            if (!result.IsValid || result.SecretName is null)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Invalid secret";
                secretInfo.Message = string.Join(Environment.NewLine, result.Errors);
                secretInfo.IsOpen = true;
                return;
            }

            try
            {
                await host.DeleteSecretAsync(result.SecretName, CancellationToken.None);
                secretValueBox.Password = string.Empty;
                secretInfo.Severity = InfoBarSeverity.Success;
                secretInfo.Title = "Secret deleted";
                secretInfo.Message = "The secret value was removed from the secret store.";
                secretInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                secretInfo.Severity = InfoBarSeverity.Error;
                secretInfo.Title = "Secret delete failed";
                secretInfo.Message = ex.Message;
                secretInfo.IsOpen = true;
            }
        };
        secretActions.Children.Add(deleteSecretButton);
        panel.Children.Add(secretActions);

        panel.Children.Add(UiFactory.Text("Data maintenance", 16, FontWeights.SemiBold));
        panel.Children.Add(UiFactory.Text("Clear local cache files without deleting config.json or saved secrets.", 14));
        var maintenanceInfo = new InfoBar
        {
            IsOpen = false,
            IsClosable = true
        };
        panel.Children.Add(maintenanceInfo);

        var maintenanceActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var clearSnapshotsButton = new Button { Content = "Clear Snapshot Cache" };
        clearSnapshotsButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.ClearSnapshotsAsync(CancellationToken.None);
                maintenanceInfo.Severity = InfoBarSeverity.Success;
                maintenanceInfo.Title = result.Deleted ? "Snapshot cache cleared" : "Snapshot cache already clear";
                maintenanceInfo.Message = Path.GetFileName(result.Path);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Snapshot cache clear failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        maintenanceActions.Children.Add(clearSnapshotsButton);

        var clearHistoryButton = new Button { Content = "Clear History" };
        clearHistoryButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.ClearHistoryAsync(CancellationToken.None);
                maintenanceInfo.Severity = InfoBarSeverity.Success;
                maintenanceInfo.Title = result.Deleted ? "History cleared" : "History already clear";
                maintenanceInfo.Message = Path.GetFileName(result.Path);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "History clear failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        maintenanceActions.Children.Add(clearHistoryButton);

        panel.Children.Add(maintenanceActions);

        panel.Children.Add(UiFactory.Text(
            $"Prune retained support artifacts while keeping the newest {SupportArtifactPruneKeepNewest} files.",
            14));
        var pruneActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var pruneBackupsButton = new Button { Content = "Prune Old Backups" };
        pruneBackupsButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.PruneConfigBackupsAsync(
                    SupportArtifactPruneKeepNewest,
                    CancellationToken.None);
                maintenanceInfo.Severity = InfoBarSeverity.Success;
                maintenanceInfo.Title = result.DeletedCount > 0
                    ? "Old config backups pruned"
                    : "No old config backups pruned";
                maintenanceInfo.Message = FormatPruneResult(result);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Config backup pruning failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        pruneActions.Children.Add(pruneBackupsButton);

        var pruneDiagnosticsExportsButton = new Button { Content = "Prune Old Diagnostics Exports" };
        pruneDiagnosticsExportsButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.PruneDiagnosticsExportsAsync(
                    SupportArtifactPruneKeepNewest,
                    CancellationToken.None);
                maintenanceInfo.Severity = InfoBarSeverity.Success;
                maintenanceInfo.Title = result.DeletedCount > 0
                    ? "Old diagnostics exports pruned"
                    : "No old diagnostics exports pruned";
                maintenanceInfo.Message = FormatPruneResult(result);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Diagnostics export pruning failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        pruneActions.Children.Add(pruneDiagnosticsExportsButton);
        panel.Children.Add(pruneActions);

        var backupActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var backupConfigButton = new Button { Content = "Export Config Backup" };
        backupConfigButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.ExportConfigBackupAsync(CancellationToken.None);
                maintenanceInfo.Severity = InfoBarSeverity.Success;
                maintenanceInfo.Title = "Config backup exported";
                maintenanceInfo.Message = result.Path;
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Config backup export failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        backupActions.Children.Add(backupConfigButton);

        var validateLatestBackupButton = new Button { Content = "Validate Latest Backup" };
        validateLatestBackupButton.Click += async (_, _) =>
        {
            try
            {
                var result = await host.ValidateLatestConfigBackupAsync(CancellationToken.None);
                maintenanceInfo.Severity = result.IsValid ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                maintenanceInfo.Title = result.IsValid ? "Config backup is valid" : "Config backup is invalid";
                maintenanceInfo.Message = CommandLineConfigBackupValidationFormatter.Format(result);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Config backup validation failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
        };
        backupActions.Children.Add(validateLatestBackupButton);

        var restoreLatestBackupButton = new Button
        {
            Content = "Restore Latest Backup",
            IsEnabled = false
        };
        var restoreConfirm = new CheckBox
        {
            Content = "Confirm restore",
            MinWidth = 120
        };
        restoreConfirm.Checked += (_, _) => restoreLatestBackupButton.IsEnabled = true;
        restoreConfirm.Unchecked += (_, _) => restoreLatestBackupButton.IsEnabled = false;
        restoreLatestBackupButton.Click += async (_, _) =>
        {
            try
            {
                restoreLatestBackupButton.IsEnabled = false;
                var result = await host.RestoreLatestConfigBackupAsync(CancellationToken.None);
                maintenanceInfo.Severity = result.Restored ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                maintenanceInfo.Title = result.Restored ? "Config backup restored" : "Config backup not restored";
                maintenanceInfo.Message = result.Restored
                    ? $"{CommandLineConfigBackupRestoreFormatter.Format(result)}{Environment.NewLine}Reopen settings to see restored values."
                    : CommandLineConfigBackupRestoreFormatter.Format(result);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Config backup restore failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
            finally
            {
                restoreConfirm.IsChecked = false;
            }
        };
        backupActions.Children.Add(restoreConfirm);
        backupActions.Children.Add(restoreLatestBackupButton);
        panel.Children.Add(backupActions);

        var resetActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var resetConfigButton = new Button
        {
            Content = "Reset Config To Defaults",
            IsEnabled = false
        };
        var resetConfirm = new CheckBox
        {
            Content = "Confirm reset",
            MinWidth = 120
        };
        resetConfirm.Checked += (_, _) => resetConfigButton.IsEnabled = true;
        resetConfirm.Unchecked += (_, _) => resetConfigButton.IsEnabled = false;
        resetConfigButton.Click += async (_, _) =>
        {
            try
            {
                resetConfigButton.IsEnabled = false;
                var result = await host.ResetConfigToDefaultsAsync(CancellationToken.None);
                maintenanceInfo.Severity = result.Reset ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                maintenanceInfo.Title = result.Reset ? "Config reset to defaults" : "Config reset did not run";
                maintenanceInfo.Message = FormatConfigResetResult(result);
                maintenanceInfo.IsOpen = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                maintenanceInfo.Severity = InfoBarSeverity.Error;
                maintenanceInfo.Title = "Config reset failed";
                maintenanceInfo.Message = ex.Message;
                maintenanceInfo.IsOpen = true;
            }
            finally
            {
                resetConfirm.IsChecked = false;
            }
        };
        resetActions.Children.Add(resetConfirm);
        resetActions.Children.Add(resetConfigButton);
        panel.Children.Add(resetActions);

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
        var appInfo = AppInfoProvider.Get();
        var panel = PageStack("About");
        panel.Children.Add(UiFactory.Text("WinAI Usage Bar", 22, FontWeights.SemiBold));
        panel.Children.Add(UiFactory.Text("MVP desktop usage monitor for AI providers.", 14));
        panel.Children.Add(UiFactory.Text($"Version {appInfo.InformationalVersion}", 14));
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

    private static string FormatConfigResetResult(ConfigResetResult result)
    {
        var lines = new List<string>
        {
            result.Reset ? "Config reset: reset" : "Config reset: not reset",
            $"Rollback backup: {result.RollbackBackupPath}",
            $"Config version: {result.ConfigVersion}",
            $"Providers: {result.EnabledProviderCount} enabled / {result.ProviderCount} configured",
            "Reopen settings to see default values."
        };

        foreach (var warning in result.Warnings)
        {
            lines.Add($"Warning: {warning}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPruneResult(DataPruneResult result)
    {
        return string.Join(
            Environment.NewLine,
            $"Matched files: {result.MatchedCount}",
            $"Kept newest: {result.KeptCount}",
            $"Deleted: {result.DeletedCount}",
            $"Freed: {DiagnosticsSummaryViewModel.FormatBytes(result.DeletedBytes)}",
            $"Directory: {result.DirectoryPath}");
    }

    private static InfoBarSeverity SeverityForUpdateCheck(ReleaseUpdateCheckResult result)
    {
        if (result.Status is UpdateCheckStatus.Error or UpdateCheckStatus.InvalidRelease)
        {
            return InfoBarSeverity.Error;
        }

        if (result.IsUpdateAvailable || result.Status is UpdateCheckStatus.MissingAssets)
        {
            return InfoBarSeverity.Warning;
        }

        return InfoBarSeverity.Success;
    }

    private static string FormatUpdateCheckResult(ReleaseUpdateCheckResult result)
    {
        var lines = new List<string>
        {
            result.Message,
            $"Status: {result.Status}",
            $"Current version: {result.CurrentVersion}",
            $"Latest version: {result.LatestVersion ?? "n/a"}"
        };

        if (result.ReleasePageUrl is not null)
        {
            lines.Add($"Release: {result.ReleasePageUrl}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static InfoBarSeverity SeverityForLatestUpdateInstall(LatestUpdateInstallResult result)
    {
        return result.Status switch
        {
            LatestUpdateInstallStatus.Launched => InfoBarSeverity.Success,
            LatestUpdateInstallStatus.SkippedNoUpdate => InfoBarSeverity.Success,
            LatestUpdateInstallStatus.UpdateCheckFailed => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error
        };
    }

    private sealed record ProviderEditor(
        Border Root,
        ProviderSettingsEditorViewModel ViewModel,
        ToggleSwitch Toggle,
        ComboBox SourceCombo,
        TextBox UsedBox,
        TextBox RemainingBox,
        TextBox ResetBox,
        TextBox ResetDescriptionBox,
        TextBox CreditsBox,
        TextBox CurrencyBox,
        TextBox CostBox,
        TextBox TokensBox,
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
            ViewModel.ResetDescriptionText = ResetDescriptionBox.Text;
            ViewModel.CreditBalanceText = CreditsBox.Text;
            ViewModel.CurrencyText = CurrencyBox.Text;
            ViewModel.MonthToDateCostText = CostBox.Text;
            ViewModel.TokensLast31DaysText = TokensBox.Text;
            ViewModel.NotesText = NotesBox.Text;
            ViewModel.ApiKeySecretNameText = ApiKeySecretNameBox?.Text ?? string.Empty;
            ViewModel.GitHubOrganizationText = GitHubOrganizationBox?.Text ?? string.Empty;
            ViewModel.GitHubEnterpriseSlugText = GitHubEnterpriseBox?.Text ?? string.Empty;
            ViewModel.GitHubPatSecretNameText = GitHubPatSecretNameBox?.Text ?? string.Empty;
        }
    }
}
