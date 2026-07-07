using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Manual;

public sealed class ManualProviderAdapter(ProviderDescriptor descriptor) : IProviderAdapter
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    public Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = UsageSnapshotMapper.FromManual(
            Descriptor,
            context.ProviderConfig.Manual,
            context.Now);

        return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot, "Manual values loaded from config."));
    }
}
