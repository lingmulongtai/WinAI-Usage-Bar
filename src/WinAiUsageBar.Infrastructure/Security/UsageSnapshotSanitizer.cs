using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Infrastructure.Security;

public static class UsageSnapshotSanitizer
{
    public static UsageSnapshot Sanitize(UsageSnapshot snapshot)
    {
        return snapshot with
        {
            DisplayName = RedactRequired(snapshot.DisplayName),
            Identity = SanitizeIdentity(snapshot.Identity),
            PrimaryWindow = SanitizeWindow(snapshot.PrimaryWindow),
            SecondaryWindow = SanitizeWindow(snapshot.SecondaryWindow),
            Credits = SanitizeCredits(snapshot.Credits),
            StatusMessage = RedactOptional(snapshot.StatusMessage),
            ErrorMessage = RedactOptional(snapshot.ErrorMessage)
        };
    }

    private static ProviderIdentity? SanitizeIdentity(ProviderIdentity? identity)
    {
        return identity is null
            ? null
            : new ProviderIdentity(
                RedactOptional(identity.Email),
                RedactOptional(identity.AccountName),
                RedactOptional(identity.PlanName),
                RedactOptional(identity.Organization));
    }

    private static UsageWindow? SanitizeWindow(UsageWindow? window)
    {
        return window is null
            ? null
            : window with
            {
                Label = RedactRequired(window.Label),
                ResetDescription = RedactOptional(window.ResetDescription),
                Unit = RedactOptional(window.Unit)
            };
    }

    private static ProviderCredits? SanitizeCredits(ProviderCredits? credits)
    {
        return credits is null
            ? null
            : credits with
            {
                Currency = RedactOptional(credits.Currency)
            };
    }

    private static string RedactRequired(string text)
    {
        return DiagnosticRedactor.Redact(text);
    }

    private static string? RedactOptional(string? text)
    {
        return text is null ? null : DiagnosticRedactor.Redact(text);
    }
}
