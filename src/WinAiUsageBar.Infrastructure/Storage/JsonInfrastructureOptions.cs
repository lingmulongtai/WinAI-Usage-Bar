using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinAiUsageBar.Infrastructure.Storage;

public static class JsonInfrastructureOptions
{
    public static JsonSerializerOptions CreateIndented()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static JsonSerializerOptions CreateNdjson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
