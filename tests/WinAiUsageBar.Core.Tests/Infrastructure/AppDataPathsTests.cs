using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class AppDataPathsTests
{
    [Fact]
    public void CreateDefault_UsesRootOverrideWhenProvided()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var fallbackAppData = Path.Combine(Path.GetTempPath(), "WinAiUsageBarFallback");

        var paths = AppDataPaths.CreateDefault(root, fallbackAppData);

        Assert.Equal(Path.GetFullPath(root), paths.RootDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "updates"), paths.UpdatesDirectory);
    }

    [Fact]
    public void CreateDefault_UsesAppDataRootWhenOverrideIsBlank()
    {
        var fallbackAppData = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));

        var paths = AppDataPaths.CreateDefault("   ", fallbackAppData);

        Assert.Equal(Path.Combine(fallbackAppData, "WinAiUsageBar"), paths.RootDirectory);
        Assert.Equal(Path.Combine(fallbackAppData, "WinAiUsageBar", "config.json"), paths.ConfigPath);
    }
}
