using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

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

    [Fact]
    public async Task FileAppDiagnosticsLog_RedactsMessagesAndExceptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var log = new FileAppDiagnosticsLog(paths);

        try
        {
            await log.InfoAsync("Starting with api_key=secret-value", CancellationToken.None);
            await log.ErrorAsync(
                "Refresh failed with Authorization: Bearer abc123",
                new InvalidOperationException("access_token=super-secret ghp_1234567890"),
                CancellationToken.None);

            var text = await File.ReadAllTextAsync(paths.DiagnosticsLogPath);

            Assert.Contains("Starting with api_key=[REDACTED]", text);
            Assert.Contains("Authorization: Bearer [REDACTED]", text);
            Assert.DoesNotContain("secret-value", text);
            Assert.DoesNotContain("super-secret", text);
            Assert.DoesNotContain("abc123", text);
            Assert.DoesNotContain("ghp_1234567890", text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
