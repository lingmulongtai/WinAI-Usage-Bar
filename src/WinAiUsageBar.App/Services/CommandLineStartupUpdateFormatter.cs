using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineStartupUpdateFormatter
{
    public static string Format(StartupUpdateResult result, string currentVersion)
    {
        var lines = new List<string>
        {
            "Startup update check",
            $"Status: {result.Status}",
            $"Message: {result.Message}",
            $"Current version: {currentVersion}",
            $"Latest version: {result.LatestVersion ?? "n/a"}",
            $"Package path: {result.PackagePath ?? "n/a"}",
            $"Install script: {result.InstallScriptPath ?? "n/a"}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
