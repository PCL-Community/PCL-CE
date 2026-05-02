using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Network;


namespace PCL;

public static partial class ModMinecraft
{
    #region 皮肤

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
        var FileName = SystemDialogs.SelectFile("皮肤文件(*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp", "选择皮肤文件");

        // 验证有效性
        if (string.IsNullOrEmpty(FileName))
            return new McSkinInfo { IsVaild = false };
        try
        {
            var Image = new MyBitmap(FileName);
            if (Image.Pic.Width != 64 || !(Image.Pic.Height == 32 || Image.Pic.Height == 64))
            {
                ModMain.Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", ModMain.HintType.Critical);
                return new McSkinInfo { IsVaild = false };
            }

            var FileInfo = new FileInfo(FileName);
            if (FileInfo.Length > 24 * 1024)
            {
                ModMain.Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " + Math.Round(FileInfo.Length / 1024d, 2) + " KB",
                    ModMain.HintType.Critical);
                return new McSkinInfo { IsVaild = false };
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "皮肤文件存在错误", ModBase.LogLevel.Hint);
            return new McSkinInfo { IsVaild = false };
        }

        // 获取皮肤种类
        var IsSlim = ModMain.MyMsgBox("此皮肤为 Steve 模型（粗手臂）还是 Alex 模型（细手臂）？", "选择皮肤种类", "Steve 模型", "Alex 模型", "我不知道",
            HighLight: false);
        if (IsSlim == 3)
        {
            ModMain.Hint("请在皮肤下载页面确认皮肤种类后再使用此皮肤！");
            return new McSkinInfo { IsVaild = false };
        }

        return new McSkinInfo { IsVaild = true, IsSlim = IsSlim == 2, LocalFile = FileName };
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    /// </summary>
    public static string McSkinGetAddress(string uuid, string type)
    {
        if (string.IsNullOrEmpty(uuid))
            throw new Exception("Uuid 为空。");

        if (uuid.StartsWith("00000"))
            throw new Exception("离线 Uuid 无正版皮肤文件。");

        // 尝试读取缓存
        var cachePath = Path.Combine(ModBase.PathTemp, $"Cache\\Skin\\Index{type}.ini");
        var cacheSkinAddress = ModBase.ReadIni(cachePath, uuid);
        if (!string.IsNullOrEmpty(cacheSkinAddress))
            return cacheSkinAddress;

        // 获取皮肤地址
        var url = type switch
        {
            "Mojang" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Ms" => "https://sessionserver.mojang.com/session/minecraft/profile/",
            "Auth" => ModProfile.SelectedProfile.Server.Replace("/authserver", "") +
                      "/sessionserver/session/minecraft/profile/",
            _ => throw new ArgumentException($"皮肤地址种类无效：{type ?? "null"}")
        };

        var skinString = ModNet.NetGetCodeByRequestRetry(url + uuid);
        if (string.IsNullOrEmpty((string?)skinString))
            throw new Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户");

        // 解析皮肤 Property
        string skinValue = null;
        try
        {
            var json = (JObject)ModBase.GetJson((string)skinString);
            foreach (var property in json["properties"])
                if (property["name"]?.ToString() == "textures")
                {
                    skinValue = property["value"]?.ToString();
                    break;
                }

            if (skinValue == null)
                throw new Exception("未从皮肤返回值中找到符合条件的 Property");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex,
                $"无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：{skinString}",
                ModBase.LogLevel.Developer);
            throw new Exception("皮肤返回值中不包含皮肤数据项，可能是未设置自定义皮肤的用户", ex);
        }

        // 解码 Base64 并解析 JSON
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(skinValue));
        var skinJson = (JObject)ModBase.GetJson(decoded.ToLowerInvariant());

        if (skinJson["textures"]?["skin"]?["url"] == null)
            throw new Exception("用户未设置自定义皮肤");

        var skinUrl = skinJson["textures"]["skin"]["url"].ToString();
        skinUrl = skinUrl.Contains("minecraft.net/") ? skinUrl.Replace("http://", "https://") : skinUrl;

        // 保存缓存
        ModBase.WriteIni(cachePath, uuid, skinUrl);
        ModBase.Log($"[Skin] UUID {uuid} 对应的皮肤文件为 {skinUrl}");

        return skinUrl;
    }

    private static readonly object McSkinDownloadLock = new();

    /// <summary>
    ///     从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    /// </summary>
    public static string McSkinDownload(string Address)
    {
        var SkinName = ModBase.GetFileNameFromPath(Address);
        var FileAddress = ModBase.PathTemp + @"Cache\Skin\" + ModBase.GetHash(Address) + ".png";
        lock (McSkinDownloadLock)
        {
            if (!File.Exists(FileAddress))
            {
                FileDownloader.Download(Address, FileAddress + ModNet.NetDownloadEnd).GetAwaiter().GetResult();
                File.Delete(FileAddress);
                FileSystem.Rename(FileAddress + ModNet.NetDownloadEnd, FileAddress);
                ModBase.Log("[Minecraft] 皮肤下载成功：" + FileAddress);
            }

            return FileAddress;
        }
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤，返回“Steve”或“Alex”。
    /// </summary>
    public static string McSkinSex(string Uuid)
    {
        if (!(Uuid.Length == 32))
            return "Steve";
        var a = int.Parse(Conversions.ToString(Uuid[7]), NumberStyles.AllowHexSpecifier);
        var b = int.Parse(Conversions.ToString(Uuid[15]), NumberStyles.AllowHexSpecifier);
        var c = int.Parse(Conversions.ToString(Uuid[23]), NumberStyles.AllowHexSpecifier);
        var d = int.Parse(Conversions.ToString(Uuid[31]), NumberStyles.AllowHexSpecifier);
        return Conversions.ToBoolean((a ^ b ^ c ^ d) % 2) ? "Alex" : "Steve";
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

    #endregion

    #region 支持库文件（Libraries）

    public class McLibToken
    {
        private string _Url;

        /// <summary>
        ///     是否为纯本地文件，若是则不尝试联网下载。
        /// </summary>
        public bool IsLocal;

        /// <summary>
        ///     是否为 Natives 文件。
        /// </summary>
        public bool IsNatives;

        /// <summary>
        ///     文件的完整本地路径。
        /// </summary>
        public string LocalPath;

        /// <summary>
        ///     原 JSON 中的 Name 项。
        /// </summary>
        public string OriginalName;

        /// <summary>
        ///     文件的 SHA1。
        /// </summary>
        public string SHA1;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long Size;

        /// <summary>
        ///     由 JSON 提供的 URL，若没有则为 Nothing。
        /// </summary>
        public string Url
        {
            get => _Url;
            set =>
                // 孤儿 Forge 作者喜欢把没有 URL 的写个空字符串
                _Url = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        ///     原 JSON 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        /// </summary>
        public string Name
        {
            get
            {
                if (OriginalName is null)
                    return null;
                var Splited = new List<string>(OriginalName.Split(":"));
                Splited.RemoveAt(2); // Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                return Splited.Join(":");
            }
        }

        public override string ToString()
        {
            return (IsNatives ? "[Native] " : "") + ModBase.GetString(Size) + " | " + LocalPath;
        }
    }

    /// <summary>
    ///     检查是否符合 JSON 中的 Rules。
    /// </summary>
    /// <param name="RuleToken">JSON 中的 "rules" 项目。</param>
    public static bool McJsonRuleCheck(JToken RuleToken)
    {
        if (RuleToken is null)
            return true;

        // 初始化
        var Required = false;
        foreach (var Rule in RuleToken)
        {
            // 单条条件验证
            var IsRightRule = true; // 是否为正确的规则
            if (Rule["os"] is not null) // 操作系统
            {
                if (Rule["os"]["name"] is not null) // 操作系统名称
                {
                    var OsName = Rule["os"]["name"].ToString();
                    if (OsName == "unknown")
                    {
                    }
                    else if (OsName == "windows")
                    {
                        if (Rule["os"]["version"] is not null) // 操作系统版本
                        {
                            var Cr = Rule["os"]["version"].ToString();
                            IsRightRule = IsRightRule && OSVersion.RegexCheck(Cr);
                        }
                    }
                    else
                    {
                        IsRightRule = false;
                    }
                }

                if (Rule["os"]["arch"] is not null) // 操作系统架构
                    IsRightRule = IsRightRule && Rule["os"]["arch"].ToString() == "x86" == ModBase.Is32BitSystem;
            }

            if (!(Rule["features"] == null)) // 标签
            {
                IsRightRule = IsRightRule && Rule["features"]["is_demo_user"] == null; // 反选是否为 Demo 用户
                if (((JObject)Rule["features"]).Children().OfType<JProperty>().Any(j => j.Name.Contains("quick_play")))
                    IsRightRule = false; // 不开 Quick Play，让玩家自己加去
            }

            // 反选确认
            if (Rule["action"].ToString() == "allow")
            {
                if (IsRightRule)
                    Required = true; // allow
            }
            else if (IsRightRule)
            {
                Required = false; // disallow
            }
        }

        return Required;
    }

    private static readonly string OSVersion = Environment.OSVersion.Version.ToString();

    /// <summary>
    ///     递归获取 Minecraft 某一实例的完整支持库列表。
    /// </summary>
    public static List<McLibToken> McLibListGet(McInstance Instance, bool IncludeInstanceJar)
    {
        // 获取当前支持库列表
        ModBase.Log("[Minecraft] 获取支持库列表：" + Instance.Name);
        var result = McLibListGetWithJson(Instance.JsonObject, TargetInstance: Instance);

        // 需要添加原版 Jar
        if (IncludeInstanceJar)
        {
            McInstance RealInstance;
            var RequiredJar = Instance.JsonObject["jar"]?.ToString();
            if (Instance.IsHmclFormatJson || RequiredJar is null)
            {
                // HMCL 项直接使用自身的 Jar
                // 根据 Inherit 获取最深层实例
                var OriginalInstance = Instance;
                // 1.17+ 的 Forge 不寻找 Inherit
                if (!((Instance.Info.HasForge || Instance.Info.HasNeoForge) && Instance.Info.Drop >= 170))
                    while (!string.IsNullOrEmpty(OriginalInstance.InheritInstanceName))
                    {
                        if ((OriginalInstance.InheritInstanceName ?? "") == (OriginalInstance.Name ?? ""))
                            break;
                        OriginalInstance = new McInstance(McFolderSelected + @"versions\" +
                                                          OriginalInstance.InheritInstanceName + @"\");
                    }

                // 需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                // 复现：启动一个 Snapshot 实例
                RealInstance = new McInstance(OriginalInstance.PathInstance);
            }
            else
            {
                // Json 已提供 Jar 字段，使用该字段的信息
                RealInstance = new McInstance(RequiredJar);
            }

            string ClientUrl;
            string ClientSHA1;
            // 判断需求的实例是否存在
            // 不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            if (!File.Exists(RealInstance.PathInstance + RealInstance.Name + ".json"))
            {
                RealInstance = Instance;
                ModBase.Log("[Minecraft] 可能缺少前置实例 " + RealInstance.Name + "，找不到对应的 JSON 文件", ModBase.LogLevel.Debug);
            }

            // 获取详细下载信息
            if (RealInstance.JsonObject["downloads"] is not null &&
                RealInstance.JsonObject["downloads"]["client"] is not null)
            {
                ClientUrl = (string)RealInstance.JsonObject["downloads"]["client"]["url"];
                ClientSHA1 = (string)RealInstance.JsonObject["downloads"]["client"]["sha1"];
            }
            else
            {
                ClientUrl = null;
                ClientSHA1 = null;
            }

            // 把所需的原版 Jar 添加进去
            result.Add(new McLibToken
            {
                LocalPath = RealInstance.PathInstance + RealInstance.Name + ".jar", Size = 0L, IsNatives = false,
                Url = ClientUrl, SHA1 = ClientSHA1
            });
        }

        return result;
    }

    /// <summary>
    ///     获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
    public static List<McLibToken> McLibListGetWithJson(JObject JsonObject,
        bool KeepSameNameDifferentVersionResult = false, string CustomMcFolder = null, McInstance TargetInstance = null)
    {
        CustomMcFolder = CustomMcFolder ?? McFolderSelected;
        var BasicArray = new List<McLibToken>();

        // 添加基础 Json 项
        var AllLibs = (JArray)JsonObject["libraries"];

        // 转换为 LibToken
        foreach (JObject Library in AllLibs.Children())
        {
            // 清理 null 项（BakaXL 会把没有的项序列化为 null，但会被 Newtonsoft 转换为 JValue，导致 Is Nothing = false；这导致了 #409）
            for (var i = Library.Properties().Count() - 1; i >= 0; i -= 1)
                if (Library.Properties().ElementAtOrDefault(i).Value.Type == JTokenType.Null)
                    Library.Remove(Library.Properties().ElementAtOrDefault(i).Name);

            // 检查是否需要（Rules）
            if (!McJsonRuleCheck(Library["rules"]))
                continue;

            // 获取根节点下的 url
            var RootUrl = (string)Library["url"];
            if (RootUrl is not null)
                RootUrl += McLibGet((string)Library["name"], false, true, CustomMcFolder).Replace(@"\", "/");

            // 是否为纯本地项
            var Hint = (string)Library["hint"];
            var IsLocal = Hint is not null ? Hint == "local" : false;

            // 根据是否本地化处理（Natives）
            if (Library["natives"] is null) // 没有 Natives
            {
                string LocalPath;
                if (IsLocal && TargetInstance is not null) // 纯本地项
                    LocalPath = TargetInstance.PathInstance + @"libraries\" +
                                Library["name"].ToString().AfterFirst(":").Replace(":", "-") + ".jar";
                else
                    LocalPath = McLibGet((string)Library["name"], customMcFolder: CustomMcFolder);
                try
                {
                    if (Library["downloads"] is not null && Library["downloads"]["artifact"] is not null)
                    {
                        var init = new McLibToken();
                        BasicArray.Add((init.OriginalName = (string)Library["name"],
                            init.Url = (string)(RootUrl ?? Library["downloads"]["artifact"]["url"]),
                            init.LocalPath = Library["downloads"]["artifact"]["path"] is null
                                ? McLibGet((string)Library["name"], customMcFolder: CustomMcFolder)
                                : CustomMcFolder + @"libraries\" + Library["downloads"]["artifact"]["path"].ToString()
                                    .Replace("/", @"\"),
                            init.Size = (long)Math.Round(
                                ModBase.Val(Library["downloads"]["artifact"]["size"].ToString())),
                            init.IsNatives = false, init.SHA1 = Library["downloads"]["artifact"]["sha1"]?.ToString(),
                            init.IsLocal = IsLocal, init).init);
                    }
                    else
                    {
                        BasicArray.Add(new McLibToken
                        {
                            OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = LocalPath, Size = 0L,
                            IsNatives = false, SHA1 = null, IsLocal = IsLocal
                        });
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（无 Natives，" + (Library["name"] ?? "Nothing") + "）");
                    BasicArray.Add(new McLibToken
                    {
                        OriginalName = (string)Library["name"], Url = RootUrl, LocalPath = LocalPath, Size = 0L,
                        IsNatives = false, SHA1 = null
                    });
                }
            }
            else if (Library["natives"]["windows"] is not null) // 有 Windows Natives
            {
                try
                {
                    if (Library["downloads"] is not null && Library["downloads"]["classifiers"] is not null &&
                        Library["downloads"]["classifiers"]["natives-windows"] is not null)
                        BasicArray.Add(new McLibToken
                        {
                            OriginalName = (string)Library["name"],
                            Url = (string)(RootUrl ?? Library["downloads"]["classifiers"]["natives-windows"]["url"]),
                            LocalPath = Library["downloads"]["classifiers"]["natives-windows"]["path"] is null
                                ? McLibGet((string)Library["name"], customMcFolder: CustomMcFolder)
                                    .Replace(".jar", "-" + Library["natives"]["windows"] + ".jar")
                                    .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32")
                                : CustomMcFolder + @"libraries\" +
                                  Library["downloads"]["classifiers"]["natives-windows"]["path"].ToString()
                                      .Replace("/", @"\"),
                            Size = (long)Math.Round(
                                ModBase.Val(Library["downloads"]["classifiers"]["natives-windows"]["size"].ToString())),
                            IsNatives = true,
                            SHA1 = Library["downloads"]["classifiers"]["natives-windows"]["sha1"].ToString(),
                            IsLocal = IsLocal
                        });
                    else
                        BasicArray.Add(new McLibToken
                        {
                            OriginalName = (string)Library["name"], Url = RootUrl,
                            LocalPath = McLibGet((string)Library["name"], customMcFolder: CustomMcFolder)
                                .Replace(".jar", "-" + Library["natives"]["windows"] + ".jar")
                                .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                            Size = 0L, IsNatives = true, SHA1 = null, IsLocal = IsLocal
                        });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（有 Natives，" + (Library["name"] ?? "Nothing") + "）");
                    BasicArray.Add(new McLibToken
                    {
                        OriginalName = (string)Library["name"], Url = RootUrl,
                        LocalPath = McLibGet((string)Library["name"], customMcFolder: CustomMcFolder)
                            .Replace(".jar", "-" + Library["natives"]["windows"] + ".jar")
                            .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                        Size = 0L, IsNatives = true, SHA1 = null, IsLocal = false
                    });
                }
            }
        }

        // 去重
        var ResultArray = new Dictionary<string, McLibToken>();

        // 测试例：
        // D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
        // D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
        // D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
        string GetVersion(McLibToken Token)
        {
            return ModBase.GetFolderNameFromPath(ModBase.GetPathFromFullPath(Token.LocalPath));
        }

        for (int i = 0, loopTo = BasicArray.Count - 1; i <= loopTo; i++)
        {
            var Key = BasicArray[i].Name + BasicArray[i].IsNatives;
            if (ResultArray.ContainsKey(Key))
            {
                var BasicArrayVersion = GetVersion(BasicArray[i]);
                var ResultArrayVersion = GetVersion(ResultArray[Key]);
                if ((BasicArrayVersion ?? "") != (ResultArrayVersion ?? "") && KeepSameNameDifferentVersionResult)
                {
                    ModBase.Log(
                        $"[Minecraft] 发现疑似重复的支持库：{BasicArray[i]} ({BasicArrayVersion}) 与 {ResultArray[Key]} ({ResultArrayVersion})");
                    ResultArray.Add(Key + ModBase.GetUuid(), BasicArray[i]);
                }
                else
                {
                    ModBase.Log(
                        $"[Minecraft] 发现重复的支持库：{BasicArray[i]} ({BasicArrayVersion}) 与 {ResultArray[Key]} ({ResultArrayVersion})，已忽略其中之一");
                    if (CompareVersionGe(BasicArrayVersion, ResultArrayVersion)) ResultArray[Key] = BasicArray[i];
                }
            }
            else
            {
                ResultArray.Add(Key, BasicArray[i]);
            }
        }

        return ResultArray.Values.ToList();
    }

    /// <summary>
    ///     获取实例所需支持库文件的 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromInstance(McInstance instance)
    {
        if (!instance.IsLoaded)
            instance.Load();
        var result = new List<DownloadFile>();

        // 更新此方法时需要同步更新 Forge 新版自动安装方法！

        // 主 Jar 文件
        try
        {
            var mainJar = ModDownload.DlClientJarGet(instance, true);
            if (mainJar is not null)
                result.Add(mainJar);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "实例缺失主 Jar 文件所必须的信息", ModBase.LogLevel.Developer);
        }

        // Library 文件
        result.AddRange(McLibNetFilesFromTokens(McLibListGet(instance, false)));

        // Authlib-Injector 文件
        var authlibTargetFile = ModBase.PathPure + @"\authlib-injector.jar";
        JObject authlibDownloadInfo = null;
        try
        {
            ModBase.Log("[Minecraft] 开始获取 Authlib-Injector 下载信息");
            authlibDownloadInfo = (JObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(
                new[]
                {
                    "https://authlib-injector.yushi.moe/artifact/latest.json",
                    "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                }, IsJson: true));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 Authlib-Injector 下载信息失败");
        }

        // 校验文件
        if (authlibDownloadInfo is not null)
        {
            var checker = new ModBase.FileChecker(Hash: authlibDownloadInfo["checksums"]["sha256"].ToString());
            if (checker.Check(authlibTargetFile) is not null)
            {
                // 开始下载
                var downloadAddress = authlibDownloadInfo["download_url"].ToString()
                    .Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe");
                ModBase.Log("[Minecraft] Authlib-Injector 需要更新：" + downloadAddress, ModBase.LogLevel.Developer);
                result.Add(new DownloadFile(
                    new[]
                    {
                        downloadAddress,
                        downloadAddress.Replace("authlib-injector.yushi.moe",
                            "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                    }, authlibTargetFile,
                    new ModBase.FileChecker(Hash: authlibDownloadInfo["checksums"]["sha256"].ToString())));
            }
        }

        // 修改渲染器
        var mesaLoaderWindowsVersion = "25.3.5";
        var mesaLoaderWindowsTargetFile =
            ModBase.PathPure + @"\mesa-loader-windows\" + mesaLoaderWindowsVersion + @"\Loader.jar";
        var renderer = -1;
        if (McInstanceSelected is not null)
            renderer = Conversions.ToInteger(
                Operators.SubtractObject(ModBase.Setup.Get("VersionAdvanceRenderer", McInstanceSelected), 1));
        if (renderer == -1) renderer = Conversions.ToInteger(Config.Launch.Renderer);

        if (renderer != 0 && !File.Exists(mesaLoaderWindowsTargetFile))
        {
            var downloadAddress =
                "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/org/glavo/mesa-loader-windows/" +
                mesaLoaderWindowsVersion + "/mesa-loader-windows-" + mesaLoaderWindowsVersion + "-" +
                (ModBase.Is32BitSystem ? "x86" : ModBase.IsArm64System ? "arm64" : "x64") + ".jar";
            result.Add(new DownloadFile(new[] { downloadAddress }, mesaLoaderWindowsTargetFile));
        }

        // LabyMod Assets 文件
        if (instance.Info.HasLabyMod)
        {
            if ((instance.PathIndie ?? "") == (instance.PathInstance ?? ""))
            {
                if (Directory.Exists(instance.PathInstance + "labymod-neo"))
                    Directory.Delete(instance.PathInstance + "labymod-neo", true);
                ModBase.CreateSymbolicLink(instance.PathInstance + "labymod-neo", McFolderSelected + "labymod-neo",
                    0x2);
            }

            try
            {
                var channelType = instance.JsonObject["labymod_data"]["channelType"].ToString();
                Directory.CreateDirectory($@"{McFolderSelected}labymod-neo\libraries");
                ModBase.Log("[Minecraft] 开始获取 LabyMod 信息");
                var labyManifest = (JObject)ModNet.NetGetCodeByRequestRetry(
                    $"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json", IsJson: true);
                var labyAssets = (JObject)labyManifest["assets"];
                var labyModCommitRef = labyManifest["commitReference"].ToString();
                foreach (var Asset in labyAssets)
                {
                    var assetName = Asset.Key;
                    var assetSHA1 = Asset.Value.ToString();
                    var assetPath = $@"{McFolderSelected}labymod-neo\assets\{assetName}.jar";
                    var assetUrl =
                        $"https://releases.r2.labymod.net/api/v1/download/assets/labymod4/{channelType}/{labyModCommitRef}/{assetName}/{assetSHA1}.jar";
                    var checker = new ModBase.FileChecker(Hash: assetSHA1);
                    if (checker.Check(assetPath) is null)
                        continue;
                    result.Add(new DownloadFile(new[] { assetUrl }, assetPath, checker));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 LabyMod 信息失败，跳过检查");
            }
        }

        // 跳过校验
        if (Conversions.ToBoolean(ShouldIgnoreFileCheck(instance)))
        {
            ModBase.Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件");
            result = result.Where(f =>
            {
                if (File.Exists(f.LocalPath))
                {
                    ModBase.Log("[Minecraft] 跳过下载的支持库文件：" + f.LocalPath, ModBase.LogLevel.Debug);
                    return false;
                }

                return true;
            }).ToList();
        }

        return result;
    }

    /// <summary>
    ///     将 McLibToken 列表转换为 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromTokens(List<McLibToken> libs, string customMcFolder = null)
    {
        customMcFolder = customMcFolder ?? McFolderSelected;
        var result = new List<DownloadFile>();
        // 获取
        foreach (var token in libs)
        {
            // 检查文件
            var checker = new ModBase.FileChecker(ActualSize: token.Size == 0L ? -1 : token.Size, Hash: token.SHA1);
            if (checker.Check(token.LocalPath) is null)
                continue;
            if (token.IsLocal)
            {
                ModBase.Log("[Download] 已跳过被标记为本地文件的支持库: " + token.OriginalName);
                continue;
            }

            // URL
            var urls = new List<string>();
            if (token.Url is null && token.Name == "net.minecraftforge:forge:universal")
                // 特判修复 Forge 部分 universal 文件缺失 URL（#5455）
                token.Url = "https://maven.minecraftforge.net" +
                            token.LocalPath.Replace(customMcFolder + "libraries", "").Replace(@"\", "/");
            if (token.Url is not null)
            {
                // 获取 URL 的真实地址
                urls.Add(token.Url);
                if (token.Url.Contains("launcher.mojang.com/v1/objects") || token.Url.Contains("client.txt") ||
                    token.Url.Contains(".tsrg"))
                    urls.AddRange(ModDownload.DlSourceLauncherOrMetaGet(token.Url)); // Mappings（#4425）
                if (token.Url.Contains("maven"))
                {
                    var bmclapiUrl = token.Url
                        .Replace(Strings.Mid(token.Url, 1, token.Url.IndexOfF("maven")),
                            "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven")
                        .Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven");
                    if (ModDownload.DlSourcePreferMojang)
                        urls.Add(bmclapiUrl); // 官方源优先
                    else
                        urls.Insert(0, bmclapiUrl); // 镜像源优先
                }
            }

            if (token.LocalPath.Contains("transformer-discovery-service"))
            {
                // Transformer 文件释放
                if (!File.Exists(token.LocalPath))
                    ModBase.WriteFile(token.LocalPath, ModBase.GetResourceStream("Resources/transformer.jar"));
                ModBase.Log("[Download] 已自动释放 Transformer Discovery Service", ModBase.LogLevel.Developer);
                continue;
            }

            if (token.LocalPath.Contains(@"optifine\OptiFine"))
            {
                // OptiFine 主 Jar
                var optiFineBase =
                    token.LocalPath.Replace(customMcFolder + @"libraries\optifine\OptiFine\", "").Split("_")[0] + "/" +
                    ModBase.GetFileNameFromPath(token.LocalPath).Replace("-", "_");
                optiFineBase = "/maven/com/optifine/" + optiFineBase;
                if (optiFineBase.Contains("_pre"))
                    optiFineBase = optiFineBase.Replace("com/optifine/", "com/optifine/preview_");
                urls.Add("https://bmclapi2.bangbang93.com" + optiFineBase);
            }
            else if (token.Name.Contains("LabyMod"))
            {
                // LabyMod 只有一个下载源
                urls.Add(token.Url);
                ModBase.Log(
                    $"[Download] 获取到 LabyMod 主要库文件的 Size = {token.Size},SHA1 = {token.SHA1}，由于 LabyMod 乱写 Size，已忽略 Size");
                checker = new ModBase.FileChecker(Hash: token.SHA1); // 只校验 SHA1
            }
            else if (urls.Count <= 2)
            {
                // 普通文件
                urls.AddRange(ModDownload.DlSourceLibraryGet("https://libraries.minecraft.net" +
                                                             token.LocalPath.Replace(customMcFolder + "libraries", "")
                                                                 .Replace(@"\", "/")));
            }

            result.Add(new DownloadFile(urls.Distinct(), token.LocalPath, checker));
        }

        // 去重并返回
        return result.Distinct((a, b) => (a.LocalPath ?? "") == (b.LocalPath ?? ""));
    }

    /// <summary>
    ///     获取对应的支持库文件地址。
    /// </summary>
    /// <param name="original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    /// <param name="withHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
    public static string McLibGet(string original, bool withHead = true, bool ignoreLiteLoader = false,
        string customMcFolder = null)
    {
        string McLibGetRet = default;
        customMcFolder = customMcFolder ?? McFolderSelected;
        var splited = original.Split(":");
        McLibGetRet = (withHead ? customMcFolder + @"libraries\" : "") + splited[0].Replace(".", @"\") + @"\" +
                      splited[1] + @"\" + splited[2] + @"\" + splited[1] + "-" + splited[2] + ".jar";
        // 判断 OptiFine 是否应该使用 installer
        if (McLibGetRet.Contains(@"optifine\OptiFine\1.") && splited[2].Split(".").Count() > 1)
        {
            var majorVersion = (int)Math.Round(ModBase.Val(splited[2].Split(".")[1].BeforeFirst("_")));
            var minorVersion = (int)Math.Round(splited[2].Split(".").Count() > 2
                ? ModBase.Val(splited[2].Split(".")[2].BeforeFirst("_"))
                : 0d);
            if ((majorVersion == 12 || (majorVersion == 20 && minorVersion >= 4) || majorVersion >= 21) && File.Exists(
                    $@"{customMcFolder}libraries\{splited[0].Replace(".", @"\")}\{splited[1]}\{splited[2]}\{splited[1]}-{splited[2]}-installer.jar")) // 仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
            {
                ModLaunch.McLaunchLog("已将 " + original + " 替换为对应的 Installer 文件");
                McLibGetRet = McLibGetRet.Replace(".jar", "-installer.jar");
            }
        }

        return McLibGetRet;
    }

    /// <summary>
    ///     检查设置，是否应当忽略文件检查？
    /// </summary>
    public static object ShouldIgnoreFileCheck(McInstance Version)
    {
        return (bool)ModBase.Setup.Get("VersionAdvanceAssetsV2", Version) ||
               Operators.ConditionalCompareObjectEqual(ModBase.Setup.Get("VersionAdvanceAssets", Version), 2, false);
    }

    #endregion

    #region 资源文件（Assets）

    // 获取索引
    /// <summary>
    ///     获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
    /// </summary>
    public static JToken McAssetsGetIndex(McInstance instance, bool returnLegacyOnError = false,
        bool checkURLEmpty = false)
    {
        string assetsName;
        try
        {
            while (true)
            {
                var index = instance.JsonObject["assetIndex"];
                if (index is not null && index["id"] is not null)
                    return index;
                if (instance.JsonObject["assets"] is not null)
                    assetsName = instance.JsonObject["assets"].ToString();
                if (checkURLEmpty && index["url"] is not null)
                    return index;
                // 下一个实例
                if (string.IsNullOrEmpty(instance.InheritInstanceName))
                    break;
                instance = new McInstance(McFolderSelected + @"versions\" + instance.InheritInstanceName);
            }
        }
        catch
        {
        }

        // 无法获取到下载地址
        if (returnLegacyOnError)
        {
            // 返回 assets 文件名会由于没有下载地址导致全局失败
            // If AssetsName IsNot Nothing AndAlso AssetsName <> "legacy" Then
            // Log("[Minecraft] 无法获取资源文件索引下载地址，使用 assets 项提供的资源文件名：" & AssetsName)
            // Return GetJson("{""id"": """ & AssetsName & """}")
            // Else
            ModBase.Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址");
            return (JToken)ModBase.GetJson(@"{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }");
        }
        // End If

        throw new Exception("该实例不存在资源文件索引信息");
    }

    /// <summary>
    ///     获取某实例资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    /// </summary>
    public static string McAssetsGetIndexName(McInstance instance)
    {
        try
        {
            while (true)
            {
                if (instance.JsonObject["assetIndex"] is not null &&
                    instance.JsonObject["assetIndex"]["id"] is not null)
                    return instance.JsonObject["assetIndex"]["id"].ToString();
                if (instance.JsonObject["assets"] is not null) return instance.JsonObject["assets"].ToString();
                if (string.IsNullOrEmpty(instance.InheritInstanceName))
                    break;
                instance = new McInstance(McFolderSelected + @"versions\" + instance.InheritInstanceName);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取资源文件索引名失败");
        }

        return "legacy";
    }

    // 获取列表
    private struct McAssetsToken
    {
        /// <summary>
        ///     文件的完整本地路径。
        /// </summary>
        public string LocalPath;

        /// <summary>
        ///     Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        /// </summary>
        public string SourcePath;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long Size;

        /// <summary>
        ///     文件的 Hash 校验码。
        /// </summary>
        public string Hash;

        public override string ToString()
        {
            return ModBase.GetString(Size) + " | " + LocalPath;
        }
    }

    /// <summary>
    ///     获取 Minecraft 的资源文件列表。失败会抛出异常。
    /// </summary>
    private static List<McAssetsToken> McAssetsListGet(McInstance instance)
    {
        var indexName = McAssetsGetIndexName(instance);
        try
        {
            // 初始化
            if (!File.Exists($@"{McFolderSelected}assets\indexes\{indexName}.json"))
                throw new FileNotFoundException("未找到 Asset Index",
                    McFolderSelected + @"assets\indexes\" + indexName + ".json");
            var result = new List<McAssetsToken>();
            var json = (JsonObject)JsonNode.Parse(
                ModBase.ReadFile($@"{McFolderSelected}assets\indexes\{indexName}.json"));

            // 读取列表
            foreach (var file in json["objects"].AsObject())
            {
                string localPath;
                if (json["map_to_resources"] is not null && json["map_to_resources"].GetValue<bool>())
                    // Remap
                    localPath = instance.PathIndie + @"resources\" + file.Key.Replace("/", @"\");
                else if (json["virtual"] is not null && json["virtual"].GetValue<bool>())
                    // Virtual
                    localPath = McFolderSelected + @"assets\virtual\legacy\" + file.Key.Replace("/", @"\");
                else
                    // 正常
                    localPath = McFolderSelected + @"assets\objects\" + Strings.Left(file.Value["hash"].ToString(), 2) +
                                @"\" + file.Value["hash"];
                result.Add(new McAssetsToken
                {
                    LocalPath = localPath,
                    SourcePath = file.Key,
                    Hash = file.Value["hash"].ToString(),
                    Size = Conversions.ToLong(file.Value["size"].ToString())
                });
            }

            return result;
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "获取资源文件列表失败：" + indexName);
            throw;
        }
    }

    // 获取缺失列表
    /// <summary>
    ///     获取实例缺失的资源文件所对应的 NetTaskFile。
    /// </summary>
    public static List<DownloadFile> McAssetsFixList(McInstance instance, bool checkHash,
        [Optional] ref ModLoader.LoaderBase progressFeed)
    {
        // 如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
        if (checkHash)
            return McAssetsListGet(instance).Select(token => new DownloadFile(
                ModDownload.DlSourceAssetsGet(
                    $"https://resources.download.minecraft.net/{Strings.Left(token.Hash, 2)}/{token.Hash}"),
                token.LocalPath,
                new ModBase.FileChecker(ActualSize: token.Size == 0L ? -1 : token.Size, Hash: token.Hash))).ToList();
        // 如果不检查 Hash，则立即处理
        var result = new List<DownloadFile>();

        List<McAssetsToken> assetsList;
        try
        {
            assetsList = McAssetsListGet(instance);
            McAssetsToken token;
            if (progressFeed is not null)
                progressFeed.Progress = 0.04d;
            for (int i = 0, loopTo = assetsList.Count - 1; i <= loopTo; i++)
            {
                // 初始化
                token = assetsList[i];
                if (progressFeed is not null)
                    progressFeed.Progress = 0.05d + 0.94d * i / assetsList.Count;
                // 检查文件是否存在
                var file = new FileInfo(token.LocalPath);
                if (file.Exists && (token.Size == 0L || token.Size == file.Length))
                    continue;
                // 文件不存在，添加下载
                result.Add(new DownloadFile(
                    ModDownload.DlSourceAssetsGet(
                        $"https://resources.download.minecraft.net/{Strings.Left(token.Hash, 2)}/{token.Hash}"),
                    token.LocalPath,
                    new ModBase.FileChecker(ActualSize: token.Size == 0L ? -1 : token.Size, Hash: token.Hash)));
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取实例缺失的资源文件下载列表失败");
        }

        if (progressFeed is not null)
            progressFeed.Progress = 0.99d;
        return result;
    }

    #endregion
}
