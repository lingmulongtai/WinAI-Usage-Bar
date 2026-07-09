using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers;

public sealed class CliProbeProviderAdapter(
    ProviderDescriptor descriptor,
    ICommandProbe commandProbe,
    string commandName,
    string installedMessage,
    string missingMessage) : IProviderAdapter
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    public async Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        var configuredOverride = CliCommandSettings.NormalizeCommandPathOverride(
            context.ProviderConfig.Cli.CommandPathOverride);
        var hasConfiguredOverride = configuredOverride is not null;
        var probe = hasConfiguredOverride
            ? CommandProbeResult.Configured(commandName, configuredOverride!)
            : await commandProbe.InspectAsync(commandName, cancellationToken).ConfigureAwait(false);

        if (!probe.IsFound)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                DataSourceKind.Cli,
                context.Now,
                missingMessage,
                probe.StatusMessage,
                "Manual mode remains available; no private local files were read.");
        }

        return ProviderFetchResult.Failure(
            Descriptor,
            ProviderHealth.Unsupported,
            DataSourceKind.Cli,
            context.Now,
            installedMessage,
            BuildUsageUnavailableDiagnostics(commandName, probe, hasConfiguredOverride));
    }

    private static string[] BuildUsageUnavailableDiagnostics(
        string commandName,
        CommandProbeResult probe,
        bool hasConfiguredOverride)
    {
        var diagnostics = new List<string>
        {
            probe.StatusMessage
        };

        if (hasConfiguredOverride)
        {
            diagnostics.Add($"{commandName} provider CLI command override is configured; the value is not included in diagnostics.");
        }

        diagnostics.Add($"{commandName} CLI usage retrieval is not implemented in the MVP.");
        diagnostics.Add("No interactive CLI usage commands, browser cookies, auth files, or private local usage files were read.");
        return diagnostics.ToArray();
    }
}
