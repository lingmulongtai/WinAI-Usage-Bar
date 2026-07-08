using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineLatestUpdateInstallFormatter
{
    public static string Format(LatestUpdateInstallResult result)
    {
        var lines = new List<string>
        {
            "Latest update install",
            $"Status: {result.Status}",
            $"Message: {CommandLineDisplayText.Safe(result.Message)}"
        };

        if (result.UpdateCheck is not null)
        {
            lines.Add($"Update status: {result.UpdateCheck.Status}");
            lines.Add($"Current version: {CommandLineDisplayText.Safe(result.UpdateCheck.CurrentVersion)}");
            lines.Add($"Latest version: {CommandLineDisplayText.Safe(result.UpdateCheck.LatestVersion)}");
            lines.Add($"Update available: {(result.UpdateCheck.IsUpdateAvailable ? "yes" : "no")}");
            lines.Add($"Release: {CommandLineDisplayText.Safe(result.UpdateCheck.ReleasePageUrl)}");
        }

        if (result.Download is not null)
        {
            lines.Add($"Download status: {result.Download.Status}");
            lines.Add($"Package path: {CommandLineDisplayText.Safe(result.Download.PackagePath)}");
            lines.Add($"Checksum path: {CommandLineDisplayText.Safe(result.Download.ChecksumPath)}");
        }
        else
        {
            lines.Add("Download status: Skipped");
        }

        if (result.Preparation is not null)
        {
            lines.Add($"Preparation status: {result.Preparation.Status}");
            lines.Add($"Script: {CommandLineDisplayText.Safe(result.Preparation.ScriptPath)}");
            lines.Add($"Result: {CommandLineDisplayText.Safe(result.Preparation.ResultPath)}");
        }
        else
        {
            lines.Add("Preparation status: Skipped");
        }

        if (result.Launch is not null)
        {
            lines.Add($"Launch status: {result.Launch.Status}");
            lines.Add($"Process ID: {result.Launch.ProcessId?.ToString() ?? "n/a"}");
        }
        else
        {
            lines.Add("Launch status: Skipped");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
