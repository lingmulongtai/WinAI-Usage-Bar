using System.Diagnostics;

namespace WinAiUsageBar.Infrastructure.Process;

public interface ICodexAppServerTransport : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    Task<string?> ReadOutputLineAsync(CancellationToken cancellationToken);

    Task<string?> ReadErrorLineAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public sealed class CodexProcessAppServerTransport : ICodexAppServerTransport
{
    private System.Diagnostics.Process? process;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "codex",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add("app-server");
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
