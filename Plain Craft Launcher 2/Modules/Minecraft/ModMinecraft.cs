using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Network;

namespace PCL;

public static class ModMinecraft
{
    public const string UNKNOWN_VERSION_KEY = "UnknownVersion";

    /// <summary>
    ///     发送 Minecraft 更新提示。
    /// </summary>
    public static void McDownloadClientUpdateHint(string versionName, JsonObject json)
    {
        try
        {
            // 获取对应版本
            JsonNode version = null;
            foreach (var Token in json["versions"].AsArray())
                if (Token["id"] is not null && (Token["id"].ToString() ?? "") == (versionName ?? ""))
                {
                    version = Token;
                    break;
                }

            // 进行提示
            if (version is null)
                return;
            var time = version["releaseTime"].ToObject<DateTime>();
            var msgBoxText = Lang.Text("Minecraft.Update.NewVersion", versionName) + "\r\n" +
                             ((DateTime.Now - time).TotalDays > 1d
                                 ? Lang.Text("Minecraft.Update.UpdateTime") + Lang.Date(time)
                                 : Lang.Text("Minecraft.Update.UpdatedAt") + Lang.TimeSpan(time - DateTime.Now));
            var msgResult = ModMain.MyMsgBox(msgBoxText, Lang.Text("Minecraft.Update.Title"),
                Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Download"),
                (DateTime.Now - time).TotalHours > 3d ? Lang.Text("Common.Action.UpdateLog") : "",
                button3Action: () => ModDownloadLib.McUpdateLogShow(version));
            // 弹窗结果
            if (msgResult == 2)
                // 下载
                ModBase.RunInUi(() =>
                {
                    PageDownloadInstall.mcVersionWaitingForSelect = versionName;
                    ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
                });
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Minecraft.Error.UpdateNotify", versionName ?? "Nothing"), ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     比较两个版本名；等同 Left >= Right。
    ///     无法比较两个预发布版的大小。
    ///     支持的格式：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
    public static bool CompareVersionGe(string left, string right)
    {
        return CompareVersion(left, right) >= 0;
    }

    /// <summary>
    ///     比较两个版本名，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1；等同 Left - Right。
    ///     无法比较两个预发布版的大小。
    ///     支持的格式：未知版本, 26.1-snapshot-1，1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
    public static int CompareVersion(string left, string right)
    {
        if (left == Lang.Text("Minecraft.Version.Unknown") || right == Lang.Text("Minecraft.Version.Unknown"))
        {
            if (left == Lang.Text("Minecraft.Version.Unknown") && right != Lang.Text("Minecraft.Version.Unknown"))
                return 1;
            if (left == Lang.Text("Minecraft.Version.Unknown") && right == Lang.Text("Minecraft.Version.Unknown"))
                return 0;
            if (left != Lang.Text("Minecraft.Version.Unknown") && right == Lang.Text("Minecraft.Version.Unknown"))
                return -1;
        }

        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();
        var lefts = left.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
        var rights = right.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
        var i = 0;
        while (true)
        {
            // 两边均缺失，感觉是一个东西
            if (lefts.Count - 1 < i && rights.Count - 1 < i)
            {
                if (string.CompareOrdinal(left, right) > 0)
                    return 1;
                if (string.CompareOrdinal(left, right) < 0)
                    return -1;
                return 0;
            }

            // 确定两边的数值
            var leftValue = lefts.Count - 1 < i ? "0" : lefts[i];
            var rightValue = rights.Count - 1 < i ? "0" : rights[i];
            if ((leftValue ?? "") == (rightValue ?? ""))
                goto NextEntry;
            if (leftValue == "rc")
                leftValue = (-1).ToString();
            if (leftValue == "pre")
                leftValue = (-2).ToString();
            if (leftValue == "snapshot")
                leftValue = (-3).ToString();
            if (leftValue == "experimental")
                leftValue = (-4).ToString();
            var leftValValue = ModBase.Val(leftValue);
            if (rightValue == "rc")
                rightValue = (-1).ToString();
            if (rightValue == "pre")
                rightValue = (-2).ToString();
            if (rightValue == "snapshot")
                rightValue = (-3).ToString();
            if (rightValue == "experimental")
                rightValue = (-4).ToString();
            var rightValValue = ModBase.Val(rightValue);
            if (leftValValue == 0d && rightValValue == 0d)
            {
                // 如果没有数值则直接比较字符串
                if (string.CompareOrdinal(leftValue, rightValue) > 0) return 1;

                if (string.CompareOrdinal(leftValue, rightValue) < 0) return -1;
            }
            // 如果有数值则比较数值
            // 这会使得一边是数字一边是字母时数字方更大
            else if (leftValValue > rightValValue)
            {
                return 1;
            }
            else if (leftValValue < rightValValue)
            {
                return -1;
            }

            NextEntry: ;

            i += 1;
        }

        return 0;
    }

    /// <summary>
    ///     打码字符串中的 AccessToken。
    /// </summary>
    public static string FilterAccessToken(string raw, char filterChar)
    {
        // 打码 "accessToken " 后的内容
        if (raw.Contains("accessToken "))
            foreach (var Token in raw.RegexSearch("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})"))
                raw = raw.Replace(Token, new string(filterChar, Token.Count()));
        // 打码当前登录的结果
        var accessToken = ModLaunch.mcLoginLoader.output.AccessToken;
        if (accessToken is not null && accessToken.Length >= 10 && raw.ContainsF(accessToken, true) &&
            (ModLaunch.mcLoginLoader.output.Uuid ?? "") !=
            (ModLaunch.mcLoginLoader.output.AccessToken ?? "")) // UUID 和 AccessToken 一样则不打码
            raw = raw.Replace(accessToken, accessToken[..5] + new string(filterChar, accessToken.Length - 10) +
                                           accessToken[^5..]);
        return raw;
    }

    /// <summary>
    ///     打码字符串中的 Windows 用户名。
    /// </summary>
    public static string FilterUserName(string raw, char filterChar)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userName = userProfile.Split(@"\").Last();
        var maskedProfile = userProfile.Replace(userName, new string(filterChar, userName.Length));
        return raw.Replace(userProfile, maskedProfile);
    }

    /// <summary>
    ///     比较两个版本名的排序器。
    /// </summary>
    public class VersionComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return CompareVersion(x, y);
        }
    }

    #region 文件夹

    /// <summary>
    ///     当前的 Minecraft 文件夹路径，以“\”结尾。
    /// </summary>
    public static string mcFolderSelected;

    /// <summary>
    ///     当前的 Minecraft 文件夹列表。
    /// </summary>
    public static List<McFolder> mcFolderList = new();

    public class McFolder // 必须是 Class，否则不是引用类型，在 ForEach 中不会得到刷新
    {
        public enum Types
        {
            Original,
            RenamedOriginal,
            Custom
        }

        /// <summary>
        ///     文件夹路径。
        ///     以 \ 结尾，例如 "D:\Game\MC\.minecraft\"。
        /// </summary>
        public string Location;

        public string Name;
        public Types type;

        public override bool Equals(object obj)
        {
            if (obj is not McFolder)
                return false;
            var folder = (McFolder)obj;
            return (Name ?? "") == (folder.Name ?? "") && (Location ?? "") == (folder.Location ?? "") &&
                   type == folder.type;
        }

        public override string ToString()
        {
            return Location;
        }
    }

    /// <summary>
    ///     加载 Minecraft 文件夹列表。
    /// </summary>
    public static ModLoader.LoaderTask<int, int> mcFolderListLoader = new("Minecraft Folder List",
        _ => McFolderListLoadSub(), priority: ThreadPriority.AboveNormal);

