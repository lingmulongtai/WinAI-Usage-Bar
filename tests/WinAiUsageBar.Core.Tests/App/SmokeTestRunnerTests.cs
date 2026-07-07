using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class SmokeTestRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsZeroWhenCoreStartupChecksPass()
    {
        var exitCode = await SmokeTestRunner.RunAsync(CancellationToken.None);

        Assert.Equal(0, exitCode);
    }
}
