using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineUpdateDownloadFormatter
{
    public static string Format(
        ReleaseUpdateCheckResult updateCheck,
        UpdateDownloadResult? download)
    {
        var lines = new List<string>
        {
            "Update download",
            $"Update status: {updateCheck.Status}",
            $"Current version: {CommandLineDisplayText.Safe(updateCheck.CurrentVersion)}",
            $"Latest version: {CommandLineDisplayText.Safe(updateCheck.LatestVersion)}",
            $"Update available: {(updateCheck.IsUpdateAvailable ? "yes" : "no")}",
            $"Update message: {CommandLineDisplayText.Safe(updateCheck.Message)}"
        };

        if (download is null)
        {
            lines.Add("Download status: Skipped");
            lines.Add("Download message: No verified update package was downloaded.");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add($"Download status: {download.Status}");
        lines.Add($"Download message: {CommandLineDisplayText.Safe(download.Message)}");
        lines.Add($"Package path: {CommandLineDisplayText.Safe(download.PackagePath)}");
        lines.Add($"Checksum path: {CommandLineDisplayText.Safe(download.ChecksumPath)}");
        lines.Add($"Expected SHA256: {CommandLineDisplayText.Safe(download.ExpectedSha256)}");
        lines.Add($"Actual SHA256: {CommandLineDisplayText.Safe(download.ActualSha256)}");
        return string.Join(Environment.NewLine, lines);
    }
}
