using System.Text.Json;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Codex;

public static class CodexJsonRpcParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateRequest(int id, string method)
    {
        return JsonSerializer.Serialize(new JsonRpcRequest("2.0", id, method, null), JsonOptions);
    }

    public static string CreateInitializeRequest(int id)
    {
        return JsonSerializer.Serialize(
            new JsonRpcRequest(
                "2.0",
                id,
                "initialize",
                new
                {
                    clientInfo = new { name = "WinAI Usage Bar", version = "0.1.0" },
                    capabilities = new { }
                }),
            JsonOptions);
    }

    public static UsageSnapshot CreateSnapshot(
        ProviderDescriptor descriptor,
        CodexAppServerData data,
        DateTimeOffset now)
    {
        var account = ParseAccount(data.AccountJson);
        var usageWindow = ParseUsageWindow(data.UsageJson)
            ?? ParseUsageWindow(data.RateLimitsJson);
        var credits = ParseCredits(data.UsageJson);

        var health = usageWindow?.RemainingPercent switch
        {
            null => ProviderHealth.Warning,
            < 10 => ProviderHealth.Error,
            < 20 => ProviderHealth.Warning,
            _ => ProviderHealth.Ok
        };

        var status = usageWindow is null
            ? "Codex app-server responded, but no usage window was recognized."
            : "Loaded from Codex app-server.";

        return new UsageSnapshot(
            descriptor.Id,
            descriptor.DisplayName,
            health,
            account,
            usageWindow,
            SecondaryWindow: null,
            credits,
            DataSourceKind.LocalAppServer,
            now,
            status,
            ErrorMessage: null);
    }

    public static JsonRpcEnvelope ParseEnvelope(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var id = TryGetNumber(root, ["id"]);
        var method = TryGetSafeString(root, ["method"]);
        var errorMessage = TryGetSafeString(root, ["message", "errorMessage", "detail"]);
        var hasError = root.TryGetProperty("error", out _);

        return new JsonRpcEnvelope(id is null ? null : Convert.ToInt32(id.Value), method, hasError, errorMessage);
    }

    public static ProviderIdentity? ParseAccount(string? json)
    {
        if (!TryGetResult(json, out var result))
        {
            return null;
        }

        var email = TryGetSafeString(result, ["email", "userEmail", "login"]);
        var accountName = TryGetSafeString(result, ["accountName", "name", "username"]);
        var planName = TryGetSafeString(result, ["plan", "planName", "subscription"]);
        var organization = TryGetSafeString(result, ["organization", "org", "workspace"]);

        if (email is null && accountName is null && planName is null && organization is null)
        {
            return null;
        }

        return new ProviderIdentity(email, accountName, planName, organization);
    }

    public static UsageWindow? ParseUsageWindow(string? json)
    {
        if (!TryGetResult(json, out var result))
        {
            return null;
        }

        var usedPercent = TryGetNumber(result, ["usedPercent", "used_percent", "usagePercent", "usage_percent", "percentUsed"]);
        var remainingPercent = TryGetNumber(result, ["remainingPercent", "remaining_percent", "percentRemaining", "leftPercent"]);
        var used = TryGetNumber(result, ["used", "usedAmount", "current"]);
        var limit = TryGetNumber(result, ["limit", "quota", "maximum", "max"]);
        var unit = TryGetSafeString(result, ["unit", "units"]);

        if (usedPercent is null && remainingPercent is null && used is not null && limit is > 0)
        {
            usedPercent = Math.Round(used.Value / limit.Value * 100, 2);
        }

        if (remainingPercent is null && usedPercent is not null)
        {
            remainingPercent = Math.Round(100 - usedPercent.Value, 2);
        }

        if (usedPercent is null && remainingPercent is null && used is null && limit is null)
        {
            return null;
        }

        var resetDescription = TryGetSafeString(result, ["resetDescription", "resetsIn", "reset"]);
        var resetsAt = TryGetDateTime(result, ["resetsAt", "resetAt", "resetTime", "resets_at"]);

        return new UsageWindow(
            "Codex usage",
            UsageSnapshotMapper.NormalizePercent(usedPercent),
            UsageSnapshotMapper.NormalizePercent(remainingPercent),
            resetsAt,
            resetDescription,
            unit,
            used,
            limit);
    }

    public static ProviderCredits? ParseCredits(string? json)
    {
        if (!TryGetResult(json, out var result))
        {
            return null;
        }

        var balance = TryGetDecimal(result, ["balance", "creditBalance", "credits", "remainingCredits"]);
        var currency = TryGetSafeString(result, ["currency"]);
        var cost = TryGetDecimal(result, ["monthToDateCost", "mtdCost", "cost", "spend"]);
        var tokens = TryGetLong(result, ["tokensLast31Days", "tokens", "inputTokens", "totalTokens"]);

        if (balance is null && cost is null && tokens is null)
        {
            return null;
        }

        return new ProviderCredits(balance, currency, cost, tokens);
    }

    private static bool TryGetResult(string? json, out JsonElement result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("result", out var resultElement))
        {
            result = resultElement.Clone();
            return true;
        }

        result = root.Clone();
        return true;
    }

    private static string? TryGetSafeString(JsonElement root, string[] names)
    {
        foreach (var (name, value) in Flatten(root))
        {
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(name) || value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    private static double? TryGetNumber(JsonElement root, string[] names)
    {
        foreach (var (name, value) in Flatten(root))
        {
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(name))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static decimal? TryGetDecimal(JsonElement root, string[] names)
    {
        var number = TryGetNumber(root, names);
        return number is null ? null : Convert.ToDecimal(number.Value);
    }

    private static long? TryGetLong(JsonElement root, string[] names)
    {
        var number = TryGetNumber(root, names);
        return number is null ? null : Convert.ToInt64(number.Value);
    }

    private static DateTimeOffset? TryGetDateTime(JsonElement root, string[] names)
    {
        foreach (var (name, value) in Flatten(root))
        {
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(name))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static IEnumerable<(string Name, JsonElement Value)> Flatten(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return (property.Name, property.Value);

                foreach (var child in Flatten(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Flatten(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool IsSensitiveName(string name)
    {
        var safeTokenCounters = new[]
        {
            "tokens",
            "inputTokens",
            "outputTokens",
            "totalTokens",
            "tokensLast31Days"
        };

        if (safeTokenCounters.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
            || name.Contains("key", StringComparison.OrdinalIgnoreCase)
            || name.Contains("auth", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record JsonRpcRequest(string Jsonrpc, int Id, string Method, object? Params);
}

public sealed record JsonRpcEnvelope(int? Id, string? Method, bool HasError, string? ErrorMessage);
