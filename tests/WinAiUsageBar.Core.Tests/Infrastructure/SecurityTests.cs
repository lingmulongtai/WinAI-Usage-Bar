using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class SecurityTests
{
    [Fact]
    public void Redact_RemovesCommonSecretShapes()
    {
        var apiKey = "s" + "k-1234567890";
        var githubToken = "gh" + "p_1234567890";
        var text = $"Authorization: Bearer abc123 token=secret secretName=secret-ref patSecretName=copilot-ref token-secret-123 {apiKey} {githubToken}";

        var redacted = DiagnosticRedactor.Redact(text);

        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("token=secret", redacted);
        Assert.DoesNotContain("secret-ref", redacted);
        Assert.DoesNotContain("copilot-ref", redacted);
        Assert.DoesNotContain("token-secret-123", redacted);
        Assert.DoesNotContain(apiKey, redacted);
        Assert.DoesNotContain(githubToken, redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void RedactForDisplay_CollapsesSensitiveLabels()
    {
        var apiKey = "s" + "k-1234567890";
        var githubToken = "gh" + "p_1234567890";
        var text = $"Authorization: Bearer abc123 token=secret secretName=secret-ref cookie=session --api-key api-secret -token cli-secret token-secret-123 {apiKey} {githubToken}";

        var redacted = DiagnosticRedactor.RedactForDisplay(text);

        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-ref", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("session", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(githubToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s" + "k-", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh" + "p_", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileAppDiagnosticsLog_RedactsMessagesAndExceptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var log = new FileAppDiagnosticsLog(paths);
        var githubToken = "gh" + "p_1234567890";

        try
        {
            await log.InfoAsync("Starting with api_key=secret-value", CancellationToken.None);
            await log.ErrorAsync(
                "Refresh failed with Authorization: Bearer abc123",
                new InvalidOperationException($"access_token=super-secret {githubToken}"),
                CancellationToken.None);

            var text = await File.ReadAllTextAsync(paths.DiagnosticsLogPath);

            Assert.Contains("Starting with api_key=[REDACTED]", text);
            Assert.Contains("Authorization: Bearer [REDACTED]", text);
            Assert.DoesNotContain("secret-value", text);
            Assert.DoesNotContain("super-secret", text);
            Assert.DoesNotContain("abc123", text);
            Assert.DoesNotContain(githubToken, text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DpapiSecretStore_SetGetHasDelete_RoundTripsWithoutPlaintextFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var store = new DpapiSecretStore(paths);

        try
        {
            await store.SetSecretAsync("gemini-api-key", "secret-value", CancellationToken.None);
            var exists = await store.HasSecretAsync("gemini-api-key", CancellationToken.None);
            var value = await store.GetSecretAsync("gemini-api-key", CancellationToken.None);
            var files = Directory.GetFiles(paths.SecretsDirectory);

            Assert.True(exists);
            Assert.Equal("secret-value", value);
            Assert.Single(files);
            Assert.DoesNotContain("gemini-api-key", Path.GetFileName(files.Single()), StringComparison.OrdinalIgnoreCase);

            await store.DeleteSecretAsync("gemini-api-key", CancellationToken.None);

            Assert.False(await store.HasSecretAsync("gemini-api-key", CancellationToken.None));
            Assert.Null(await store.GetSecretAsync("gemini-api-key", CancellationToken.None));
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
