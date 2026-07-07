using WinAiUsageBar.Core.Abstractions;
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
        var probe = await commandProbe.InspectAsync(commandName, cancellationToken).ConfigureAwait(false);

        if (!probe.IsFound)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                DataSourceKind.Cli,
                context.Now,
                missingMessage,
                probe.StatusMessage);
        }

        return ProviderFetchResult.Failure(
            Descriptor,
            ProviderHealth.AuthRequired,
            DataSourceKind.Cli,
            context.Now,
            installedMessage,
            probe.StatusMessage,
            $"{commandName} command exists; automatic usage source is not implemented in the MVP.");
    }
}
