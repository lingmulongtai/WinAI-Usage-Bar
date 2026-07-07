using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers;

public static class UsageSnapshotMapper
{
    public static UsageSnapshot FromManual(
        ProviderDescriptor descriptor,
        ManualUsageSettings manual,
        DateTimeOffset now)
    {
        var usedPercent = NormalizePercent(manual.UsedPercent);
        var remainingPercent = NormalizePercent(manual.RemainingPercent);

        if (usedPercent is null && remainingPercent is not null)
        {
            usedPercent = Math.Round(100 - remainingPercent.Value, 2);
        }

        if (remainingPercent is null && usedPercent is not null)
        {
            remainingPercent = Math.Round(100 - usedPercent.Value, 2);
        }

        var health = remainingPercent switch
        {
            null => ProviderHealth.Unknown,
            < 10 => ProviderHealth.Error,
            < 20 => ProviderHealth.Warning,
            _ => ProviderHealth.Ok
        };

        var primaryWindow = new UsageWindow(
            "Manual usage",
            usedPercent,
            remainingPercent,
            manual.ResetsAt,
            manual.ResetDescription,
            "%",
            usedPercent,
            100);

        var credits = manual.CreditBalance is null
            && manual.MonthToDateCost is null
            && manual.TokensLast31Days is null
                ? null
                : new ProviderCredits(
                    manual.CreditBalance,
                    manual.Currency,
                    manual.MonthToDateCost,
                    manual.TokensLast31Days);

        var status = string.IsNullOrWhiteSpace(manual.Notes)
            ? "Manual mode"
            : manual.Notes;

        return new UsageSnapshot(
            descriptor.Id,
            descriptor.DisplayName,
            health,
            Identity: null,
            primaryWindow,
            SecondaryWindow: null,
            credits,
            DataSourceKind.Manual,
            now,
            status,
            ErrorMessage: null);
    }

    public static double? NormalizePercent(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return null;
        }

        return Math.Round(Math.Clamp(value.Value, 0, 100), 2);
    }
}
