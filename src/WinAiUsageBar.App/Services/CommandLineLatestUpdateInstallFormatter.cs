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
            $"Message: {result.Message}"
        };

        if (result.UpdateCheck is not null)
        {
            lines.Add($"Update status: {result.UpdateCheck.Status}");
            lines.Add($"Current version: {result.UpdateCheck.CurrentVersion}");
            lines.Add($"Latest version: {result.UpdateCheck.LatestVersion ?? "n/a"}");
            lines.Add($"Update available: {(result.UpdateCheck.IsUpdateAvailable ? "yes" : "no")}");
            lines.Add($"Release: {result.UpdateCheck.ReleasePageUrl?.ToString() ?? "n/a"}");
        }

        if (result.Download is not null)
        {
            lines.Add($"Download status: {result.Download.Status}");
            lines.Add($"Package path: {result.Download.PackagePath ?? "n/a"}");
            lines.Add($"Checksum path: {result.Download.ChecksumPath ?? "n/a"}");
        }
        else
        {
            lines.Add("Download status: Skipped");
        }

        if (result.Preparation is not null)
        {
            lines.Add($"Preparation status: {result.Preparation.Status}");
            lines.Add($"Script: {result.Preparation.ScriptPath ?? "n/a"}");
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
