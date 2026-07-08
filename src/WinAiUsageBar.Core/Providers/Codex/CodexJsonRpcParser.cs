using System.Globalization;
using System.Text.Json;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Codex;

public static class CodexJsonRpcParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] UsedPercentFields =
    [
        "usedPercent",
        "used_percent",
        "used_pct",
        "usagePercent",
        "usage_percent",
        "usagePct",
        "usage_pct",
        "percentUsed",
        "percent_used",
        "usedPercentage",
        "quotaUsedPercent",
        "quota_used_percent"
    ];
    private static readonly string[] RemainingPercentFields =
    [
        "remainingPercent",
        "remaining_percent",
        "remaining_pct",
        "percentRemaining",
        "percent_remaining",
        "remainingPercentage",
        "leftPercent",
        "left_percent",
        "quotaRemainingPercent",
        "quota_remaining_percent"
    ];
    private static readonly string[] UsedFields =
    [
        "used",
        "usedAmount",
        "used_amount",
        "current",
        "consumed",
        "consumedAmount",
        "consumed_amount",
        "quotaUsed",
        "quota_used",
        "usedQuota",
        "used_quota",
        "usageUsed",
        "usage_used"
    ];
    private static readonly string[] LimitFields =
    [
        "limit",
        "quota",
        "maximum",
        "max",
        "total",
        "totalQuota",
        "total_quota",
        "quotaLimit",
        "quota_limit",
        "limitAmount",
        "limit_amount",
        "usageLimit",
        "usage_limit"
    ];
    private static readonly string[] RemainingFields =
    [
        "remaining",
        "remainingAmount",
        "remaining_amount",
        "remainingQuota",
        "remaining_quota",
        "quotaRemaining",
        "quota_remaining",
        "available",
        "availableQuota",
        "available_quota"
    ];
    private static readonly string[] UnitFields = ["unit", "units"];
    private static readonly string[] ResetDescriptionFields = ["resetDescription", "resetsIn", "reset"];
    private static readonly string[] ResetAtFields =
    [
        "resetsAt",
        "resetAt",
        "resetTime",
        "resetTimestamp",
        "resetUnixSeconds",
        "resetUnixMilliseconds",
        "resetEpochSeconds",
        "resetEpochMilliseconds",
        "resets_at",
        "reset_at",
        "reset_time",
        "reset_timestamp",
        "reset_unix_seconds",
        "reset_unix_milliseconds",
        "reset_epoch_seconds",
        "reset_epoch_milliseconds"
    ];
    private static readonly string[] ResetDurationFields =
    [
        "resetsInSeconds",
        "resetInSeconds",
        "resetSeconds",
        "secondsUntilReset",
        "resetAfterSeconds",
        "retryAfterSeconds",
        "resets_in_seconds",
        "reset_in_seconds",
        "reset_seconds",
        "seconds_until_reset",
        "reset_after_seconds",
        "retry_after_seconds",
        "resetsInMilliseconds",
        "resetInMilliseconds",
        "resetMilliseconds",
        "millisecondsUntilReset",
        "resetAfterMilliseconds",
        "retryAfterMilliseconds",
        "resetAfterMs",
        "retryAfterMs",
        "resets_in_milliseconds",
        "reset_in_milliseconds",
        "reset_milliseconds",
        "milliseconds_until_reset",
        "reset_after_milliseconds",
        "retry_after_milliseconds",
        "reset_after_ms",
        "retry_after_ms"
    ];
    private static readonly string[] UsageWindowCandidateNames =
    [
        "usage",
        "quota",
        "limit",
        "limits",
        "limitsByWindow",
        "limits_by_window",
        "rateLimit",
        "rate_limit",
        "rateLimits",
        "rate_limits",
        "window",
        "windows",
        "usageWindow",
        "usage_window",
        "usageWindows",
        "usage_windows",
        "current",
        "data"
    ];

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
        DateTimeOffset now,
        DataSourceKind sourceKind = DataSourceKind.LocalAppServer)
    {
        var account = ParseAccount(data.AccountJson);
        var usageWindow = ParseUsageWindow(data.UsageJson, "Codex usage", now);
        var rateLimitWindow = ParseUsageWindow(data.RateLimitsJson, "Codex rate limit", now);
        var primaryWindow = usageWindow ?? rateLimitWindow;
        var secondaryWindow = usageWindow is not null && rateLimitWindow is not null
            ? rateLimitWindow
            : null;
        var credits = ParseCredits(data.UsageJson);

        var health = primaryWindow?.RemainingPercent switch
        {
            null => ProviderHealth.Warning,
            < 10 => ProviderHealth.Error,
            < 20 => ProviderHealth.Warning,
            _ => ProviderHealth.Ok
        };

        var status = primaryWindow is null
            ? "Codex app-server responded, but no usage window was recognized."
            : "Loaded from Codex app-server.";

        return new UsageSnapshot(
            descriptor.Id,
            descriptor.DisplayName,
            health,
            account,
            primaryWindow,
            secondaryWindow,
            credits,
            sourceKind,
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

    public static UsageWindow? ParseUsageWindow(
        string? json,
        string label = "Codex usage",
        DateTimeOffset? now = null)
    {
        if (!TryGetResult(json, out var result))
        {
            return null;
        }

        foreach (var candidate in EnumerateUsageWindowCandidates(result))
        {
            var window = TryParseUsageWindowCandidate(candidate, label, now);
            if (window is not null)
            {
                return window;
            }
        }

        return null;
    }

    private static UsageWindow? TryParseUsageWindowCandidate(
        JsonElement result,
        string label,
        DateTimeOffset? now)
    {
        var usedPercent = TryGetDirectNumber(result, UsedPercentFields);
        var remainingPercent = TryGetDirectNumber(result, RemainingPercentFields);
        var used = TryGetDirectNumber(result, UsedFields);
        var limit = TryGetDirectNumber(result, LimitFields);
        var remaining = TryGetDirectNumber(result, RemainingFields);
        var unit = TryGetDirectSafeString(result, UnitFields);

        if (used is null && remaining is not null && limit is > 0)
        {
            used = Math.Max(0, limit.Value - remaining.Value);
        }

        if (usedPercent is null && remainingPercent is null && used is not null && limit is > 0)
        {
            usedPercent = Math.Round(used.Value / limit.Value * 100, 2);
        }

        if (remainingPercent is null && remaining is not null && limit is > 0)
        {
            remainingPercent = Math.Round(remaining.Value / limit.Value * 100, 2);
        }

        if (remainingPercent is null && used is null && limit is null && remaining is not null)
        {
            remainingPercent = remaining;
        }

        if (remainingPercent is null && usedPercent is not null)
        {
            remainingPercent = Math.Round(100 - usedPercent.Value, 2);
        }

        if (usedPercent is null && remainingPercent is null && used is null && limit is null)
        {
            return null;
        }

        var resetDescription = TryGetDirectSafeString(result, ResetDescriptionFields);
        var resetsAt = TryGetDirectDateTime(result, ResetAtFields);
        var resetDuration = TryGetDirectDuration(result, ResetDurationFields);

        if (resetsAt is null && resetDuration is not null && now is not null)
        {
            resetsAt = now.Value.Add(resetDuration.Value);
            resetDescription ??= FormatResetDuration(resetDuration.Value);
        }

        return new UsageWindow(
            label,
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

    private static string? TryGetDirectSafeString(JsonElement root, string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                || IsSensitiveName(property.Name)
                || property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = property.Value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    private static double? TryGetDirectNumber(JsonElement root, string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(property.Name))
            {
                continue;
            }

            var number = TryGetNumberValue(property.Value);
            if (number is not null)
            {
                return number;
            }
        }

        return null;
    }

    private static DateTimeOffset? TryGetDirectDateTime(JsonElement root, string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(property.Value.GetString(), out var parsed))
            {
                return parsed;
            }

            var timestamp = TryGetNumberValue(property.Value);
            if (timestamp is not null)
            {
                return TryCreateUnixTimestamp(timestamp.Value, property.Name);
            }
        }

        return null;
    }

    private static TimeSpan? TryGetDirectDuration(JsonElement root, string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(property.Name))
            {
                continue;
            }

            var duration = TryGetNumberValue(property.Value);
            if (duration is null || duration.Value < 0)
            {
                continue;
            }

            return IsMillisecondField(property.Name)
                ? TimeSpan.FromMilliseconds(duration.Value)
                : TimeSpan.FromSeconds(duration.Value);
        }

        return null;
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

            var timestamp = TryGetNumberValue(value);
            if (timestamp is not null)
            {
                return TryCreateUnixTimestamp(timestamp.Value, name);
            }
        }

        return null;
    }

    private static TimeSpan? TryGetDuration(JsonElement root, string[] names)
    {
        foreach (var (name, value) in Flatten(root))
        {
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase) || IsSensitiveName(name))
            {
                continue;
            }

            var duration = TryGetNumberValue(value);
            if (duration is null || duration.Value < 0)
            {
                continue;
            }

            return IsMillisecondField(name)
                ? TimeSpan.FromMilliseconds(duration.Value)
                : TimeSpan.FromSeconds(duration.Value);
        }

        return null;
    }

    private static double? TryGetNumberValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateTimeOffset? TryCreateUnixTimestamp(double value, string fieldName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        long timestamp;
        try
        {
            timestamp = Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            return null;
        }

        var looksLikeMilliseconds = timestamp is >= 10_000_000_000 or <= -10_000_000_000;
        try
        {
            return IsMillisecondField(fieldName) || looksLikeMilliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool IsMillisecondField(string fieldName)
    {
        return fieldName.Contains("millisecond", StringComparison.OrdinalIgnoreCase)
            || fieldName.Contains("millis", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith("Ms", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith("_ms", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatResetDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"resets in {Math.Round(duration.TotalHours, 1)}h";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"resets in {Math.Round(duration.TotalMinutes, 1)}m";
        }

        return $"resets in {Math.Round(duration.TotalSeconds, 1)}s";
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

    private static IEnumerable<JsonElement> EnumerateUsageWindowCandidates(JsonElement result)
    {
        yield return result;

        foreach (var candidate in EnumerateNestedUsageWindowCandidates(
            result,
            includeObject: result.ValueKind == JsonValueKind.Array))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<JsonElement> EnumerateNestedUsageWindowCandidates(
        JsonElement element,
        bool includeObject)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var isCandidate = UsageWindowCandidateNames.Contains(
                    property.Name,
                    StringComparer.OrdinalIgnoreCase);
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    if (includeObject || isCandidate)
                    {
                        yield return property.Value;
                    }

                    foreach (var child in EnumerateNestedUsageWindowCandidates(
                        property.Value,
                        includeObject || isCandidate))
                    {
                        yield return child;
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in EnumerateNestedUsageWindowCandidates(
                        property.Value,
                        includeObject || isCandidate))
                    {
                        yield return child;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && includeObject)
                {
                    yield return item;
                }

                foreach (var child in EnumerateNestedUsageWindowCandidates(item, includeObject))
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
