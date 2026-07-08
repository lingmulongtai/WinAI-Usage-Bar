using WinAiUsageBar.App.Services;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class AppInfoProviderTests
{
    [Fact]
    public void Get_ReturnsAppMetadata()
    {
        var info = AppInfoProvider.Get();
        var expectedVersion = GetProjectVersion();

        Assert.Equal("WinAI Usage Bar", info.ProductName);
        Assert.StartsWith(expectedVersion, info.InformationalVersion, StringComparison.Ordinal);
        Assert.StartsWith(expectedVersion, info.Version, StringComparison.Ordinal);
    }

    private static string GetProjectVersion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(
                directory.FullName,
                "src",
                "WinAiUsageBar.App",
                "WinAiUsageBar.App.csproj");
            if (File.Exists(projectPath))
            {
                var projectText = File.ReadAllText(projectPath);
                var match = System.Text.RegularExpressions.Regex.Match(
                    projectText,
                    "<Version>([^<]+)</Version>");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("WinAiUsageBar.App.csproj was not found.");
    }
}
