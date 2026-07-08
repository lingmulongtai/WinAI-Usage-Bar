using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class CompactPanelPlacementServiceTests
{
    [Fact]
    public void Calculate_PlacesPanelAboveBottomTaskbar()
    {
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(0, 0, 1920, 1080),
            new CompactPanelBounds(0, 0, 1920, 1040),
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(1548, placement.Left);
        Assert.Equal(508, placement.Top);
        AssertInside(new CompactPanelBounds(0, 0, 1920, 1040), placement);
    }

    [Fact]
    public void Calculate_PlacesPanelBelowTopTaskbar()
    {
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(0, 0, 1920, 1080),
            new CompactPanelBounds(0, 40, 1920, 1040),
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(1548, placement.Left);
        Assert.Equal(52, placement.Top);
        AssertInside(new CompactPanelBounds(0, 40, 1920, 1040), placement);
    }

    [Theory]
    [InlineData(80, 0, 1840, 1080, 92, 548)]
    [InlineData(0, 0, 1840, 1080, 1468, 548)]
    public void Calculate_HandlesVerticalTaskbars(
        int workLeft,
        int workTop,
        int workWidth,
        int workHeight,
        int expectedLeft,
        int expectedTop)
    {
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(0, 0, 1920, 1080),
            new CompactPanelBounds(workLeft, workTop, workWidth, workHeight),
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(expectedLeft, placement.Left);
        Assert.Equal(expectedTop, placement.Top);
        AssertInside(new CompactPanelBounds(workLeft, workTop, workWidth, workHeight), placement);
    }

    [Fact]
    public void Calculate_KeepsPanelInsideWorkAreaForEachTaskbarEdge()
    {
        var monitor = new CompactPanelBounds(0, 0, 1920, 1080);
        var cases = new[]
        {
            new CompactPanelBounds(0, 0, 1920, 1040),
            new CompactPanelBounds(0, 40, 1920, 1040),
            new CompactPanelBounds(80, 0, 1840, 1080),
            new CompactPanelBounds(0, 0, 1840, 1080)
        };

        foreach (var workArea in cases)
        {
            var placement = CompactPanelPlacementCalculator.Calculate(
                monitor,
                workArea,
                width: 360,
                height: 520,
                margin: 12);

            AssertInside(workArea, placement);
        }
    }

    [Fact]
    public void Calculate_HandlesNegativeCoordinateMonitor()
    {
        var workArea = new CompactPanelBounds(-1920, 0, 1920, 1040);
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(-1920, 0, 1920, 1080),
            workArea,
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(-372, placement.Left);
        Assert.Equal(508, placement.Top);
        AssertInside(workArea, placement);
    }

    [Fact]
    public void Calculate_ClampsOversizedPanelToWorkAreaOrigin()
    {
        var workArea = new CompactPanelBounds(100, 80, 300, 200);
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(100, 80, 300, 200),
            workArea,
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(100, placement.Left);
        Assert.Equal(80, placement.Top);
        Assert.Equal(360, placement.Width);
        Assert.Equal(520, placement.Height);
    }

    private static void AssertInside(CompactPanelBounds workArea, WindowPlacement placement)
    {
        Assert.True(placement.Left >= workArea.Left, $"Left {placement.Left} should be inside {workArea.Left}.");
        Assert.True(placement.Top >= workArea.Top, $"Top {placement.Top} should be inside {workArea.Top}.");
        Assert.True(placement.Left + placement.Width <= workArea.Right, $"Right {placement.Left + placement.Width} should be inside {workArea.Right}.");
        Assert.True(placement.Top + placement.Height <= workArea.Bottom, $"Bottom {placement.Top + placement.Height} should be inside {workArea.Bottom}.");
    }
}
