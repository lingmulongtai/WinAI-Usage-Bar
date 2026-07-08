using System.Text.Json;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Infrastructure.Process;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class CodexAppServerClientTests
{
    [Fact]
    public async Task FetchAccountUsageAsync_MatchesResponseIdsAndBuffersOutOfOrderResponses()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                """{"jsonrpc":"2.0","method":"session/changed","params":{}}""",
                Response(3, """{"used":10,"limit":100}"""),
                Response(2, """{"email":"person@example.com","access_token":"raw-secret"}"""),
                Response(4, """{"used":20,"limit":100}""")
            ],
            ["token=stderr-secret"],
            waitForErrorReadBeforeOutput: true);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        var data = await client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None);

        Assert.Contains("person@example.com", data.AccountJson);
        Assert.Contains("\"id\":3", data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.DoesNotContain("raw-secret", data.AccountJson);
        Assert.Contains(data.Diagnostics, line => line.Contains("[REDACTED]", StringComparison.Ordinal));
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_IgnoresNotificationsWithNestedIds()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                Response(2, """{"email":"person@example.com"}"""),
                """{"jsonrpc":"2.0","method":"session/changed","params":{"id":3,"message":"not a response"}}""",
                Response(3, """{"used":10,"limit":100}"""),
                Response(4, """{"used":20,"limit":100}""")
            ],
            []);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        var data = await client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None);

        Assert.Contains("person@example.com", data.AccountJson);
        Assert.Contains("\"id\":3", data.RateLimitsJson);
        Assert.Contains("\"used\":10", data.RateLimitsJson);
        Assert.DoesNotContain("session/changed", data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ContinuesWhenOptionalMethodReturnsNonAuthError()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                Response(2, """{"email":"person@example.com"}"""),
                Error(3, "Method not found token=rate-secret"),
                Response(4, """{"used":20,"limit":100}""")
            ],
            []);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        var data = await client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None);

        Assert.Contains("person@example.com", data.AccountJson);
        Assert.Null(data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.Contains(data.Diagnostics, line => line.Contains("account/rateLimits/read failed", StringComparison.Ordinal));
        Assert.DoesNotContain("rate-secret", string.Join('\n', data.Diagnostics), StringComparison.Ordinal);
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ContinuesWhenOptionalMethodTimesOut()
    {
        var transport = new TimeoutAccountReadCodexTransport();
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromMilliseconds(50));

        var data = await client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None);

        Assert.Null(data.AccountJson);
        Assert.Contains("\"id\":3", data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.Contains(data.Diagnostics, line => line.Contains("account/read timed out", StringComparison.Ordinal));
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_PassesCommandProbeToTransportFactory()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                Response(2, """{"email":"person@example.com"}"""),
                Response(3, """{"used":10,"limit":100}"""),
                Response(4, """{"used":20,"limit":100}""")
            ],
            []);
        CommandProbeResult? capturedProbe = null;
        var client = new CodexAppServerClient(
            probe =>
            {
                capturedProbe = probe;
                return transport;
            },
            TimeSpan.FromSeconds(1));
        var probe = CommandProbeResult.Found("codex", [@"C:\Users\me\AppData\Roaming\npm\codex.cmd"]);

        await client.FetchAccountUsageAsync(probe, CancellationToken.None);

        Assert.NotNull(capturedProbe);
        Assert.Equal("codex", capturedProbe.CommandName);
        Assert.Equal(@"C:\Users\me\AppData\Roaming\npm\codex.cmd", Assert.Single(capturedProbe.Paths));
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_SendsConfiguredInitializeClientVersion()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                Response(2, """{"email":"person@example.com"}"""),
                Response(3, """{"used":10,"limit":100}"""),
                Response(4, """{"used":20,"limit":100}""")
            ],
            []);
        var client = new CodexAppServerClient(
            () => transport,
            TimeSpan.FromSeconds(1),
            "9.8.7+metadata");

        await client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None);

        Assert.Contains("\"method\":\"initialize\"", transport.Requests[0], StringComparison.Ordinal);
        Assert.Equal("9.8.7+metadata", ReadInitializeClientVersion(transport.Requests[0]));
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ThrowsUnauthorizedAccessExceptionForAuthError()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                Error(2, "Auth required: token=secret-value")
            ],
            []);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None));

        Assert.Contains("Auth required", exception.Message);
        Assert.DoesNotContain("secret-value", exception.Message);
        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ThrowsInvalidDataExceptionForMalformedJson()
    {
        var transport = new FakeCodexTransport(
            [
                Response(1, """{"ok":true}"""),
                "{ this is not json"
            ],
            []);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None));

        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ThrowsEndOfStreamExceptionForPartialResponse()
    {
        var transport = new FakeCodexTransport(
            [Response(1, """{"ok":true}""")],
            []);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None));

        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ThrowsTimeoutExceptionWhenResponseNeverArrives()
    {
        var transport = new FakeCodexTransport([], [], waitForOutputWhenEmpty: true);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.FetchAccountUsageAsync(CodexProbe(), CancellationToken.None));

        Assert.True(transport.Stopped);
    }

    private static string Response(int id, string result)
    {
        return $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";
    }

    private static CommandProbeResult CodexProbe()
    {
        return CommandProbeResult.Found("codex", [@"C:\Tools\codex.exe"]);
    }

    private static string Error(int id, string message)
    {
        return $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"message\":\"{message}\"}}}}";
    }

    private static string? ReadInitializeClientVersion(string request)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement
            .GetProperty("params")
            .GetProperty("clientInfo")
            .GetProperty("version")
            .GetString();
    }

    private sealed class FakeCodexTransport(
        IEnumerable<string> outputLines,
        IEnumerable<string> errorLines,
        bool waitForOutputWhenEmpty = false,
        bool waitForErrorReadBeforeOutput = false) : ICodexAppServerTransport
    {
        private readonly Queue<string> outputLines = new(outputLines);
        private readonly Queue<string> errorLines = new(errorLines);
        private readonly TaskCompletionSource errorRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool waitedForErrorRead;

        public List<string> Requests { get; } = [];

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Started = true;
            return Task.CompletedTask;
        }

        public Task WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(line);
            return Task.CompletedTask;
        }

        public async Task<string?> ReadOutputLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (waitForErrorReadBeforeOutput && !waitedForErrorRead)
            {
                waitedForErrorRead = true;
                await errorRead.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (outputLines.TryDequeue(out var line))
            {
                return line;
            }

            if (waitForOutputWhenEmpty)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        public async Task<string?> ReadErrorLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (errorLines.TryDequeue(out var line))
            {
                errorRead.TrySetResult();
                return line;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return null;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TimeoutAccountReadCodexTransport : ICodexAppServerTransport
    {
        private int outputReadCount;

        public List<string> Requests { get; } = [];

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(line);
            return Task.CompletedTask;
        }

        public async Task<string?> ReadOutputLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outputReadCount++;
            return outputReadCount switch
            {
                1 => Response(1, """{"ok":true}"""),
                2 => await WaitForTimeoutAsync(cancellationToken).ConfigureAwait(false),
                3 => Response(3, """{"used":10,"limit":100}"""),
                4 => Response(4, """{"used":20,"limit":100}"""),
                _ => null
            };
        }

        public async Task<string?> ReadErrorLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return null;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static async Task<string?> WaitForTimeoutAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }
}
