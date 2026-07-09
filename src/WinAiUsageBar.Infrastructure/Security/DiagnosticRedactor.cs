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
        redacted = GitHubFineGrainedTokenRegex().Replace(redacted, "github_pat_[REDACTED]");
        redacted = GoogleApiKeyRegex().Replace(redacted, "AIza[REDACTED]");
        redacted = SecretishTokenRegex().Replace(redacted, "[REDACTED]");
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
        redacted = DisplayGitHubFineGrainedTokenRegex().Replace(redacted, "[REDACTED]");
        redacted = DisplayGoogleApiKeyRegex().Replace(redacted, "[REDACTED]");
        redacted = SecretishTokenRegex().Replace(redacted, "[REDACTED]");
        return redacted;
    }

    public static string RedactForSupportExport(string? text)
    {
        var redacted = Redact(text);
        if (string.IsNullOrEmpty(redacted))
        {
            return string.Empty;
        }

        redacted = SupportExportQuotedKeyValueRegex().Replace(redacted, "$1[REDACTED]$3");
        redacted = SupportExportBareKeyValueRegex().Replace(redacted, "$1[REDACTED]");
        redacted = EscapedWindowsUserProfilePathRegex().Replace(redacted, "[LOCAL_PATH]");
        redacted = WindowsUserProfilePathRegex().Replace(redacted, "[LOCAL_PATH]");
        redacted = EmailRegex().Replace(redacted, "[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s,;]+")]
    private static partial Regex AuthorizationRegex();

    [GeneratedRegex(@"(?i)((?:""?(?:x[_-]?api[_-]?key|api[_-]?key|access[_-]?token|refresh[_-]?token|personal[_-]?access[_-]?token|github[_-]?pat|pat(?:[_-]?token)?|token|client[_-]?secret|private[_-]?key|password|secret(?:[_-]?(?:name|ref(?:erence)?))?|pat[_-]?secret(?:[_-]?(?:name|ref(?:erence)?))?|cookie)""?)\s*[:=]\s*""?)[^""\s,;}]+")]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{8,}")]
    private static partial Regex OpenAiStyleKeyRegex();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{8,}")]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"github_pat_[A-Za-z0-9_]{10,}")]
    private static partial Regex GitHubFineGrainedTokenRegex();

    [GeneratedRegex(@"AIza[A-Za-z0-9_\-]{10,}")]
    private static partial Regex GoogleApiKeyRegex();

    [GeneratedRegex(@"(?i)\b(?:authorization\s*[:=]\s*bearer\s+|""?(?:x[_-]?api[_-]?key|api[_-]?key|access[_-]?token|refresh[_-]?token|personal[_-]?access[_-]?token|github[_-]?pat|pat(?:[_-]?token)?|token|client[_-]?secret|private[_-]?key|password|secret(?:[_-]?(?:name|ref(?:erence)?))?|pat[_-]?secret(?:[_-]?(?:name|ref(?:erence)?))?|cookie)""?\s*[:=]\s*""?)\[REDACTED\]""?")]
    private static partial Regex DisplayKeyValueSecretRegex();

    [GeneratedRegex(@"(?i)(?:--?|/)(?:x[_-]?api[_-]?key|api[_-]?key|access[_-]?token|refresh[_-]?token|personal[_-]?access[_-]?token|github[_-]?pat|pat(?:[_-]?token)?|token|client[_-]?secret|private[_-]?key|password|secret(?:[_-]?(?:name|ref(?:erence)?))?|pat[_-]?secret(?:[_-]?(?:name|ref(?:erence)?))?|cookie)(?:\s+|=)""?[^""\s,;}]+")]
    private static partial Regex DisplaySwitchSecretRegex();

    [GeneratedRegex(@"sk-\[REDACTED\]")]
    private static partial Regex DisplayOpenAiStyleKeyRegex();

    [GeneratedRegex(@"ghp_\[REDACTED\]")]
    private static partial Regex DisplayGitHubTokenRegex();

    [GeneratedRegex(@"github_pat_\[REDACTED\]")]
    private static partial Regex DisplayGitHubFineGrainedTokenRegex();

    [GeneratedRegex(@"AIza\[REDACTED\]")]
    private static partial Regex DisplayGoogleApiKeyRegex();

    [GeneratedRegex(@"(?i)\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|token|secret|cookie|pat)[_-][A-Za-z0-9][A-Za-z0-9._-]{5,}\b")]
    private static partial Regex SecretishTokenRegex();

    [GeneratedRegex(@"(?i)((?:""?(?:email|user[_-]?email|account(?:[_-]?name)?|owner(?:[_-]?(?:id|name|login))?|organization(?:[_-]?(?:id|name|slug))?|org(?:[_-]?(?:id|name|slug))?|workspace(?:[_-]?(?:id|name|slug))?|enterprise(?:[_-]?(?:id|name|slug))?|scope|scopes|tenant(?:[_-]?(?:id|name|slug))?|secret(?:[_-]?(?:name|ref(?:erence)?))?|pat(?:[_-]?(?:secret|token))?[_-]?(?:name|ref(?:erence)?)|token[_-]?secret[_-]?(?:name|ref(?:erence)?)|command[_-]?(?:path[_-]?)?override|commandPathOverride|cli[_-]?(?:command|override)|provider[_-]?cli[_-]?override)""?)\s*[:=]\s*"")([^""]*)("")")]
    private static partial Regex SupportExportQuotedKeyValueRegex();

    [GeneratedRegex(@"(?i)((?:""?(?:email|user[_-]?email|account(?:[_-]?name)?|owner(?:[_-]?(?:id|name|login))?|organization(?:[_-]?(?:id|name|slug))?|org(?:[_-]?(?:id|name|slug))?|workspace(?:[_-]?(?:id|name|slug))?|enterprise(?:[_-]?(?:id|name|slug))?|scope|scopes|tenant(?:[_-]?(?:id|name|slug))?|secret(?:[_-]?(?:name|ref(?:erence)?))?|pat(?:[_-]?(?:secret|token))?[_-]?(?:name|ref(?:erence)?)|token[_-]?secret[_-]?(?:name|ref(?:erence)?)|command[_-]?(?:path[_-]?)?override|commandPathOverride|cli[_-]?(?:command|override)|provider[_-]?cli[_-]?override)""?)\s*[:=]\s*)[^\s;}]+")]
    private static partial Regex SupportExportBareKeyValueRegex();

    [GeneratedRegex(@"(?i)\b[A-Z]:\\\\Users\\\\[^\r\n""'`]+?(?=\s+[A-Za-z_][A-Za-z0-9_-]*\s*[:=]|[\r\n""'`]|$)")]
    private static partial Regex EscapedWindowsUserProfilePathRegex();

    [GeneratedRegex(@"(?i)\b[A-Z]:\\Users\\[^\r\n""'`]+?(?=\s+[A-Za-z_][A-Za-z0-9_-]*\s*[:=]|[\r\n""'`]|$)")]
    private static partial Regex WindowsUserProfilePathRegex();

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailRegex();
}
