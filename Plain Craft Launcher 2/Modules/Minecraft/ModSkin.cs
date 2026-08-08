using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Skin;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static class ModSkin
{
    public struct McSkinInfo
    {
        public bool IsSlim;
        public string LocalFile;
        public bool IsVaild;
    }

    /// <summary>
    ///     要求玩家选择一个皮肤文件，并进行相关校验。
    /// </summary>
    public static McSkinInfo McSkinSelect()
    {
        var fileName = SystemDialogs.SelectFile(Lang.Text("Launch.Skin.FileDialog.Filter"), Lang.Text("Launch.Skin.FileDialog.Title"));

        // 验证有效性
        if (string.IsNullOrEmpty(fileName))
            return new McSkinInfo { IsVaild = false };
        try
        {
            var image = new MyBitmap(fileName);
            if (image.pic.Width != 64 || !(image.pic.Height == 32 || image.pic.Height == 64))
            {
                HintService.Hint(Lang.Text("Launch.Skin.InvalidSize"), HintType.Error);
                return new McSkinInfo { IsVaild = false };
            }

            var fileInfo = new FileInfo(fileName);
            if (fileInfo.Length > 24 * 1024)
            {
                HintService.Hint(Lang.Text("Launch.Skin.FileTooLarge", Lang.Number(fileInfo.Length / 1024d, "N2")),
                    HintType.Error);
                return new McSkinInfo { IsVaild = false };
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Launch.Skin.File.Error"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Launch.Skin.File.Error"));
            return new McSkinInfo { IsVaild = false };
        }

        // 获取皮肤种类
        var isSlim = ModMain.MyMsgBox(Lang.Text("Launch.Skin.Model.SelectMessage"), Lang.Text("Launch.Skin.Model.SelectTitle"), Lang.Text("Launch.Skin.Model.Steve"), Lang.Text("Launch.Skin.Model.Alex"), Lang.Text("Common.Option.IDontKnow"),
            highLight: false);
        if (isSlim == 3)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Model.UnknownHint"));
            return new McSkinInfo { IsVaild = false };
        }

        return new McSkinInfo { IsVaild = true, IsSlim = isSlim == 2, LocalFile = fileName };
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    /// </summary>
    public static string McSkinGetAddress(string uuid, string type)
    {
        if (string.IsNullOrEmpty(uuid))
            throw new Exception(Lang.Text("Minecraft.Skin.Error.UuidEmpty"));

        if (uuid.StartsWith("00000"))
            throw new Exception(Lang.Text("Minecraft.Skin.Error.OfflineNoSkin"));

        // 尝试读取缓存
        var cachePath = Path.Combine(ModBase.pathTemp, $"Cache\\Skin\\Index{type}.ini");
        var cacheSkinAddress = ModBase.ReadIni(cachePath, uuid);
        if (!string.IsNullOrEmpty(cacheSkinAddress))
            return cacheSkinAddress;

        // 获取皮肤地址
        var url = type switch
        {
            "Mojang" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Ms" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Auth" => ModProfile.selectedProfile.Server.Replace("/authserver", "") +
                      "/sessionserver/session/minecraft/profile/",
            _ => throw new ArgumentException(Lang.Text("Minecraft.Skin.Error.InvalidSkinType", type ?? "null"))
        };

        var skinString = ModNet.NetGetCodeByRequestRetry(url + uuid);
        if (string.IsNullOrEmpty((string?)skinString))
            throw new Exception(Lang.Text("Minecraft.Skin.Error.SkinReturnEmpty"));

        // 解析皮肤 Property
        string skinValue = null;
        try
        {
            var json = (JsonObject)ModBase.GetJson((string)skinString);
            foreach (var property in json["properties"].AsArray())
                if (property["name"]?.ToString() == "textures")
                {
                    skinValue = property["value"]?.ToString();
                    break;
                }

            if (skinValue is null)
                throw new Exception(Lang.Text("Minecraft.Skin.Error.PropertyNotFound"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex,
                $"无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：{skinString}",
                ModBase.LogLevel.Developer);
            throw new Exception(Lang.Text("Minecraft.Skin.Error.NoSkinData"), ex);
        }

        // 解码 Base64 并解析 JSON
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(skinValue));
        var skinJson = (JsonObject)ModBase.GetJson(decoded.ToLowerInvariant());

        if (skinJson["textures"]?["skin"]?["url"] is null)
            throw new Exception(Lang.Text("Minecraft.Skin.Error.NoCustomSkin"));

        var skinUrl = skinJson["textures"]["skin"]["url"].ToString();
        skinUrl = skinUrl.Contains("minecraft.net/") ? skinUrl.Replace("http://", "https://") : skinUrl;

        // 保存缓存
        ModBase.WriteIni(cachePath, uuid, skinUrl);
        ModBase.Log($"[Skin] UUID {uuid} 对应的皮肤文件为 {skinUrl}");

        return skinUrl;
    }

    private static readonly object mcSkinDownloadLock = new();

    /// <summary>
    ///     从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    /// </summary>
    public static string McSkinDownload(string address)
    {
        var skinName = ModBase.GetFileNameFromPath(address);
        var fileAddress = ModBase.pathTemp + @"Cache\Skin\" + ModBase.GetHash(address) + ".png";
        lock (mcSkinDownloadLock)
        {
            if (!File.Exists(fileAddress))
            {
                FileDownloader.DownloadAsync(address, fileAddress + ModNet.netDownloadEnd).GetAwaiter().GetResult();
                File.Delete(fileAddress);
                FileSystem.Rename(fileAddress + ModNet.netDownloadEnd, fileAddress);
                ModBase.Log("[Minecraft] 皮肤下载成功：" + fileAddress);
            }

            return fileAddress;
        }
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤，返回"Steve"或"Alex"。
    /// </summary>
    public static string McSkinSex(string uuid)
    {
        if (uuid.Length != 32)
            return "Steve";
        var a = int.Parse(uuid[7].ToString(), NumberStyles.AllowHexSpecifier);
        var b = int.Parse(uuid[15].ToString(), NumberStyles.AllowHexSpecifier);
        var c = int.Parse(uuid[23].ToString(), NumberStyles.AllowHexSpecifier);
        var d = int.Parse(uuid[31].ToString(), NumberStyles.AllowHexSpecifier);
        return ((a ^ b ^ c ^ d) % 2) != 0 ? "Alex" : "Steve";
        // Math.floorMod(uuid.hashCode(), 18)

        // Public Function hashCode(ByVal str As String) As Integer
        // Dim hash As Integer = 0
        // Dim n As Integer = str.Length
        // If n = 0 Then
        // Return hash
        // End If
        // For i As Integer = 0 To n - 1
        // hash = hash + Asc(str(i)) * (1 << (n - i - 1))
        // Next
        // Return hash
        // End Function
    }

    /// <summary>
    ///     根据皮肤配置加载离线皮肤的贴图数据。无皮肤或加载失败时返回 <c>null</c>。
    /// </summary>
    /// <param name="skin">离线账户的皮肤配置。</param>
    /// <param name="username">离线用户名，用于请求 Custom Skin Loader API。</param>
    /// <returns>加载到的皮肤数据；无皮肤或加载失败为 <c>null</c>。</returns>
    public static async Task<LoadedSkin?> LoadSkinAsync(Skin skin, string username)
    {
        switch (skin.Type)
        {
            case SkinType.Default:
                return null;
            case SkinType.Steve:
                return LoadBuiltin("Steve", TextureModel.Wide);
            case SkinType.Alex:
                return LoadBuiltin("Alex", TextureModel.Slim);
            case SkinType.LocalFile:
                return await LoadLocalFileAsync(skin).ConfigureAwait(false);
            case SkinType.LittleSkin:
            case SkinType.CustomSkinLoaderApi:
                return await LoadCslAsync(skin, username).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"不支持的皮肤来源类型：{skin.Type}");
        }
    }

    /// <summary>
    ///     加载启动器内置的皮肤贴图（Steve / Alex），不含披风。
    /// </summary>
    /// <param name="name">皮肤名称（Steve 或 Alex），对应 <see cref="ModBase.pathImage" /> 下 Skins 文件夹中的文件名。</param>
    /// <param name="model">皮肤的纹理模型。</param>
    /// <returns>内置皮肤数据。</returns>
    private static LoadedSkin LoadBuiltin(string name, TextureModel model)
    {
        var bitmap = new MyBitmap(ModBase.pathImage + "Skins/" + name + ".png").pic;
        return new LoadedSkin(model, SkinTexture.Load(bitmap), null);
    }

    /// <summary>
    ///     加载本地皮肤文件与本地披风文件。
    /// </summary>
    /// <param name="skin">皮肤配置，包含本地文件路径。</param>
    /// <returns>加载到的皮肤数据；皮肤与披风均未设置或读取失败时为 <c>null</c>。</returns>
    private static async Task<LoadedSkin?> LoadLocalFileAsync(Skin skin)
    {
        var skinBitmap = await LoadBitmapAsync(skin.LocalSkinPath).ConfigureAwait(false);
        var capeBitmap = await LoadBitmapAsync(skin.LocalCapePath).ConfigureAwait(false);
        return new LoadedSkin(skin.TextureModel,
            skinBitmap is null ? null : SkinTexture.Load(skinBitmap),
            capeBitmap is null ? null : SkinTexture.Load(capeBitmap));
    }

    /// <summary>
    ///     从本地文件加载位图。路径为空、文件不存在或图片损坏时返回 <c>null</c> 并记录开发者日志。
    /// </summary>
    /// <param name="path">图片文件路径。</param>
    /// <returns>加载到的位图；失败为 <c>null</c>。</returns>
    private static async Task<Bitmap?> LoadBitmapAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        try
        {
            return await Task.Run(() => new MyBitmap(path).pic).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"加载本地皮肤图片失败：{path}", ModBase.LogLevel.Developer);
            return null;
        }
    }

    /// <summary>
    ///     通过 Custom Skin Loader API 加载皮肤与披风。
    ///     分为两步：先请求 <c>{api}/{username}.json</c> 获取皮肤信息，再并行下载皮肤与披风贴图。
    /// </summary>
    /// <param name="skin">皮肤配置。</param>
    /// <param name="username">离线用户名。</param>
    /// <returns>加载到的皮肤数据；请求失败或无皮肤时为 <c>null</c>。</returns>
    private static async Task<LoadedSkin?> LoadCslAsync(Skin skin, string username)
    {
        var api = skin.Type == SkinType.LittleSkin ? "https://littleskin.cn/csl" : NormalizeCslUrl(skin.CslApi);
        if (string.IsNullOrEmpty(api) || string.IsNullOrEmpty(username))
            return null;

        // 第一步：获取皮肤信息 JSON
        var jsonText = ModNet.NetGetCodeByRequestRetry($"{api}/{username}.json")?.ToString();
        if (string.IsNullOrEmpty(jsonText))
            return null;
        var parsed = SkinJson.FromJson(jsonText);
        if (parsed is null || !parsed.HasSkin)
            return null;

        var model = parsed.GetModel();

        // 第二步：并行下载皮肤与披风贴图
        var skinTask = parsed.SkinHash is null
            ? Task.FromResult<SkinTexture?>(null)
            : DownloadCslTextureAsync(api, parsed.SkinHash);
        var capeTask = parsed.CapeHash is null
            ? Task.FromResult<SkinTexture?>(null)
            : DownloadCslTextureAsync(api, parsed.CapeHash);
        var skinTex = await skinTask.ConfigureAwait(false);
        var capeTex = await capeTask.ConfigureAwait(false);
        if (skinTex is null)
            return null;
        return new LoadedSkin(model ?? TextureModel.Wide, skinTex, capeTex);
    }

    /// <summary>
    ///     下载并加载指定哈希的 CSL 皮肤贴图。
    ///     贴图先暂存到 <see cref="ModBase.pathTemp" /> 下的 Skin 文件夹（已存在则直接复用，不重复下载），
    ///     读取完成后删除临时文件。
    /// </summary>
    /// <param name="api">Custom Skin Loader API 地址。</param>
    /// <param name="hash">贴图哈希。</param>
    /// <returns>加载到的贴图；下载或读取失败为 <c>null</c>。</returns>
    private static async Task<SkinTexture?> DownloadCslTextureAsync(string api, string hash)
    {
        var directory = Path.Combine(ModBase.pathTemp, "Skin");
        var tempPath = Path.Combine(directory, hash + ".png");
        var url = $"{api}/textures/{hash}";
        try
        {
            Directory.CreateDirectory(directory);
            if (!File.Exists(tempPath))
                await FileDownloader.DownloadAsync(url, tempPath).ConfigureAwait(false);

            var bitmap = await Task.Run(() => new MyBitmap(tempPath).pic).ConfigureAwait(false);
            return SkinTexture.Load(bitmap);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"下载或读取 CSL 皮肤贴图失败：{url}", ModBase.LogLevel.Developer);
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // 临时文件删除失败不影响皮肤加载结果
            }
        }
    }

    /// <summary>
    ///     规范化 Custom Skin Loader API 地址：空白返回空字符串，缺失协议时补全 <c>https://</c>，并去除末尾的斜杠。
    /// </summary>
    /// <param name="url">原始 API 地址。</param>
    /// <returns>规范化后的 API 地址。</returns>
    public static string NormalizeCslUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        var result = url.Trim();
        if (!result.Contains("://"))
            result = "https://" + result;
        return result.TrimEnd('/');
    }
}
