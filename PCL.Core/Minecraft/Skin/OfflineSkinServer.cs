using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
///     内嵌的离线 Yggdrasil 皮肤服务器。注册到本地回环地址，供 authlib-injector 与游戏客户端
///     通过 <c>http://localhost:{Port}</c> 访问元数据、档案与皮肤贴图。对齐 HMCL 的
///     <c>YggdrasilServer</c>。
/// </summary>
public sealed class OfflineSkinServer : HttpServer
{
    /// <summary>
    ///     用于对 textures 属性签名的 RSA 私钥，公钥通过 <c>GET /</c> 元数据暴露。
    /// </summary>
    private readonly RSA _signKey = RsaKeyUtils.CreateKey();

    /// <summary>
    ///     注册到本服务器的玩家角色。
    /// </summary>
    private sealed record Character(Guid Uuid, string Name, LoadedSkin? Skin);

    private readonly Dictionary<Guid, Character> _charactersByUuid = new();
    private readonly Dictionary<string, Character> _charactersByName = new();
    // 角色表在 HTTP 处理线程上读取、在 AddCharacter 写入，需要互斥保护。
    private readonly object _syncRoot = new();

    public OfflineSkinServer() : base([IPAddress.Loopback, IPAddress.IPv6Loopback], port: 0)
    {
    }

    /// <summary>
    ///     注册一个玩家角色。同名角色重复添加会覆盖旧的映射。
    /// </summary>
    /// <param name="uuid">玩家 UUID</param>
    /// <param name="name">玩家名</param>
    /// <param name="skin">皮肤（可为 null，表示不携带贴图）</param>
    public void AddCharacter(Guid uuid, string name, LoadedSkin? skin)
    {
        lock (_syncRoot)
        {
            var character = new Character(uuid, name, skin);
            _charactersByUuid[uuid] = character;
            _charactersByName[name] = character;
        }
    }

    protected override void Init()
    {
        Register(HttpMethod.Get, "/", _HandleMeta);
        Register(HttpMethod.Get, "/status", _HandleStatus);
        Register(HttpMethod.Post, "/api/profiles/minecraft", _HandleProfiles);
        Register(HttpMethod.Get, "/sessionserver/session/minecraft/hasJoined", _HandleHasJoined);
        Register(HttpMethod.Post, "/sessionserver/session/minecraft/join", _HandleJoin);
        RegisterWithParams(HttpMethod.Get, "/sessionserver/session/minecraft/profile/{uuid}", _HandleProfile);
        RegisterWithParams(HttpMethod.Get, "/textures/{hash}", _HandleTexture);
    }

    /// <summary>
    ///     authlib-injector 启动时请求的元数据。
    /// </summary>
    private Task<HttpRouteResponse> _HandleMeta(HttpListenerRequest request)
    {
        var metadata = new JsonObject
        {
            ["signaturePublickey"] = RsaKeyUtils.GetPublicKeyPem(_signKey),
            ["skinDomains"] = new JsonArray("127.0.0.1", "localhost"),
            ["meta"] = new JsonObject
            {
                ["serverName"] = "PCL CE",
                ["implementationName"] = "PCL CE",
                // 版本号与 metadata.json 保持一致；升级启动器版本时需同步更新
                ["implementationVersion"] = "2.15.0",
                ["feature.non_email_login"] = true
            }
        };
        return HttpRouteResponse.Json(metadata).AsTask();
    }

    private Task<HttpRouteResponse> _HandleStatus(HttpListenerRequest request)
    {
        int characterCount;
        lock (_syncRoot)
            characterCount = _charactersByUuid.Count;

        var status = new JsonObject
        {
            ["user.count"] = characterCount,
            ["token.count"] = 0,
            ["pendingAuthentication.count"] = 0
        };
        return HttpRouteResponse.Json(status).AsTask();
    }

    /// <summary>
    ///     按玩家名批量查询档案（id 无横线）。无匹配时按 HMCL 行为返回 204 No Content。
    /// </summary>
    private async Task<HttpRouteResponse> _HandleProfiles(HttpListenerRequest request)
    {
        string body;
        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return HttpRouteResponse.BadRequest;
        }

        if (parsed is not JsonArray names)
            return HttpRouteResponse.BadRequest;

        var result = new JsonArray();
        foreach (var nameNode in names)
        {
            if (nameNode is not JsonValue value || !value.TryGetValue<string>(out var name))
                continue;
            if (!_TryGetByName(name, out var character))
                continue;

            result.Add(new JsonObject
            {
                ["id"] = character.Uuid.ToString("N"),
                ["name"] = character.Name
            });
        }

