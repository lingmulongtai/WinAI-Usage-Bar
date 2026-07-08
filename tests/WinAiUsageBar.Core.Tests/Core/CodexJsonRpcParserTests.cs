using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Core.Providers.Codex;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class CodexJsonRpcParserTests
{
    [Fact]
    public void ParseAccount_ExtractsOnlySafeFields()
    {
        const string json = """
        {
          "jsonrpc": "2.0",
          "id": 2,
          "result": {
            "email": "person@example.com",
            "planName": "Plus",
            "access_token": "secret-token"
          }
        }
        """;

        var account = CodexJsonRpcParser.ParseAccount(json);

        Assert.Equal("person@example.com", account?.Email);
        Assert.Equal("Plus", account?.PlanName);
    }

    [Fact]
    public void CreateSnapshot_MapsUsageAndCredits()
    {
        const string usage = """
        {
          "jsonrpc": "2.0",
          "id": 4,
          "result": {
            "used": 75,
            "limit": 100,
            "resetsAt": "2026-07-08T12:00:00Z",
            "balance": 9.5,
            "currency": "USD",
            "monthToDateCost": 3.25,
            "tokensLast31Days": 12345
          }
        }
        """;

        var data = new CodexAppServerData(
            AccountJson: null,
            RateLimitsJson: null,
            UsageJson: usage,
            Diagnostics: []);

        var snapshot = CodexJsonRpcParser.CreateSnapshot(
            ProviderDescriptors.Get(ProviderId.Codex),
            data,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(75, snapshot.PrimaryWindow?.UsedPercent);
        Assert.Equal(25, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero), snapshot.PrimaryWindow?.ResetsAt);
        Assert.Equal(9.5m, snapshot.Credits?.Balance);
        Assert.Equal(12345, snapshot.Credits?.TokensLast31Days);
        Assert.Equal(ProviderHealth.Ok, snapshot.Health);
    }

    [Fact]
    public void CreateSnapshot_MapsRateLimitsAsSecondaryWindow()
    {
        const string usage = """
        {
          "jsonrpc": "2.0",
          "id": 4,
          "result": {
            "usedPercent": 25,
            "remainingPercent": 75
          }
        }
        """;
        const string rateLimits = """
        {
          "jsonrpc": "2.0",
          "id": 3,
          "result": {
            "used": 80,
            "limit": 100,
            "resetDescription": "daily"
          }
        }
        """;

        var data = new CodexAppServerData(
            AccountJson: null,
            RateLimitsJson: rateLimits,
            UsageJson: usage,
            Diagnostics: []);

        var snapshot = CodexJsonRpcParser.CreateSnapshot(
            ProviderDescriptors.Get(ProviderId.Codex),
            data,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("Codex usage", snapshot.PrimaryWindow?.Label);
        Assert.Equal(75, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Equal("Codex rate limit", snapshot.SecondaryWindow?.Label);
        Assert.Equal(20, snapshot.SecondaryWindow?.RemainingPercent);
        Assert.Equal("daily", snapshot.SecondaryWindow?.ResetDescription);
    }

    [Fact]
    public void CreateSnapshot_UsesRateLimitsAsPrimaryFallback()
    {
        const string rateLimits = """
        {
          "jsonrpc": "2.0",
          "id": 3,
          "result": {
            "used": 90,
            "limit": 100
          }
        }
        """;

        var data = new CodexAppServerData(
            AccountJson: null,
            RateLimitsJson: rateLimits,
            UsageJson: null,
            Diagnostics: []);

        var snapshot = CodexJsonRpcParser.CreateSnapshot(
            ProviderDescriptors.Get(ProviderId.Codex),
            data,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("Codex rate limit", snapshot.PrimaryWindow?.Label);
        Assert.Equal(10, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Null(snapshot.SecondaryWindow);
        Assert.Equal(ProviderHealth.Warning, snapshot.Health);
    }

    [Fact]
    public void ParseUsageWindow_ParsesUnixSecondResetTimestamp()
    {
        var expected = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var json = $$"""
        {
          "result": {
            "used": 5,
            "limit": 10,
            "resetUnixSeconds": {{expected.ToUnixTimeSeconds()}}
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(expected, window?.ResetsAt);
    }

    [Fact]
    public void ParseUsageWindow_ParsesUnixMillisecondResetTimestamp()
    {
        var expected = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var json = $$"""
        {
          "result": {
            "used": 5,
            "limit": 10,
            "resetUnixMilliseconds": {{expected.ToUnixTimeMilliseconds()}}
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(expected, window?.ResetsAt);
    }

    [Fact]
    public void ParseUsageWindow_UsesRelativeResetSecondsWhenNowIsAvailable()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "used": 5,
            "limit": 10,
            "resetsInSeconds": 5400
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(now.AddMinutes(90), window?.ResetsAt);
        Assert.Equal("resets in 1.5h", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_IgnoresSensitiveLookingResetDurationFields()
    {
        const string json = """
        {
          "result": {
            "used": 5,
            "limit": 10,
            "authResetAt": "2026-07-08T12:00:00Z",
            "resetTokenExpiresAt": 1783512000
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Null(window?.ResetsAt);
    }

    [Fact]
    public void ParseUsageWindow_IgnoresOutOfRangeResetTimestamp()
    {
        const string json = """
        {
          "result": {
            "used": 5,
            "limit": 10,
            "resetUnixSeconds": 1e100
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Null(window?.ResetsAt);
    }

    [Fact]
    public void ParseEnvelope_RecognizesJsonRpcErrors()
    {
        var envelope = CodexJsonRpcParser.ParseEnvelope("""{"jsonrpc":"2.0","id":1,"error":{"message":"Auth required"}}""");

        Assert.Equal(1, envelope.Id);
        Assert.True(envelope.HasError);
        Assert.Equal("Auth required", envelope.ErrorMessage);
    }
}
