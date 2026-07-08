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

    [Fact]
    public void ReleaseWorkflow_UsesExplicitEnglishReleaseNotesFile()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Assert.Contains("new-release-notes.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes-file", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--generate-notes", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReleaseNotesScript_GeneratesEnglishNotesFromChangelog()
    {
        var root = FindRepositoryRoot();
        var version = ReadAppVersion(root);
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            "WinAiUsageBarTests",
            $"release-notes-{Guid.NewGuid():N}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var scriptPath = Path.Combine(root, "scripts", "new-release-notes.ps1");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-TagName");
            startInfo.ArgumentList.Add($"v{version}");
            startInfo.ArgumentList.Add("-OutputPath");
            startInfo.ArgumentList.Add(outputPath);

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start PowerShell.");
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"stdout: {output}{Environment.NewLine}stderr: {error}");
            var notes = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("## Changes", notes, StringComparison.Ordinal);
            Assert.Contains("## Verification", notes, StringComparison.Ordinal);
            Assert.Contains("## Assets", notes, StringComparison.Ordinal);
            Assert.Contains("## Notes", notes, StringComparison.Ordinal);
            Assert.Contains($"WinAI Usage Bar v{version}", notes, StringComparison.Ordinal);
            Assert.Contains($"WinAIUsageBar-{version}-setup.exe", notes, StringComparison.Ordinal);
            Assert.DoesNotMatch("[ぁ-ゟ゠-ヿ一-龯]", notes);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
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
