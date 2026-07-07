using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers;

public sealed class UnsupportedProviderAdapter(
    ProviderDescriptor descriptor,
    DataSourceKind sourceKind,
    string message) : IProviderAdapter
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    public Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ProviderFetchResult.Failure(
            Descriptor,
            ProviderHealth.Unsupported,
            sourceKind,
            context.Now,
            message,
            "Automatic provider source is not implemented in the MVP."));
    }
}
