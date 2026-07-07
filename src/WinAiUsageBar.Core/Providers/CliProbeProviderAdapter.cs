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
        var exists = await commandProbe.ExistsAsync(commandName, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                DataSourceKind.Cli,
                context.Now,
                missingMessage,
                $"{commandName} command was not found on PATH.");
        }

        return ProviderFetchResult.Failure(
            Descriptor,
            ProviderHealth.AuthRequired,
            DataSourceKind.Cli,
            context.Now,
            installedMessage,
            $"{commandName} command exists; automatic usage source is not implemented in the MVP.");
    }
}
