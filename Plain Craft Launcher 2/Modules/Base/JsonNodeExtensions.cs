using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PCL;

public static class JsonNodeExtensions
{
    private static readonly JsonSerializerOptions CompatOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new LocalDateTimeConverter() }
    };

    private sealed class LocalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
                    return dateTimeOffset.LocalDateTime;
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
                    return dateTime.Kind == DateTimeKind.Utc ? dateTime.ToLocalTime() : dateTime;
            }

            var result = reader.GetDateTime();
            return result.Kind == DateTimeKind.Utc ? result.ToLocalTime() : result;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    public static void Merge(this JsonObject target, JsonNode? source)
    {
        if (source is not JsonObject sourceObj) return;

        foreach (var prop in sourceObj.ToArray())
            switch (target[prop.Key])
            {
                case JsonObject targetChild when
                    prop.Value is JsonObject sourceChild:
                    targetChild.Merge(sourceChild);
                    break;
                case JsonArray targetArray when
                    prop.Value is JsonArray sourceArray:
                    targetArray.Merge(sourceArray);
                    break;
                default:
                    target[prop.Key] = prop.Value?.DeepClone();
                    break;
            }
    }

    public static void Merge(this JsonArray target, JsonNode? source)
    {
        if (source is not JsonArray sourceArr) return;
        foreach (var item in sourceArr)
            target.Add(item?.DeepClone());
    }

    public static T? ToObject<T>(this JsonNode node)
    {
        return node.Deserialize<T>(CompatOptions);
    }

    public static JsonArray FromObject<T>(IEnumerable<T> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonSerializer.SerializeToNode(item, CompatOptions));
        return arr;
    }
}
