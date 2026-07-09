using System.Text.Json;
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
    public void CreateInitializeRequest_UsesProvidedClientVersion()
    {
        var request = CodexJsonRpcParser.CreateInitializeRequest(1, " 9.8.7+local ");

        Assert.Contains("\"method\":\"initialize\"", request, StringComparison.Ordinal);
        Assert.Equal("9.8.7+local", ReadInitializeClientVersion(request));
    }

    [Fact]
    public void CreateInitializeRequest_UsesFallbackClientVersionWhenBlank()
    {
        var request = CodexJsonRpcParser.CreateInitializeRequest(1, " ");

        Assert.Equal("0.0.0", ReadInitializeClientVersion(request));
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
    public void ParseCredits_ParsesSnakeCaseAliases()
    {
        const string json = """
        {
          "result": {
            "billing": {
              "credit_balance": "42.25",
              "currency_code": "USD",
              "month_to_date_cost": "5.75",
              "tokens_last_31_days": "123456"
            }
          }
        }
        """;

        var credits = CodexJsonRpcParser.ParseCredits(json);

        Assert.Equal(42.25m, credits?.Balance);
        Assert.Equal("USD", credits?.Currency);
        Assert.Equal(5.75m, credits?.MonthToDateCost);
        Assert.Equal(123456, credits?.TokensLast31Days);
    }

    [Fact]
    public void ParseCredits_IgnoresSecretAndQuotaTokenNamesWhileParsingSafeAliases()
    {
        const string json = """
        {
          "result": {
            "billing": {
              "accessToken": 999,
              "authTokenLimit": 888,
              "tokenLimit": 777,
              "remaining_credits": 12,
              "current_month_cost": 3.5,
              "last_31_days_tokens": 4567
            }
          }
        }
        """;

        var credits = CodexJsonRpcParser.ParseCredits(json);

        Assert.Equal(12m, credits?.Balance);
        Assert.Null(credits?.Currency);
        Assert.Equal(3.5m, credits?.MonthToDateCost);
        Assert.Equal(4567, credits?.TokensLast31Days);
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
    public void CreateSnapshot_UsesProviderSpecificLabelsForChatGpt()
    {
        const string usage = """
        {
          "result": {
            "usedPercent": 25,
            "remainingPercent": 75
          }
        }
        """;
        const string rateLimits = """
        {
          "result": {
            "used": 80,
            "limit": 100
          }
        }
        """;

        var data = new CodexAppServerData(
            AccountJson: null,
            RateLimitsJson: rateLimits,
            UsageJson: usage,
            Diagnostics: []);

        var snapshot = CodexJsonRpcParser.CreateSnapshot(
            ProviderDescriptors.Get(ProviderId.ChatGPT),
            data,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("ChatGPT usage", snapshot.PrimaryWindow?.Label);
        Assert.Equal("ChatGPT rate limit", snapshot.SecondaryWindow?.Label);
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
    public void CreateSnapshot_UsesProviderSpecificRateLimitFallbackLabelForChatGpt()
    {
        const string rateLimits = """
        {
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
            ProviderDescriptors.Get(ProviderId.ChatGPT),
            data,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("ChatGPT rate limit", snapshot.PrimaryWindow?.Label);
        Assert.Equal(10, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Null(snapshot.SecondaryWindow);
    }

    [Fact]
    public void ParseUsageWindow_ComputesUsedPercentFromRemainingPercentOnly()
    {
        const string json = """
        {
          "result": {
            "remainingPercent": 70
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(30, window?.UsedPercent);
        Assert.Equal(70, window?.RemainingPercent);
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
    public void ParseUsageWindow_ParsesGenericRetryAfterAliasAsSeconds()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "used": 5,
            "limit": 10,
            "retry_after": "900"
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(now.AddMinutes(15), window?.ResetsAt);
        Assert.Equal("resets in 15m", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_FormatsNumericResetsInDescriptionWhenUsedAsDuration()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "used": 5,
            "limit": 10,
            "resetsIn": "3600"
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(now.AddHours(1), window?.ResetsAt);
        Assert.Equal("resets in 1h", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_ParsesNestedQuotaAliases()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "limits": {
              "primary": {
                "consumed": 350,
                "total": 1000,
                "remainingQuota": 650,
                "unit": "requests",
                "reset_after_ms": 7200000
              }
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(350, window?.Used);
        Assert.Equal(1000, window?.Limit);
        Assert.Equal(35, window?.UsedPercent);
        Assert.Equal(65, window?.RemainingPercent);
        Assert.Equal("requests", window?.Unit);
        Assert.Equal(now.AddHours(2), window?.ResetsAt);
        Assert.Equal("resets in 2h", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_ParsesDeepNestedDataCurrentWindow()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "data": {
              "current": {
                "usedAmount": 42,
                "limitAmount": 100,
                "remainingAmount": 58,
                "unit": "messages",
                "resetAfterSeconds": 3600
              }
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(42, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(42, window?.UsedPercent);
        Assert.Equal(58, window?.RemainingPercent);
        Assert.Equal("messages", window?.Unit);
        Assert.Equal(now.AddHours(1), window?.ResetsAt);
    }

    [Fact]
    public void ParseUsageWindow_ParsesFirstValidTopLevelArrayWindow()
    {
        const string json = """
        {
          "result": [
            {
              "label": "metadata"
            },
            {
              "used": 30,
              "limit": 100,
              "unit": "messages"
            },
            {
              "used": 80,
              "limit": 100
            }
          ]
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(30, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(30, window?.UsedPercent);
        Assert.Equal(70, window?.RemainingPercent);
        Assert.Equal("messages", window?.Unit);
    }

    [Fact]
    public void ParseUsageWindow_ParsesPluralNestedArrayWindow()
    {
        const string json = """
        {
          "result": {
            "data": {
              "rateLimits": [
                {
                  "name": "daily"
                },
                {
                  "remaining": 45,
                  "limit": 100,
                  "resetDescription": "daily"
                }
              ]
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(55, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(45, window?.RemainingPercent);
        Assert.Equal("daily", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_PrefersTopLevelFieldsBeforeTopLevelArrayCandidate()
    {
        const string json = """
        {
          "result": {
            "used": 10,
            "limit": 100,
            "usageWindows": [
              {
                "used": 90,
                "limit": 100
              }
            ]
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(10, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(90, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_PrefersTopLevelWindowBeforeNestedCandidates()
    {
        const string json = """
        {
          "result": {
            "used": 10,
            "limit": 100,
            "usage": {
              "used": 90,
              "limit": 100
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(10, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(10, window?.UsedPercent);
        Assert.Equal(90, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_PrefersFirstNestedCandidateWithoutMixingLaterCandidates()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "used": 20
            },
            "rateLimit": {
              "limit": 100,
              "remaining": 40
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(20, window?.Used);
        Assert.Null(window?.Limit);
        Assert.Null(window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_DoesNotMixSeparateArrayCandidates()
    {
        const string json = """
        {
          "result": {
            "windows": [
              {
                "used": 20
              },
              {
                "limit": 100,
                "remaining": 40
              }
            ]
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(20, window?.Used);
        Assert.Null(window?.Limit);
        Assert.Null(window?.RemainingPercent);
    }

    [Fact]
    public void CreateSnapshot_ParsesNestedRateLimitWindow()
    {
        const string usage = """
        {
          "result": {
            "usage": {
              "remainingPercent": 70
            }
          }
        }
        """;
        const string rateLimits = """
        {
          "result": {
            "data": {
              "rate_limit": {
                "used": 82,
                "limit": 100,
                "resetDescription": "rolling"
              }
            }
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

        Assert.Equal(70, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Equal("Codex rate limit", snapshot.SecondaryWindow?.Label);
        Assert.Equal(82, snapshot.SecondaryWindow?.Used);
        Assert.Equal(18, snapshot.SecondaryWindow?.RemainingPercent);
        Assert.Equal("rolling", snapshot.SecondaryWindow?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_ParsesSnakeCasePercentAndRetryAfterAliases()
    {
        var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        const string json = """
        {
          "result": {
            "quota": {
              "percent_used": "12.5",
              "percent_remaining": "87.5",
              "retry_after_seconds": "1800"
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json, now: now);

        Assert.Equal(12.5, window?.UsedPercent);
        Assert.Equal(87.5, window?.RemainingPercent);
        Assert.Equal(now.AddMinutes(30), window?.ResetsAt);
        Assert.Equal("resets in 30m", window?.ResetDescription);
    }

    [Fact]
    public void ParseUsageWindow_ParsesUsedFractionAliases()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "usedFraction": 0.375
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(37.5, window?.UsedPercent);
        Assert.Equal(62.5, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_ParsesRemainingRatioAliases()
    {
        const string json = """
        {
          "result": {
            "rateLimits": [
              {
                "name": "metadata"
              },
              {
                "remaining_ratio": "0.625"
              }
            ]
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(37.5, window?.UsedPercent);
        Assert.Equal(62.5, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_TreatsFractionAliasAboveOneAsPercent()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "usageRatio": 42
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(42, window?.UsedPercent);
        Assert.Equal(58, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_ParsesTokenCounterAliases()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "usedTokens": "2500",
              "maxTokens": 10000,
              "tokensRemaining": 7500,
              "unit": "tokens"
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(2500, window?.Used);
        Assert.Equal(10000, window?.Limit);
        Assert.Equal(25, window?.UsedPercent);
        Assert.Equal(75, window?.RemainingPercent);
        Assert.Equal("tokens", window?.Unit);
    }

    [Fact]
    public void ParseUsageWindow_IgnoresSecretTokenNamesWhileParsingTokenCounters()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "accessToken": "not-a-counter",
              "authTokenLimit": 999,
              "used_tokens": 20,
              "token_limit": 100
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(20, window?.Used);
        Assert.Equal(100, window?.Limit);
        Assert.Equal(20, window?.UsedPercent);
        Assert.Equal(80, window?.RemainingPercent);
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
    public void ParseUsageWindow_IgnoresSensitiveLookingFractionFields()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "authUsageRatio": 0.99,
              "tokenRemainingRatio": 0.01,
              "used": 25,
              "limit": 100
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(25, window?.Used);
        Assert.Equal(25, window?.UsedPercent);
        Assert.Equal(75, window?.RemainingPercent);
    }

    [Fact]
    public void ParseUsageWindow_IgnoresSensitiveLookingNestedUsageFields()
    {
        const string json = """
        {
          "result": {
            "usage": {
              "tokenUsed": 99,
              "used": 25,
              "limit": 100,
              "authResetAt": "2026-07-08T12:00:00Z",
              "unit": "requests"
            }
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(25, window?.Used);
        Assert.Equal(25, window?.UsedPercent);
        Assert.Equal(75, window?.RemainingPercent);
        Assert.Equal("requests", window?.Unit);
        Assert.Null(window?.ResetsAt);
    }

    [Fact]
    public void ParseUsageWindow_IgnoresSensitiveLookingArrayFields()
    {
        const string json = """
        {
          "result": {
            "usage_windows": [
              {
                "tokenUsed": 99,
                "used": 25,
                "limit": 100,
                "authResetAt": "2026-07-08T12:00:00Z"
              }
            ]
          }
        }
        """;

        var window = CodexJsonRpcParser.ParseUsageWindow(json);

        Assert.Equal(25, window?.Used);
        Assert.Equal(25, window?.UsedPercent);
        Assert.Equal(75, window?.RemainingPercent);
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

    [Fact]
    public void ParseEnvelope_IgnoresNestedIdsWhenTopLevelIdIsMissing()
    {
        var envelope = CodexJsonRpcParser.ParseEnvelope(
            """{"jsonrpc":"2.0","method":"session/changed","params":{"id":3,"message":"not an error"}}""");

        Assert.Null(envelope.Id);
        Assert.Equal("session/changed", envelope.Method);
        Assert.False(envelope.HasError);
        Assert.Null(envelope.ErrorMessage);
    }

    [Fact]
    public void ParseEnvelope_PrefersTopLevelIdWhenNestedObjectsHaveIds()
    {
        var envelope = CodexJsonRpcParser.ParseEnvelope(
            """{"jsonrpc":"2.0","id":4,"result":{"id":99,"message":"not an envelope message"}}""");

        Assert.Equal(4, envelope.Id);
        Assert.Null(envelope.ErrorMessage);
    }

    [Fact]
    public void ParseEnvelope_ThrowsForInvalidTopLevelId()
    {
        Assert.Throws<FormatException>(
            () => CodexJsonRpcParser.ParseEnvelope("""{"jsonrpc":"2.0","id":"not-a-number","result":{}}"""));
    }

    private static string? ReadInitializeClientVersion(string request)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement
            .GetProperty("params")
            .GetProperty("clientInfo")
            .GetProperty("version")
            .GetString();
    }
}
