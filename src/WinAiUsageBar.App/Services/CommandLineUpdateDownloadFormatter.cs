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
            $"Current version: {updateCheck.CurrentVersion}",
            $"Latest version: {updateCheck.LatestVersion ?? "n/a"}",
            $"Update available: {(updateCheck.IsUpdateAvailable ? "yes" : "no")}",
            $"Update message: {updateCheck.Message}"
        };

        if (download is null)
        {
            lines.Add("Download status: Skipped");
            lines.Add("Download message: No verified update package was downloaded.");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add($"Download status: {download.Status}");
        lines.Add($"Download message: {download.Message}");
        lines.Add($"Package path: {download.PackagePath ?? "n/a"}");
        lines.Add($"Checksum path: {download.ChecksumPath ?? "n/a"}");
        lines.Add($"Expected SHA256: {download.ExpectedSha256 ?? "n/a"}");
        lines.Add($"Actual SHA256: {download.ActualSha256 ?? "n/a"}");
        return string.Join(Environment.NewLine, lines);
    }
}
