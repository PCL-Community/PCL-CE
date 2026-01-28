using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PCL.Core.IO;

namespace PCL.Core.App.Configuration.Impl;

/// <summary>
/// 提供 JSON 格式的键值文件读写。
/// </summary>
public class JsonFileProvider : CommonFileProvider, IEnumerableKeyProvider
{
    private readonly JsonObject _rootElement;

    private static readonly JsonDocumentOptions _DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions _SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    private static readonly JsonWriterOptions _WriterOptions = new()
    {
        Indented = true
    };

    public JsonFileProvider(string path) : base(path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var parseResult = JsonNode.Parse(stream, documentOptions: _DocumentOptions);
                if (parseResult is not JsonObject root)
                    throw new FileInitException(path,
                        $"Invalid root element type: {parseResult?.GetValueKind().ToString() ?? "Empty"}");
                _rootElement = root;
            }
            else
            {
                using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                _rootElement = new JsonObject();
                JsonSerializer.Serialize(stream, _rootElement, _SerializerOptions);
            }
        }
        catch (Exception ex)
        {
            if (ex is FileInitException) throw;
            throw new FileInitException(path, "Failed to read JSON file", ex);
        }
    }

    public override T Get<T>(string key)
    {
        var result = _rootElement[key];
        if (result == null) throw new KeyNotFoundException($"Not found: '{key}'");
        try
        {
            var r = result.Deserialize<T>();
            return r ?? throw GetNullException();
        }
        catch (JsonException)
        {
            T fallback;
            var type = typeof(T);
            if (type == typeof(string)) fallback = (T)(object)result.ToString();
            else
            {
                var jsonStr = result.Deserialize<string>()!;
                if (type == typeof(bool)) fallback = (T)(object)(jsonStr.ToLowerInvariant() is "true" or "1");
                else fallback = JsonSerializer.Deserialize<T>(jsonStr, _SerializerOptions) ?? throw GetNullException();
            }
            Set(key, fallback);
            return fallback;
        }
        Exception GetNullException() => new NullReferenceException($"Deserialized value is null: '{key}'");
    }

    public override void Set<T>(string key, T value)
    {
        _rootElement[key] = JsonSerializer.SerializeToNode(value);
    }

    public override bool Exists(string key)
    {
        return _rootElement.ContainsKey(key);
    }

    public override void Remove(string key)
    {
        _rootElement.Remove(key);
    }

    protected override void WriteToStream(Stream stream)
    {
        var writer = new Utf8JsonWriter(stream, _WriterOptions);
        _rootElement.WriteTo(writer, _SerializerOptions);
        writer.Flush();
    }

    public IEnumerable<string> Keys => _rootElement.Select(pair => pair.Key);
}
