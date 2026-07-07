using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class AppInfoProviderTests
{
    [Fact]
    public void Get_ReturnsAppMetadata()
    {
        var info = AppInfoProvider.Get();

        Assert.Equal("WinAI Usage Bar", info.ProductName);
        Assert.StartsWith("0.1.0", info.InformationalVersion, StringComparison.Ordinal);
        Assert.StartsWith("0.1.0", info.Version, StringComparison.Ordinal);
    }
}
