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

    /// <summary>
    ///     统一的宽松 JSON 序列化配置。该实例在静态初始化时已被冻结，调用方不能修改全局行为。
    ///     如需追加调用点专用设置，请使用 <c>new JsonSerializerOptions(JsonCompat.SerializerOptions)</c> 克隆后修改。
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = _CreateSerializerOptions();

    private static JsonSerializerOptions _CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
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

        options.MakeReadOnly(true);
        return options;
    }

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

    public static bool TryGetDateTime(JsonNode? node, out DateTime dateTime)
    {
        dateTime = default;
        switch (node)
        {
            case null:
                return false;
            case JsonValue value when value.TryGetValue<DateTime>(out var rawDateTime):
                dateTime = NormalizeDateTime(rawDateTime);
                return true;
            case JsonValue value
                when value.TryGetValue<string>(out var rawText) && TryParseDateTime(rawText, out dateTime):
                return true;
            default:
                try
                {
                    dateTime = NormalizeDateTime(node.Deserialize<DateTime>(SerializerOptions));
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
        }
    }

    public static bool TryParseDateTime(string? value, out DateTime dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out var dateTimeOffset))
        {
            dateTime = dateTimeOffset.LocalDateTime;
            return true;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return false;

        dateTime = NormalizeDateTime(parsed);
        return true;
    }

    public static DateTime NormalizeDateTime(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Utc ? dateTime.ToLocalTime() : dateTime;
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
            if (reader.TokenType != JsonTokenType.String)
                return NormalizeDateTime(reader.GetDateTime());

            var value = reader.GetString();
            return TryParseDateTime(value, out var dateTime)
                ? dateTime
                : NormalizeDateTime(reader.GetDateTime());
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