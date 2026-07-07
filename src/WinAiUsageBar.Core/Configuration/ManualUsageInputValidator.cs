using System.Globalization;

namespace WinAiUsageBar.Core.Configuration;

public sealed record ManualUsageInput(
    string? UsedPercent,
    string? RemainingPercent,
    string? ResetDateTime,
    string? CreditBalance,
    string? MonthToDateCost,
    string? Notes);

public sealed record ManualUsageValidationResult(
    ManualUsageSettings Settings,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ManualUsageInputValidator
{
    public static ManualUsageValidationResult Parse(
        ManualUsageSettings current,
        ManualUsageInput input)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var settings = new ManualUsageSettings
        {
            ResetDescription = current.ResetDescription,
            Currency = current.Currency,
            TokensLast31Days = current.TokensLast31Days,
            Notes = TrimToNull(input.Notes)
        };

        settings.UsedPercent = ParsePercent(input.UsedPercent, "Used %", errors, warnings);
        settings.RemainingPercent = ParsePercent(input.RemainingPercent, "Remaining %", errors, warnings);
        settings.ResetsAt = ParseDateTime(input.ResetDateTime, "Reset datetime", errors);
        settings.CreditBalance = ParseMoney(input.CreditBalance, "Credits", errors);
        settings.MonthToDateCost = ParseMoney(input.MonthToDateCost, "Month cost", errors);

        if (settings.UsedPercent is double used
            && settings.RemainingPercent is double remaining
            && Math.Abs(used + remaining - 100) > 0.01)
        {
            errors.Add("Used % and Remaining % must add up to 100, or leave one field blank.");
        }

        return new ManualUsageValidationResult(settings, errors, warnings);
    }

    private static double? ParsePercent(
        string? text,
        string label,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        var normalized = TrimToNull(text);
        if (normalized is null)
        {
            return null;
        }

        if (!double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
        {
            errors.Add($"{label} must be a number from 0 to 100.");
            return null;
        }

        var clamped = Math.Clamp(value, 0, 100);
        if (Math.Abs(clamped - value) > 0.001)
        {
            warnings.Add($"{label} was clamped to {clamped:0.##}.");
        }

        return Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset? ParseDateTime(
        string? text,
        string label,
        ICollection<string> errors)
    {
        var normalized = TrimToNull(text);
        if (normalized is null)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
            normalized,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
            out var value))
        {
            return value;
        }

        errors.Add($"{label} must be an ISO-like datetime, for example 2026-07-08T12:00:00Z.");
        return null;
    }

    private static decimal? ParseMoney(
        string? text,
        string label,
        ICollection<string> errors)
    {
        var normalized = TrimToNull(text);
        if (normalized is null)
        {
            return null;
        }

        if (!decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value))
        {
            errors.Add($"{label} must be a valid number.");
            return null;
        }

        if (value < 0)
        {
            errors.Add($"{label} cannot be negative.");
            return null;
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
