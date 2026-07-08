using System.Diagnostics;
using WinAiUsageBar.Core.Abstractions;

namespace WinAiUsageBar.Infrastructure.Process;

public interface ICodexAppServerTransport : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    Task<string?> ReadOutputLineAsync(CancellationToken cancellationToken);

    Task<string?> ReadErrorLineAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public sealed class CodexProcessAppServerTransport(
    CommandProbeResult commandProbe) : ICodexAppServerTransport
{
    private System.Diagnostics.Process? process;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = CliCommandLaunchPlanner
            .Create(commandProbe.CommandName, commandProbe.Paths)
            .CreateStartInfo(["app-server"], redirectStandardInput: true);

        process = new System.Diagnostics.Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Start();

        return Task.CompletedTask;
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        var activeProcess = GetStartedProcess();
        await activeProcess.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await activeProcess.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> ReadOutputLineAsync(CancellationToken cancellationToken)
    {
        return GetStartedProcess().StandardOutput.ReadLineAsync(cancellationToken).AsTask();
    }

    public Task<string?> ReadErrorLineAsync(CancellationToken cancellationToken)
    {
        return GetStartedProcess().StandardError.ReadLineAsync(cancellationToken).AsTask();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Process cleanup is best-effort during shutdown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        process?.Dispose();
    }

    private System.Diagnostics.Process GetStartedProcess()
    {
        return process ?? throw new InvalidOperationException("Codex app-server process has not started.");
    }
}
