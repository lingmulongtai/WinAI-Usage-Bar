using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinAiUsageBar.App.Windows;

public sealed class SimpleInfoBar : StackPanel
{
    private readonly TextBlock titleBlock = new()
    {
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap
    };

    private readonly TextBlock messageBlock = new()
    {
        TextWrapping = TextWrapping.Wrap
    };

    private InfoBarSeverity severity = InfoBarSeverity.Informational;
    private string? title;
    private string? message;
    private bool isOpen;

    public SimpleInfoBar()
    {
        Spacing = 2;
        Margin = new Thickness(0, 0, 0, 8);
        Children.Add(titleBlock);
        Children.Add(messageBlock);
        Update();
    }

    public InfoBarSeverity Severity
    {
        get => severity;
        set
        {
            severity = value;
            Update();
        }
    }

    public bool IsOpen
    {
        get => isOpen;
        set
        {
            isOpen = value;
            Update();
        }
    }

    public bool IsClosable { get; set; }

    public string? Title
    {
        get => title;
        set
        {
            title = value;
            Update();
        }
    }

    public string? Message
    {
        get => message;
        set
        {
            message = value;
            Update();
        }
    }

    private void Update()
    {
        Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        titleBlock.Text = string.IsNullOrWhiteSpace(title)
            ? SeverityLabel(severity)
            : $"{SeverityLabel(severity)}: {title}";
        messageBlock.Text = message ?? string.Empty;
        messageBlock.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string SeverityLabel(InfoBarSeverity severity)
    {
        return severity switch
        {
            InfoBarSeverity.Success => "Success",
            InfoBarSeverity.Warning => "Warning",
            InfoBarSeverity.Error => "Error",
            _ => "Info"
        };
    }
}
