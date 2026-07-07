using System.Diagnostics;
using WinAiUsageBar.Core.Abstractions;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed class CliCommandProbe : ICommandProbe
{
    public async Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken)
    {
        var result = await InspectAsync(commandName, cancellationToken).ConfigureAwait(false);
        return result.IsFound;
    }

    public async Task<CommandProbeResult> InspectAsync(string commandName, CancellationToken cancellationToken)
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
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                return CommandProbeResult.Missing(commandName);
            }

            var paths = output
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            return paths.Count == 0
                ? CommandProbeResult.Missing(commandName)
                : CommandProbeResult.Found(commandName, paths);
        }
        catch
        {
            return CommandProbeResult.Missing(commandName);
        }
    }
}