    private static void McFolderListLoadSub()
    {
        try
        {
            // 初始化
            var cacheMcFolderList = new List<McFolder>();

            #region 读取自定义（Custom）文件夹，可能没有结果

            // 格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
            foreach (string folder in (IEnumerable)((dynamic)States.Game.Folders).Split("|"))
            {
                if (string.IsNullOrEmpty(folder))
                    continue;
                if (!folder.Contains(">") || !folder.EndsWithF(@"\"))
                {
                    ModMain.Hint(Lang.Text("Select.Folder.Invalid", folder), ModMain.HintType.Critical);
                    continue;
                }

                var name = folder.Split(">")[0];
                var path = folder.Split(">")[1];
                try
                {
                    ModBase.CheckPermissionWithException(path);
                    cacheMcFolderList.Add(new McFolder { Name = name, Location = path, type = McFolder.Types.Custom });
                }
                catch (Exception ex)
                {
                    ModMain.MyMsgBox(
                        Lang.Text("Select.Folder.Invalid", path) + "\r\n" + "\r\n" +
                        ex.Message, Lang.Text("Select.Folder.InvalidTitle"), isWarn: true);
                    ModBase.Log(ex, $"无法访问 Minecraft 文件夹 {path}");
                }
            }

            #endregion

            #region 读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果

            var currentMcFolderList = new List<McFolder>();
            var originalMcFolderList = new List<McFolder>();
            // 扫描当前文件夹
            try
            {
                if (Directory.Exists(ModBase.exePath + @"versions\"))
                    originalMcFolderList.Add(new McFolder
                        { Name = Lang.Text("Select.Folder.CurrentFolder"), Location = ModBase.exePath, type = McFolder.Types.Original });
                foreach (var folder in new DirectoryInfo(ModBase.exePath).GetDirectories())
                    if (Directory.Exists(Path.Combine(folder.FullName, "versions")) || folder.Name == ".minecraft")
                    {
                        var newCurrentFolder = new McFolder
                            { Name = folder.Name, Location = folder.FullName + @"\", type = McFolder.Types.Original };
                        originalMcFolderList.Add(newCurrentFolder);
                        currentMcFolderList.Add(newCurrentFolder);
                    }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败");
            }

            // 扫描官启文件夹
            var mojangPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft") + @"\";
            if ((!currentMcFolderList.Any() || (mojangPath ?? "") != (currentMcFolderList[0].Location ?? "")) &&
                Directory.Exists(Path.Combine(mojangPath, "versions"))) // 当前文件夹不是官启文件夹
                // 具有权限且存在 versions 文件夹
                originalMcFolderList.Add(new McFolder
                    { Name = Lang.Text("Select.Folder.OfficialLauncherFolder"), Location = mojangPath, type = McFolder.Types.Original });

            ModBase.Log(cacheMcFolderList.Count + " 个自定义文件夹，" + originalMcFolderList.Count + " 个原始文件夹");

            var unAdded = false;
            foreach (var newOriginalFolder in originalMcFolderList)
            {
                foreach (var cacheFolder in cacheMcFolderList)
                    if ((cacheFolder.Location ?? "") == (newOriginalFolder.Location ?? ""))
                    {
                        if ((cacheFolder.Name ?? "") != (newOriginalFolder.Name ?? ""))
                            cacheFolder.type = McFolder.Types.RenamedOriginal;
                        else
                            cacheFolder.type = McFolder.Types.Original;
                        unAdded = true;
                    }

                if (!unAdded)
                    cacheMcFolderList.Add(newOriginalFolder); // 如果没有重命名，则添加当前文件夹
            }

            #endregion

            #region 读取自定义文件夹情况并写入设置

            // 将自定义文件夹情况同步到设置
            var config = new List<string>();
            foreach (var Folder in cacheMcFolderList)
                config.Add(Folder.Name + ">" + Folder.Location);
            if (!config.Any())
                config.Add(""); // 防止 0 元素 Join 返回 Nothing
            States.Game.Folders = config.Join("|");

            #endregion

            // 若没有可用文件夹，则创建 .minecraft
            if (!cacheMcFolderList.Any())
            {
                Directory.CreateDirectory(ModBase.exePath + @".minecraft\versions\");
                cacheMcFolderList.Add(new McFolder
                    { Name = Lang.Text("Select.Folder.CurrentFolder"), Location = ModBase.exePath + @".minecraft\", type = McFolder.Types.Original });
            }

            foreach (var Folder in cacheMcFolderList) McFolderLauncherProfilesJsonCreate(Folder.Location);
            if (Config.Debug.AddRandomDelay)
                Thread.Sleep(RandomUtils.NextInt(200, 2000));

            // 回设
            mcFolderList = cacheMcFolderList;
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Folder.Error.Load"), ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    /// </summary>
    public static void McFolderLauncherProfilesJsonCreate(string folder)
    {
        try
        {
            if (File.Exists(Path.Combine(folder, "launcher_profiles.json")))
                return;
            var now = DateTime.Now;
            var resultJson = @"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ + now.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture) + "T" +
                             now.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture) + @".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}";
            ModBase.WriteFile(Path.Combine(folder, "launcher_profiles.json"), resultJson, encoding: Encoding.GetEncoding("GB18030"));
            ModBase.Log("[Minecraft] 已创建 launcher_profiles.json：" + folder);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "创建 launcher_profiles.json 失败（" + folder + "）", ModBase.LogLevel.Feedback);
        }
    }

    #endregion

    #region 实例处理

    public const int mcInstanceCacheVersion = 30;

    private static Instance _mcInstanceSelected;
    private static object _McInstanceSelected_mcInstanceSelectedLast = 0; // 为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化

    /// <summary>
    ///     当前的 Minecraft 版本。
    /// </summary>
    public static Instance McInstanceSelected
    {
        get => _mcInstanceSelected;
        set
        {
            if (ReferenceEquals(_McInstanceSelected_mcInstanceSelectedLast, value))
                return;
            _mcInstanceSelected = value; // 由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
            _McInstanceSelected_mcInstanceSelectedLast = value;
            if (value is null)
                return;
            // 重置缓存的 Mod 文件夹
            PageDownloadCompDetail.cachedFolder.Clear();
        }
    }

    private static bool _JsonVersion_jsonVersionInited;

    public class Instance
    {
        private McInstanceInfo _info;
        private string _inheritInstanceName;
        private JsonObject _jsonObject;
        private string _jsonText;
        private JsonObject _jsonVersion;
        private string _name;

        /// <summary>
        ///     显示的描述文本。
        /// </summary>
        public string Desc = Lang.Text("Select.Instance.Description.NotLoaded");

        /// <summary>
        ///     强制实例分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        /// </summary>
        public McInstanceCardType displayType = McInstanceCardType.Auto;

        public bool IsLoaded;

        /// <summary>
        ///     是否为收藏的实例。
        /// </summary>
        public bool IsStar;

        /// <summary>
        ///     显示的实例图标。
        /// </summary>
        public string Logo;

        /// <summary>
        ///     实例的发布时间。
        /// </summary>
        public DateTime releaseTime = new(1970, 1, 1, 15, 0, 0);

        /// <summary>
        ///     该实例的列表检查原始结果，不受自定义影响。
        /// </summary>
        public McInstanceState state = McInstanceState.Error;

        /// <summary></summary>
        /// <param name="name">实例名，或实例文件夹的完整路径（不规定是否以 \ 结尾）。</param>
        public Instance(string name)
        {
            PathInstance = (name.Contains(":") ? name : Path.Combine(mcFolderSelected, "versions", name)) + (name.EndsWithF(@"\") ? "" : @"\");
        }

        /// <summary>
        ///     该实例的实例文件夹，以“\”结尾。
        /// </summary>
        public string PathInstance { get; }

        /// <summary>
        ///     应用版本隔离后，该实例所对应的 Minecraft 根文件夹，以“\”结尾。
        /// </summary>
        public string PathIndie
        {
            get
            {
                if (Config.Instance.IndieV2Config.IsDefault(PathInstance))
                {
                    if (!IsLoaded)
                        Load();

                    // 决定该实例是否应该被隔离
                    bool ShouldBeIndie()
                    {
                        // 从老的实例独立设置中迁移：-1 未决定，0 使用全局设置，1 手动开启，2 手动关闭
                        if (!Config.Instance.IndieV1Config.IsDefault(PathInstance) && Config.Instance.IndieV1[PathInstance] > 0)
                        {
                            ModBase.Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的实例独立设置中迁移");
                            return Config.Instance.IndieV1[PathInstance] == 1;
                        }

                        // 若实例文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                        var modFolder = new DirectoryInfo(PathInstance + @"mods\");
                        var saveFolder = new DirectoryInfo(PathInstance + @"saves\");
                        if ((modFolder.Exists && modFolder.EnumerateFiles().Any()) ||
                            (saveFolder.Exists && saveFolder.EnumerateDirectories().Any()))
                        {
                            ModBase.Log($"[Minecraft] 版本隔离初始化（{Name}）：实例文件夹下存在 mods 或 saves 文件夹，自动开启");
                            return true;
                        }

                        // 根据全局的默认设置决定是否隔离
                        var isRelease = state != McInstanceState.Fool && state != McInstanceState.Old &&
                                        state != McInstanceState.Snapshot;
                        ModBase.Log(
                            $"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{Config.Launch.IndieSolutionV2}）判断，State {ModBase.GetStringFromEnum(state)}，IsRelease {isRelease}，Modable {Modable}");
                        
                        return Config.Launch.IndieSolutionV2 switch
                        {
                            0 => false, // 关闭
                            1 => Info.HasLabyMod || Modable, // 仅隔离可安装 Mod 的实例
                            2 => !isRelease, // 仅隔离非正式版
                            3 => Info.HasLabyMod || Modable || !isRelease, // 隔离非正式版与可安装 Mod 的实例
                            _ => true // 隔离所有实例
                        };
                    }
                    
                    Config.Instance.IndieV2[PathInstance] = ShouldBeIndie();
                }

                return Config.Instance.IndieV2[PathInstance] ? PathInstance : mcFolderSelected;
            }
        }

        /// <summary>
        ///     该实例的实例文件夹名称。
        /// </summary>
        public string Name
        {
            get
            {
                if (_name is null && !string.IsNullOrEmpty(PathInstance))
                    _name = ModBase.GetFolderNameFromPath(PathInstance);
                return _name;
            }
        }

        /// <summary>
        ///     该实例是否可以安装 Mod。
        /// </summary>
        public bool Modable
        {
            get
            {
                if (!IsLoaded)
                    Load();
                return Info.HasFabric || Info.HasLegacyFabric || Info.HasQuilt || Info.HasForge || Info.HasLiteLoader ||
                       Info.HasNeoForge || Info.HasCleanroom || displayType == McInstanceCardType.API; // #223
            }
        }

        /// <summary>
        ///     实例信息。
        /// </summary>
        public McInstanceInfo Info
        {
            get
            {
                if (_info is not null)
                    return _info;
                _info = new McInstanceInfo();

                #region 获取游戏版本

                try
                {
                    // 获取发布时间并判断是否为老版本
                    try
                    {
                        if (JsonObject["releaseTime"] is null)
                            releaseTime = new DateTime(1970, 1, 1, 15, 0, 0); // 未知版本也可能显示为 1970 年
                        else
                            releaseTime = JsonObject["releaseTime"].ToObject<DateTime>();
                        if (releaseTime.Year > 2000 && releaseTime.Year < 2013)
                        {
                            _info.VanillaName = "Old";
                            goto VersionSearchFinish;
                        }
                    }
                    catch
                    {
                        releaseTime = new DateTime(1970, 1, 1, 15, 0, 0);
                    }

                    // 实验性快照
                    if ((string)(JsonObject["type"] ?? "") == "pending")
                    {
                        _info.VanillaName = "pending";
                        goto VersionSearchFinish;
                    }

                    // 从 PCL 下载的版本信息中获取版本号
                    if (JsonObject["clientVersion"] is not null)
                    {
                        _info.VanillaName = (string)JsonObject["clientVersion"];
                        goto VersionSearchFinish;
                    }

                    // 从 HMCL 下载的版本信息中获取版本号
                    if (JsonObject["patches"] is not null)
                        foreach (var patchNode in JsonObject["patches"].AsArray()) { var patch = patchNode.AsObject();
                            if ((patch["id"] ?? "").ToString() == "game" && patch["version"] is not null)
                            {
                                _info.VanillaName = patch["version"].ToString();
                                goto VersionSearchFinish;
                            } }

                    // 从 Forge / NeoForge / LabyMod Arguments 中获取版本号
                    if (JsonObject["arguments"] is not null)
                    {
                        if (JsonObject["arguments"]["game"] is not null)
                        {
                            var mark = false;
                            foreach (var Argument in JsonObject["arguments"]["game"].AsArray())
                            {
                                if (mark)
                                {
                                    _info.VanillaName = Argument.ToString();
                                    goto VersionSearchFinish;
                                }

                                if (Argument.ToString() == "--fml.mcVersion")
                                    mark = true;
                            }
                        }

                        if (JsonObject["arguments"]["jvm"] is not null)
                            foreach (var Argument in JsonObject["arguments"]["jvm"].AsArray())
                            {
                                var regexArgument = Argument.ToString().RegexSeek(RegexPatterns.LabyModVersion);
                                if (regexArgument is not null)
                                {
                                    _info.VanillaName = regexArgument;
                                    goto VersionSearchFinish;
                                }
                            }
                    }

                    // 从继承实例中获取版本号
                    if (!string.IsNullOrEmpty(InheritInstanceName))
                    {
                        _info.VanillaName = (JsonObject["jar"] ?? "").ToString(); // LiteLoader 优先使用 Jar
                        if (string.IsNullOrEmpty(_info.VanillaName))
                            _info.VanillaName = InheritInstanceName;
                        goto VersionSearchFinish;
                    }

                    // 从下载地址中获取版本号
                    var regex = (JsonObject["downloads"] ?? "").ToString()
                        .RegexSeek(RegexPatterns.MinecraftDownloadUrlVersion);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 Forge 版本中获取版本号
                    var librariesString = JsonObject["libraries"].ToString();
                    regex = librariesString.RegexSeek(RegexPatterns.ForgeLibVersion);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 OptiFine 版本中获取版本号
                    regex = librariesString.RegexSeek(RegexPatterns.OptiFineLibVersion);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 Fabric / Quilt / Legacy Fabric 版本中获取版本号
                    regex = librariesString.RegexSeek(RegexPatterns.FabricLikeLibVersion);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 jar 项中获取版本号
                    if (JsonObject["jar"] is not null)
                    {
                        _info.VanillaName = JsonObject["jar"].ToString();
                        goto VersionSearchFinish;
                    }

                    // 从 jar 文件的 version.json 中获取版本号
                    if (JsonVersion?["name"] is not null)
                    {
                        var jsonVerName = JsonVersion["name"].ToString();
                        if (jsonVerName.Length < 32) // 因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                        {
                            _info.VanillaName = jsonVerName;
                            ModBase.Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" + jsonVerName);
                            goto VersionSearchFinish;
                        }
                    }

                    // 从 JSON 的 ID 中获取
                    regex = ((string)JsonObject["id"]).RegexSeek(RegexPatterns.MinecraftJsonVersion,
                        RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 非准确的版本判断警告
                    ModBase.Log("[Minecraft] 无法完全确认 MC 版本号的版本：" + Name);
                    _info.Reliable = false;
                    // 从文件夹名中获取
                    regex = Name.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 JSON 出现的版本号中获取
                    var jsonRaw = (JsonObject)JsonObject.DeepClone();
                    jsonRaw.Remove("libraries");
                    var jsonRawText = jsonRaw.ToString();
                    regex = jsonRawText.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 无法获取
                    _info.VanillaName = "Unknown";
                    Desc = Lang.Text("Select.Instance.Description.UnknownMcVersion");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "识别 Minecraft 版本时出错");
                    _info.VanillaName = "Unknown";
                    Desc = Lang.Text("Minecraft.Error.Unrecognizable", ex.Message);
                }

                #endregion

                VersionSearchFinish: ;

                if (_info.VanillaName.StartsWithF("20.") || _info.VanillaName.StartsWithF("21."))
                {
                    _info.VanillaName = "1." + _info.VanillaName;
                }
                
                _info.VanillaName = _info.VanillaName.Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
                // 获取版本号
                if (_info.VanillaName.StartsWithF("1."))
                {
                    var segments = _info.VanillaName.Split(" _-.".ToCharArray());
                    _info.vanilla = new Version((int)Math.Round(ModBase.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        0, (int)Math.Round(ModBase.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else if (_info.VanillaName.RegexCheck(@"^[2-9][0-9]\."))
                {
                    var segments = _info.VanillaName.Split(" _-.".ToCharArray());
                    _info.vanilla = new Version((int)Math.Round(ModBase.Val(segments[0])),
                        (int)Math.Round(ModBase.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        (int)Math.Round(ModBase.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else
                {
                    _info.vanilla = new Version(9999, 0, 0);
                }

                return _info;
            }
            set { _info = value; }
        }

        /// <summary>
        ///     该实例的 JSON 文本。
        /// </summary>
        public string JsonText
        {
            get
            {
                // 快速检查 JSON 是否以 { 开头、} 结尾；忽略空白字符
                bool FastJsonCheck(string json)
                {
                    var trimedJson = json.Trim();
                    return trimedJson.StartsWithF("{") && trimedJson.EndsWithF("}");
                }

                ;
                if (_jsonText is null)
                {
                    var jsonPath = PathInstance + Name + ".json";
                    if (!File.Exists(jsonPath))
                    {
                        // 如果文件夹下只有一个 JSON 文件，则将其作为实例 JSON
                        var jsonFiles = Directory.GetFiles(PathInstance, "*.json");
                        if (jsonFiles.Count() == 1)
                        {
                            jsonPath = jsonFiles[0];
                            ModBase.Log("[Minecraft] 未找到同名实例 JSON，自动换用 " + jsonPath, ModBase.LogLevel.Debug);
                        }
                        else
                        {
                            throw new Exception(Lang.Text("Minecraft.Error.InstanceJsonNotFound",
                                $"{PathInstance}{Name}.json"));
                        }
                    }

                    _jsonText = ModBase.ReadFile(jsonPath);
                    // 如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    if (!FastJsonCheck(_jsonText))
                    {
                        if (ModBase.RunInUi())
                        {
                            ModBase.Log($"[Minecraft] 实例 JSON 文件为空或有误，将进行短暂重试（{jsonPath}）", ModBase.LogLevel.Debug);
                            Thread.Sleep(200);
                            _jsonText = ModBase.ReadFile(jsonPath);
                        }
                        else
                        {
                            ModBase.Log($"[Minecraft] 实例 JSON 文件为空或有误，将在 2s 后重试读取（{jsonPath}）", ModBase.LogLevel.Debug);
                            Thread.Sleep(2000);
                            _jsonText = ModBase.ReadFile(jsonPath);
                        }
                        if (!FastJsonCheck(_jsonText))
                            ModBase.GetJson(_jsonText);
                    }
                }

                return _jsonText;
            }
            set => _jsonText = value;
        }

        /// <summary>
        ///     该实例的 JSON 对象。
        ///     若 JSON 存在问题，在获取该属性时即会抛出异常。
        /// </summary>
        public JsonObject JsonObject
        {
            get
            {
                if (_jsonObject is null)
                {
                    var text = JsonText; // 触发 JsonText 的 Get 事件
                    try
                    {
                        _jsonObject = (JsonObject)ModBase.GetJson(text);
                        // 转换 HMCL 关键项
                        if (_jsonObject.ContainsKey("patches") && !_jsonObject.ContainsKey("time"))
                        {
                            IsHmclFormatJson = true;
                            // 合并 JSON
                            // Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            JsonObject currentObject = null;
                            var subjsonList = new List<JsonObject>();
                            foreach (var SubjsonNode in _jsonObject["patches"].AsArray()) { var subjson = SubjsonNode.AsObject();
                                subjsonList.Add(subjson); }
                            subjsonList.Sort((left, right) =>
                                ModBase.Val((left["priority"] ?? "0").ToString()) <
                                ModBase.Val((right["priority"] ?? "0").ToString()));
                            foreach (var Subjson in subjsonList)
                            {
                                var id = (string)Subjson["id"];
                                if (id is not null)
                                {
                                    // 合并 JSON
                                    ModBase.Log("[Minecraft] 合并 HMCL 分支项：" + id);
                                    if (currentObject is not null)
                                        currentObject.Merge(Subjson);
                                    else
                                        currentObject = Subjson;
                                }
                                else
                                {
                                    ModBase.Log("[Minecraft] 存在为空的 HMCL 分支项");
                                }
                            }

                            _jsonObject = currentObject;
                            // 修改附加项
                            _jsonObject["id"] = Name;
                            if (_jsonObject.ContainsKey("inheritsFrom"))
                                _jsonObject.Remove("inheritsFrom");
                        }

                        // 与继承实例合并
                        object inheritInstanceName = null;
                        do
                        {
                            try
                            {
                                inheritInstanceName = _jsonObject["inheritsFrom"] is null
                                    ? ""
                                    : _jsonObject["inheritsFrom"].ToString();
                                if (Equals(inheritInstanceName, Name))
                                {
                                    ModBase.Log("[Minecraft] 自引用的继承实例：" + Name, ModBase.LogLevel.Debug);
                                    inheritInstanceName = "";
                                    break;
                                }

                                Recheck: ;

                                if (!Equals(inheritInstanceName, ""))
                                {
                                    var inheritInstance = new Instance(inheritInstanceName?.ToString() ?? "");
                                    // 继续循环
                                    if (Equals(inheritInstance.InheritInstanceName,
                                            inheritInstanceName))
                                        throw new Exception(Lang.Text("Minecraft.Error.DependencyRecursion",
                                            inheritInstanceName));
                                    inheritInstanceName = inheritInstance.InheritInstanceName;
                                    // 合并
                                    inheritInstance.JsonObject.Merge(_jsonObject);
                                    _jsonObject = inheritInstance.JsonObject;
                                    goto Recheck;
                                }
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "合并实例依赖项 JSON 失败（" + (inheritInstanceName ?? "null") + "）");
                            }
                        } while (false);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Minecraft.Error.InitInstanceJsonFailed", Name ?? "null"), ex);
                    }
                }

                return _jsonObject;
            }
            set => _jsonObject = value;
        }

        /// <summary>
        ///     是否为旧版 JSON 格式。
        /// </summary>
        public bool IsOldJson => JsonObject["minecraftArguments"] is not null &&
                                 (string)JsonObject["minecraftArguments"] != "";

        /// <summary>
        ///     JSON 是否为 HMCL 格式。
        /// </summary>
        public bool IsHmclFormatJson { get; set; }

        /// <summary>
        ///     实例 JAR 中的 version.json 文件对象。
        ///     若没有则返回 Nothing。
        /// </summary>
        public JsonObject JsonVersion
        {
            get
            {
                if (!_JsonVersion_jsonVersionInited)
                {
                    _JsonVersion_jsonVersionInited = true;
                    do
                    {
                        try
                        {
                            if (!File.Exists(PathInstance + Name + ".jar"))
                                break;
                            using (var jarArchive = new ZipArchive(new FileStream(PathInstance + Name + ".jar",
                                       FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                            {
                                var versionJson = jarArchive.GetEntry("version.json");
                                if (versionJson is not null)
                                    using (var versionJsonStream = new StreamReader(versionJson.Open()))
                                    {
                                        _jsonVersion = (JsonObject)ModBase.GetJson(versionJsonStream.ReadToEnd());
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"从实例 JAR 中读取 version.json 失败 ({PathInstance}{Name}.jar)");
                        }
                    } while (false);
                }

                return _jsonVersion;
            }
        }

        /// <summary>
        ///     该实例的依赖实例。若无依赖实例则为空字符串。
        /// </summary>
        public string InheritInstanceName
        {
            get
            {
                if (_inheritInstanceName is null)
                {
                    _inheritInstanceName = (JsonObject["inheritsFrom"] ?? "").ToString();
                    // 由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承实例
                    // 此外，由于这里的加载早于实例种类判断，所以需要手动判断是否为 LiteLoader
                    // 如果实例提供了不同的 JAR，代表所需的 JAR 可能已被更改，则跳过 Inherit 替换
                    if (JsonText.Contains("liteloader") && (Info.VanillaName ?? "") != (Name ?? "") &&
                        !JsonText.Contains("logging"))
                        if (((JsonObject["jar"] ?? Info.VanillaName).ToString() ?? "") == (Info.VanillaName ?? ""))
                            _inheritInstanceName = Info.VanillaName;
                    // HMCL 实例无 JSON
                    if (IsHmclFormatJson)
                        _inheritInstanceName = "";
                }

                return _inheritInstanceName;
            }
        }

        /// <summary>
        ///     检查 Minecraft 版本，若检查通过 State 则为 Original 且返回 True。
        /// </summary>
        public bool Check()
        {
            // 检查文件夹
            if (!Directory.Exists(PathInstance))
            {
                state = McInstanceState.Error;
                Desc = Lang.Text("Select.Instance.Description.NotFound", Name);
                return false;
            }

            // 检查权限
            try
            {
                Directory.CreateDirectory(PathInstance + @"PCL\");
                ModBase.CheckPermissionWithException(PathInstance + @"PCL\");
            }
            catch (Exception ex)
            {
                state = McInstanceState.Error;
                Desc = Lang.Text("Select.Instance.Description.NoPermission");
                ModBase.Log(ex, "没有访问实例文件夹的权限");
                return false;
            }

            // 确认 JSON 可用性
            try
            {
                var jsonObjCheck = JsonObject;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "实例 JSON 可用性检查失败（" + PathInstance + "）");
                JsonText = "";
                JsonObject = null;
                Desc = ex.Message;
                state = McInstanceState.Error;
                return false;
            }

            // 检查版本号获取
            try
            {
                if (string.IsNullOrEmpty(Info.VanillaName))
                    throw new Exception(Lang.Text("Minecraft.Error.VersionNumberEmpty"));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "版本号获取失败（" + Name + "）");
                state = McInstanceState.Error;
                Desc = Lang.Text("Minecraft.Error.VersionNumberFetchFailed", ex);
                return false;
            }

            // 检查依赖实例
            try
            {
                if (!string.IsNullOrEmpty(InheritInstanceName))
                    if (!File.Exists(Path.Combine(ModBase.GetPathFromFullPath(PathInstance), InheritInstanceName, InheritInstanceName + ".json")))
                    {
                        state = McInstanceState.Error;
                        Desc = Lang.Text("Select.Instance.Description.NeedInherit", InheritInstanceName);
                        return false;
                    }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "依赖实例检查出错（" + Name + "）");
                state = McInstanceState.Error;
                Desc = Lang.Text("Select.Instance.Description.UnknownError") + ": " + ex;
                return false;
            }

            state = McInstanceState.Original;
            return true;
        }

        /// <summary>
        ///     加载 Minecraft 实例的详细信息。不使用其缓存，且会更新缓存。
        /// </summary>
        public Instance Load()
        {
            try
            {
                // 检查实例，若出错则跳过数据确定阶段
                if (!Check())
                    goto ExitDataLoad;

                #region 确定实例分类

                switch (Info.VanillaName ?? "") // 在获取 Version.Original 对象时会完成它的加载
                {
                    case "Unknown":
                    {
                        state = McInstanceState.Error;
                        break;
                    }
                    case "Old":
                    {
                        state = McInstanceState.Old; // 根据 API 进行筛选
                        break;
                    }

                    default:
                    {
                        var realJson = JsonObject is not null ? JsonObject.ToString() : JsonText;
                        // 愚人节与快照版本
                        if ((JsonObject["type"] ?? "").ToString() == "fool" ||
                            !string.IsNullOrEmpty(McVersionClassifier.GetMcFoolName(Info.VanillaName)))
                            state = McInstanceState.Fool;
                        else if (IsSnapshot()) state = McInstanceState.Snapshot;
                        // OptiFine
                        if (realJson.Contains("optifine"))
                        {
                            state = McInstanceState.OptiFine;
                            Info.HasOptiFine = true;
                            Info.OptiFine = realJson.RegexSeek(RegexPatterns.OptiFineVersion) ??
                                            Lang.Text("Minecraft.Version.Unknown");
                        }

                        // LiteLoader
                        if (realJson.Contains("liteloader"))
                        {
                            state = McInstanceState.LiteLoader;
                            Info.HasLiteLoader = true;
                        }

                        // Fabric、Forge、Quilt、LabyMod、Legacy Fabric
                        if (realJson.Contains("labymod_data"))
                        {
                            state = McInstanceState.LabyMod;
                            Info.HasLabyMod = true;
                            Info.LabyMod = (string)JsonObject["labymod_data"]["version"];
                        }
                        else if (realJson.Contains("net.legacyfabric:intermediary"))
                        {
                            state = McInstanceState.LegacyFabric;
                            Info.HasLegacyFabric = true;
                            Info.LegacyFabric =
                                (realJson.RegexSeek(RegexPatterns.LegacyFabricVersion) ??
                                 Lang.Text("Minecraft.Version.Unknown"))
                                .Replace("+build", "");
                        }
                        else if (realJson.Contains("net.fabricmc:fabric-loader"))
                        {
                            state = McInstanceState.Fabric;
                            Info.HasFabric = true;
                            Info.Fabric =
                                (realJson.RegexSeek(RegexPatterns.FabricVersion) ??
                                 Lang.Text("Minecraft.Version.Unknown")).Replace("+build", "");
                        }
                        else if (realJson.Contains("org.quiltmc:quilt-loader"))
                        {
                            state = McInstanceState.Quilt;
                            Info.HasQuilt = true;
                            Info.Quilt =
                                (realJson.RegexSeek(RegexPatterns.QuiltVersion) ??
                                 Lang.Text("Minecraft.Version.Unknown")).Replace("+build", "");
                        }
                        else if (realJson.Contains("com.cleanroommc:cleanroom:"))
                        {
                            state = McInstanceState.Cleanroom;
                            Info.HasCleanroom = true;
                            Info.Cleanroom =
                                (realJson.RegexSeek(RegexPatterns.CleanroomVersion) ??
                                 Lang.Text("Minecraft.Version.Unknown")).Replace("+build", "");
                        }
                        else if (realJson.Contains("minecraftforge") && !realJson.Contains("net.neoforge"))
                        {
                            state = McInstanceState.Forge;
                            Info.HasForge = true;
                            Info.Forge = realJson.RegexSeek(RegexPatterns.ForgeMainVersion) ??
                                         realJson.RegexSeek(RegexPatterns.ForgeLibVersion) ??
                                         Lang.Text("Minecraft.Version.Unknown");
                        }
                        else if (realJson.Contains("net.neoforge"))
                        {
                            // 1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                            // 1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                            state = McInstanceState.NeoForge;
                            Info.HasNeoForge = true;
                            Info.NeoForge = realJson.RegexSeek(RegexPatterns.NeoForgeVersion) ??
                                            Lang.Text("Minecraft.Version.Unknown");
                        }

                        break;
                    }
                }

                #endregion

                ExitDataLoad: ;

                // 确定实例图标
                Logo = States.Instance.LogoPath[PathInstance];
                if (string.IsNullOrEmpty(Logo) || !States.Instance.IsLogoCustom[PathInstance])
                    switch (state)
                    {
                        case McInstanceState.Original:
                        {
                            Logo = ModBase.pathImage + "Blocks/Grass.png";
                            break;
                        }
                        case McInstanceState.Snapshot:
                        {
                            Logo = ModBase.pathImage + "Blocks/CommandBlock.png";
                            break;
                        }
                        case McInstanceState.Old:
                        {
                            Logo = ModBase.pathImage + "Blocks/CobbleStone.png";
                            break;
                        }
                        case McInstanceState.Forge:
                        {
                            Logo = ModBase.pathImage + "Blocks/Anvil.png";
                            break;
                        }
                        case McInstanceState.NeoForge:
                        {
                            Logo = ModBase.pathImage + "Blocks/NeoForge.png";
                            break;
                        }
                        case McInstanceState.Cleanroom:
                        {
                            Logo = ModBase.pathImage + "Blocks/Cleanroom.png";
                            break;
                        }
                        case McInstanceState.Fabric:
                        {
                            Logo = ModBase.pathImage + "Blocks/Fabric.png";
                            break;
                        }
                        case McInstanceState.LegacyFabric:
                        {
                            Logo = ModBase.pathImage + "Blocks/Fabric.png";
                            break;
                        }
                        case McInstanceState.Quilt:
                        {
                            Logo = ModBase.pathImage + "Blocks/Quilt.png";
                            break;
                        }
                        case McInstanceState.OptiFine:
                        {
                            Logo = ModBase.pathImage + "Blocks/GrassPath.png";
                            break;
                        }
                        case McInstanceState.LiteLoader:
                        {
                            Logo = ModBase.pathImage + "Blocks/Egg.png";
                            break;
                        }
                        case McInstanceState.Fool:
                        {
                            Logo = ModBase.pathImage + "Blocks/GoldBlock.png";
                            break;
                        }
                        case McInstanceState.LabyMod:
                        {
                            Logo = ModBase.pathImage + "Blocks/LabyMod.png";
                            break;
                        }

                        default:
                        {
                            Logo = ModBase.pathImage + "Blocks/RedstoneBlock.png";
                            break;
                        }
                    }

                // 确定实例描述
                if (state == McInstanceState.Error)
                {
                    Desc = Desc;
                }
                else
                {
                    Desc = States.Instance.CustomInfo[PathInstance];
                    if ((Desc ?? "") == (GetDefaultDescription() ?? ""))
                        Desc = "";
                }

                // 确定实例收藏状态
                IsStar = States.Instance.Starred[PathInstance];
                // 确定实例显示种类
                displayType = (McInstanceCardType)States.Instance.CardType[PathInstance];
                // 写入缓存
                if (Directory.Exists(PathInstance))
                {
                    States.Instance.State[PathInstance] = (int)state;
                    States.Instance.Info[PathInstance] = Desc;
                    States.Instance.LogoPath[PathInstance] = Logo;
                }

                if (state != McInstanceState.Error)
                {
                    States.Instance.ReleaseTime[PathInstance] = releaseTime.ToString("yyyy'-'MM'-'dd HH':'mm", CultureInfo.InvariantCulture);
                    States.Instance.FabricVersion[PathInstance] = Info.Fabric;
                    States.Instance.LegacyFabricVersion[PathInstance] = Info.LegacyFabric;
                    States.Instance.QuiltVersion[PathInstance] = Info.Quilt;
                    States.Instance.LabyModVersion[PathInstance] = Info.LabyMod;
                    States.Instance.OptiFineVersion[PathInstance] = Info.OptiFine;
                    States.Instance.HasLiteLoader[PathInstance] = Info.HasLiteLoader;
                    States.Instance.ForgeVersion[PathInstance] = Info.Forge;
                    States.Instance.NeoForgeVersion[PathInstance] = Info.NeoForge;
                    States.Instance.CleanroomVersion[PathInstance] = Info.Cleanroom;
                    States.Instance.VanillaVersionName[PathInstance] = Info.VanillaName;
                    States.Instance.VanillaVersion[PathInstance] = Info.vanilla.ToString();
                }
            }
            catch (Exception ex)
            {
                Desc = Lang.Text("Select.Instance.Description.UnknownError") + ": " + ex;
                Logo = ModBase.pathImage + "Blocks/RedstoneBlock.png";
                state = McInstanceState.Error;
                ModBase.Log(ex, Lang.Text("Select.Instance.Error.Load", Name), ModBase.LogLevel.Feedback);
            }
            finally
            {
                IsLoaded = true;
            }

            return this;
        }

        private bool IsSnapshot()
        {
            return new[] { "w", "snapshot", "rc", "pre", "experimental", "-" }.Any(s =>
                       Info.VanillaName.ContainsF(s, true)) || Name.ContainsF("combat", true) ||
                   (JsonObject["type"] ?? "").ToString() == "snapshot" ||
                   (JsonObject["type"] ?? "").ToString() == "pending";
        }

        /// <summary>
        ///     获取实例的默认描述。
        /// </summary>
        public string GetDefaultDescription()
        {
            // Mod Loader 信息
            var modLoaderInfo = "";
            if (this.Info.HasForge)
                modLoaderInfo += ", Forge" + (this.Info.Forge == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.Forge);
            if (this.Info.HasNeoForge)
                modLoaderInfo += ", NeoForge" + (this.Info.NeoForge == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.NeoForge);
            if (this.Info.HasCleanroom)
                modLoaderInfo += ", Cleanroom" + (this.Info.Cleanroom == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.Cleanroom);
            if (this.Info.HasLabyMod)
                modLoaderInfo += ", LabyMod" + (this.Info.LabyMod == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.LabyMod);
            if (this.Info.HasFabric)
                modLoaderInfo += ", Fabric" + (this.Info.Fabric == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.Fabric);
            if (this.Info.HasQuilt)
                modLoaderInfo += ", Quilt" + (this.Info.Quilt == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.Quilt);
            if (this.Info.HasLegacyFabric)
                modLoaderInfo += ", Legacy Fabric" +
                                 (this.Info.LegacyFabric == Lang.Text("Minecraft.Version.Unknown")
                                     ? ""
                                     : " " + this.Info.LegacyFabric);
            if (this.Info.HasOptiFine)
                modLoaderInfo += ", OptiFine" + (this.Info.OptiFine == Lang.Text("Minecraft.Version.Unknown")
                    ? ""
                    : " " + this.Info.OptiFine.Replace("-", " ").Replace("_", " "));
            if (this.Info.HasLiteLoader)
                modLoaderInfo += ", LiteLoader";
            // 基础信息
            string info;
            switch (state)
            {
                case McInstanceState.Snapshot:
                case McInstanceState.Original:
                case McInstanceState.Forge:
                case McInstanceState.NeoForge:
                case McInstanceState.Fabric:
                case McInstanceState.OptiFine:
                case McInstanceState.LiteLoader:
                {
                    if (this.Info.VanillaName.ContainsF("pre", true))
                        info = Lang.Text("Select.Instance.Description.PreRelease", this.Info.VanillaName);
                    else if (this.Info.VanillaName.ContainsF("rc", true))
                        info = Lang.Text("Select.Instance.Description.ReleaseCandidate", this.Info.VanillaName);
                    else if (this.Info.VanillaName.Contains("experimental"))
                        info = Lang.Text("Select.Instance.Description.ExperimentalSnapshot", this.Info.VanillaName);
                    else if (this.Info.VanillaName == "pending")
                        info = Lang.Text("Select.Instance.Description.ExperimentalSnapshot.Pending");
                    else if (IsSnapshot())
                        info = this.Info.Reliable ? Lang.Text("Select.Instance.Description.Snapshot", this.Info.VanillaName.Replace("-snapshot", "")) : Lang.Text("Select.Instance.Description.Snapshot.Unknown");
                    else
                        info = this.Info.Reliable ? Lang.Text("Select.Instance.Description.Release", this.Info.VanillaName) : Lang.Text("Select.Instance.Description.Release.Unknown");

                    break;
                }
                case McInstanceState.Old:
                {
                    info = Lang.Text("Select.Instance.Description.Old");
                    break;
                }
                case McInstanceState.Fool:
                {
                    info = Lang.Text("Select.Instance.Description.AprilFools", this.Info.VanillaName);
                    break;
                }
                case McInstanceState.Error:
                {
                    return Desc; // 已有错误信息
                }

                default:
                {
                    return Lang.Text("Select.Instance.Description.ReportUnknownError");
                }
            }

            return (info + modLoaderInfo).Replace("_", "-");
        }

        // 运算符支持
        public override bool Equals(object obj)
        {
            var instance = obj as Instance;
            return instance is not null && (PathInstance ?? "") == (instance.PathInstance ?? "");
        }

        public static bool operator ==(Instance? a, Instance? b)
        {
            if (a is null && b is null)
                return true;
            if (a is null || b is null)
                return false;
            return (a.PathInstance ?? "") == (b.PathInstance ?? "");
        }

        public static bool operator !=(Instance a, Instance b)
        {
            return !(a == b);
        }
    }

    public enum McInstanceState
    {
        Error,
        Original,
        Snapshot,
        Fool,
        OptiFine,
        Old,
        Forge,
        NeoForge,
        LiteLoader,
        Fabric,
        LegacyFabric,
        Quilt,
        Cleanroom,
        LabyMod
    }

    /// <summary>
    ///     某个 Minecraft 实例的版本名、附加组件信息。
    /// </summary>
    public class McInstanceInfo
    {
        /// <summary>
        ///     Cleanroom 版本号，如 0.2.4-alpha。
        /// </summary>
        public string Cleanroom = "";

        /// <summary>
        ///     Fabric 版本号，如 0.7.2.175。
        /// </summary>
        public string Fabric = "";

        /// <summary>
        ///     Forge 版本号，如 31.1.2、14.23.5.2847。
        /// </summary>
        public string Forge = "";

        // Cleanroom

        /// <summary>
        ///     该实例是否安装了 Cleanroom。
        /// </summary>
        public bool HasCleanroom;

        // Fabric

        /// <summary>
        ///     该实例是否安装了 Fabric。
        /// </summary>
        public bool HasFabric;

        // Forge

        /// <summary>
        ///     该实例是否安装了 Forge。
        /// </summary>
        public bool HasForge;

        // LabyMod

        /// <summary>
        ///     该实例是否安装了 LabyMod。
        /// </summary>
        public bool HasLabyMod;

        // LegacyFabric

        /// <summary>
        ///     该实例是否安装了 Fabric。
        /// </summary>
        public bool HasLegacyFabric;

        // LiteLoader

        /// <summary>
        ///     该实例是否安装了 LiteLoader。
        /// </summary>
        public bool HasLiteLoader;

        // NeoForge

        /// <summary>
        ///     该实例是否安装了 NeoForge。
        /// </summary>
        public bool HasNeoForge;

        // OptiFine

        /// <summary>
        ///     该实例是否通过 JSON 安装了 OptiFine。
        /// </summary>
        public bool HasOptiFine;


        // Quilt

        /// <summary>
        ///     该实例是否安装了 Quilt。
        /// </summary>
        public bool HasQuilt;

        /// <summary>
        ///     LabyMod 版本号，如 4.2.59。
        /// </summary>
        public string LabyMod = "";

        /// <summary>
        ///     Fabric 版本号，如 0.7.2.175。
        /// </summary>
        public string LegacyFabric = "";

        /// <summary>
        ///     NeoForge 版本号，如 21.0.2-beta、47.1.79。
        /// </summary>
        public string NeoForge = "";

        /// <summary>
        ///     OptiFine 版本号，如 C8、C9_pre10。
        /// </summary>
        public string OptiFine = "";

        /// <summary>
        ///     Quilt 版本号，如 0.26.1-beta.1、0.26.0。
        /// </summary>
        public string Quilt = "";

        /// <summary>
        ///     指示原版版本号是否可靠（不是通过猜测获取）。
        /// </summary>
        public bool Reliable = true;

        /// <summary>
        ///     可比较的三段式原版版本号。
        ///     对老版本格式，例如 1.20.3，会被转换为 20.0.3。
        ///     若没有版本号，例如旧快照，则为 9999.0.0。
        /// </summary>
        public Version vanilla;

        // 原版

        /// <summary>
        ///     原版版本名。
        ///     如 26.1，26.1-snapshot-1，1.12.2，16w01a。
        /// </summary>
        public string VanillaName;

        /// <summary>
        ///     原版版本号是否有效。
        /// </summary>
        public bool Valid => vanilla.Major < 1000;

        /// <summary>
        ///     可供比较的原版 Drop 序数。
        ///     例如 26.3.2 为 263，1.21.5 为 210。
        ///     若没有版本号，例如旧快照，则直接指定为 209。
        /// </summary>
        public int Drop => Valid ? vanilla.Major * 10 + vanilla.Minor : 209;

        /// <summary>
        ///     可供比较的 OptiFine 版本序数。
        /// </summary>
        public int OptiFineCode
        {
            get
            {
                if (string.IsNullOrEmpty(OptiFine) || OptiFine == Lang.Text("Minecraft.Version.Unknown"))
                    return 0;
                // 字母编号，如 G2 中的 G（7）
                var result = char.ToUpperInvariant(OptiFine.First()) - 'A' + 1;
                // 末尾数字，如 C5 beta4 中的 5
                result *= 100;
                result = (int)Math.Round(result +
                                         ModBase.Val(OptiFine[1..].RegexSeek("[0-9]+")));
                // 测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
                result *= 100;
                if (OptiFine.ContainsF("pre", true))
                    result += 50;
                if (OptiFine.ContainsF("pre", true) || OptiFine.ContainsF("beta", true))
                {
                    var lastChar = OptiFine[^1..];
                    if (ModBase.Val(lastChar) == 0d && lastChar != "0")
                        result += 1; // 为 pre 或 beta 结尾，视作 1
                    else
                        result =
                            (int)Math.Round(result +
                                            ModBase.Val(OptiFine.ToLower().RegexSeek("(?<=((pre)|(beta)))[0-9]+")));
                }
                else
                {
                    result += 99;
                }

                return result;
            }
        }

        // Forgelike

        /// <summary>
        ///     该版本是否安装了 Forgelike 加载器。
        /// </summary>
        public bool HasForgelike => HasForge || HasNeoForge || HasCleanroom;

        /// <summary>
        ///     可供比较的类 Forge 版本序数。
        /// </summary>
        public int ForgelikeCode
        {
            get
            {
                if (!HasForgelike)
                    return 0;
                if ((string.IsNullOrEmpty(Forge) || Forge == Lang.Text("Minecraft.Version.Unknown")) &&
                    (string.IsNullOrEmpty(NeoForge) || NeoForge == Lang.Text("Minecraft.Version.Unknown")))
                    return 0;
                var segments = (HasForge ? Forge : NeoForge).RegexSearch(@"\d+");
                switch (segments.Count)
                {
                    case var @case when @case > 4:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d +
                                               ModBase.Val(segments[3]));
                    }
                    case 3:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d +
                                               ModBase.Val(segments[2]));
                    }
                    case 2:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d);
                    }

                    default:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d);
                    }
                }
            }
        }

        // Fabriclike

        /// <summary>
        ///     该版本是否安装了 Fabriclike 加载器。
        /// </summary>
        public bool HasFabriclike => HasFabric || HasQuilt || HasLegacyFabric;

        // API

        /// <summary>
        ///     生成对此实例信息的用户友好的描述性字符串。
        /// </summary>
        public override string ToString()
        {
            string toStringRet = default;
            toStringRet = "";
            if (HasForge)
                toStringRet += ", Forge" + (Forge == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Forge);
            if (HasNeoForge)
                toStringRet += ", NeoForge" +
                               (NeoForge == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + NeoForge);
            if (HasCleanroom)
                toStringRet += ", Cleanroom" +
                               (Cleanroom == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Cleanroom);
            if (HasFabric)
                toStringRet += ", Fabric" + (Fabric == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Fabric);
            if (HasLegacyFabric)
                toStringRet += ", LegacyFabric" +
                               (LegacyFabric == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + LegacyFabric);
            if (HasQuilt)
                toStringRet += ", Quilt" + (Quilt == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Quilt);
            if (HasLabyMod)
                toStringRet += ", LabyMod" + (LabyMod == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + LabyMod);
            if (HasOptiFine)
                toStringRet += ", OptiFine" +
                               (OptiFine == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + OptiFine);
            if (HasLiteLoader)
                toStringRet += ", LiteLoader";
            if (string.IsNullOrEmpty(toStringRet)) return Lang.Text("Minecraft.Version.Vanilla") + " " + VanillaName;

            return VanillaName + toStringRet;
        }

        // Helpers

        /// <summary>
        ///     版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
        /// </summary>
        public static bool IsFormatFit(string version)
        {
            if (version is null)
                return false;
            if (version.RegexCheck(@"^1\.\d"))
                return true;
            if (ModBase.Val(version.RegexSeek(@"^[2-9]\d\.\d+")) > 25d)
                return true;
            return false;
        }

        /// <summary>
        ///     尝试将版本字符串转换为 Drop 序数。
        ///     若无法转换则返回 0。
        /// </summary>
        public static int VersionToDrop(string? version, bool allowSnapshot = false)
        {
            if (!allowSnapshot && version.Contains("-"))
                return 0;
            if (version is null)
                return 0;
            var segments = version.BeforeFirst("-").Split(".");
            if (segments.Length < 2)
                return 0;
            var major = (int)Math.Round(ModBase.Val(segments[0]));
            var minor = (int)Math.Round(ModBase.Val(segments[1]));
            if (major == 1) return minor * 10;

            if (major < 25) return 0;

            return major * 10 + minor;
        }

        /// <summary>
        ///     将 Drop 序数转换为版本字符串。
        /// </summary>
        public static string DropToVersion(int drop)
        {
            if (drop >= 250) return $"{drop / 10}.{drop % 10}";

            return $"1.{drop / 10}";
        }
    }

    /// <summary>
    ///     根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    /// </summary>
    /// <summary>
    ///     当前按卡片分类的所有版本列表。
    /// </summary>
    public static Dictionary<McInstanceCardType, List<Instance>> mcInstanceList = new();

    #endregion

    #region 实例列表加载

    /// <summary>
    ///     是否要求本次加载强制刷新实例列表。
    /// </summary>
    public static bool mcInstanceListForceRefresh;

    /// <summary>
    ///     是否为本次打开 PCL 后第一次加载实例列表。
    ///     这会清理所有 .pclignore 文件，而非跳过这些对应实例。
    /// </summary>
    private static bool _isFirstMcInstanceListLoad = true;

    /// <summary>
    ///     加载 Minecraft 文件夹的实例列表。
    /// </summary>
    public static ModLoader.LoaderTask<string, int> mcInstanceListLoader =
        new("Minecraft Instance List", InitMcInstanceList) { reloadTimeout = 1 };

    private static void InitMcInstanceList(ModLoader.LoaderTask<string, int> loader)
    {
        var path = loader.input;
        try
        {
            // 初始化
            mcInstanceList = new Dictionary<McInstanceCardType, List<Instance>>();
            var versionsPath = Path.Combine(path, "versions");
            var folderList = new List<string>();

            // 读取版本文件夹
            if (Directory.Exists(versionsPath))
                try
                {
                    foreach (var folder in new DirectoryInfo(versionsPath).GetDirectories())
                        folderList.Add(folder.Name);
                }
                catch (Exception ex)
                {
                    throw new Exception(Lang.Text("Minecraft.Error.CannotReadInstanceFolder", versionsPath), ex);
                }

            // 如果没有可用实例，清空缓存并跳过后续处理
            if (!folderList.Any())
            {
                ModBase.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", "");
                McInstanceSelected = null;
                States.Game.SelectedInstance = "";
                ModBase.Log("[Minecraft] 未找到可用 Minecraft 实例");
                return;
            }

            // 根据文件夹名列表生成辨识码
            var folderListHash = ModBase.GetHash(mcInstanceCacheVersion + "#" + string.Join("#", folderList));
            var folderListCheck = (int)(folderListHash % (int.MaxValue - 1));

            // 尝试使用缓存
            var useCache = !mcInstanceListForceRefresh &&
                           ModBase.Val(ModBase.ReadIni(Path.Combine(path, "PCL.ini"), "InstanceCache")) ==
                           folderListCheck;

            if (useCache)
            {
                var cachedResult = InitMcInstanceListWithCache(path);
                if (cachedResult is not null)
                    mcInstanceList = cachedResult;
                else
                    useCache = false; // 缓存无效，需要重载
            }

            // 如果不能使用缓存，重新加载
            if (!useCache)
            {
                mcInstanceListForceRefresh = false;
                ModBase.Log("[Minecraft] 文件夹列表变更或缓存无效，重载所有实例");
                ModBase.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", folderListCheck.ToString());
                mcInstanceList = InitMcInstanceListWithoutCache(path);
            }

            _isFirstMcInstanceListLoad = false;

            if (loader.IsAborted)
                return;

            // 尝试读取已储存的选择
            var savedSelection = ModBase.ReadIni(Path.Combine(path, "PCL.ini"), "Version");
            if (!string.IsNullOrEmpty(savedSelection))
                foreach (var card in mcInstanceList)
                foreach (var instance in card.Value)
                    if ((instance.Name ?? "") == savedSelection && instance.state != McInstanceState.Error)
                    {
                        McInstanceSelected = instance;
                        States.Game.SelectedInstance = McInstanceSelected.Name;
                        ModBase.Log("[Minecraft] 选择该文件夹储存的 Minecraft 实例：" + McInstanceSelected.PathInstance);
                        return;
                    }

            // 自动选择第一项
            var firstInstance = mcInstanceList
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(i => i.state != McInstanceState.Error);

            if (firstInstance is not null)
            {
                McInstanceSelected = firstInstance;
                States.Game.SelectedInstance = McInstanceSelected.Name;
                ModBase.Log("[Launch] 自动选择 Minecraft 实例：" + McInstanceSelected.PathInstance);
            }
            else
            {
                McInstanceSelected = null;
                States.Game.SelectedInstance = "";
                ModBase.Log("[Minecraft] 未找到可用 Minecraft 实例");
            }

            // 调试延迟
            if (Config.Debug.AddRandomDelay is bool debugDelay && debugDelay)
                Thread.Sleep(RandomUtils.NextInt(200, 3000));
        }
        catch (ThreadInterruptedException)
        {
            // 中断线程时什么也不做
        }
        catch (Exception ex)
        {
            ModBase.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", ""); // 要求下次重新加载
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.ListLoad"), ModBase.LogLevel.Feedback);
        }
    }

    // 获取实例列表
    private static Dictionary<McInstanceCardType, List<Instance>> InitMcInstanceListWithCache(string path)
    {
        var results = new Dictionary<McInstanceCardType, List<Instance>>();
        try
        {
            var cardCount = int.Parse(ModBase.ReadIni(path + "PCL.ini", "CardCount", (-1).ToString()));
            if (cardCount == -1)
                return null;
            for (int i = 0, loopTo = cardCount - 1; i <= loopTo; i++)
            {
                var cardType =
                    (McInstanceCardType)int.Parse(ModBase.ReadIni(path + "PCL.ini", "CardKey" + (i + 1),
                        "0"));
                var instanceList = new List<Instance>();

                // 循环读取实例
                foreach (var folder in ModBase.ReadIni(path + "PCL.ini", "CardValue" + (i + 1), ":").Split(":"))
                {
                    if (string.IsNullOrEmpty(folder))
                        continue;
                    var versionFolder = $@"{path}versions\{folder}\";
                    if (File.Exists(versionFolder + ".pclignore"))
                    {
                        if (_isFirstMcInstanceListLoad)
                        {
                            ModBase.Log("[Minecraft] 清理残留的忽略项目：" + versionFolder); // #2781
                            File.Delete(versionFolder + ".pclignore");
                        }
                        else
                        {
                            ModBase.Log("[Minecraft] 跳过要求忽略的项目：" + versionFolder);
                            continue;
                        }
                    }

                    try
                    {
                        // 读取单个实例
                        var instance = new Instance(versionFolder);
                        instanceList.Add(instance);
                        var instanceCfg = States.Instance;
                        instance.Desc = instanceCfg.CustomInfo[instance.PathInstance];

                        if (string.IsNullOrEmpty(instance.Desc))
                            instance.Desc = instanceCfg.Info[instance.PathInstance];
                        if (!instanceCfg.LogoPathConfig.IsDefault(instance.PathInstance))
                            instance.Logo = instanceCfg.LogoPath[instance.PathInstance];
                        if (!instanceCfg.ReleaseTimeConfig.IsDefault(instance.PathInstance))
                            instance.releaseTime = DateTime.Parse(instanceCfg.ReleaseTime[instance.PathInstance]);
                        if (!instanceCfg.StateConfig.IsDefault(instance.PathInstance))
                            instance.state =
                                (McInstanceState)(int)instanceCfg.State[instance.PathInstance];
                        instance.IsStar = instanceCfg.Starred[instance.PathInstance];
                        instance.displayType =
                            (McInstanceCardType)(int)instanceCfg.CardType[instance.PathInstance];
                        if (instance.state != McInstanceState.Error &&
                            !instanceCfg.VanillaVersionNameConfig.IsDefault(instance.PathInstance) &&
                            !instanceCfg.VanillaVersionConfig
                                .IsDefault(instance.PathInstance)) // 旧版本可能没有这一项，导致 Instance 不加载（#643）
                        {
                            var instanceInfo = new McInstanceInfo
                            {
                                Fabric = instanceCfg.FabricVersion[instance.PathInstance],
                                LegacyFabric = instanceCfg.LegacyFabricVersion[instance.PathInstance],
                                Quilt = instanceCfg.QuiltVersion[instance.PathInstance],
                                Forge = instanceCfg.ForgeVersion[instance.PathInstance],
                                LabyMod = instanceCfg.LabyModVersion[instance.PathInstance],
                                NeoForge = instanceCfg.NeoForgeVersion[instance.PathInstance],
                                Cleanroom = instanceCfg.CleanroomVersion[instance.PathInstance],
                                OptiFine = instanceCfg.OptiFineVersion[instance.PathInstance],
                                HasLiteLoader = instanceCfg.HasLiteLoader[instance.PathInstance],
                                VanillaName = instanceCfg.VanillaVersionName[instance.PathInstance],
                                vanilla = new Version(instanceCfg.VanillaVersion[instance.PathInstance])
                            };
                            instanceInfo.HasFabric = instanceInfo.Fabric.Any();
                            instanceInfo.HasLegacyFabric = instanceInfo.LegacyFabric.Any();
                            instanceInfo.HasQuilt = instanceInfo.Quilt.Any();
                            instanceInfo.HasForge = instanceInfo.Forge.Any();
                            instanceInfo.HasNeoForge = instanceInfo.NeoForge.Any();
                            instanceInfo.HasCleanroom = instanceInfo.Cleanroom.Any();
                            instanceInfo.HasOptiFine = instanceInfo.OptiFine.Any();
                            instance.Info = instanceInfo;
                        }

                        // 重新检查错误实例
                        if (instance.state == McInstanceState.Error)
                        {
                            // 重新获取实例错误信息
                            var oldDesc = instance.Desc;
                            instance.state = McInstanceState.Original;
                            instance.Check();
                            // 校验错误原因是否改变
                            var customInfo = States.Instance.CustomInfo[instance.PathInstance];
                            if (instance.state == McInstanceState.Original || (string.IsNullOrEmpty(customInfo) &&
                                                                               !((oldDesc ?? "") ==
                                                                                   (instance.Desc ?? ""))))
                            {
                                ModBase.Log("[Minecraft] 实例 " + instance.Name + " 的错误状态已变更，新的状态为：" + instance.Desc);
                                return null;
                            }
                        }

                        // 校验未加载的实例
                        if (string.IsNullOrEmpty(instance.Logo))
                        {
                            ModBase.Log("[Minecraft] 实例 " + instance.Name + " 未被加载");
                            return null;
                        }
                    }

                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取实例加载缓存失败（" + folder + "）");
                        return null;
                    }
                }

                if (instanceList.Any())
                    results.Add(cardType, instanceList);
            }

            return results;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取实例缓存失败");
            return null;
        }
    }

    private static Dictionary<McInstanceCardType, List<Instance>> InitMcInstanceListWithoutCache(string path)
    {
        var instanceList = new List<Instance>();

        #region 循环加载每个实例的信息

        foreach (var folder in new DirectoryInfo(path + "versions").GetDirectories())
        {
            if (!folder.Exists || !folder.EnumerateFiles().Any())
            {
                ModBase.Log("[Minecraft] 跳过空文件夹：" + folder.FullName);
                continue;
            }

            if ((folder.Name == "cache" || folder.Name == "BLClient" || folder.Name == "PCL") &&
                !File.Exists(Path.Combine(folder.FullName, folder.Name + ".json")))
            {
                ModBase.Log("[Minecraft] 跳过可能不是实例文件夹的项目：" + folder.FullName);
                continue;
            }

            var instanceFolder = folder.FullName + @"\";
            if (File.Exists(instanceFolder + ".pclignore"))
            {
                if (_isFirstMcInstanceListLoad)
                {
                    ModBase.Log("[Minecraft] 清理残留的忽略项目：" + instanceFolder); // #2781
                    try
                    {
                        File.Delete(instanceFolder + ".pclignore");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Select.Folder.Error.Cleanup", instanceFolder), ModBase.LogLevel.Hint);
                    }
                }
                else
                {
                    ModBase.Log("[Minecraft] 跳过要求忽略的项目：" + instanceFolder);
                    continue;
                }
            }

            var instance = new Instance(instanceFolder);
            instanceList.Add(instance);
            instance.Load();
        }

        #endregion

        var results = new Dictionary<McInstanceCardType, List<Instance>>();

        #region 将实例分类到各个卡片

        try
        {
            // 未经过自定义的实例列表
            var instanceListOriginal = new Dictionary<McInstanceCardType, List<Instance>>();

            // 单独列出收藏的实例
            var staredInstances = new List<Instance>();
            foreach (var instance in instanceList.ToList())
            {
                if (!instance.IsStar)
                    continue;
                if (instance.displayType == McInstanceCardType.Hidden)
                    continue;
                staredInstances.Add(instance);
                instanceList.Remove(instance);
            }

            if (staredInstances.Any())
                instanceListOriginal.Add(McInstanceCardType.Star, staredInstances);

            // 预先筛选出愚人节和错误的实例
            McInstanceFilter(ref instanceList, ref instanceListOriginal, new[] { McInstanceState.Error },
                McInstanceCardType.Error);
            McInstanceFilter(ref instanceList, ref instanceListOriginal, new[] { McInstanceState.Fool },
                McInstanceCardType.Fool);

            // 筛选 API 实例
            McInstanceFilter(ref instanceList, ref instanceListOriginal,
                new[]
                {
                    McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.LiteLoader, McInstanceState.Fabric,
                    McInstanceState.LegacyFabric, McInstanceState.Quilt, McInstanceState.Cleanroom,
                    McInstanceState.LabyMod
                }, McInstanceCardType.API);

            // 将老实例预先分类入不常用，只剩余原版、快照、OptiFine
            var instanceUseful = new List<Instance>();
            var instanceRubbish = new List<Instance>();
            McInstanceFilter(ref instanceList, new[] { McInstanceState.Old }, ref instanceRubbish);

            // 确认最新实例，若为快照则加入常用列表
            var latestInstance = instanceList
                .Where(v => v.state == McInstanceState.Original || v.state == McInstanceState.Snapshot)
                .MaxOrDefault(v => v.releaseTime);
            if (latestInstance is not null && latestInstance.state == McInstanceState.Snapshot)
            {
                instanceUseful.Add(latestInstance);
                instanceList.Remove(latestInstance);
            }

            // 将剩余的快照全部拖进不常用列表
            McInstanceFilter(ref instanceList, new[] { McInstanceState.Snapshot }, ref instanceRubbish);

            // 获取每个 Drop 下最新的原版与 OptiFine
            var newerInstance = new Dictionary<string, Instance>();
            var existDrops = new List<int>();
            foreach (var instance in instanceList)
            {
                if (!instance.Info.Valid)
                    continue;
                if (!existDrops.Contains(instance.Info.Drop))
                    existDrops.Add(instance.Info.Drop);
                var key = instance.Info.Drop + "-" + (int)instance.state;
                if (!newerInstance.ContainsKey(key))
                {
                    newerInstance.Add(key, instance);
                    continue;
                }

                if (instance.Info.HasOptiFine)
                {
                    if (instance.Info.OptiFineCode > newerInstance[key].Info.OptiFineCode)
                        newerInstance[key] = instance; // OptiFine 根据版本号判断
                }
                else if (instance.releaseTime > newerInstance[key].releaseTime)
                {
                    newerInstance[key] = instance; // 原版根据发布时间判断
                }
            }

            // 将每个 Drop 下的最常规版本加入
            foreach (var drop in existDrops)
                if (newerInstance.ContainsKey(drop + "-" + (int)McInstanceState.OptiFine) &&
                    newerInstance.ContainsKey(drop + "-" + (int)McInstanceState.Original))
                {
                    // 同时存在 OptiFine 与原版
                    var vanillaInstance = newerInstance[drop + "-" + (int)McInstanceState.Original];
                    var optiFineInstance = newerInstance[drop + "-" + (int)McInstanceState.OptiFine];
                    if (vanillaInstance.Info.Drop > optiFineInstance.Info.Drop)
                    {
                        // 仅在原版比 OptiFine 更新时才加入原版
                        instanceUseful.Add(vanillaInstance);
                        instanceList.Remove(vanillaInstance);
                    }

                    instanceUseful.Add(optiFineInstance);
                    instanceList.Remove(optiFineInstance);
                }
                else if (newerInstance.ContainsKey(drop + "-" + (int)McInstanceState.OptiFine))
                {
                    // 没有原版，直接加入 OptiFine
                    instanceUseful.Add(newerInstance[drop + "-" + (int)McInstanceState.OptiFine]);
                    instanceList.Remove(newerInstance[drop + "-" + (int)McInstanceState.OptiFine]);
                }
                else if (newerInstance.ContainsKey(drop + "-" + (int)McInstanceState.Original))
                {
                    // 没有 OptiFine，直接加入原版
                    instanceUseful.Add(newerInstance[drop + "-" + (int)McInstanceState.Original]);
                    instanceList.Remove(newerInstance[drop + "-" + (int)McInstanceState.Original]);
                }

            // 将剩余的东西添加进去
            instanceRubbish.AddRange(instanceList);
            if (instanceUseful.Any())
                instanceListOriginal.Add(McInstanceCardType.OriginalLike, instanceUseful);
            if (instanceRubbish.Any())
                instanceListOriginal.Add(McInstanceCardType.Rubbish, instanceRubbish);

            // 按照自定义实例分类重新添加
            foreach (var instancePair in instanceListOriginal)
            foreach (var instance in instancePair.Value)
            {
                var realType = instance.displayType == 0 || instancePair.Key == McInstanceCardType.Star
                    ? instancePair.Key
                    : instance.displayType;
                if (!results.ContainsKey(realType))
                    results.Add(realType, new List<Instance>());
                results[realType].Add(instance);
            }
        }

        catch (Exception ex)
        {
            results.Clear();
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.Classify"), ModBase.LogLevel.Feedback);
        }

        #endregion

        #region 对卡片与实例进行排序

        // 卡片排序
        var sortedInstanceList = new Dictionary<McInstanceCardType, List<Instance>>();
        foreach (var sortRule in new[]
                 {
                     McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike,
                     McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error,
                     McInstanceCardType.Hidden
                 })
            if (results.ContainsKey(sortRule))
                sortedInstanceList.Add(sortRule,
                    results[sortRule]);
        results = sortedInstanceList;

        // 版本排序
        foreach (var cardType in new[]
                 {
                     McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike,
                     McInstanceCardType.Rubbish, McInstanceCardType.Fool
                 })
        {
            if (!results.ContainsKey(cardType))
                continue;

            int getComponentCode(Instance instance)
            {
                if (instance.Info.ForgelikeCode > 0)
                    return instance.Info.ForgelikeCode;
                if (instance.Info.HasOptiFine)
                    return instance.Info.OptiFineCode;
                return 0;
            }

            ;
            results[cardType] = SortUtils.Sort(results[cardType], (left, right) =>
            {
                // 发布时间
                if ((left.releaseTime.Year >= 2000 || right.releaseTime.Year >= 2000) &&
                    left.releaseTime != right.releaseTime)
                    return left.releaseTime > right.releaseTime;
                // 附加组件种类
                if (left.Info.HasFabric != right.Info.HasFabric)
                    return left.Info.HasFabric;
                if (left.Info.HasQuilt != right.Info.HasQuilt)
                    return left.Info.HasQuilt;
                if (left.Info.HasLegacyFabric != right.Info.HasLegacyFabric)
                    return left.Info.HasLegacyFabric;
                if (left.Info.HasNeoForge != right.Info.HasNeoForge)
                    return left.Info.HasNeoForge;
                if (left.Info.HasForge != right.Info.HasForge)
                    return left.Info.HasForge;
                if (left.Info.HasCleanroom != right.Info.HasCleanroom)
                    return left.Info.HasCleanroom;
                if (left.Info.HasLabyMod != right.Info.HasLabyMod)
                    return left.Info.HasLabyMod;
                if (left.Info.HasOptiFine != right.Info.HasOptiFine)
                    return left.Info.HasOptiFine;
                if (left.Info.HasLiteLoader != right.Info.HasLiteLoader)
                    return left.Info.HasLiteLoader;
                // 附加组件版本
                if (getComponentCode(left) != getComponentCode(right))
                    return getComponentCode(left) > getComponentCode(right);
                // 名称
                return string.CompareOrdinal(left.Name, right.Name) > 0;
            });
        }

        #endregion

        #region 保存卡片缓存

        ModBase.WriteIni(path + "PCL.ini", "CardCount", results.Count.ToString());
        for (int i = 0, loopTo = results.Count - 1; i <= loopTo; i++)
        {
            ModBase.WriteIni(path + "PCL.ini", "CardKey" + (i + 1),
                ((int)results.Keys.ElementAtOrDefault(i)).ToString());
            var value = "";
            foreach (var Instance in results.Values.ElementAtOrDefault(i))
                value += Instance.Name + ":";
            ModBase.WriteIni(path + "PCL.ini", "CardValue" + (i + 1), value);
        }

        #endregion

        return results;
    }

    /// <summary>
    ///     筛选特定种类的实例，并直接添加为卡片。
    /// </summary>
    /// <param name="instanceList">用于筛选的列表。</param>
    /// <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    /// <param name="cardType">卡片的名称。</param>
    private static void McInstanceFilter(ref List<Instance> instanceList,
        ref Dictionary<McInstanceCardType, List<Instance>> target, McInstanceState[] formula,
        McInstanceCardType cardType)
    {
        var keepList = instanceList.Where(v => formula.Contains(v.state)).ToList();
        // 加入实例列表，并从剩余中删除
        if (keepList.Any())
        {
            target.Add(cardType, keepList);
            instanceList = instanceList.Except(keepList).ToList();
        }
    }

    /// <summary>
    ///     筛选特定种类的实例，并增加入一个已有列表中。
    /// </summary>
    /// <param name="instanceList">用于筛选的列表。</param>
    /// <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    /// <param name="keepList">传入需要增加入的列表。</param>
    private static void McInstanceFilter(ref List<Instance> instanceList, McInstanceState[] formula,
        ref List<Instance> keepList)
    {
        keepList.AddRange(instanceList.Where(v => formula.Contains(v.state)));
        // 加入实例列表，并从剩余中删除
        if (keepList.Any()) instanceList = instanceList.Except(keepList).ToList();
    }

    public enum McInstanceCardType
    {
        Star = -1,
        Auto = 0, // 仅用于强制实例分类的自动
        Hidden = 1,
        API = 2,
        OriginalLike = 3,
        Rubbish = 4,
        Fool = 5,
        Error = 6
    }

    #endregion

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
        var fileName = SystemDialogs.SelectFile(Lang.Text("Launch.Skin.FileDialog.Filter"), Lang.Text("Launch.Skin.FileDialog.Title"));

        // 验证有效性
        if (string.IsNullOrEmpty(fileName))
            return new McSkinInfo { IsVaild = false };
        try
        {
            var image = new MyBitmap(fileName);
            if (image.pic.Width != 64 || !(image.pic.Height == 32 || image.pic.Height == 64))
            {
                ModMain.Hint(Lang.Text("Launch.Skin.InvalidSize"), ModMain.HintType.Critical);
                return new McSkinInfo { IsVaild = false };
            }

            var fileInfo = new FileInfo(fileName);
            if (fileInfo.Length > 24 * 1024)
            {
                ModMain.Hint(Lang.Text("Launch.Skin.FileTooLarge", Lang.Number(fileInfo.Length / 1024d, "N2")),
                    ModMain.HintType.Critical);
                return new McSkinInfo { IsVaild = false };
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Skin.File.Error"), ModBase.LogLevel.Hint);
            return new McSkinInfo { IsVaild = false };
        }

        // 获取皮肤种类
        var isSlim = ModMain.MyMsgBox(Lang.Text("Launch.Skin.Model.SelectMessage"), Lang.Text("Launch.Skin.Model.SelectTitle"), Lang.Text("Launch.Skin.Model.Steve"), Lang.Text("Launch.Skin.Model.Alex"), Lang.Text("Common.Option.IDontKnow"),
            highLight: false);
        if (isSlim == 3)
        {
            ModMain.Hint(Lang.Text("Launch.Skin.Model.UnknownHint"));
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
                FileDownloader.Download(address, fileAddress + ModNet.netDownloadEnd).GetAwaiter().GetResult();
                File.Delete(fileAddress);
                FileSystem.Rename(fileAddress + ModNet.netDownloadEnd, fileAddress);
                ModBase.Log("[Minecraft] 皮肤下载成功：" + fileAddress);
            }

            return fileAddress;
        }
    }

    /// <summary>
    ///     获取 Uuid 对应的皮肤，返回“Steve”或“Alex”。
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
        public string Sha1;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long size;

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
                var splited = new List<string>(OriginalName.Split(":"));
                splited.RemoveAt(2); // Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                return splited.Join(":");
            }
        }

        public override string ToString()
        {
            return (IsNatives ? "[Native] " : "") + ModBase.GetString(size) + " | " + LocalPath;
        }
    }

    /// <summary>
    ///     检查是否符合 JSON 中的 Rules。
    /// </summary>
    /// <param name="ruleToken">JSON 中的 "rules" 项目。</param>
    public static bool McJsonRuleCheck(JsonNode ruleToken)
    {
        if (ruleToken is null)
            return true;

        // 初始化
        var required = false;
        foreach (var Rule in ruleToken.AsArray())
        {
            // 单条条件验证
            var isRightRule = true; // 是否为正确的规则
            if (Rule["os"] is not null) // 操作系统
            {
                if (Rule["os"]["name"] is not null) // 操作系统名称
                {
                    var osName = Rule["os"]["name"].ToString();
                    if (osName == "unknown")
                    {
                    }
                    else if (osName == "windows")
                    {
                        if (Rule["os"]["version"] is not null) // 操作系统版本
                        {
                            var cr = Rule["os"]["version"].ToString();
                            isRightRule = isRightRule && osVersion.RegexCheck(cr);
                        }
                    }
                    else
                    {
                        isRightRule = false;
                    }
                }

                if (Rule["os"]["arch"] is not null) // 操作系统架构
                    isRightRule = isRightRule && Rule["os"]["arch"].ToString() == "x86" == SystemInfo.Is32BitSystem;
            }

            if (Rule["features"] is not null) // 标签
            {
                isRightRule = isRightRule && Rule["features"]["is_demo_user"] is null; // 反选是否为 Demo 用户
                if (Rule["features"].AsObject().Any(prop => prop.Key.Contains("quick_play")))
                    isRightRule = false; // 不开 Quick Play，让玩家自己加去
            }

            // 反选确认
            if (Rule["action"].ToString() == "allow")
            {
                if (isRightRule)
                    required = true; // allow
            }
            else if (isRightRule)
            {
                required = false; // disallow
            }
        }

        return required;
    }

    private static readonly string osVersion = Environment.OSVersion.Version.ToString();

    /// <summary>
    ///     递归获取 Minecraft 某一实例的完整支持库列表。
    /// </summary>
    public static List<McLibToken> McLibListGet(Instance instance, bool includeInstanceJar)
    {
        // 获取当前支持库列表
        ModBase.Log("[Minecraft] 获取支持库列表：" + instance.Name);
        var result = McLibListGetWithJson(instance.JsonObject, targetInstance: instance);

        // 需要添加原版 Jar
        if (includeInstanceJar)
        {
            Instance realInstance;
            var requiredJar = instance.JsonObject["jar"]?.ToString();
            if (instance.IsHmclFormatJson || requiredJar is null)
            {
                // HMCL 项直接使用自身的 Jar
                // 根据 Inherit 获取最深层实例
                var originalInstance = instance;
                // 1.17+ 的 Forge 不寻找 Inherit
                if (!((instance.Info.HasForge || instance.Info.HasNeoForge) && instance.Info.Drop >= 170))
                    while (!string.IsNullOrEmpty(originalInstance.InheritInstanceName))
                    {
                        if ((originalInstance.InheritInstanceName ?? "") == (originalInstance.Name ?? ""))
                            break;
                        originalInstance = new Instance(Path.Combine(mcFolderSelected, "versions", originalInstance.InheritInstanceName));
                    }

                // 需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                // 复现：启动一个 Snapshot 实例
                realInstance = new Instance(originalInstance.PathInstance);
            }
            else
            {
                // Json 已提供 Jar 字段，使用该字段的信息
                realInstance = new Instance(requiredJar);
            }

            string clientUrl;
            string clientSHA1;
            // 判断需求的实例是否存在
            // 不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            if (!File.Exists(realInstance.PathInstance + realInstance.Name + ".json"))
            {
                realInstance = instance;
                ModBase.Log("[Minecraft] 可能缺少前置实例 " + realInstance.Name + "，找不到对应的 JSON 文件", ModBase.LogLevel.Debug);
            }

            // 获取详细下载信息
            if (realInstance.JsonObject["downloads"] is not null &&
                realInstance.JsonObject["downloads"]["client"] is not null)
            {
                clientUrl = (string)realInstance.JsonObject["downloads"]["client"]["url"];
                clientSHA1 = (string)realInstance.JsonObject["downloads"]["client"]["sha1"];
            }
            else
            {
                clientUrl = null;
                clientSHA1 = null;
            }

            // 把所需的原版 Jar 添加进去
            result.Add(new McLibToken
            {
                LocalPath = realInstance.PathInstance + realInstance.Name + ".jar", size = 0L, IsNatives = false,
                Url = clientUrl, Sha1 = clientSHA1
            });
        }

        return result;
    }

    /// <summary>
    ///     获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
    public static List<McLibToken> McLibListGetWithJson(JsonObject jsonObject,
        bool keepSameNameDifferentVersionResult = false, string customMcFolder = null, Instance targetInstance = null)
    {
        customMcFolder = customMcFolder ?? mcFolderSelected;
        var basicArray = new List<McLibToken>();

        // 添加基础 Json 项
        var allLibs = (JsonArray)jsonObject["libraries"];

        // 转换为 LibToken
        foreach (var LibraryNode in allLibs)
        {
            var library = LibraryNode.AsObject();
            // 清理 null 项（BakaXL 会把没有的项序列化为 null；这导致了 #409）
            var keysToRemove = library.Where(p => p.Value?.GetValueKind() == JsonValueKind.Null).Select(p => p.Key).ToList();
            foreach (var key in keysToRemove)
                library.Remove(key);

            // 检查是否需要（Rules）
            if (!McJsonRuleCheck(library["rules"]))
                continue;

            // 获取根节点下的 url
            var rootUrl = (string)library["url"];
            if (rootUrl is not null)
                rootUrl += McLibGet((string)library["name"], false, true, customMcFolder).Replace(@"\", "/");

            // 是否为纯本地项
            var hint = (string)library["hint"];
            var isLocal = hint is not null ? hint == "local" : false;

            // 根据是否本地化处理（Natives）
            if (library["natives"] is null) // 没有 Natives
            {
                string localPath;
                if (isLocal && targetInstance is not null) // 纯本地项
                    localPath = targetInstance.PathInstance + @"libraries\" +
                                library["name"].ToString().AfterFirst(":").Replace(":", "-") + ".jar";
                else
                    localPath = McLibGet((string)library["name"], customMcFolder: customMcFolder);
                try
                {
                    if (library["downloads"] is not null && library["downloads"]["artifact"] is not null)
                    {
                        var init = new McLibToken();
                        basicArray.Add((init.OriginalName = (string)library["name"],
                            init.Url = (string)(rootUrl ?? library["downloads"]["artifact"]["url"]),
                            init.LocalPath = library["downloads"]["artifact"]["path"] is null
                                ? McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                : Path.Combine(customMcFolder, "libraries", library["downloads"]["artifact"]["path"].ToString()
                                    .Replace("/", @"\")),
                            init.size = (long)Math.Round(
                                ModBase.Val(library["downloads"]["artifact"]["size"].ToString())),
                            init.IsNatives = false, init.Sha1 = library["downloads"]["artifact"]["sha1"]?.ToString(),
                            init.IsLocal = isLocal, init).init);
                    }
                    else
                    {
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"], Url = rootUrl, LocalPath = localPath, size = 0L,
                            IsNatives = false, Sha1 = null, IsLocal = isLocal
                        });
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（无 Natives，" + (library["name"] ?? "Nothing") + "）");
                    basicArray.Add(new McLibToken
                    {
                        OriginalName = (string)library["name"], Url = rootUrl, LocalPath = localPath, size = 0L,
                        IsNatives = false, Sha1 = null
                    });
                }
            }
            else if (library["natives"]["windows"] is not null) // 有 Windows Natives
            {
                try
                {
                    if (library["downloads"] is not null && library["downloads"]["classifiers"] is not null &&
                        library["downloads"]["classifiers"]["natives-windows"] is not null)
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"],
                            Url = (string)(rootUrl ?? library["downloads"]["classifiers"]["natives-windows"]["url"]),
                            LocalPath = library["downloads"]["classifiers"]["natives-windows"]["path"] is null
                                ? McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                    .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                                    .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32")
                                : Path.Combine(customMcFolder, "libraries",
                                  library["downloads"]["classifiers"]["natives-windows"]["path"].ToString()
                                      .Replace("/", @"\")),
                            size = (long)Math.Round(
                                ModBase.Val(library["downloads"]["classifiers"]["natives-windows"]["size"].ToString())),
                            IsNatives = true,
                            Sha1 = library["downloads"]["classifiers"]["natives-windows"]["sha1"].ToString(),
                            IsLocal = isLocal
                        });
                    else
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"], Url = rootUrl,
                            LocalPath = McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                                .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                            size = 0L, IsNatives = true, Sha1 = null, IsLocal = isLocal
                        });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（有 Natives，" + (library["name"] ?? "Nothing") + "）");
                    basicArray.Add(new McLibToken
                    {
                        OriginalName = (string)library["name"], Url = rootUrl,
                        LocalPath = McLibGet((string)library["name"], customMcFolder: customMcFolder)
                            .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                            .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                        size = 0L, IsNatives = true, Sha1 = null, IsLocal = false
                    });
                }
            }
        }

        // 去重
        var resultArray = new Dictionary<string, McLibToken>();

        // 测试例：
        // D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
        // D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
        // D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
        string GetVersion(McLibToken token)
        {
            return ModBase.GetFolderNameFromPath(ModBase.GetPathFromFullPath(token.LocalPath));
        }

        for (int i = 0, loopTo = basicArray.Count - 1; i <= loopTo; i++)
        {
            var key = basicArray[i].Name + basicArray[i].IsNatives;
            if (resultArray.ContainsKey(key))
            {
                var basicArrayVersion = GetVersion(basicArray[i]);
                var resultArrayVersion = GetVersion(resultArray[key]);
                if ((basicArrayVersion ?? "") != (resultArrayVersion ?? "") && keepSameNameDifferentVersionResult)
                {
                    ModBase.Log(
                        $"[Minecraft] 发现疑似重复的支持库：{basicArray[i]} ({basicArrayVersion}) 与 {resultArray[key]} ({resultArrayVersion})");
                    resultArray.Add(key + ModBase.GetUuid(), basicArray[i]);
                }
                else
                {
                    ModBase.Log(
                        $"[Minecraft] 发现重复的支持库：{basicArray[i]} ({basicArrayVersion}) 与 {resultArray[key]} ({resultArrayVersion})，已忽略其中之一");
                    if (CompareVersionGe(basicArrayVersion, resultArrayVersion)) resultArray[key] = basicArray[i];
                }
            }
            else
            {
                resultArray.Add(key, basicArray[i]);
            }
        }

        return resultArray.Values.ToList();
    }

    /// <summary>
    ///     获取实例所需支持库文件的 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromInstance(Instance instance)
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
        var authlibTargetFile = Path.Combine(ModBase.pathPure, "authlib-injector.jar");
        JsonObject authlibDownloadInfo = null;
        try
        {
            ModBase.Log("[Minecraft] 开始获取 Authlib-Injector 下载信息");
            authlibDownloadInfo = (JsonObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(
                new[]
                {
                    "https://authlib-injector.yushi.moe/artifact/latest.json",
                    "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                }, isJson: true));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 Authlib-Injector 下载信息失败");
        }

        // 校验文件
        if (authlibDownloadInfo is not null)
        {
            var checker = new ModBase.FileChecker(hash: authlibDownloadInfo["checksums"]["sha256"].ToString());
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
                    new ModBase.FileChecker(hash: authlibDownloadInfo["checksums"]["sha256"].ToString())));
            }
        }

        // 修改渲染器
        var mesaLoaderWindowsTargetFile =
            Path.Combine(ModBase.pathPure, "mesa-loader-windows", ModLaunch.mesaLoaderWindowsVersion, "Loader.jar");
        var renderer = -1;
        if (McInstanceSelected is not null)
            renderer = Config.Instance.Renderer[McInstanceSelected?.PathInstance] - 1;
        if (renderer == -1) renderer = Config.Launch.Renderer;

        if (renderer != 0 && !File.Exists(mesaLoaderWindowsTargetFile))
        {
            var downloadAddress =
                "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/org/glavo/mesa-loader-windows/" +
                ModLaunch.mesaLoaderWindowsVersion + "/mesa-loader-windows-" + ModLaunch.mesaLoaderWindowsVersion + "-" +
                (SystemInfo.Is32BitSystem ? "x86" : SystemInfo.IsArm64System ? "arm64" : "x64") + ".jar";
            result.Add(new DownloadFile(new[] { downloadAddress }, mesaLoaderWindowsTargetFile));
        }

        // LabyMod Assets 文件
        if (instance.Info.HasLabyMod)
        {
            if ((instance.PathIndie ?? "") == (instance.PathInstance ?? ""))
            {
                if (Directory.Exists(Path.Combine(instance.PathInstance, "labymod-neo")))
                    Directory.Delete(Path.Combine(instance.PathInstance, "labymod-neo"), true);
                ModBase.CreateSymbolicLink(Path.Combine(instance.PathInstance, "labymod-neo"), Path.Combine(mcFolderSelected, "labymod-neo"),
                    0x2);
            }

            try
            {
                var channelType = instance.JsonObject["labymod_data"]["channelType"].ToString();
                Directory.CreateDirectory($@"{mcFolderSelected}labymod-neo\libraries");
                ModBase.Log("[Minecraft] 开始获取 LabyMod 信息");
                var labyManifest = (JsonObject)ModNet.NetGetCodeByRequestRetry(
                    $"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json", isJson: true);
                var labyAssets = (JsonObject)labyManifest["assets"];
                var labyModCommitRef = labyManifest["commitReference"].ToString();
                foreach (var Asset in labyAssets)
                {
                    var assetName = Asset.Key;
                    var assetSHA1 = Asset.Value.ToString();
                    var assetPath = $@"{mcFolderSelected}labymod-neo\assets\{assetName}.jar";
                    var assetUrl =
                        $"https://releases.r2.labymod.net/api/v1/download/assets/labymod4/{channelType}/{labyModCommitRef}/{assetName}/{assetSHA1}.jar";
                    var checker = new ModBase.FileChecker(hash: assetSHA1);
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
        if (ShouldIgnoreFileCheck(instance))
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
        customMcFolder = customMcFolder ?? mcFolderSelected;
        var result = new List<DownloadFile>();
        // 获取
        foreach (var token in libs)
        {
            // 检查文件
            var checker = new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: token.Sha1);
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
                        .Replace(token.Url.Substring(0, token.Url.IndexOfF("maven")),
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
                    token.LocalPath.Replace(Path.Combine(customMcFolder, "libraries", "optifine", "OptiFine") + @"\", "").Split("_")[0] + "/" +
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
                    $"[Download] 获取到 LabyMod 主要库文件的 Size = {token.size},SHA1 = {token.Sha1}，由于 LabyMod 乱写 Size，已忽略 Size");
                checker = new ModBase.FileChecker(hash: token.Sha1); // 只校验 SHA1
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
        string mcLibGetRet = default;
        customMcFolder = customMcFolder ?? mcFolderSelected;
        var splited = original.Split(":");
        mcLibGetRet = withHead
            ? Path.Combine(customMcFolder, "libraries", splited[0].Replace(".", @"\"), splited[1], splited[2], splited[1] + "-" + splited[2] + ".jar")
            : Path.Combine(splited[0].Replace(".", @"\"), splited[1], splited[2], splited[1] + "-" + splited[2] + ".jar");
        // 判断 OptiFine 是否应该使用 installer
        if (mcLibGetRet.Contains(@"optifine\OptiFine\1.") && splited[2].Split(".").Count() > 1)
        {
            var majorVersion = (int)Math.Round(ModBase.Val(splited[2].Split(".")[1].BeforeFirst("_")));
            var minorVersion = (int)Math.Round(splited[2].Split(".").Count() > 2
                ? ModBase.Val(splited[2].Split(".")[2].BeforeFirst("_"))
                : 0d);
            if ((majorVersion == 12 || (majorVersion == 20 && minorVersion >= 4) || majorVersion >= 21) && File.Exists(
                    $@"{customMcFolder}libraries\{splited[0].Replace(".", @"\")}\{splited[1]}\{splited[2]}\{splited[1]}-{splited[2]}-installer.jar")) // 仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
            {
                ModLaunch.McLaunchLog("已将 " + original + " 替换为对应的 Installer 文件");
                mcLibGetRet = mcLibGetRet.Replace(".jar", "-installer.jar");
            }
        }

        return mcLibGetRet;
    }

    /// <summary>
    ///     检查设置，是否应当忽略文件检查？
    /// </summary>
    public static bool ShouldIgnoreFileCheck(Instance version)
    {
        return Config.Instance.DisableAssetVerifyV2[version.PathInstance] ||
               Config.Instance.AssetVerifySolutionV1[version.PathInstance] == 2;
    }

    #endregion

    #region 资源文件（Assets）

    // 获取索引
    /// <summary>
    ///     获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
    /// </summary>
    public static JsonNode McAssetsGetIndex(Instance instance, bool returnLegacyOnError = false,
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
                instance = new Instance(Path.Combine(mcFolderSelected, "versions", instance.InheritInstanceName));
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
            return (JsonNode)ModBase.GetJson(@"{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }");
        }
        // End If

        throw new Exception(Lang.Text("Minecraft.Error.NoAssetIndexInfo"));
    }

    /// <summary>
    ///     获取某实例资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    /// </summary>
    public static string McAssetsGetIndexName(Instance instance)
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
                instance = new Instance(Path.Combine(mcFolderSelected, "versions", instance.InheritInstanceName));
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
        public string localPath;

        /// <summary>
        ///     Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        /// </summary>
        public string sourcePath;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long size;

        /// <summary>
        ///     文件的 Hash 校验码。
        /// </summary>
        public string hash;

        public override string ToString()
        {
            return ModBase.GetString(size) + " | " + localPath;
        }
    }

    private static string McAssetsHashPrefix(string hash)
    {
        return hash[..2];
    }

    private static string McAssetsUrl(string hash)
    {
        return $"https://resources.download.minecraft.net/{McAssetsHashPrefix(hash)}/{hash}";
    }

    /// <summary>
    ///     获取 Minecraft 的资源文件列表。失败会抛出异常。
    /// </summary>
    private static List<McAssetsToken> McAssetsListGet(Instance instance)
    {
        var indexName = McAssetsGetIndexName(instance);
        try
        {
            // 初始化
            if (!File.Exists($@"{mcFolderSelected}assets\indexes\{indexName}.json"))
                throw new FileNotFoundException(Lang.Text("Minecraft.Error.AssetIndexNotFound"),
                    Path.Combine(mcFolderSelected, "assets", "indexes", indexName + ".json"));
            var result = new List<McAssetsToken>();
            var json = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile($@"{mcFolderSelected}assets\indexes\{indexName}.json"));

            // 读取列表
            foreach (var file in json["objects"].AsObject())
            {
                string localPath;
                var hash = file.Value["hash"].ToString();
                if (json["map_to_resources"] is not null && json["map_to_resources"].ToObject<bool>())
                    // Remap
                    localPath = Path.Combine(instance.PathIndie, "resources", file.Key.Replace("/", @"\"));
                else if (json["virtual"] is not null && json["virtual"].ToObject<bool>())
                    // Virtual
                    localPath = Path.Combine(mcFolderSelected, "assets", "virtual", "legacy", file.Key.Replace("/", @"\"));
                else
                {
                    // 正常
                    localPath = Path.Combine(mcFolderSelected, "assets", "objects", McAssetsHashPrefix(hash), hash);
                }
                result.Add(new McAssetsToken
                {
                    localPath = localPath,
                    sourcePath = file.Key,
                    hash = hash,
                    size = long.Parse(file.Value["size"].ToString())
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
    public static List<DownloadFile> McAssetsFixList(Instance instance, bool checkHash,
        [Optional] ref ModLoader.LoaderBase progressFeed)
    {
        // 如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
        if (checkHash)
            return McAssetsListGet(instance).Select(token =>
            {
                var hash = token.hash;
                return new DownloadFile(
                    ModDownload.DlSourceAssetsGet(McAssetsUrl(hash)),
                    token.localPath,
                    new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: hash));
            }).ToList();
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
                var file = new FileInfo(token.localPath);
                if (file.Exists && (token.size == 0L || token.size == file.Length))
                    continue;
                // 文件不存在，添加下载
                var hash = token.hash;
                result.Add(new DownloadFile(
                    ModDownload.DlSourceAssetsGet(McAssetsUrl(hash)),
                    token.localPath,
                    new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: hash)));
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
