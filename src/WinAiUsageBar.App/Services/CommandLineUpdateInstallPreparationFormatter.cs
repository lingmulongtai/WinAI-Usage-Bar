using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineUpdateInstallPreparationFormatter
{
    public static string Format(UpdateInstallPreparationResult result)
    {
        var lines = new List<string>
        {
            "Update install preparation",
            $"Status: {result.Status}",
            $"Message: {result.Message}",
            $"Package: {result.PackagePath ?? "n/a"}",
            $"Install directory: {result.InstallDirectory ?? "n/a"}",
            $"Staging directory: {result.StagingDirectory ?? "n/a"}",
            $"Backup directory: {result.BackupDirectory ?? "n/a"}",
            $"Script: {result.ScriptPath ?? "n/a"}",
            $"Command: {result.Command ?? "n/a"}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
