using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsUnhandledWhenNoArgs()
    {
        var result = await CommandLineHandler.TryHandleAsync(
            [],
            new StringWriter(),
            new StringWriter(),
            _ => Task.FromResult(0),
            AppInfo,
            CancellationToken.None);

        Assert.False(result.Handled);
        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public async Task TryHandleAsync_WritesHelp(string arg)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            [arg],
            output,
            error,
            _ => Task.FromResult(1),
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--version", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_WritesVersion()
    {
        using var output = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            ["--version"],
            output,
            new StringWriter(),
            _ => Task.FromResult(1),
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("WinAI Usage Bar 9.8.7", output.ToString().Trim());
    }

    [Fact]
    public async Task TryHandleAsync_RunsSmokeTest()
    {
        var smokeTestCount = 0;

        var result = await CommandLineHandler.TryHandleAsync(
            ["--smoke-test"],
            new StringWriter(),
            new StringWriter(),
            _ =>
            {
                smokeTestCount++;
                return Task.FromResult(42);
            },
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(42, result.ExitCode);
        Assert.Equal(1, smokeTestCount);
    }

    [Theory]
    [InlineData("--wat")]
    [InlineData("--version", "--help")]
    public async Task TryHandleAsync_ReturnsErrorForUnknownArguments(params string[] args)
    {
        using var error = new StringWriter();

        var result = await CommandLineHandler.TryHandleAsync(
            args,
            new StringWriter(),
            error,
            _ => Task.FromResult(0),
            AppInfo,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown command-line argument", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("--help", error.ToString(), StringComparison.Ordinal);
    }

    private static AppInfo AppInfo()
    {
        return new AppInfo("WinAI Usage Bar", "9.8.7.0", "9.8.7");
    }
}
