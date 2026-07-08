using System.ComponentModel;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Core.Providers.Codex;
using WinAiUsageBar.Core.Providers.GitHubCopilot;
using WinAiUsageBar.Core.Providers.Manual;
using WinAiUsageBar.Core.Providers.Mock;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class ProviderAdapterTests
{
    [Fact]
    public async Task ManualProviderAdapter_ReturnsManualSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Gemini);
        var adapter = new ManualProviderAdapter(descriptor);
        var context = new ProviderFetchContext(
            new ProviderConfig
            {
                ProviderId = ProviderId.Gemini,
                SourceKind = DataSourceKind.Manual,
                Manual = new ManualUsageSettings
                {
                    RemainingPercent = 44,
                    Notes = "Gemini manual"
                }
            },
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.Manual, result.Snapshot?.SourceKind);
        Assert.Equal(44, result.Snapshot?.PrimaryWindow?.RemainingPercent);
    }

    [Fact]
    public async Task MockProviderAdapter_ReturnsRealisticSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new MockProviderAdapter(descriptor);
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            new DateTimeOffset(2026, 7, 8, 0, 5, 0, TimeSpan.Zero),
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.Mock, result.Snapshot?.SourceKind);
        Assert.InRange(result.Snapshot?.PrimaryWindow?.RemainingPercent ?? -1, 0, 100);
    }

    [Fact]
    public async Task UnsupportedProviderAdapter_ReturnsFailureSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var adapter = new UnsupportedProviderAdapter(
            descriptor,
            DataSourceKind.OfficialApi,
            "Metrics permission is missing.");
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.Unsupported, result.Snapshot?.Health);
        Assert.Equal("Metrics permission is missing.", result.ErrorMessage);
        Assert.NotNull(result.Snapshot);
    }

    [Fact]
    public async Task CliProbeProviderAdapter_ReturnsAuthRequiredWhenCliExistsButUsageIsUnsupported()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.ClaudeCode);
        var adapter = new CliProbeProviderAdapter(
            descriptor,
            FixedCommandProbe.Found("claude"),
            "claude",
            "Claude CLI exists, but usage retrieval is unavailable.",
            "Claude CLI is missing.");
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.AuthRequired, result.Snapshot?.Health);
        Assert.Equal(DataSourceKind.Cli, result.Snapshot?.SourceKind);
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_ReturnsUnsupportedWhenCodexCliIsMissing()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            FixedCommandProbe.Missing("codex"),
            new ThrowingCodexClient(new InvalidOperationException("client should not be called")));
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.Unsupported, result.Snapshot?.Health);
        Assert.Equal(DataSourceKind.LocalAppServer, result.Snapshot?.SourceKind);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DataSourceKind.LocalAppServer)]
    [InlineData(DataSourceKind.Cli)]
    public async Task CodexAppServerProviderAdapter_ReportsConfiguredSourceWhenCodexCliIsMissing(
        DataSourceKind sourceKind)
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            FixedCommandProbe.Missing("codex"),
            new ThrowingCodexClient(new InvalidOperationException("client should not be called")),
            sourceKind);
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = sourceKind;

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(sourceKind, result.Snapshot?.SourceKind);
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_ReturnsAuthRequiredWhenClientRejects()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            FixedCommandProbe.Found("codex"),
            new ThrowingCodexClient(new UnauthorizedAccessException("auth required")));
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.AuthRequired, result.Snapshot?.Health);
        Assert.Equal("Codex app-server requires authentication.", result.ErrorMessage);
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_ReportsConfiguredSourceForSuccessfulSnapshot()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            FixedCommandProbe.Found("codex"),
            new SuccessfulCodexClient(),
            DataSourceKind.Cli);
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.Cli;

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.Cli, result.Snapshot?.SourceKind);
        Assert.Equal(ProviderHealth.Ok, result.Snapshot?.Health);
    }

    [Theory]
    [InlineData(DataSourceKind.Cli)]
    [InlineData(DataSourceKind.LocalAppServer)]
    public async Task CodexAppServerProviderAdapter_ReportsConfiguredSourceForFailures(
        DataSourceKind sourceKind)
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var failures = new Exception[]
        {
            new UnauthorizedAccessException("auth required"),
            new Win32Exception(5, "Access is denied."),
            new InvalidOperationException("app-server failed")
        };

        foreach (var failure in failures)
        {
            var adapter = new CodexAppServerProviderAdapter(
                descriptor,
                FixedCommandProbe.Found("codex"),
                new ThrowingCodexClient(failure),
                sourceKind);
            var config = ProviderConfig.CreateDefault(descriptor);
            config.SourceKind = sourceKind;

            var result = await adapter.FetchAsync(
                new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(sourceKind, result.Snapshot?.SourceKind);
        }
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_UsesConfiguredCommandOverrideBeforePathProbe()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var commandProbe = new CountingCommandProbe(CommandProbeResult.Missing("codex"));
        var client = new RecordingCodexClient(new UnauthorizedAccessException("auth required"));
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.LocalAppServer;
        config.Cli.CommandPathOverride = @" C:\Tools\codex.cmd ";
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            commandProbe,
            client);

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.AuthRequired, result.Snapshot?.Health);
        Assert.Equal(0, commandProbe.InspectCount);
        Assert.Equal("codex", client.LastProbe?.CommandName);
        Assert.Equal(@"C:\Tools\codex.cmd", Assert.Single(client.LastProbe?.Paths ?? []));
        Assert.Contains("override", client.LastProbe?.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_NormalizesQuotedCommandOverrideBeforeClient()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var commandProbe = new CountingCommandProbe(CommandProbeResult.Missing("codex"));
        var client = new RecordingCodexClient(new UnauthorizedAccessException("auth required"));
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.LocalAppServer;
        config.Cli.CommandPathOverride = " \"C:\\Program Files\\Codex\\codex.cmd\" ";
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            commandProbe,
            client);

        await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.Equal(0, commandProbe.InspectCount);
        Assert.Equal(@"C:\Program Files\Codex\codex.cmd", Assert.Single(client.LastProbe?.Paths ?? []));
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_FallsBackToPathProbeWithoutCommandOverride()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var commandProbe = new CountingCommandProbe(CommandProbeResult.Found("codex", [@"C:\Tools\codex.exe"]));
        var client = new RecordingCodexClient(new UnauthorizedAccessException("auth required"));
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.LocalAppServer;
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            commandProbe,
            client);

        await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.Equal(1, commandProbe.InspectCount);
        Assert.Equal(@"C:\Tools\codex.exe", Assert.Single(client.LastProbe?.Paths ?? []));
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_ReturnsUnsupportedWhenCodexCliCannotStart()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            FixedCommandProbe.Found("codex", @"C:\Program Files\WindowsApps\Codex\codex.exe"),
            new ThrowingCodexClient(new Win32Exception(5, "Access is denied.")));
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.Unsupported, result.Snapshot?.Health);
        Assert.Contains("could not start", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app execution alias", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider CLI command override", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("found on PATH", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("startup failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProviderRegistry_CodexCliFallbackWithoutServicesReportsCliSource()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var registry = new ProviderRegistry();
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.Cli;

        var adapter = registry.CreateAdapter(descriptor, config);

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DataSourceKind.Cli, result.Snapshot?.SourceKind);
    }

    [Fact]
    public async Task ProviderRegistry_ChatGptAppServerReportsLocalAppServerSource()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.ChatGPT);
        var registry = new ProviderRegistry(
            FixedCommandProbe.Found("codex"),
            new SuccessfulCodexClient());
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.LocalAppServer;

        var adapter = registry.CreateAdapter(descriptor, config);
        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DataSourceKind.LocalAppServer, result.Snapshot?.SourceKind);
        Assert.Equal(ProviderId.ChatGPT, result.Snapshot?.ProviderId);
    }

    [Fact]
    public async Task GitHubCopilotMetricsProviderAdapter_ReturnsAuthRequiredWhenSecretIsMissing()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var adapter = new GitHubCopilotMetricsProviderAdapter(
            descriptor,
            new FixedSecretResolver(null),
            new RecordingGitHubCopilotMetricsClient());
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.OfficialApi;
        config.GitHubCopilot.Organization = "octo-org";
        config.GitHubCopilot.PatSecretName = "copilot-pat";

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, DateTimeOffset.UtcNow, "test"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.AuthRequired, result.Snapshot?.Health);
        Assert.Contains("PAT secret", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubCopilotMetricsProviderAdapter_ReturnsReportSnapshotWithoutLeakingToken()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.GitHubCopilot);
        var metricsClient = new RecordingGitHubCopilotMetricsClient();
        var adapter = new GitHubCopilotMetricsProviderAdapter(
            descriptor,
            new FixedSecretResolver("sample-github-token"),
            metricsClient);
        var config = ProviderConfig.CreateDefault(descriptor);
        config.SourceKind = DataSourceKind.OfficialApi;
        config.GitHubCopilot.Organization = "octo-org";
        config.GitHubCopilot.PatSecretName = "copilot-pat";

        var result = await adapter.FetchAsync(
            new ProviderFetchContext(config, new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero), "test"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ProviderHealth.Ok, result.Snapshot?.Health);
        Assert.Equal(DataSourceKind.OfficialApi, result.Snapshot?.SourceKind);
        Assert.Equal("octo-org", result.Snapshot?.Identity?.Organization);
        Assert.Equal(2, result.Snapshot?.PrimaryWindow?.Used);
        Assert.Equal("2026-06-01 to 2026-06-28", result.Snapshot?.PrimaryWindow?.ResetDescription);
        Assert.Equal(GitHubCopilotMetricsScope.Organization, metricsClient.LastRequest?.Scope);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("sample-github-token", StringComparison.Ordinal));
    }

    private sealed class FixedCommandProbe(CommandProbeResult result) : ICommandProbe
    {
        public static FixedCommandProbe Missing(string commandName)
        {
            return new FixedCommandProbe(CommandProbeResult.Missing(commandName));
        }

        public static FixedCommandProbe Found(string commandName, params string[] paths)
        {
            var resolvedPaths = paths.Length == 0
                ? [$@"C:\Tools\{commandName}.exe"]
                : paths;
            return new FixedCommandProbe(CommandProbeResult.Found(commandName, resolvedPaths));
        }

        public Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result.IsFound);
        }

        public Task<CommandProbeResult> InspectAsync(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class CountingCommandProbe(CommandProbeResult result) : ICommandProbe
    {
        public int InspectCount { get; private set; }

        public Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result.IsFound);
        }

        public Task<CommandProbeResult> InspectAsync(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingCodexClient(Exception exception) : ICodexAppServerClient
    {
        public Task<CodexAppServerData> FetchAccountUsageAsync(
            CommandProbeResult commandProbe,
            CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class SuccessfulCodexClient : ICodexAppServerClient
    {
        public Task<CodexAppServerData> FetchAccountUsageAsync(
            CommandProbeResult commandProbe,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string usage = """
            {
              "result": {
                "used": 20,
                "limit": 100
              }
            }
            """;

            return Task.FromResult(new CodexAppServerData(
                AccountJson: null,
                RateLimitsJson: null,
                UsageJson: usage,
                Diagnostics: []));
        }
    }

    private sealed class RecordingCodexClient(Exception exception) : ICodexAppServerClient
    {
        public CommandProbeResult? LastProbe { get; private set; }

        public Task<CodexAppServerData> FetchAccountUsageAsync(
            CommandProbeResult commandProbe,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProbe = commandProbe;
            throw exception;
        }
    }

    private sealed class FixedSecretResolver(string? secret) : ISecretResolver
    {
        public Task<string?> ResolveSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(secret);
        }
    }

    private sealed class RecordingGitHubCopilotMetricsClient : IGitHubCopilotMetricsClient
    {
        public GitHubCopilotMetricsRequest? LastRequest { get; private set; }

        public Task<GitHubCopilotMetricsFetchResult> FetchLatestReportAsync(
            GitHubCopilotMetricsRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            var report = new GitHubCopilotMetricsReport(
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 6, 28),
                ReportDay: null,
                DownloadLinkCount: 2);
            return Task.FromResult(GitHubCopilotMetricsFetchResult.FromReport(report, "report ok"));
        }
    }
}
