using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 离线账户的自定义皮肤配置模型。
/// </summary>
/// <param name="Type">皮肤来源类型。</param>
/// <param name="CslApi">Custom Skin Loader API 的皮肤接口地址。</param>
/// <param name="Model">皮肤纹理模型（宽/细）。</param>
/// <param name="LocalSkinPath">本地皮肤文件路径。</param>
/// <param name="LocalCapePath">本地披风文件路径。</param>
public sealed record Skin(
    SkinType Type,
    string? CslApi,
    TextureModel Model,
    string? LocalSkinPath,
    string? LocalCapePath)
{
    /// <summary>
    /// 是否为纤细（Alex）模型。
    /// </summary>
    public bool IsSlim => Model == TextureModel.Slim;

    /// <summary>
    /// 存储键（snake_case，忽略大小写）到皮肤类型的映射。
    /// </summary>
    private static readonly Dictionary<string, SkinType> TypeByStorageKey = CreateTypeByStorageKey();

    private static Dictionary<string, SkinType> CreateTypeByStorageKey()
    {
        var map = new Dictionary<string, SkinType>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<SkinType>())
            map[JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString())] = value;
        return map;
    }

    /// <summary>
    /// 从存储 JSON 中反序列化皮肤配置。
    /// </summary>
    /// <param name="storage">已存在的存储对象，内部各键可直接读取。</param>
    /// <returns>解析成功返回皮肤配置；type 缺失或未知、任一字段解析失败均返回 <c>null</c>。</returns>
    public static Skin? FromStorage(JsonObject storage)
    {
        try
        {
            var typeNode = storage["type"];
            if (typeNode is not JsonValue typeValue || !typeValue.TryGetValue<string>(out var typeText))
                return null;
            if (!TryParseType(typeText, out var type))
                return null;

            // textureModel 只有严格等于 "slim" 才算纤细模型，其余一律视为经典模型
            var model = TextureModel.Wide;
            if (storage["textureModel"] is JsonValue modelValue
                && modelValue.TryGetValue<string>(out var modelText)
                && string.Equals(modelText, "slim", StringComparison.Ordinal))
                model = TextureModel.Slim;

            return new Skin(
                type,
                GetNullableString(storage, "cslApi"),
                model,
                GetNullableString(storage, "localSkinPath"),
                GetNullableString(storage, "localCapePath"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将皮肤配置写入存储 JSON。所有键都会写入（null 值保留为 JSON null）。
    /// </summary>
    /// <param name="storage">已存在的存储对象，键会写入其中。</param>
    public void WriteStorage(JsonObject storage)
    {
        storage["type"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(Type.ToString());
        storage["cslApi"] = CslApi;
        storage["textureModel"] = IsSlim ? "slim" : "wide";
        storage["localSkinPath"] = LocalSkinPath;
        storage["localCapePath"] = LocalCapePath;
    }

    private static string? GetNullableString(JsonObject storage, string key)
    {
        var node = storage[key];
        if (node is null) return null;
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        throw new InvalidOperationException($"字段 {key} 的类型不是字符串。");
    }

    private static bool TryParseType(string text, out SkinType type)
    {
        return TypeByStorageKey.TryGetValue(text, out type);
    }
}
