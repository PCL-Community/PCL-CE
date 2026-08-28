using System;
using System.Text.Json.Nodes;
using PCL.Core.Minecraft.Skin;

namespace PCL;

/// <summary>
///     解析 Custom Skin Loader API 返回的皮肤信息 JSON。
///     同时支持旧版字段（skin / cape / elytra）与新版 textures（或别名 skins）对象。
/// </summary>
internal sealed class SkinJson
{
    /// <summary>
    ///     皮肤所属的用户名。
    /// </summary>
    public string? Username { get; }

    /// <summary>
    ///     皮肤贴图哈希；未找到任何皮肤哈希时为 <c>null</c>。
    /// </summary>
    public string? SkinHash { get; }

    /// <summary>
    ///     披风贴图哈希；未设置时为 <c>null</c>。
    /// </summary>
    public string? CapeHash { get; }

    /// <summary>
    ///     是否包含皮肤信息（以用户名是否为非空字符串判断）。
    /// </summary>
    public bool HasSkin => !string.IsNullOrEmpty(Username);

    private readonly JsonObject? _textures;

    private SkinJson(string? username, string? skinHash, string? capeHash, JsonObject? textures)
    {
        Username = username;
        SkinHash = skinHash;
        CapeHash = capeHash;
        _textures = textures;
    }

    /// <summary>
    ///     从 JSON 文本解析皮肤信息；解析失败返回 <c>null</c>。
    /// </summary>
    /// <param name="jsonText">Custom Skin Loader API 返回的 JSON 文本。</param>
    /// <returns>解析结果；JSON 非法或结构不符为 <c>null</c>。</returns>
    public static SkinJson? FromJson(string jsonText)
    {
        try
        {
            var root = JsonNode.Parse(jsonText) as JsonObject;
            if (root is null)
                return null;

            var username = GetString(root, "username");
            var textures = GetObject(root, "textures") ?? GetObject(root, "skins");
            var model = GetModel(textures);
            var skinHash = GetSkinHash(textures, model, root);
            var capeHash = GetCapeHash(textures, root);
            return new SkinJson(username, skinHash, capeHash, textures);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     获取皮肤的纹理模型：优先纤细（slim），其次经典（default），均未设置时为 <c>null</c>。
    /// </summary>
    /// <returns>纹理模型；无法确定时为 <c>null</c>。</returns>
    public TextureModel? GetModel()
    {
        return GetModel(_textures);
    }

    /// <summary>
    ///     计算纹理模型：<c>textures.slim</c> 非空 → Slim；<c>textures.default</c> 非空 → Wide；否则为 <c>null</c>。
    /// </summary>
    private static TextureModel? GetModel(JsonObject? textures)
    {
        if (textures is null)
            return null;
        if (!string.IsNullOrEmpty(GetString(textures, "slim")))
            return TextureModel.Slim;
        if (!string.IsNullOrEmpty(GetString(textures, "default")))
            return TextureModel.Wide;
        return null;
    }

    /// <summary>
    ///     确定皮肤贴图哈希：有模型信息时优先取对应模型（slim / default）的哈希，
    ///     均不可用时回退到顶层旧字段 <c>skin</c>。
    /// </summary>
    private static string? GetSkinHash(JsonObject? textures, TextureModel? model, JsonObject root)
    {
        var slimHash = textures is null ? null : GetString(textures, "slim");
        var defaultHash = textures is null ? null : GetString(textures, "default");
        if (model == TextureModel.Slim && !string.IsNullOrEmpty(slimHash))
            return slimHash;
        if (model == TextureModel.Wide && !string.IsNullOrEmpty(defaultHash))
            return defaultHash;
        return GetString(root, "skin");
    }

    /// <summary>
    ///     确定披风贴图哈希：优先 <c>textures.cape</c>，其次顶层旧字段 <c>cape</c>。
    /// </summary>
    private static string? GetCapeHash(JsonObject? textures, JsonObject root)
    {
        var capeHash = textures is null ? null : GetString(textures, "cape");
        if (!string.IsNullOrEmpty(capeHash))
            return capeHash;
        return GetString(root, "cape");
    }

    private static string? GetString(JsonObject? obj, string key)
    {
        if (obj is null)
            return null;
        if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
            return text;
        return null;
    }

    private static JsonObject? GetObject(JsonObject? obj, string key)
    {
        if (obj is null)
            return null;
        return obj[key] as JsonObject;
    }
}
