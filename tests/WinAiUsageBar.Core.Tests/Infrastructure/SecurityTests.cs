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
        var fineGrainedPat = "github" + "_pat_" + new string('A', 18);
        var googleApiKey = "AI" + "za" + new string('B', 18);
        var text = $"Authorization: Bearer abc123 token=secret ApiKey=mixed-key ACCESS_TOKEN=upper-token githubPat=pat-value clientSecret=client-secret private_key=private-secret secretName=secret-ref cookie=session --api-key api-secret -token cli-secret /personal-access-token cli-pat token-secret-123 {apiKey} {githubToken} {fineGrainedPat} {googleApiKey}";

        var redacted = DiagnosticRedactor.RedactForDisplay(text);

        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("mixed-key", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("upper-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("pat-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("private-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-ref", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("session", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-pat", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(githubToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fineGrainedPat, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(googleApiKey, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s" + "k-", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh" + "p_", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github" + "_pat_", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI" + "za", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactForSupportExport_RemovesAccountAndScopeIdentifiers()
    {
        var escapedUserPath = @"C:\Users\person\OneDrive - School Name\Tools\claude.cmd".Replace(@"\", @"\\");
        var text = """
            {
              "email": "person@example.com",
              "accountName": "Personal Account",
              "organization": "octo-org",
              "OrgSlug": "octo-org-slug",
              "organizationId": "octo-org-id",
              "enterpriseSlug": "octo-enterprise",
              "workspace_slug": "octo-workspace",
              "Scopes": "copilot.read,copilot.write",
              "secretName": "gemini-reference",
              "secret_ref": "gemini-secret-ref",
              "patSecretName": "copilot-reference",
              "PATReference": "copilot-pat-reference",
              "tokenSecretName": "token-secret-reference",
              "commandPathOverride": "C:\\Users\\person\\Tools\\codex.cmd",
              "providerCliOverride": "__ESCAPED_USER_PATH__",
              "commandOverride": "D:\\Tools\\private\\gh.exe",
              "visible": "keep this"
            }
            owner=owner@example.test org=raw-org workspace=raw-workspace account=raw-account scope=raw-scope scopes=raw-scope-a,raw-scope-b enterprise_slug=raw-enterprise pat_ref=raw-pat-ref cli_command=C:\Users\person\Tools\gemini.cmd
            """.Replace("__ESCAPED_USER_PATH__", escapedUserPath, StringComparison.Ordinal);

        var redacted = DiagnosticRedactor.RedactForSupportExport(text);

        Assert.Contains("keep this", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("person@example.com", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Personal Account", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("octo-org", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("octo-org-slug", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("octo-org-id", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("octo-enterprise", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("octo-workspace", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("copilot.read", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini-reference", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini-secret-ref", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("copilot-reference", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("copilot-pat-reference", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret-reference", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("codex.cmd", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claude.cmd", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh.exe", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gemini.cmd", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner@example.test", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-org", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-workspace", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-account", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-scope", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-scope-a", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-scope-b", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-enterprise", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-pat-ref", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("School Name", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactForSupportExport_RemovesLocalUserProfilePaths()
    {
        var text = """
            diagnostics failed at C:\Users\person\OneDrive - School Name\WinAI\diagnostics.log
            startup failed at C:\Users\person\OneDrive - School Name\WinAI\app.exe token=path-adjacent-secret
            {"path":"C:\\Users\\person\\AppData\\Roaming\\WinAiUsageBar\\config.json","visible":"keep"}
            """;

        var redacted = DiagnosticRedactor.RedactForSupportExport(text);
        var display = DiagnosticRedactor.RedactForDisplay(text);

        Assert.Contains("keep", redacted, StringComparison.Ordinal);
        Assert.Contains("[LOCAL_PATH]", redacted, StringComparison.Ordinal);
        Assert.Contains("token=[REDACTED]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("School Name", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AppData", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:\\Users\\person", display, StringComparison.OrdinalIgnoreCase);
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
