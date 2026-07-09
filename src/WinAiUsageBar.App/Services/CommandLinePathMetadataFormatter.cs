using System.Text;

namespace WinAiUsageBar.App.Services;

internal static class CommandLinePathMetadataFormatter
{
    private const string ConfigBackupsRelativeDirectory = "config-backups";

    public static void AppendFileName(StringBuilder builder, string label, string? path)
    {
        var fileName = GetFileName(path);
        if (fileName is not null)
        {
            builder.AppendLine($"{label}: {fileName}");
        }
    }

    public static void AppendConfigBackupRelativePath(StringBuilder builder, string label, string? path)
    {
        var fileName = GetFileName(path);
        if (fileName is not null)
        {
            builder.AppendLine($"{label}: {Path.Combine(ConfigBackupsRelativeDirectory, fileName)}");
        }
    }

    private static string? GetFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fileName = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
