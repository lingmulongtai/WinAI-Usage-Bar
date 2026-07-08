using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineUpdateCheckFormatter
{
    public static string Format(ReleaseUpdateCheckResult result)
    {
        var lines = new List<string>
        {
            "Update check",
            $"Status: {result.Status}",
            $"Current version: {CommandLineDisplayText.Safe(result.CurrentVersion)}",
            $"Latest version: {CommandLineDisplayText.Safe(result.LatestVersion)}",
            $"Update available: {(result.IsUpdateAvailable ? "yes" : "no")}",
            $"Message: {CommandLineDisplayText.Safe(result.Message)}",
            $"Release page: {CommandLineDisplayText.Safe(result.ReleasePageUrl)}",
            $"Package: {FormatAsset(result.Package)}",
            $"Checksum: {FormatAsset(result.Checksum)}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAsset(UpdatePackageAsset? asset)
    {
        if (asset is null)
        {
            return "n/a";
        }

        var size = asset.SizeBytes is long sizeBytes
            ? $", {FormatBytes(sizeBytes)}"
            : string.Empty;
        return $"{CommandLineDisplayText.Safe(asset.Name)}{size}, {CommandLineDisplayText.Safe(asset.DownloadUrl)}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var units = new[] { "KB", "MB", "GB" };
        var value = bytes / 1024d;
        foreach (var unit in units)
        {
            if (value < 1024d || unit == units[^1])
            {
                return $"{value:0.##} {unit}";
            }

            value /= 1024d;
        }

        return $"{bytes} B";
    }
}
