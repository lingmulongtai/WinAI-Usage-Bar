using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class CommandLineConfigBackupExportFormatter
{
    public static string Format(ConfigBackupResult result)
    {
        return string.Join(
            Environment.NewLine,
            "Config backup export",
            $"Path: {result.Path}",
            $"Created: {result.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
    }
}
