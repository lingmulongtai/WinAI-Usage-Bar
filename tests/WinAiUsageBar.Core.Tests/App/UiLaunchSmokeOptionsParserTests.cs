using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class UiLaunchSmokeOptionsParserTests
{
    [Fact]
    public void Parse_ReturnsNoMatchForNormalCliCommands()
    {
        var result = UiLaunchSmokeOptionsParser.Parse(["--smoke-test"]);

        Assert.False(result.IsMatch);
        Assert.True(result.IsValid);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_UsesDefaultHoldDuration()
    {
        var result = UiLaunchSmokeOptionsParser.Parse(["--ui-launch-smoke"]);

        Assert.True(result.IsMatch);
        Assert.True(result.IsValid);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Options?.HoldDuration);
    }

    [Fact]
    public void Parse_AcceptsHoldSeconds()
    {
        var result = UiLaunchSmokeOptionsParser.Parse(["--ui-launch-smoke", "--hold-seconds", "12"]);

        Assert.True(result.IsMatch);
        Assert.True(result.IsValid);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Options?.HoldDuration);
    }

    [Theory]
    [InlineData("--ui-launch-smoke", "--hold-seconds", "0")]
    [InlineData("--ui-launch-smoke", "--hold-seconds", "61")]
    [InlineData("--ui-launch-smoke", "--hold-seconds", "abc")]
    [InlineData("--ui-launch-smoke", "--hold-seconds")]
    [InlineData("--ui-launch-smoke", "--hold-seconds", "5", "--hold-seconds", "6")]
    [InlineData("--ui-launch-smoke", "--unexpected")]
    public void Parse_RejectsInvalidUiLaunchSmokeOptions(params string[] args)
    {
        var result = UiLaunchSmokeOptionsParser.Parse(args);

        Assert.True(result.IsMatch);
        Assert.False(result.IsValid);
        Assert.Null(result.Options);
        Assert.NotEmpty(result.ErrorMessage);
    }
}
