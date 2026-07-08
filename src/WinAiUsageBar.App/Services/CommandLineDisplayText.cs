using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.App.Services;

internal static class CommandLineDisplayText
{
    public static string Safe(string? value, string fallback = "n/a")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : DiagnosticRedactor.RedactForDisplay(value);
    }

    public static string Safe(Uri? value, string fallback = "n/a")
    {
        return value is null ? fallback : Safe(value.ToString(), fallback);
    }
}
