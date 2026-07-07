using System.Diagnostics;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Providers.Codex;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed class CodexAppServerClient(TimeSpan? requestTimeout = null) : ICodexAppServerClient
{
    private readonly TimeSpan timeout = requestTimeout ?? TimeSpan.FromSeconds(8);

    public async Task<CodexAppServerData> FetchAccountUsageAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();

        using var process = new System.Diagnostics.Process
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

        try
        {
            process.Start();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            diagnostics.Add(DiagnosticRedactor.Redact(line));
                        }
                    }
                }
                catch
                {
                    diagnostics.Add("Stopped reading Codex app-server diagnostics.");
                }
            }, cancellationToken);

            await SendAndReadAsync(process, CodexJsonRpcParser.CreateInitializeRequest(1), cancellationToken).ConfigureAwait(false);
            var account = await SendAndReadAsync(process, CodexJsonRpcParser.CreateRequest(2, "account/read"), cancellationToken).ConfigureAwait(false);
            var rateLimits = await SendAndReadAsync(process, CodexJsonRpcParser.CreateRequest(3, "account/rateLimits/read"), cancellationToken).ConfigureAwait(false);
            var usage = await SendAndReadAsync(process, CodexJsonRpcParser.CreateRequest(4, "account/usage/read"), cancellationToken).ConfigureAwait(false);

            return new CodexAppServerData(
                DiagnosticRedactor.Redact(account),
                DiagnosticRedactor.Redact(rateLimits),
                DiagnosticRedactor.Redact(usage),
                diagnostics);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                diagnostics.Add("Codex app-server process cleanup failed.");
            }
        }
    }

    private async Task<string?> SendAndReadAsync(
        System.Diagnostics.Process process,
        string request,
        CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync(request.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await process.StandardOutput.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for Codex app-server JSON-RPC response.");
        }
    }
}
