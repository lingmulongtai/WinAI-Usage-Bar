using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Providers.Codex;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed class CodexAppServerClient : ICodexAppServerClient
{
    private const int InitializeRequestId = 1;
    private const int AccountRequestId = 2;
    private const int RateLimitsRequestId = 3;
    private const int UsageRequestId = 4;

    private readonly Func<CommandProbeResult, ICodexAppServerTransport> transportFactory;
    private readonly TimeSpan timeout;

    public CodexAppServerClient(TimeSpan? requestTimeout = null)
        : this(commandProbe => new CodexProcessAppServerTransport(commandProbe), requestTimeout)
    {
    }

    public CodexAppServerClient(
        Func<ICodexAppServerTransport> transportFactory,
        TimeSpan? requestTimeout = null)
        : this(_ => transportFactory(), requestTimeout)
    {
    }

    public CodexAppServerClient(
        Func<CommandProbeResult, ICodexAppServerTransport> transportFactory,
        TimeSpan? requestTimeout = null)
    {
        this.transportFactory = transportFactory;
        timeout = requestTimeout ?? TimeSpan.FromSeconds(8);
    }

    public async Task<CodexAppServerData> FetchAccountUsageAsync(
        CommandProbeResult commandProbe,
        CancellationToken cancellationToken)
    {
        var diagnostics = new ConcurrentQueue<string>();
        var pendingResponses = new Dictionary<int, string>();
        await using var transport = transportFactory(commandProbe);
        using var diagnosticsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var diagnosticsTask = Task.CompletedTask;
        var cleanupCompleted = false;

        try
        {
            await transport.StartAsync(cancellationToken).ConfigureAwait(false);
            diagnosticsTask = Task.Run(
                () => CollectDiagnosticsAsync(transport, diagnostics, diagnosticsCts.Token),
                CancellationToken.None);

            await SendAndReadAsync(
                transport,
                pendingResponses,
                InitializeRequestId,
                CodexJsonRpcParser.CreateInitializeRequest(InitializeRequestId),
                cancellationToken).ConfigureAwait(false);

            var account = await TrySendOptionalAsync(
                transport,
                pendingResponses,
                AccountRequestId,
                "account/read",
                CodexJsonRpcParser.CreateRequest(AccountRequestId, "account/read"),
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            var rateLimits = await TrySendOptionalAsync(
                transport,
                pendingResponses,
                RateLimitsRequestId,
                "account/rateLimits/read",
                CodexJsonRpcParser.CreateRequest(RateLimitsRequestId, "account/rateLimits/read"),
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            var usage = await TrySendOptionalAsync(
                transport,
                pendingResponses,
                UsageRequestId,
                "account/usage/read",
                CodexJsonRpcParser.CreateRequest(UsageRequestId, "account/usage/read"),
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            await CleanupAsync(transport, diagnosticsCts, diagnosticsTask, diagnostics).ConfigureAwait(false);
            cleanupCompleted = true;

            return new CodexAppServerData(
                RedactOrNull(account),
                RedactOrNull(rateLimits),
                RedactOrNull(usage),
                diagnostics.ToArray());
        }
        finally
        {
            if (!cleanupCompleted)
            {
                await CleanupAsync(transport, diagnosticsCts, diagnosticsTask, diagnostics).ConfigureAwait(false);
            }
        }
    }

    private async Task<string?> TrySendOptionalAsync(
        ICodexAppServerTransport transport,
        IDictionary<int, string> pendingResponses,
        int expectedId,
        string methodName,
        string request,
        ConcurrentQueue<string> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAndReadAsync(
                transport,
                pendingResponses,
                expectedId,
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (CodexJsonRpcResponseException ex)
        {
            diagnostics.Enqueue(DiagnosticRedactor.Redact(
                $"Codex app-server method {methodName} failed: {ex.Message}"));
            return null;
        }
    }

    private async Task<string?> SendAndReadAsync(
        ICodexAppServerTransport transport,
        IDictionary<int, string> pendingResponses,
        int expectedId,
        string request,
        CancellationToken cancellationToken)
    {
        await transport.WriteLineAsync(request, cancellationToken).ConfigureAwait(false);

        if (pendingResponses.Remove(expectedId, out var pendingResponse))
        {
            return EnsureSuccessfulResponse(expectedId, pendingResponse);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (true)
        {
            var line = await ReadOutputLineAsync(transport, expectedId, timeoutCts.Token, cancellationToken)
                .ConfigureAwait(false);

            if (line is null)
            {
                throw new EndOfStreamException(
                    $"Codex app-server closed stdout before response id {expectedId} was received.");
            }

            var envelope = ParseEnvelope(line);
            if (envelope.Id == expectedId)
            {
                return EnsureSuccessfulResponse(expectedId, line, envelope);
            }

            if (envelope.Id is int responseId)
            {
                pendingResponses[responseId] = line;
            }
        }
    }

    private async Task<string?> ReadOutputLineAsync(
        ICodexAppServerTransport transport,
        int expectedId,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        try
        {
            return await transport.ReadOutputLineAsync(timeoutToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out waiting for Codex app-server JSON-RPC response id {expectedId}.");
        }
    }

    private static string? RedactOrNull(string? text)
    {
        return text is null ? null : DiagnosticRedactor.Redact(text);
    }

    private static string EnsureSuccessfulResponse(int expectedId, string line)
    {
        return EnsureSuccessfulResponse(expectedId, line, ParseEnvelope(line));
    }

    private static string EnsureSuccessfulResponse(int expectedId, string line, JsonRpcEnvelope envelope)
    {
        if (envelope.HasError)
        {
            var message = DiagnosticRedactor.Redact(
                envelope.ErrorMessage ?? $"Codex app-server returned a JSON-RPC error for id {expectedId}.");

            if (message.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || message.Contains("login", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(message);
            }

            throw new CodexJsonRpcResponseException(message);
        }

        return line;
    }

    private static JsonRpcEnvelope ParseEnvelope(string line)
    {
        try
        {
            return CodexJsonRpcParser.ParseEnvelope(line);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Codex app-server returned malformed JSON-RPC.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Codex app-server returned an invalid JSON-RPC id.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException("Codex app-server returned an invalid JSON-RPC envelope.", ex);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Codex app-server returned an out-of-range JSON-RPC id.", ex);
        }
    }

    private static async Task CollectDiagnosticsAsync(
        ICodexAppServerTransport transport,
        ConcurrentQueue<string> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await transport.ReadErrorLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    diagnostics.Enqueue(DiagnosticRedactor.Redact(line));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            diagnostics.Enqueue("Stopped reading Codex app-server diagnostics.");
        }
    }

    private static async Task CleanupAsync(
        ICodexAppServerTransport transport,
        CancellationTokenSource diagnosticsCts,
        Task diagnosticsTask,
        ConcurrentQueue<string> diagnostics)
    {
        diagnosticsCts.Cancel();

        try
        {
            await transport.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            diagnostics.Enqueue("Codex app-server process cleanup failed.");
        }

        try
        {
            await diagnosticsTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            diagnostics.Enqueue("Stopped reading Codex app-server diagnostics.");
        }
    }

    private sealed class CodexJsonRpcResponseException(string message) : InvalidOperationException(message);
}
