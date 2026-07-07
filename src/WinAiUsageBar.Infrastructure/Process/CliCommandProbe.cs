using System.Diagnostics;
using WinAiUsageBar.Core.Abstractions;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed class CliCommandProbe : ICommandProbe
{
    public async Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add(commandName);

        try
        {
            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
