using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Mock;

public sealed class MockProviderAdapter(ProviderDescriptor descriptor) : IProviderAdapter
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    public Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var seed = (int)Descriptor.Id * 13 + context.Now.Minute;
        var usedPercent = Math.Clamp(28 + seed % 57, 0, 100);
        var remainingPercent = 100 - usedPercent;

        var health = remainingPercent switch
        {
            < 10 => ProviderHealth.Error,
            < 20 => ProviderHealth.Warning,
            _ => ProviderHealth.Ok
        };

        var primaryWindow = new UsageWindow(
            "Current quota",
            usedPercent,
            remainingPercent,
            context.Now.AddHours(2 + ((int)Descriptor.Id % 5)),
            "resets soon",
            "%",
            usedPercent,
            100);

        var credits = Descriptor.SupportsCredits
            ? new ProviderCredits(
                Balance: Math.Round(25.0m + (int)Descriptor.Id * 3.2m, 2),
                Currency: "USD",
                MonthToDateCost: Math.Round(4.5m + (int)Descriptor.Id * 1.35m, 2),
                TokensLast31Days: 1_250_000 + ((int)Descriptor.Id * 175_000))
            : null;

        var snapshot = new UsageSnapshot(
            Descriptor.Id,
            Descriptor.DisplayName,
            health,
            new ProviderIdentity(
                Email: $"demo-{Descriptor.ShortName.ToLowerInvariant()}@example.local",
                AccountName: "Demo account",
                PlanName: Descriptor.SupportsCredits ? "Team" : "Personal",
                Organization: null),
            primaryWindow,
            SecondaryWindow: null,
            credits,
            DataSourceKind.Mock,
            context.Now,
            "Mock data for UI development",
            ErrorMessage: null);

        return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot, "Mock provider returned deterministic demo data."));
    }
}
