using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineProviderCatalogFormatterTests
{
    [Fact]
    public void Format_ListsProviderDescriptors()
    {
        var report = CommandLineProviderCatalogFormatter.Format(ProviderDescriptors.All);

        Assert.Contains("Provider catalog", report, StringComparison.Ordinal);
        Assert.Contains($"Total providers: {ProviderDescriptors.All.Count}", report, StringComparison.Ordinal);
        Assert.Contains("ChatGPT (ChatGPT)", report, StringComparison.Ordinal);
        Assert.Contains("Codex (Codex)", report, StringComparison.Ordinal);
        Assert.Contains("Sources: Manual, Mock, LocalAppServer, Cli", report, StringComparison.Ordinal);
        Assert.Contains("GitHub Copilot (GitHubCopilot)", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_UsesDescriptorValuesWithoutConfig()
    {
        var descriptors = new[]
        {
            new ProviderDescriptor(
                ProviderId.Gemini,
                "Gemini",
                "Gemini",
                IsEnabledByDefault: false,
                SupportsLogin: true,
                SupportsCredits: false,
                SupportsStatusPolling: false,
                [DataSourceKind.Manual, DataSourceKind.OfficialApi])
        };

        var report = CommandLineProviderCatalogFormatter.Format(descriptors);

        Assert.Contains("Total providers: 1", report, StringComparison.Ordinal);
        Assert.Contains("Enabled by default: no", report, StringComparison.Ordinal);
        Assert.Contains("Supports login: yes", report, StringComparison.Ordinal);
        Assert.Contains("Supports credits: no", report, StringComparison.Ordinal);
        Assert.Contains("Sources: Manual, OfficialApi", report, StringComparison.Ordinal);
    }
}
