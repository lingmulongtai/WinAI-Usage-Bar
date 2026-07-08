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

        var data = await client.FetchAccountUsageAsync(CancellationToken.None);

        Assert.Contains("person@example.com", data.AccountJson);
        Assert.Contains("\"id\":3", data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.DoesNotContain("raw-secret", data.AccountJson);
        Assert.Contains(data.Diagnostics, line => line.Contains("[REDACTED]", StringComparison.Ordinal));
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

        var data = await client.FetchAccountUsageAsync(CancellationToken.None);

        Assert.Contains("person@example.com", data.AccountJson);
        Assert.Null(data.RateLimitsJson);
        Assert.Contains("\"id\":4", data.UsageJson);
        Assert.Contains(data.Diagnostics, line => line.Contains("account/rateLimits/read failed", StringComparison.Ordinal));
        Assert.DoesNotContain("rate-secret", string.Join('\n', data.Diagnostics), StringComparison.Ordinal);
        Assert.Equal(4, transport.Requests.Count);
        Assert.True(transport.Stopped);
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
            () => client.FetchAccountUsageAsync(CancellationToken.None));

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
            () => client.FetchAccountUsageAsync(CancellationToken.None));

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
            () => client.FetchAccountUsageAsync(CancellationToken.None));

        Assert.True(transport.Stopped);
    }

    [Fact]
    public async Task FetchAccountUsageAsync_ThrowsTimeoutExceptionWhenResponseNeverArrives()
    {
        var transport = new FakeCodexTransport([], [], waitForOutputWhenEmpty: true);
        var client = new CodexAppServerClient(() => transport, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.FetchAccountUsageAsync(CancellationToken.None));

        Assert.True(transport.Stopped);
    }

    private static string Response(int id, string result)
    {
        return $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";
    }

    private static string Error(int id, string message)
    {
        return $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"message\":\"{message}\"}}}}";
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
}
