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
        Assert.Equal(9.5m, snapshot.Credits?.Balance);
        Assert.Equal(12345, snapshot.Credits?.TokensLast31Days);
        Assert.Equal(ProviderHealth.Ok, snapshot.Health);
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
