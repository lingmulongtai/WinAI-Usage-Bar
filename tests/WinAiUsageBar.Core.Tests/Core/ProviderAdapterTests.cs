using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Core.Providers.Codex;
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
            new FixedCommandProbe(true),
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
            new FixedCommandProbe(false),
            new ThrowingCodexClient(new InvalidOperationException("client should not be called")));
        var context = new ProviderFetchContext(
            ProviderConfig.CreateDefault(descriptor),
            DateTimeOffset.UtcNow,
            "test");

        var result = await adapter.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProviderHealth.Unsupported, result.Snapshot?.Health);
        Assert.Equal(DataSourceKind.LocalAppServer, result.Snapshot?.SourceKind);
    }

    [Fact]
    public async Task CodexAppServerProviderAdapter_ReturnsAuthRequiredWhenClientRejects()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var adapter = new CodexAppServerProviderAdapter(
            descriptor,
            new FixedCommandProbe(true),
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

    private sealed class FixedCommandProbe(bool exists) : ICommandProbe
    {
        public Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(exists);
        }
    }

    private sealed class ThrowingCodexClient(Exception exception) : ICodexAppServerClient
    {
        public Task<CodexAppServerData> FetchAccountUsageAsync(CancellationToken cancellationToken)
        {
            throw exception;
        }
    }
}
