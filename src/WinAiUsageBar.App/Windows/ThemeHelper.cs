using Microsoft.UI.Xaml;

namespace WinAiUsageBar.App.Windows;

public static class ThemeHelper
{
    public static ElementTheme ToElementTheme(string? theme)
    {
        return theme?.Trim() switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    public static void Apply(UIElement? element, string? theme)
    {
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.RequestedTheme = ToElementTheme(theme);
        }
    }
}
