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
    }

    [Fact]
    public void Calculate_ClampsPanelInsideSmallWorkArea()
    {
        var placement = CompactPanelPlacementCalculator.Calculate(
            new CompactPanelBounds(0, 0, 300, 200),
            new CompactPanelBounds(0, 0, 300, 200),
            width: 360,
            height: 520,
            margin: 12);

        Assert.Equal(0, placement.Left);
        Assert.Equal(0, placement.Top);
    }
}
