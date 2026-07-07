using System.Text.RegularExpressions;

namespace WinAiUsageBar.Infrastructure.Security;

public static partial class DiagnosticRedactor
{
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var redacted = AuthorizationRegex().Replace(text, "$1[REDACTED]");
        redacted = KeyValueSecretRegex().Replace(redacted, "$1[REDACTED]");
        redacted = OpenAiStyleKeyRegex().Replace(redacted, "sk-[REDACTED]");
        redacted = GitHubTokenRegex().Replace(redacted, "ghp_[REDACTED]");
        return redacted;
    }

    public static IReadOnlyList<string> RedactAll(IEnumerable<string> diagnostics)
    {
        return diagnostics.Select(Redact).ToList();
    }

    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s,;]+")]
    private static partial Regex AuthorizationRegex();

    [GeneratedRegex(@"(?i)((?:""?(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret|cookie)""?)\s*[:=]\s*""?)[^""\s,;}]+")]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{8,}")]
    private static partial Regex OpenAiStyleKeyRegex();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{8,}")]
    private static partial Regex GitHubTokenRegex();
}
