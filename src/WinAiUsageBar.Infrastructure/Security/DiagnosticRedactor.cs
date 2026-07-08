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

    public static string RedactForDisplay(string? text)
    {
        var redacted = Redact(text);
        if (string.IsNullOrEmpty(redacted))
        {
            return string.Empty;
        }

        redacted = DisplayKeyValueSecretRegex().Replace(redacted, "[REDACTED]");
        redacted = DisplaySwitchSecretRegex().Replace(redacted, "[REDACTED]");
        redacted = DisplayOpenAiStyleKeyRegex().Replace(redacted, "[REDACTED]");
        redacted = DisplayGitHubTokenRegex().Replace(redacted, "[REDACTED]");
        redacted = DisplaySecretishTokenRegex().Replace(redacted, "[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s,;]+")]
    private static partial Regex AuthorizationRegex();

    [GeneratedRegex(@"(?i)((?:""?(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret(?:[_-]?name)?|pat[_-]?secret(?:[_-]?name)?|cookie)""?)\s*[:=]\s*""?)[^""\s,;}]+")]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{8,}")]
    private static partial Regex OpenAiStyleKeyRegex();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{8,}")]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"(?i)\b(?:authorization\s*[:=]\s*bearer\s+|""?(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret(?:[_-]?name)?|pat[_-]?secret(?:[_-]?name)?|cookie)""?\s*[:=]\s*""?)\[REDACTED\]""?")]
    private static partial Regex DisplayKeyValueSecretRegex();

    [GeneratedRegex(@"(?i)(?:--?|/)(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret(?:[_-]?name)?|pat[_-]?secret(?:[_-]?name)?|cookie)(?:\s+|=)""?[^""\s,;}]+")]
    private static partial Regex DisplaySwitchSecretRegex();

    [GeneratedRegex(@"sk-\[REDACTED\]")]
    private static partial Regex DisplayOpenAiStyleKeyRegex();

    [GeneratedRegex(@"ghp_\[REDACTED\]")]
    private static partial Regex DisplayGitHubTokenRegex();

    [GeneratedRegex(@"(?i)\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret|cookie|pat)[_-][A-Za-z0-9][A-Za-z0-9._-]{5,}\b")]
    private static partial Regex DisplaySecretishTokenRegex();
}
