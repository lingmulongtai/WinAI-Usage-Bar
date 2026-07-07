using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Core.Models;

public sealed record UsageWindow(
    string Label,
    double? UsedPercent,
    double? RemainingPercent,
    DateTimeOffset? ResetsAt,
    string? ResetDescription,
    string? Unit,
    double? Used,
    double? Limit);

public sealed record ProviderIdentity(
    string? Email,
    string? AccountName,
    string? PlanName,
    string? Organization);

public sealed record ProviderCredits(
    decimal? Balance,
    string? Currency,
    decimal? MonthToDateCost,
    long? TokensLast31Days);

public sealed record UsageSnapshot(
    ProviderId ProviderId,
    string DisplayName,
    ProviderHealth Health,
    ProviderIdentity? Identity,
    UsageWindow? PrimaryWindow,
    UsageWindow? SecondaryWindow,
    ProviderCredits? Credits,
    DataSourceKind SourceKind,
    DateTimeOffset UpdatedAt,
    string? StatusMessage,
    string? ErrorMessage);

public sealed record ProviderDescriptor(
    ProviderId Id,
    string DisplayName,
    string ShortName,
    bool IsEnabledByDefault,
    bool SupportsLogin,
    bool SupportsCredits,
    bool SupportsStatusPolling,
    IReadOnlyList<DataSourceKind> SupportedSources);

public sealed record ProviderFetchResult(
    UsageSnapshot? Snapshot,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> Diagnostics)
{
    public static ProviderFetchResult FromSnapshot(UsageSnapshot snapshot, params string[] diagnostics)
    {
        return new ProviderFetchResult(snapshot, true, null, diagnostics);
    }

    public static ProviderFetchResult Failure(
        ProviderDescriptor descriptor,
        ProviderHealth health,
        DataSourceKind sourceKind,
        DateTimeOffset now,
        string message,
        params string[] diagnostics)
    {
        var snapshot = new UsageSnapshot(
            descriptor.Id,
            descriptor.DisplayName,
            health,
            Identity: null,
            PrimaryWindow: null,
            SecondaryWindow: null,
            Credits: null,
            sourceKind,
            now,
            StatusMessage: message,
            ErrorMessage: message);

        return new ProviderFetchResult(snapshot, false, message, diagnostics);
    }
}

public sealed record ProviderFetchContext(
    ProviderConfig ProviderConfig,
    DateTimeOffset Now,
    string AppDataDirectory);
