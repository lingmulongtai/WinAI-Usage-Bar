using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Windows;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ThemeHelperTests
{
    [Theory]
    [InlineData("System", ElementTheme.Default)]
    [InlineData("", ElementTheme.Default)]
    [InlineData("Light", ElementTheme.Light)]
    [InlineData("Dark", ElementTheme.Dark)]
    public void ToElementTheme_MapsConfiguredTheme(string theme, ElementTheme expected)
    {
        Assert.Equal(expected, ThemeHelper.ToElementTheme(theme));
    }
}
