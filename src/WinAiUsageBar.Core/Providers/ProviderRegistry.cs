using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers.Codex;
using WinAiUsageBar.Core.Providers.GitHubCopilot;
using WinAiUsageBar.Core.Providers.Manual;
using WinAiUsageBar.Core.Providers.Mock;

namespace WinAiUsageBar.Core.Providers;

public interface IProviderAdapterSource
{
    IReadOnlyList<ProviderDescriptor> GetDescriptors();

    IProviderAdapter CreateAdapter(ProviderDescriptor descriptor, ProviderConfig config);
}

public sealed class ProviderRegistry(
    ICommandProbe? commandProbe = null,
    ICodexAppServerClient? codexAppServerClient = null,
    ISecretResolver? secretResolver = null,
    IGitHubCopilotMetricsClient? gitHubCopilotMetricsClient = null) : IProviderAdapterSource
{
    public IReadOnlyList<ProviderDescriptor> GetDescriptors()
    {
        return ProviderDescriptors.All;
    }

    public IProviderAdapter CreateAdapter(ProviderDescriptor descriptor, ProviderConfig config)
    {
        if (!descriptor.SupportedSources.Contains(config.SourceKind))
        {
            return new UnsupportedProviderAdapter(
                descriptor,
                config.SourceKind,
                $"{descriptor.DisplayName} does not support {config.SourceKind} source.");
        }

        return config.SourceKind switch
        {
            DataSourceKind.Manual => new ManualProviderAdapter(descriptor),
            DataSourceKind.Mock => new MockProviderAdapter(descriptor),
            DataSourceKind.LocalAppServer when descriptor.Id is ProviderId.Codex or ProviderId.ChatGPT =>
                CreateCodexAdapter(descriptor, config.SourceKind),
            DataSourceKind.Cli when descriptor.Id is ProviderId.Codex =>
                CreateCodexAdapter(descriptor, config.SourceKind),
            DataSourceKind.Cli when descriptor.Id is ProviderId.Claude or ProviderId.ClaudeCode =>
                CreateClaudeAdapter(descriptor),
            DataSourceKind.OfficialApi when descriptor.Id == ProviderId.Gemini =>
                new UnsupportedProviderAdapter(
                    descriptor,
                    DataSourceKind.OfficialApi,
                    "Gemini API key setup is available, but usage retrieval is not implemented until an official endpoint is selected."),
            DataSourceKind.OfficialApi when descriptor.Id == ProviderId.OpenCodeZen =>
                new UnsupportedProviderAdapter(
                    descriptor,
                    DataSourceKind.OfficialApi,
                    "OpenCode Zen balance API support is a TODO until a documented endpoint is confirmed."),
            DataSourceKind.OfficialApi when descriptor.Id == ProviderId.GitHubCopilot =>
                CreateGitHubCopilotAdapter(descriptor),
            _ => new UnsupportedProviderAdapter(
                descriptor,
                config.SourceKind,
                $"{descriptor.DisplayName} automatic usage is unsupported in the MVP.")
        };
    }

    public IReadOnlyList<IProviderAdapter> CreateEnabledAdapters(AppConfig config)
    {
        var adapters = new List<IProviderAdapter>();

        foreach (var descriptor in GetDescriptors())
        {
            var providerConfig = config.GetOrCreateProvider(descriptor);
            if (!providerConfig.IsEnabled)
            {
                continue;
            }

            adapters.Add(CreateAdapter(descriptor, providerConfig));
        }

        return adapters;
    }

    private IProviderAdapter CreateCodexAdapter(ProviderDescriptor descriptor, DataSourceKind sourceKind)
    {
        if (commandProbe is null || codexAppServerClient is null)
        {
            return new UnsupportedProviderAdapter(
                descriptor,
                sourceKind,
                "Codex app-server source needs process services; use Manual mode when running without Infrastructure.");
        }

        return new CodexAppServerProviderAdapter(descriptor, commandProbe, codexAppServerClient, sourceKind);
    }

    private IProviderAdapter CreateClaudeAdapter(ProviderDescriptor descriptor)
    {
        if (commandProbe is null)
        {
            return new UnsupportedProviderAdapter(
                descriptor,
                DataSourceKind.Cli,
                "Claude CLI probing needs process services; use Manual mode when running without Infrastructure.");
        }

        return new CliProbeProviderAdapter(
            descriptor,
            commandProbe,
            "claude",
            $"{descriptor.DisplayName} CLI source can only verify the shared claude command in the MVP. Automatic usage retrieval is not implemented; the app does not run interactive usage commands, scrape private local files, or invent unofficial endpoints. Use Manual mode until official usage telemetry or SDK support is added.",
            $"{descriptor.DisplayName} CLI source needs a launchable claude command. Install or sign in to the Claude CLI, set a provider CLI command override, or use Manual mode.");
    }

    private IProviderAdapter CreateGitHubCopilotAdapter(ProviderDescriptor descriptor)
    {
        if (secretResolver is null || gitHubCopilotMetricsClient is null)
        {
            return new UnsupportedProviderAdapter(
                descriptor,
                DataSourceKind.OfficialApi,
                "GitHub Copilot organization or enterprise metrics need secret and HTTP services; personal use remains Manual mode.");
        }

        return new GitHubCopilotMetricsProviderAdapter(
            descriptor,
            secretResolver,
            gitHubCopilotMetricsClient);
    }
}
