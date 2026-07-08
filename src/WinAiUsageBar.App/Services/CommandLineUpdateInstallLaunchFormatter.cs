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
            $"Message: {result.Message}",
            $"Script: {result.ScriptPath ?? "n/a"}",
            $"Command: {result.Command ?? "n/a"}",
            $"Process ID: {result.ProcessId?.ToString() ?? "n/a"}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
