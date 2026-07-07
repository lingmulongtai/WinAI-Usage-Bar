using System.Drawing;
using System.Windows.Forms;

namespace WinAiUsageBar.Infrastructure.Windows;

public interface ICompactPanelPlacementService
{
    WindowPlacement Calculate(int width, int height);
}

public sealed class CompactPanelPlacementService : ICompactPanelPlacementService
{
    private const int DefaultMargin = 12;

    public WindowPlacement Calculate(int width, int height)
    {
        try
        {
            var screen = Screen.FromPoint(Cursor.Position);
            return CompactPanelPlacementCalculator.Calculate(
                ToBounds(screen.Bounds),
                ToBounds(screen.WorkingArea),
                width,
                height,
                DefaultMargin);
        }
        catch
        {
            return CalculateFallback(width, height);
        }
    }

    private static WindowPlacement CalculateFallback(int width, int height)
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return new WindowPlacement(80, 80, width, height, TopMost: false);
        }

        return CompactPanelPlacementCalculator.Center(
            ToBounds(screen.WorkingArea),
            width,
            height);
    }

    private static CompactPanelBounds ToBounds(Rectangle rectangle)
    {
        return new CompactPanelBounds(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
    }
}

public static class CompactPanelPlacementCalculator
{
    public static WindowPlacement Calculate(
        CompactPanelBounds monitorBounds,
        CompactPanelBounds workArea,
        int width,
        int height,
        int margin)
    {
        var edge = DetectTaskbarEdge(monitorBounds, workArea);
        var left = edge switch
        {
            TaskbarEdge.Left => workArea.Left + margin,
            _ => workArea.Right - width - margin
        };
        var top = edge switch
        {
            TaskbarEdge.Top => workArea.Top + margin,
            _ => workArea.Bottom - height - margin
        };

        return Clamp(workArea, left, top, width, height);
    }

    public static WindowPlacement Center(CompactPanelBounds workArea, int width, int height)
    {
        var left = workArea.Left + (workArea.Width - width) / 2;
        var top = workArea.Top + (workArea.Height - height) / 2;
        return Clamp(workArea, left, top, width, height);
    }

    private static TaskbarEdge DetectTaskbarEdge(
        CompactPanelBounds monitorBounds,
        CompactPanelBounds workArea)
    {
        if (workArea.Bottom < monitorBounds.Bottom)
        {
            return TaskbarEdge.Bottom;
        }

        if (workArea.Top > monitorBounds.Top)
        {
            return TaskbarEdge.Top;
        }

        if (workArea.Left > monitorBounds.Left)
        {
            return TaskbarEdge.Left;
        }

        if (workArea.Right < monitorBounds.Right)
        {
            return TaskbarEdge.Right;
        }

        return TaskbarEdge.Unknown;
    }

    private static WindowPlacement Clamp(
        CompactPanelBounds workArea,
        int left,
        int top,
        int width,
        int height)
    {
        var clampedLeft = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var clampedTop = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));

        return new WindowPlacement(clampedLeft, clampedTop, width, height, TopMost: false);
    }
}

public sealed record CompactPanelBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}

public enum TaskbarEdge
{
    Unknown,
    Bottom,
    Top,
    Left,
    Right
}
