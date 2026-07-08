using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineUpdateInstallLaunchFormatter
{
    public static string Format(UpdateInstallLaunchResult result)
    {
        var lines = new List<string>
        {
            "Update install launch",
            $"Status: {result.Status}",
            $"Message: {CommandLineDisplayText.Safe(result.Message)}",
            $"Script: {CommandLineDisplayText.Safe(result.ScriptPath)}",
            $"Command: {CommandLineDisplayText.Safe(result.Command)}",
            $"Process ID: {result.ProcessId?.ToString() ?? "n/a"}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
