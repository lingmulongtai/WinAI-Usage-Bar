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
            $"Message: {CommandLineDisplayText.Safe(result.Message)}",
            $"Current version: {CommandLineDisplayText.Safe(currentVersion)}",
            $"Latest version: {CommandLineDisplayText.Safe(result.LatestVersion)}",
            $"Package path: {CommandLineDisplayText.Safe(result.PackagePath)}",
            $"Install script: {CommandLineDisplayText.Safe(result.InstallScriptPath)}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
