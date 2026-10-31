using System.Globalization;
using System.IO;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Network;

namespace PCL;

public static class Skin
{
    private static readonly Lock McSkinDownloadLock = new();

    private static McSkinInfo InvalidSkinInfo()
    {
        return new McSkinInfo
        {
            IsValid = false,
            LocalFile = ""
        };
    }

    /// <summary>
    ///     要求玩家选择一个皮肤文件，并进行相关校验。
    /// </summary>
    public static McSkinInfo McSkinSelect()
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Launch.Skin.FileDialog.Filter"),
            Lang.Text("Launch.Skin.FileDialog.Title"));

        if (string.IsNullOrEmpty(fileName))
            return InvalidSkinInfo();
        try
        {
            var image = new MyBitmap(fileName);
            if (image.pic.Width != 64 || image.pic.Height is not (32 or 64))
            {
                HintService.Hint(Lang.Text("Launch.Skin.InvalidSize"), HintType.Error);
                return InvalidSkinInfo();
            }

            var fileInfo = new FileInfo(fileName);
            if (fileInfo.Length > 24 * 1024)
            {
                HintService.Hint(
                    Lang.Text(
                        "Launch.Skin.FileTooLarge",
                        Lang.Number(fileInfo.Length / 1024d, "N2")),
                    HintType.Error);
                return InvalidSkinInfo();
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Launch.Skin.File.Error"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Launch.Skin.File.Error"));
            return InvalidSkinInfo();
        }

        var isSlim = DialogService.Instance.Show(new TextDialogRequest
        {
            Title = Lang.Text("Launch.Skin.Model.SelectTitle"),
            Content = Lang.Text("Launch.Skin.Model.SelectMessage"),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Launch.Skin.Model.Steve")),
                DialogButton.Create(Lang.Text("Launch.Skin.Model.Alex")),
                DialogButton.Create(Lang.Text("Common.Option.IDontKnow"))),
            HighLight = false,
            ForceWait = true
        }).Result?.Button;

        if (isSlim != 3)
            return new McSkinInfo
            {
                IsValid = true,
                IsSlim = isSlim == 2,
                LocalFile = fileName
            };

        HintService.Hint(Lang.Text("Launch.Skin.Model.UnknownHint"));
        return InvalidSkinInfo();
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    /// </summary>
    public static string McSkinGetAddress(string uuid, string type)
    {
        if (string.IsNullOrEmpty(uuid))
            throw new ArgumentException(
                Lang.Text("Minecraft.Skin.Error.UuidEmpty"),
                nameof(uuid));

        if (uuid.StartsWith("00000"))
            throw new InvalidOperationException(Lang.Text("Minecraft.Skin.Error.OfflineNoSkin"));

        var cachePath = Path.Combine(Common.pathTemp, $@"Cache\Skin\Index{type}.ini");
        var cacheSkinAddress = Common.ReadIni(cachePath, uuid);
        if (!string.IsNullOrEmpty(cacheSkinAddress))
            return cacheSkinAddress;

        var url = type switch
        {
            "Mojang" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Ms" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Auth" => (ProfileService.Current?.Server ?? string.Empty).Replace("/authserver", "")
                + "/sessionserver/session/minecraft/profile/",
            _ => throw new ArgumentException(
                Lang.Text(
                    "Minecraft.Skin.Error.InvalidSkinType",
                    type ?? "null"))
        };

        var skinString = NetFacade.NetGetCodeByRequestRetry(url + uuid) as string;
        if (string.IsNullOrEmpty(skinString))
            throw new InvalidDataException(Lang.Text("Minecraft.Skin.Error.SkinReturnEmpty"));

        string? skinValue = null;
        try
        {
            var json = (JsonObject)Common.GetJson(skinString);
            var properties = json["properties"]?.AsArray();
            if (properties is not null)
                foreach (var property in properties.OfType<JsonObject>())
                    if (property["name"]?.ToString() == "textures")
                    {
                        skinValue = property["value"]?.ToString();
                        break;
                    }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：{skinString}",
                Common.LogLevel.Developer);
            throw new InvalidDataException(Lang.Text("Minecraft.Skin.Error.NoSkinData"), ex);
        }

        if (skinValue is null)
            throw new InvalidDataException(Lang.Text("Minecraft.Skin.Error.PropertyNotFound"));

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(skinValue));
        var skinJson = (JsonObject)Common.GetJson(decoded.ToLowerInvariant());
        var skinUrlNode = skinJson["textures"]?["skin"]?["url"];
        if (skinUrlNode is null)
            throw new InvalidDataException(Lang.Text("Minecraft.Skin.Error.NoCustomSkin"));

        var skinUrl = skinUrlNode.ToString();
        skinUrl = skinUrl.Contains("minecraft.net/")
            ? skinUrl.Replace("http://", "https://")
            : skinUrl;

        Common.WriteIni(cachePath, uuid, skinUrl);
        Common.Log($"[Skin] UUID {uuid} 对应的皮肤文件为 {skinUrl}");

        return skinUrl;
    }

    /// <summary>
    ///     从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    /// </summary>
    public static string McSkinDownload(string address)
    {
        var fileAddress = Common.pathTemp + @"Cache\Skin\" + Common.GetHash(address) + ".png";
        lock (McSkinDownloadLock)
        {
            if (!File.Exists(fileAddress))
            {
                FileDownloader.DownloadAsync(address, fileAddress).GetAwaiter().GetResult();
                Common.Log("[Minecraft] 皮肤下载成功：" + fileAddress);
            }

            return fileAddress;
        }
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤，返回"Steve"或"Alex"。
    /// </summary>
    public static string McSkinVariant(string uuid)
    {
        if (uuid.Length != 32)
            return "Steve";

        var a = int.Parse(uuid[7].ToString(), NumberStyles.AllowHexSpecifier);
        var b = int.Parse(uuid[15].ToString(), NumberStyles.AllowHexSpecifier);
        var c = int.Parse(uuid[23].ToString(), NumberStyles.AllowHexSpecifier);
        var d = int.Parse(uuid[31].ToString(), NumberStyles.AllowHexSpecifier);

        return (a ^ b ^ c ^ d) % 2 != 0 ? "Alex" : "Steve";
    }

    public struct McSkinInfo
    {
        public bool IsSlim;
        public string LocalFile;
        public bool IsValid;
    }
}