        return result.Count == 0
            ? HttpRouteResponse.Empty(HttpStatusCode.NoContent)
            : HttpRouteResponse.Json(result);
    }

    private Task<HttpRouteResponse> _HandleHasJoined(HttpListenerRequest request)
    {
        var username = request.QueryString["username"];
        if (string.IsNullOrEmpty(username) || !_TryGetByName(username, out var character))
            return HttpRouteResponse.NotFound.AsTask();
        return HttpRouteResponse.Json(_CreateCompleteResponse(character)).AsTask();
    }

    private Task<HttpRouteResponse> _HandleJoin(HttpListenerRequest request) =>
        HttpRouteResponse.NoContent.AsTask();

    /// <summary>
    ///     按 UUID（32 位无横线）返回完整档案。
    /// </summary>
    private Task<HttpRouteResponse> _HandleProfile(HttpListenerRequest request, IReadOnlyDictionary<string, string> parameters)
    {
        var uuidText = parameters["uuid"];
        if (!Guid.TryParseExact(uuidText, "N", out var uuid) || !_TryGetByUuid(uuid, out var character))
            return HttpRouteResponse.NotFound.AsTask();
        return HttpRouteResponse.Json(_CreateCompleteResponse(character)).AsTask();
    }

    /// <summary>
    ///     按 hash 从 <see cref="SkinTexture" /> 缓存取出贴图并返回 PNG 字节。
    /// </summary>
    private Task<HttpRouteResponse> _HandleTexture(HttpListenerRequest request, IReadOnlyDictionary<string, string> parameters)
    {
        var hash = parameters["hash"];
        var texture = SkinTexture.Get(hash);
        if (texture?.Image is not { } image)
            return HttpRouteResponse.NotFound.AsTask();

        var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        // 不显式释放 MemoryStream：HttpRouteResponse.Pour 只把 InputStream CopyTo 到响应输出流，
        // 且不会释放该流（见 HttpRouteResponse.Pour 源码）。流交由 HttpRouteResponse 持有，
        // 请求处理完毕后由 GC 回收，与 HttpRouteResponse.Json 内部对 MemoryStream 的处理方式一致。
        return HttpRouteResponse.Input(stream, "image/png").AsTask();
    }

    /// <summary>
    ///     构造完整档案响应（对齐 HMCL YggdrasilServer.Character.toCompleteResponse）。
    /// </summary>
    private JsonObject _CreateCompleteResponse(Character character)
    {
        var texturesPayload = _CreateTexturesPayload(character);
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(texturesPayload.ToJsonString()));

        return new JsonObject
        {
            ["id"] = character.Uuid.ToString("N"),
            ["name"] = character.Name,
            ["properties"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "textures",
                    ["value"] = value,
                    ["signature"] = RsaKeyUtils.SignData(_signKey, Encoding.UTF8.GetBytes(value))
                }
            }
        };
    }

    /// <summary>
    ///     构造 textures 属性值的 JSON 载荷。皮肤为 null 时 textures 对象保持为空。
    ///     metadata.model 仅在 Slim 模型时写入。
    /// </summary>
    private JsonObject _CreateTexturesPayload(Character character)
    {
        var textures = new JsonObject();
        var loadedSkin = character.Skin;

        if (loadedSkin?.Skin is { } skinTexture)
        {
            var skin = new JsonObject
            {
                // 用 127.0.0.1 而非 localhost：HttpListener 对具体 IP 前缀要求 Host 头严格匹配，
                // 与 ModLaunch 注入的 javaagent 地址保持一致，避免 Host 头不匹配导致 404
                ["url"] = $"http://127.0.0.1:{Port}/textures/{skinTexture.Hash}"
            };
            if (loadedSkin.Model == TextureModel.Slim)
                skin["metadata"] = new JsonObject { ["model"] = "slim" };
            textures["SKIN"] = skin;
        }

        if (loadedSkin?.Cape is { } capeTexture)
            textures["CAPE"] = new JsonObject
            {
                ["url"] = $"http://127.0.0.1:{Port}/textures/{capeTexture.Hash}"
            };

        return new JsonObject
        {
            ["timestamp"] = 0,
            ["profileId"] = character.Uuid.ToString("N"),
            ["profileName"] = character.Name,
            ["textures"] = textures
        };
    }

    private bool _TryGetByName(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Character character)
    {
        lock (_syncRoot)
        {
            if (_charactersByName.TryGetValue(name, out var found))
            {
                character = found;
                return true;
            }
            character = null!;
            return false;
        }
    }

    private bool _TryGetByUuid(Guid uuid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Character character)
    {
        lock (_syncRoot)
        {
            if (_charactersByUuid.TryGetValue(uuid, out var found))
            {
                character = found;
                return true;
            }
            character = null!;
            return false;
        }
    }
}
