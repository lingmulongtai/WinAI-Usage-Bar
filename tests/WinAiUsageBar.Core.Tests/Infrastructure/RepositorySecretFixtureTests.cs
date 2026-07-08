using System.Text.RegularExpressions;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class RepositorySecretFixtureTests
{
    private static readonly string[] SkippedDirectories =
    [
        ".git",
        ".vs",
        "artifacts",
        "bin",
        "obj"
    ];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".editorconfig",
        ".iss",
        ".json",
        ".md",
        ".ps1",
        ".props",
        ".targets",
        ".toml",
        ".txt",
        ".xml",
        ".yaml",
        ".yml"
    };

    [Fact]
    public void Repository_DoesNotContainSecretShapedFixtures()
    {
        var root = FindRepositoryRoot();
        var patterns = CreateSecretPatterns();
        var findings = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => ShouldScan(root, path)))
        {
            var text = File.ReadAllText(path);
            foreach (var pattern in patterns)
            {
                if (pattern.Regex.IsMatch(text))
                {
                    var relativePath = Path.GetRelativePath(root, path);
                    findings.Add($"{relativePath}: {pattern.Label}");
                }
            }
        }

        Assert.True(
            findings.Count == 0,
            "Secret-shaped fixture patterns were found. Use obvious sample placeholders or compose secret-shaped redaction samples at runtime instead."
            + Environment.NewLine
            + string.Join(Environment.NewLine, findings));
    }

    private static IReadOnlyList<SecretPattern> CreateSecretPatterns()
    {
        return
        [
            Pattern("OpenAI-style key", "s" + "k-[A-Za-z0-9_-]{8,}"),
            Pattern("GitHub classic PAT", "gh" + "p_[A-Za-z0-9_]{8,}"),
            Pattern("GitHub fine-grained PAT", "github" + "_pat_[A-Za-z0-9_]{10,}"),
            Pattern("Google API key", "AI" + "za[A-Za-z0-9_-]{10,}")
        ];
    }

    private static SecretPattern Pattern(string label, string pattern)
    {
        return new SecretPattern(
            label,
            new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }

    private static bool ShouldScan(string root, string path)
    {
        if (!TextExtensions.Contains(Path.GetExtension(path)))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(root, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !segments.Any(segment => SkippedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
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

    private sealed record SecretPattern(string Label, Regex Regex);
}
