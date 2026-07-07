using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers;

public static class ProviderDescriptors
{
    public static IReadOnlyList<ProviderDescriptor> All { get; } =
    [
        new ProviderDescriptor(
            ProviderId.ChatGPT,
            "ChatGPT",
            "GPT",
            IsEnabledByDefault: true,
            SupportsLogin: true,
            SupportsCredits: true,
            SupportsStatusPolling: true,
            [DataSourceKind.Manual, DataSourceKind.Mock, DataSourceKind.LocalAppServer]),

        new ProviderDescriptor(
            ProviderId.Codex,
            "Codex",
            "Codex",
            IsEnabledByDefault: true,
            SupportsLogin: true,
            SupportsCredits: true,
            SupportsStatusPolling: true,
            [DataSourceKind.Manual, DataSourceKind.Mock, DataSourceKind.LocalAppServer, DataSourceKind.Cli]),

        new ProviderDescriptor(
            ProviderId.Gemini,
            "Gemini",
            "Gemini",
            IsEnabledByDefault: false,
            SupportsLogin: true,
            SupportsCredits: false,
            SupportsStatusPolling: false,
            [DataSourceKind.Manual, DataSourceKind.OfficialApi]),

        new ProviderDescriptor(
            ProviderId.Claude,
            "Claude",
            "Claude",
            IsEnabledByDefault: false,
            SupportsLogin: true,
            SupportsCredits: true,
            SupportsStatusPolling: false,
            [DataSourceKind.Manual, DataSourceKind.Cli]),

        new ProviderDescriptor(
            ProviderId.ClaudeCode,
            "Claude Code",
            "Claude",
            IsEnabledByDefault: false,
            SupportsLogin: true,
            SupportsCredits: true,
            SupportsStatusPolling: false,
            [DataSourceKind.Manual, DataSourceKind.Cli]),

        new ProviderDescriptor(
            ProviderId.OpenCodeZen,
            "OpenCode Zen",
            "Zen",
            IsEnabledByDefault: false,
            SupportsLogin: true,
            SupportsCredits: true,
            SupportsStatusPolling: false,
            [DataSourceKind.Manual, DataSourceKind.OfficialApi]),

        new ProviderDescriptor(
            ProviderId.GitHubCopilot,
            "GitHub Copilot",
            "Copilot",
            IsEnabledByDefault: false,
            SupportsLogin: true,
            SupportsCredits: false,
            SupportsStatusPolling: true,
            [DataSourceKind.Manual, DataSourceKind.OfficialApi])
    ];

    public static ProviderDescriptor Get(ProviderId providerId)
    {
        return All.First(descriptor => descriptor.Id == providerId);
    }
}
