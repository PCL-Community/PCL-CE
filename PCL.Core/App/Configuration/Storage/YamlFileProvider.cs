using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PCL.Core.Logging;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PCL.Core.App.Configuration.Storage;

/// <summary>
/// 提供 YAML 格式的键值文件读写。当提供的文件找不到时，将尝试读取其它同名文件并将其转换到 YAML。
/// </summary>
public class YamlFileProvider : CommonFileProvider, IEnumerableKeyProvider
{
    private readonly YamlMappingNode _rootNode;

    private static readonly IDeserializer _Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties().WithEnforceRequiredMembers().Build();

    private static readonly ISerializer _Serializer = new SerializerBuilder()
        .DisableAliases().Build();

    private static YamlMappingNode? _LoadFile(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        try
        {
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0) return [];
            var rootNode = yaml.Documents[0].RootNode;
            return rootNode as YamlMappingNode ?? throw new ConfigFileInitException(path, $"Invalid root node type: {rootNode.NodeType}");
        }
        catch (Exception ex)
        {
            if (ex is ConfigFileInitException) throw;
            throw new ConfigFileInitException(path, "Failed to load YAML content", ex);
        }
    }

    public YamlFileProvider(string path) : base(path)
    {
        var rootNode = _LoadFile(path);
        if (rootNode is not null)
        {
            _rootNode = rootNode;
            return;
        }
        try // 尝试从 JSON 和 LTCat-style ini 转换
        {
            var jsonPath = Path.Combine(Basics.GetParentPath(path)!, Path.GetFileNameWithoutExtension(path) + ".json");
            if (File.Exists(jsonPath))
            {
                using var jsonStream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var yamlStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                JsonToYamlConverter.Convert(jsonStream, yamlStream); // yamlStream 会被自动关闭
                _rootNode = _LoadFile(path)!;
                return;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "转换失败，已忽略");
        }
        _rootNode = [];
    }

    public override T Get<T>(string key)
    {
        var result = _rootNode.Children[key];
        var parser = result.ConvertToEventStream().GetParser();
        try
        {
            return _Deserializer.Deserialize<T>(parser);
        }
        catch (Exception ex)
        {
            // 兜底分支必须比正常分支更不可能失败。
            // 原先的 (T)(object)graphStr 只对 bool 与 string 成立：T 为 int/double/DateTime 时
            // 会在 catch 内部再抛 InvalidCastException 并逃逸出整个 Get，
            // 而且 Set() 已经把错误值写回了配置文件。
            var graphStr = result.ToString();
            if (_TryConvert(graphStr, out T? fallback))
            {
                Set(key, fallback!);
                return fallback!;
            }

            // 不要返回 default(T)：那会让调用方（FileConfigStorage.OnAccess）认为读取成功，
            // 于是 ConfigItem 声明的默认值永远不会生效，得到 0 / false / null 这类静默的非法配置。
            // 抛出存储层可识别的异常，使其走清理分支并回落到声明默认值。
            LogWrapper.Warn(ex, $"配置项 '{key}' 无法解析为 {typeof(T).Name}，已丢弃该值并使用默认值。当前值: {graphStr}");
            throw new FormatException(
                $"配置项 '{key}' 的值 '{graphStr}' 无法转换为 {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// 尝试把 YAML 标量的文本表示转换为 <typeparamref name="T"/>，失败时不抛异常。
    /// </summary>
    private static bool _TryConvert<T>(string? raw, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var type = typeof(T);
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        var text = raw.Trim();

        try
        {
            if (targetType == typeof(string))
            {
                value = (T)(object)raw;
                return true;
            }

            if (targetType == typeof(bool))
            {
                switch (text.ToLowerInvariant())
                {
                    case "true" or "1" or "yes" or "on":
                        value = (T)(object)true;
                        return true;
                    case "false" or "0" or "no" or "off":
                        value = (T)(object)false;
                        return true;
                    default:
                        return false;
                }
            }

            if (targetType.IsEnum)
            {
                if (!Enum.TryParse(targetType, text, true, out var parsed)) return false;
                value = (T)parsed;
                return true;
            }

            if (targetType == typeof(Guid))
            {
                if (!Guid.TryParse(text, out var guid)) return false;
                value = (T)(object)guid;
                return true;
            }

            var converted = Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
            if (converted is null) return false;
            value = (T)converted;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override void Set<T>(string key, T value)
    {
        var emitter = new YamlNodeEmitter();
        _Serializer.Serialize(emitter, value);
        _rootNode.Children[key] = emitter.SingleRootNode;
    }

    public override bool Exists(string key)
    {
        return _rootNode.Children.ContainsKey(key);
    }

    public override void Remove(string key)
    {
        _rootNode.Children.Remove(key);
    }

    protected override void WriteToStream(Stream stream)
    {
        var writer = new StreamWriter(stream, Encoding.UTF8);
        _Serializer.Serialize(writer, _rootNode);
        writer.Flush();
    }

    public IEnumerable<string> Keys => _rootNode.Select(pair => pair.Key.ToString());
}
