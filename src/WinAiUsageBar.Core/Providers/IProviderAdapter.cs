using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers;

public interface IProviderAdapter
{
    ProviderDescriptor Descriptor { get; }

    Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken);
}
