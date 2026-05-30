using System.Globalization;
using System.Text.Json.Serialization;

namespace PCL;

public static class JsonNodeExtensions
{
    public static readonly JsonNodeOptions CompatNodeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonDocumentOptions CompatDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static readonly JsonSerializerOptions CompatOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new LocalDateTimeConverter(),
            new FlexibleBoolConverter(),
            new FlexibleStringConverter(),
            new JsonStringEnumConverter()
        }
    };

    public static JsonNode ParseJson(string data)
    {
        return JsonNode.Parse(data, CompatNodeOptions, CompatDocumentOptions)!;
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

    private sealed class LocalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out var dateTimeOffset))
                    return dateTimeOffset.LocalDateTime;
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal,
                        out var dateTime))
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


    private sealed class FlexibleBoolConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String when bool.TryParse(reader.GetString(), out var value) => value,
                JsonTokenType.String when int.TryParse(reader.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var number) => number != 0,
                JsonTokenType.Number when reader.TryGetInt64(out var number) => number != 0,
                _ => throw new JsonException($"无法将 JSON Token {reader.TokenType} 转换为 Boolean。")
            };
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    private sealed class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => _ReadRawJsonValue(ref reader),
                JsonTokenType.True => bool.TrueString,
                JsonTokenType.False => bool.FalseString,
                _ => _ReadRawJsonValue(ref reader)
            };
        }

        private static string _ReadRawJsonValue(ref Utf8JsonReader reader)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetRawText();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}