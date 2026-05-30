using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PCL.Core.Utils;

/// <summary>
///     System.Text.Json 兼容 Newtonsoft.Json 宽松行为的统一入口。
/// </summary>
public static class JsonCompat
{
    public static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new FlexibleDateTimeConverter(),
            new FlexibleBoolConverter(),
            new FlexibleStringConverter(),
            new JsonStringEnumConverter()
        }
    };

    public static JsonNode ParseNode(string text)
    {
        return JsonNode.Parse(text, NodeOptions, DocumentOptions)!;
    }

    public static T? ToObject<T>(this JsonNode? node)
    {
        return node is null ? default : node.Deserialize<T>(SerializerOptions);
    }

    public static JsonArray FromObject<T>(IEnumerable<T> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonSerializer.SerializeToNode(item, SerializerOptions));
        return arr;
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

    private sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
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
                _ => throw new JsonException($"Can not convert JSON token {reader.TokenType} to Boolean.")
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