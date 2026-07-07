using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class SecurityTests
{
    [Fact]
    public void Redact_RemovesCommonSecretShapes()
    {
        var text = "Authorization: Bearer abc123 token=secret sk-1234567890 ghp_1234567890";

        var redacted = DiagnosticRedactor.Redact(text);

        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("secret", redacted);
        Assert.DoesNotContain("sk-1234567890", redacted);
        Assert.DoesNotContain("ghp_1234567890", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }
}
