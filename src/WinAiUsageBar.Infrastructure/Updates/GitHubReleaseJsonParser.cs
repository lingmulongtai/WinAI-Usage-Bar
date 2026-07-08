using System.Text.Json;

namespace WinAiUsageBar.Infrastructure.Updates;

public static class GitHubReleaseJsonParser
{
    public static GitHubReleaseMetadata Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var assets = new List<GitHubReleaseAsset>();

        if (root.TryGetProperty("assets", out var assetsElement)
            && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var assetElement in assetsElement.EnumerateArray())
            {
                var name = GetString(assetElement, "name");
                var url = GetString(assetElement, "browser_download_url");
                if (string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(url)
                    || !Uri.TryCreate(url, UriKind.Absolute, out var downloadUrl))
                {
                    continue;
                }

                assets.Add(new GitHubReleaseAsset(
                    name,
                    downloadUrl,
                    GetNullableInt64(assetElement, "size")));
            }
        }

        return new GitHubReleaseMetadata(
            GetString(root, "tag_name") ?? string.Empty,
            GetString(root, "name"),
            GetBoolean(root, "draft"),
            GetBoolean(root, "prerelease"),
            TryCreateUri(GetString(root, "html_url")),
            assets);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static long? GetNullableInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static Uri? TryCreateUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }
}
