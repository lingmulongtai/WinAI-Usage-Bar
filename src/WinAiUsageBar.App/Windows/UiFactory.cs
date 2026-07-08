using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinAiUsageBar.App.ViewModels;
using Windows.UI;
using Windows.UI.Text;

namespace WinAiUsageBar.App.Windows;

public static class UiFactory
{
    public static Button IconButton(Symbol symbol, string tooltip)
    {
        var button = new Button
        {
            Content = new SymbolIcon(symbol),
            MinWidth = 36,
            Padding = new Thickness(8)
        };

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    public static TextBlock Text(string value, double size = 14, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = value,
            FontSize = size,
            FontWeight = weight ?? Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap
        };
    }

    public static Border ProviderCard(ProviderCardViewModel provider)
    {
        var root = new StackPanel
        {
            Spacing = 6
        };

        var header = new Grid
        {
            ColumnSpacing = 8
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = Text(provider.DisplayName, 15, Microsoft.UI.Text.FontWeights.SemiBold);
        var health = Text(provider.HealthText, 12);
        health.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(health, 1);
        header.Children.Add(name);
        header.Children.Add(health);

        root.Children.Add(header);
        root.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = provider.ProgressValue,
            Height = 8
        });
        root.Children.Add(Text(provider.PercentText, 13));
        root.Children.Add(Text(provider.ResetText, 12));

        if (provider.HasStatusMessage)
        {
            root.Children.Add(Text(provider.StatusText, 12));
        }

        if (!string.IsNullOrWhiteSpace(provider.CreditsLine))
        {
            root.Children.Add(Text(provider.CreditsLine, 12));
        }

        root.Children.Add(Text($"{provider.SourceText} / {provider.UpdatedText}", 12));
        if (provider.HasTimestampWarning)
        {
            root.Children.Add(Text(provider.TimestampWarningText, 12));
        }

        if (provider.HasError)
        {
            root.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Error,
                IsOpen = true,
                IsClosable = false,
                Message = provider.ErrorMessage
            });
        }

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 120, 120, 120)),
            Child = root
        };
    }
}
