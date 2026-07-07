using System.Xml.Linq;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class VersionDocumentationTests
{
    [Fact]
    public void Readme_DocumentsCurrentAppVersion()
    {
        var root = FindRepositoryRoot();
        var version = ReadAppVersion(root);
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains($"Current app version: `{version}`.", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Changelog_ContainsCurrentAppVersionEntry()
    {
        var root = FindRepositoryRoot();
        var version = ReadAppVersion(root);
        var changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));

        Assert.Contains($"## {version} - ", changelog, StringComparison.Ordinal);
    }

    private static string ReadAppVersion(string root)
    {
        var projectPath = Path.Combine(
            root,
            "src",
            "WinAiUsageBar.App",
            "WinAiUsageBar.App.csproj");
        var document = XDocument.Load(projectPath);
        var version = document
            .Descendants("Version")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (version is null)
        {
            throw new InvalidOperationException("App project Version was not found.");
        }

        return version;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinAIUsageBar.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with WinAIUsageBar.sln was not found.");
    }
}
