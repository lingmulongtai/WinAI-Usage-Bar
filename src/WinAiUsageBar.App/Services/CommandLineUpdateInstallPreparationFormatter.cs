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
            $"Message: {CommandLineDisplayText.Safe(result.Message)}",
            $"Package: {CommandLineDisplayText.Safe(result.PackagePath)}",
            $"Install directory: {CommandLineDisplayText.Safe(result.InstallDirectory)}",
            $"Staging directory: {CommandLineDisplayText.Safe(result.StagingDirectory)}",
            $"Backup directory: {CommandLineDisplayText.Safe(result.BackupDirectory)}",
            $"Script: {CommandLineDisplayText.Safe(result.ScriptPath)}",
            $"Command: {CommandLineDisplayText.Safe(result.Command)}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
