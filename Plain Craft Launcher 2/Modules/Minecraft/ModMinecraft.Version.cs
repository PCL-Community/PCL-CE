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
    #region 实例处理

    public const int McInstanceCacheVersion = 30;

    private static McInstance _mcInstanceSelected;
    private static object _McInstanceSelected_mcInstanceSelectedLast = 0; // 为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化

    /// <summary>
    ///     当前的 Minecraft 版本。
    /// </summary>
    public static McInstance McInstanceSelected
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
            PageDownloadCompDetail.CachedFolder.Clear();
        }
    }

    private static bool _JsonVersion_jsonVersionInited;

    public class McInstance
    {
        private McInstanceInfo _info;
        private string _inheritInstanceName;
        private JObject _jsonObject;
        private string _jsonText;
        private JObject _jsonVersion;
        private string _name;

        /// <summary>
        ///     显示的描述文本。
        /// </summary>
        public string Desc = "该实例未被加载，请向作者反馈此问题";

        /// <summary>
        ///     强制实例分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        /// </summary>
        public McInstanceCardType DisplayType = McInstanceCardType.Auto;

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
        public DateTime ReleaseTime = new(1970, 1, 1, 15, 0, 0);

        /// <summary>
        ///     该实例的列表检查原始结果，不受自定义影响。
        /// </summary>
        public McInstanceState State = McInstanceState.Error;

        /// <summary></summary>
        /// <param name="name">实例名，或实例文件夹的完整路径（不规定是否以 \ 结尾）。</param>
        public McInstance(string name)
        {
            PathInstance = (name.Contains(":") ? "" : McFolderSelected + @"versions\") + name +
                           (name.EndsWithF(@"\") ? "" : @"\"); // 补全完整路径
            // 补全右划线
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
                            LauncherLogger.Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的实例独立设置中迁移");
                            return Config.Instance.IndieV1[PathInstance] == 1;
                        }

                        // 若实例文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                        var ModFolder = new DirectoryInfo(PathInstance + @"mods\");
                        var SaveFolder = new DirectoryInfo(PathInstance + @"saves\");
                        if ((ModFolder.Exists && ModFolder.EnumerateFiles().Any()) ||
                            (SaveFolder.Exists && SaveFolder.EnumerateDirectories().Any()))
                        {
                            LauncherLogger.Log($"[Minecraft] 版本隔离初始化（{Name}）：实例文件夹下存在 mods 或 saves 文件夹，自动开启");
                            return true;
                        }

                        // 根据全局的默认设置决定是否隔离
                        var IsRelease = State != McInstanceState.Fool && State != McInstanceState.Old &&
                                        State != McInstanceState.Snapshot;
                        LauncherLogger.Log(
                            $"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{Config.Launch.IndieSolutionV2}）判断，State {LauncherText.GetStringFromEnum(State)}，IsRelease {IsRelease}，Modable {Modable}");
                        
                        return Config.Launch.IndieSolutionV2 switch
                        {
                            0 => false, // 关闭
                            1 => Info.HasLabyMod || Modable, // 仅隔离可安装 Mod 的实例
                            2 => !IsRelease, // 仅隔离非正式版
                            3 => Info.HasLabyMod || Modable || !IsRelease, // 隔离非正式版与可安装 Mod 的实例
                            _ => true // 隔离所有实例
                        };
                    }
                    
                    Config.Instance.IndieV2[PathInstance] = ShouldBeIndie();
                }

                return Config.Instance.IndieV2[PathInstance] ? PathInstance : McFolderSelected;
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
                    _name = LauncherPaths.GetFolderName(PathInstance);
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
                       Info.HasNeoForge || Info.HasCleanroom || DisplayType == McInstanceCardType.API; // #223
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
                            ReleaseTime = new DateTime(1970, 1, 1, 15, 0, 0); // 未知版本也可能显示为 1970 年
                        else
                            ReleaseTime = JsonObject["releaseTime"].ToObject<DateTime>();
                        if (ReleaseTime.Year > 2000 && ReleaseTime.Year < 2013)
                        {
                            _info.VanillaName = "Old";
                            goto VersionSearchFinish;
                        }
                    }
                    catch
                    {
                        ReleaseTime = new DateTime(1970, 1, 1, 15, 0, 0);
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
                        foreach (JObject patch in JsonObject["patches"])
                            if ((patch["id"] ?? "").ToString() == "game" && patch["version"] is not null)
                            {
                                _info.VanillaName = patch["version"].ToString();
                                goto VersionSearchFinish;
                            }

                    // 从 Forge / NeoForge / LabyMod Arguments 中获取版本号
                    if (JsonObject["arguments"] is not null)
                    {
                        if (JsonObject["arguments"]["game"] is not null)
                        {
                            var Mark = false;
                            foreach (var Argument in JsonObject["arguments"]["game"])
                            {
                                if (Mark)
                                {
                                    _info.VanillaName = Argument.ToString();
                                    goto VersionSearchFinish;
                                }

                                if (Argument.ToString() == "--fml.mcVersion")
                                    Mark = true;
                            }
                        }

                        if (JsonObject["arguments"]["jvm"] is not null)
                            foreach (var Argument in JsonObject["arguments"]["game"])
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
                            LauncherLogger.Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" + jsonVerName);
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
                    LauncherLogger.Log("[Minecraft] 无法完全确认 MC 版本号的版本：" + Name);
                    _info.Reliable = false;
                    // 从文件夹名中获取
                    regex = Name.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 JSON 出现的版本号中获取
                    var JsonRaw = (JObject)JsonObject.DeepClone();
                    JsonRaw.Remove("libraries");
                    var JsonRawText = JsonRaw.ToString();
                    regex = JsonRawText.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        _info.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 无法获取
                    _info.VanillaName = "Unknown";
                    Desc = "PCL 无法识别该版本的 MC 版本号";
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log(ex, "识别 Minecraft 版本时出错");
                    _info.VanillaName = "Unknown";
                    Desc = "无法识别：" + ex.Message;
                }

                #endregion

                VersionSearchFinish: ;

                _info.VanillaName = _info.VanillaName.Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
                // 获取版本号
                if (_info.VanillaName.StartsWithF("1."))
                {
                    var segments = _info.VanillaName.Split(" _-.".ToCharArray());
                    _info.Vanilla = new Version((int)Math.Round(MigrationHelpers.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        0, (int)Math.Round(MigrationHelpers.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else if (_info.VanillaName.RegexCheck(@"^[2-9][0-9]\."))
                {
                    var segments = _info.VanillaName.Split(" _-.".ToCharArray());
                    _info.Vanilla = new Version((int)Math.Round(MigrationHelpers.Val(segments[0])),
                        (int)Math.Round(MigrationHelpers.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        (int)Math.Round(MigrationHelpers.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else
                {
                    _info.Vanilla = new Version(9999, 0, 0);
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
                bool FastJsonCheck(string Json)
                {
                    var TrimedJson = Json.Trim();
                    return TrimedJson.StartsWithF("{") && TrimedJson.EndsWithF("}");
                }

                ;
                if (_jsonText is null)
                {
                    var JsonPath = PathInstance + Name + ".json";
                    if (!File.Exists(JsonPath))
                    {
                        // 如果文件夹下只有一个 JSON 文件，则将其作为实例 JSON
                        var JsonFiles = Directory.GetFiles(PathInstance, "*.json");
                        if (JsonFiles.Count() == 1)
                        {
                            JsonPath = JsonFiles[0];
                            LauncherLogger.Log("[Minecraft] 未找到同名实例 JSON，自动换用 " + JsonPath, LauncherLogger.LogLevel.Debug);
                        }
                        else
                        {
                            throw new Exception($"未找到实例 JSON 文件：{PathInstance}{Name}.json");
                        }
                    }

                    _jsonText = LauncherFileSystem.ReadFile(JsonPath);
                    // 如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    if (!FastJsonCheck(_jsonText))
                    {
                        if (LauncherDispatcher.RunInUi())
                        {
                            LauncherLogger.Log("[Minecraft] 实例 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试", LauncherLogger.LogLevel.Debug);
                            LauncherSerialization.GetJson(_jsonText); // 触发异常
                        }
                        else
                        {
                            LauncherLogger.Log($"[Minecraft] 实例 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）", LauncherLogger.LogLevel.Debug);
                            Thread.Sleep(2000);
                            _jsonText = LauncherFileSystem.ReadFile(JsonPath);
                            if (!FastJsonCheck(_jsonText))
                                LauncherSerialization.GetJson(_jsonText);
                        } // 触发异常
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
        public JObject JsonObject
        {
            get
            {
                if (_jsonObject is null)
                {
                    var Text = JsonText; // 触发 JsonText 的 Get 事件
                    try
                    {
                        _jsonObject = (JObject)LauncherSerialization.GetJson(Text);
                        // 转换 HMCL 关键项
                        if (_jsonObject.ContainsKey("patches") && !_jsonObject.ContainsKey("time"))
                        {
                            IsHmclFormatJson = true;
                            // 合并 JSON
                            // Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            JObject CurrentObject = null;
                            var SubjsonList = new List<JObject>();
                            foreach (JObject Subjson in _jsonObject["patches"])
                                SubjsonList.Add(Subjson);
                            SubjsonList.Sort((left, right) =>
                                MigrationHelpers.Val((left["priority"] ?? "0").ToString()) <
                                MigrationHelpers.Val((right["priority"] ?? "0").ToString()));
                            foreach (var Subjson in SubjsonList)
                            {
                                var Id = (string)Subjson["id"];
                                if (Id is not null)
                                {
                                    // 合并 JSON
                                    LauncherLogger.Log("[Minecraft] 合并 HMCL 分支项：" + Id);
                                    if (CurrentObject is not null)
                                        CurrentObject.Merge(Subjson);
                                    else
                                        CurrentObject = Subjson;
                                }
                                else
                                {
                                    LauncherLogger.Log("[Minecraft] 存在为空的 HMCL 分支项");
                                }
                            }

                            _jsonObject = CurrentObject;
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
                                if (Conversions.ToBoolean(
                                        Operators.ConditionalCompareObjectEqual(inheritInstanceName, Name, false)))
                                {
                                    LauncherLogger.Log("[Minecraft] 自引用的继承实例：" + Name, LauncherLogger.LogLevel.Debug);
                                    inheritInstanceName = "";
                                    break;
                                }

                                Recheck: ;

                                if (Conversions.ToBoolean(
                                        Operators.ConditionalCompareObjectNotEqual(inheritInstanceName, "", false)))
                                {
                                    var inheritInstance = new McInstance(Conversions.ToString(inheritInstanceName));
                                    // 继续循环
                                    if (Conversions.ToBoolean(
                                            Operators.ConditionalCompareObjectEqual(inheritInstance.InheritInstanceName,
                                                inheritInstanceName, false)))
                                        throw new Exception(Conversions.ToString(
                                            Operators.ConcatenateObject("版本依赖项出现嵌套：", inheritInstanceName)));
                                    inheritInstanceName = inheritInstance.InheritInstanceName;
                                    // 合并
                                    inheritInstance.JsonObject.Merge(_jsonObject);
                                    _jsonObject = inheritInstance.JsonObject;
                                    goto Recheck;
                                }
                            }
                            catch (Exception ex)
                            {
                                LauncherLogger.Log(ex, "合并实例依赖项 JSON 失败（" + (inheritInstanceName ?? "null") + "）");
                            }
                        } while (false);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("初始化实例 JSON 时失败（" + (Name ?? "null") + "）", ex);
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
        public JObject JsonVersion
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
                                        _jsonVersion = (JObject)LauncherSerialization.GetJson(versionJsonStream.ReadToEnd());
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            LauncherLogger.Log(ex, $"从实例 JAR 中读取 version.json 失败 ({PathInstance}{Name}.jar)");
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
                State = McInstanceState.Error;
                Desc = "未找到实例 " + Name;
                return false;
            }

            // 检查权限
            try
            {
                Directory.CreateDirectory(PathInstance + @"PCL\");
                LauncherFileSystem.CheckPermissionWithException(PathInstance + @"PCL\");
            }
            catch (Exception ex)
            {
                State = McInstanceState.Error;
                Desc = "PCL 没有对该文件夹的访问权限，请右键以管理员身份运行 PCL";
                LauncherLogger.Log(ex, "没有访问实例文件夹的权限");
                return false;
            }

            // 确认 JSON 可用性
            try
            {
                var jsonObjCheck = JsonObject;
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "实例 JSON 可用性检查失败（" + PathInstance + "）");
                JsonText = "";
                JsonObject = null;
                Desc = ex.Message;
                State = McInstanceState.Error;
                return false;
            }

            // 检查版本号获取
            try
            {
                if (string.IsNullOrEmpty(Info.VanillaName))
                    throw new Exception("无法获取版本号，结果为空");
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "版本号获取失败（" + Name + "）");
                State = McInstanceState.Error;
                Desc = "版本号获取失败：" + ex;
                return false;
            }

            // 检查依赖实例
            try
            {
                if (!string.IsNullOrEmpty(InheritInstanceName))
                    if (!File.Exists(LauncherPaths.GetDirectoryFromPath(PathInstance) + InheritInstanceName + @"\" +
                                     InheritInstanceName + ".json"))
                    {
                        State = McInstanceState.Error;
                        Desc = "需要安装 " + InheritInstanceName + " 作为前置实例";
                        return false;
                    }
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "依赖实例检查出错（" + Name + "）");
                State = McInstanceState.Error;
                Desc = "未知错误：" + ex;
                return false;
            }

            State = McInstanceState.Original;
            return true;
        }

        /// <summary>
        ///     加载 Minecraft 实例的详细信息。不使用其缓存，且会更新缓存。
        /// </summary>
        public McInstance Load()
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
                        State = McInstanceState.Error;
                        break;
                    }
                    case "Old":
                    {
                        State = McInstanceState.Old; // 根据 API 进行筛选
                        break;
                    }

                    default:
                    {
                        var realJson = JsonObject != null ? JsonObject.ToString() : JsonText;
                        // 愚人节与快照版本
                        if ((JsonObject["type"] ?? "").ToString() == "fool" ||
                            !string.IsNullOrEmpty(GetMcFoolName(Info.VanillaName)))
                            State = McInstanceState.Fool;
                        else if (IsSnapshot()) State = McInstanceState.Snapshot;
                        // OptiFine
                        if (realJson.Contains("optifine"))
                        {
                            State = McInstanceState.OptiFine;
                            Info.HasOptiFine = true;
                            Info.OptiFine = realJson.RegexSeek(RegexPatterns.OptiFineVersion) ?? "未知版本";
                        }

                        // LiteLoader
                        if (realJson.Contains("liteloader"))
                        {
                            State = McInstanceState.LiteLoader;
                            Info.HasLiteLoader = true;
                        }

                        // Fabric、Forge、Quilt、LabyMod、Legacy Fabric
                        if (realJson.Contains("labymod_data"))
                        {
                            State = McInstanceState.LabyMod;
                            Info.HasLabyMod = true;
                            Info.LabyMod = (string)JsonObject["labymod_data"]["version"];
                        }
                        else if (realJson.Contains("net.legacyfabric:intermediary"))
                        {
                            State = McInstanceState.LegacyFabric;
                            Info.HasLegacyFabric = true;
                            Info.LegacyFabric =
                                (realJson.RegexSeek(RegexPatterns.LegacyFabricVersion) ?? "未知版本")
                                .Replace("+build", "");
                        }
                        else if (realJson.Contains("net.fabricmc:fabric-loader"))
                        {
                            State = McInstanceState.Fabric;
                            Info.HasFabric = true;
                            Info.Fabric =
                                (realJson.RegexSeek(RegexPatterns.FabricVersion) ?? "未知版本").Replace("+build", "");
                        }
                        else if (realJson.Contains("org.quiltmc:quilt-loader"))
                        {
                            State = McInstanceState.Quilt;
                            Info.HasQuilt = true;
                            Info.Quilt =
                                (realJson.RegexSeek(RegexPatterns.QuiltVersion) ?? "未知版本").Replace("+build", "");
                        }
                        else if (realJson.Contains("com.cleanroommc:cleanroom:"))
                        {
                            State = McInstanceState.Cleanroom;
                            Info.HasCleanroom = true;
                            Info.Cleanroom =
                                (realJson.RegexSeek(RegexPatterns.CleanroomVersion) ?? "未知版本").Replace("+build", "");
                        }
                        else if (realJson.Contains("minecraftforge") && !realJson.Contains("net.neoforge"))
                        {
                            State = McInstanceState.Forge;
                            Info.HasForge = true;
                            Info.Forge = realJson.RegexSeek(RegexPatterns.ForgeMainVersion);
                            if (Info.Forge is null)
                                Info.Forge = realJson.RegexSeek(RegexPatterns.ForgeLibVersion) ?? "未知版本";
                        }
                        else if (realJson.Contains("net.neoforge"))
                        {
                            // 1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                            // 1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                            State = McInstanceState.NeoForge;
                            Info.HasNeoForge = true;
                            Info.NeoForge = realJson.RegexSeek(RegexPatterns.NeoForgeVersion) ?? "未知版本";
                        }

                        break;
                    }
                }

                #endregion

                ExitDataLoad: ;

                // 确定实例图标
                Logo = States.Instance.LogoPath[PathInstance];
                if (string.IsNullOrEmpty(Logo) || !States.Instance.IsLogoCustom[PathInstance])
                    switch (State)
                    {
                        case McInstanceState.Original:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Grass.png";
                            break;
                        }
                        case McInstanceState.Snapshot:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/CommandBlock.png";
                            break;
                        }
                        case McInstanceState.Old:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/CobbleStone.png";
                            break;
                        }
                        case McInstanceState.Forge:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Anvil.png";
                            break;
                        }
                        case McInstanceState.NeoForge:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/NeoForge.png";
                            break;
                        }
                        case McInstanceState.Cleanroom:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Cleanroom.png";
                            break;
                        }
                        case McInstanceState.Fabric:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Fabric.png";
                            break;
                        }
                        case McInstanceState.LegacyFabric:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Fabric.png";
                            break;
                        }
                        case McInstanceState.Quilt:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Quilt.png";
                            break;
                        }
                        case McInstanceState.OptiFine:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/GrassPath.png";
                            break;
                        }
                        case McInstanceState.LiteLoader:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/Egg.png";
                            break;
                        }
                        case McInstanceState.Fool:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/GoldBlock.png";
                            break;
                        }
                        case McInstanceState.LabyMod:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/LabyMod.png";
                            break;
                        }

                        default:
                        {
                            Logo = LauncherEnvironment.PathImage + "Blocks/RedstoneBlock.png";
                            break;
                        }
                    }

                // 确定实例描述
                if (State == McInstanceState.Error)
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
                DisplayType = (McInstanceCardType)Conversions.ToInteger(States.Instance.CardType[PathInstance]);
                // 写入缓存
                if (Directory.Exists(PathInstance))
                {
                    States.Instance.State[PathInstance] = (int)State;
                    States.Instance.Info[PathInstance] = Desc;
                    States.Instance.LogoPath[PathInstance] = Logo;
                }

                if (State != McInstanceState.Error)
                {
                    States.Instance.ReleaseTime[PathInstance] = ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm");
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
                    States.Instance.VanillaVersion[PathInstance] = Info.Vanilla.ToString();
                }
            }
            catch (Exception ex)
            {
                Desc = "未知错误：" + ex;
                Logo = LauncherEnvironment.PathImage + "Blocks/RedstoneBlock.png";
                State = McInstanceState.Error;
                LauncherLogger.Log(ex, "加载实例失败（" + Name + "）", LauncherLogger.LogLevel.Feedback);
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
            var ModLoaderInfo = "";
            if (this.Info.HasForge)
                ModLoaderInfo += ", Forge" + (this.Info.Forge == "未知版本" ? "" : " " + this.Info.Forge);
            if (this.Info.HasNeoForge)
                ModLoaderInfo += ", NeoForge" + (this.Info.NeoForge == "未知版本" ? "" : " " + this.Info.NeoForge);
            if (this.Info.HasCleanroom)
                ModLoaderInfo += ", Cleanroom" + (this.Info.Cleanroom == "未知版本" ? "" : " " + this.Info.Cleanroom);
            if (this.Info.HasLabyMod)
                ModLoaderInfo += ", LabyMod" + (this.Info.LabyMod == "未知版本" ? "" : " " + this.Info.LabyMod);
            if (this.Info.HasFabric)
                ModLoaderInfo += ", Fabric" + (this.Info.Fabric == "未知版本" ? "" : " " + this.Info.Fabric);
            if (this.Info.HasQuilt)
                ModLoaderInfo += ", Quilt" + (this.Info.Quilt == "未知版本" ? "" : " " + this.Info.Quilt);
            if (this.Info.HasLegacyFabric)
                ModLoaderInfo += ", Legacy Fabric" +
                                 (this.Info.LegacyFabric == "未知版本" ? "" : " " + this.Info.LegacyFabric);
            if (this.Info.HasOptiFine)
                ModLoaderInfo += ", OptiFine" + (this.Info.OptiFine == "未知版本"
                    ? ""
                    : " " + this.Info.OptiFine.Replace("-", " ").Replace("_", " "));
            if (this.Info.HasLiteLoader)
                ModLoaderInfo += ", LiteLoader";
            // 基础信息
            string Info;
            switch (State)
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
                        Info = "预发布版 " + this.Info.VanillaName;
                    else if (this.Info.VanillaName.ContainsF("rc", true))
                        Info = "发布候选 " + this.Info.VanillaName;
                    else if (this.Info.VanillaName.Contains("experimental"))
                        Info = "实验性快照" + this.Info.VanillaName;
                    else if (this.Info.VanillaName == "pending")
                        Info = "实验性快照";
                    else if (IsSnapshot())
                        Info = this.Info.Reliable ? "快照版 " + this.Info.VanillaName.Replace("-snapshot", "") : "快照版";
                    else
                        Info = this.Info.Reliable ? "正式版 " + this.Info.VanillaName : "正式版";

                    break;
                }
                case McInstanceState.Old:
                {
                    Info = "远古版本";
                    break;
                }
                case McInstanceState.Fool:
                {
                    Info = "愚人节版本 " + this.Info.VanillaName;
                    break;
                }
                case McInstanceState.Error:
                {
                    return Desc; // 已有错误信息
                }

                default:
                {
                    return "发生了未知错误，请向作者反馈此问题";
                }
            }

            return (Info + ModLoaderInfo).Replace("_", "-");
        }

        // 运算符支持
        public override bool Equals(object obj)
        {
            var instance = obj as McInstance;
            return instance is not null && (PathInstance ?? "") == (instance.PathInstance ?? "");
        }

        public static bool operator ==(McInstance? a, McInstance? b)
        {
            if (a is null && b is null)
                return true;
            if (a is null || b is null)
                return false;
            return (a.PathInstance ?? "") == (b.PathInstance ?? "");
        }

        public static bool operator !=(McInstance a, McInstance b)
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
        public Version Vanilla;

        // 原版

        /// <summary>
        ///     原版版本名。
        ///     如 26.1，26.1-snapshot-1，1.12.2，16w01a。
        /// </summary>
        public string VanillaName;

        /// <summary>
        ///     原版版本号是否有效。
        /// </summary>
        public bool Valid => Vanilla.Major < 1000;

        /// <summary>
        ///     可供比较的原版 Drop 序数。
        ///     例如 26.3.2 为 263，1.21.5 为 210。
        ///     若没有版本号，例如旧快照，则直接指定为 209。
        /// </summary>
        public int Drop => Valid ? Vanilla.Major * 10 + Vanilla.Minor : 209;

        /// <summary>
        ///     可供比较的 OptiFine 版本序数。
        /// </summary>
        public int OptiFineCode
        {
            get
            {
                if (string.IsNullOrEmpty(OptiFine) || OptiFine == "未知版本")
                    return 0;
                // 字母编号，如 G2 中的 G（7）
                var result = Strings.Asc(OptiFine.ToUpper().First()) - Strings.Asc('A') + 1;
                // 末尾数字，如 C5 beta4 中的 5
                result *= 100;
                result = (int)Math.Round(result +
                                         MigrationHelpers.Val(Strings.Right(OptiFine, OptiFine.Length - 1).RegexSeek("[0-9]+")));
                // 测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
                result *= 100;
                if (OptiFine.ContainsF("pre", true))
                    result += 50;
                if (OptiFine.ContainsF("pre", true) || OptiFine.ContainsF("beta", true))
                {
                    if (MigrationHelpers.Val(Strings.Right(OptiFine, 1)) == 0d && Strings.Right(OptiFine, 1) != "0")
                        result += 1; // 为 pre 或 beta 结尾，视作 1
                    else
                        result =
                            (int)Math.Round(result +
                                            MigrationHelpers.Val(OptiFine.ToLower().RegexSeek("(?<=((pre)|(beta)))[0-9]+")));
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
                if ((string.IsNullOrEmpty(Forge) || Forge == "未知版本") &&
                    (string.IsNullOrEmpty(NeoForge) || NeoForge == "未知版本"))
                    return 0;
                var segments = (HasForge ? Forge : NeoForge).RegexSearch(@"\d+");
                switch (segments.Count)
                {
                    case var @case when @case > 4:
                    {
                        return (int)Math.Round(MigrationHelpers.Val(segments[0]) * 1000000d + MigrationHelpers.Val(segments[1]) * 10000d +
                                               MigrationHelpers.Val(segments[3]));
                    }
                    case 3:
                    {
                        return (int)Math.Round(MigrationHelpers.Val(segments[0]) * 1000000d + MigrationHelpers.Val(segments[1]) * 10000d +
                                               MigrationHelpers.Val(segments[2]));
                    }
                    case 2:
                    {
                        return (int)Math.Round(MigrationHelpers.Val(segments[0]) * 1000000d + MigrationHelpers.Val(segments[1]) * 10000d);
                    }

                    default:
                    {
                        return (int)Math.Round(MigrationHelpers.Val(segments[0]) * 1000000d);
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
            string ToStringRet = default;
            ToStringRet = "";
            if (HasForge)
                ToStringRet += ", Forge" + (Forge == "未知版本" ? "" : " " + Forge);
            if (HasNeoForge)
                ToStringRet += ", NeoForge" + (NeoForge == "未知版本" ? "" : " " + NeoForge);
            if (HasCleanroom)
                ToStringRet += ", Cleanroom" + (Cleanroom == "未知版本" ? "" : " " + Cleanroom);
            if (HasFabric)
                ToStringRet += ", Fabric" + (Fabric == "未知版本" ? "" : " " + Fabric);
            if (HasLegacyFabric)
                ToStringRet += ", LegacyFabric" + (LegacyFabric == "未知版本" ? "" : " " + LegacyFabric);
            if (HasQuilt)
                ToStringRet += ", Quilt" + (Quilt == "未知版本" ? "" : " " + Quilt);
            if (HasLabyMod)
                ToStringRet += ", LabyMod" + (LabyMod == "未知版本" ? "" : " " + LabyMod);
            if (HasOptiFine)
                ToStringRet += ", OptiFine" + (OptiFine == "未知版本" ? "" : " " + OptiFine);
            if (HasLiteLoader)
                ToStringRet += ", LiteLoader";
            if (string.IsNullOrEmpty(ToStringRet)) return "原版 " + VanillaName;

            return VanillaName + ToStringRet;
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
            if (MigrationHelpers.Val(version.RegexSeek(@"^[2-9]\d\.\d+")) > 25d)
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
            var major = (int)Math.Round(MigrationHelpers.Val(segments[0]));
            var minor = (int)Math.Round(MigrationHelpers.Val(segments[1]));
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
    public static string GetMcFoolName(string name)
    {
        name = name.ToLower();
        if (name.StartsWithF("2.0") || name.StartsWithF("2point0"))
        {
            var tag = "";
            if (name.EndsWith("red"))
                tag = "（红色版本）";
            else if (name.EndsWith("blue"))
                tag = "（蓝色版本）";
            else if (name.EndsWith("purple")) tag = "（紫色版本）";
            return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！" + tag;
        }

        if (name == "15w14a") return "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。";

        if (name == "1.rv-pre1") return "2016 | 是时候将现代科技带入 Minecraft 了！";

        if (name == "3d shareware v1.34") return "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！";

        if (name.StartsWithF("20w14inf") || name == "20w14∞") return "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！";

        if (name == "22w13oneblockatatime") return "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！";

        if (name == "23w13a_or_b") return "2023 | 研究表明：玩家喜欢作出选择——越多越好！";

        if (name == "24w14potato") return "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！";

        if (name == "25w14craftmine") return "2025 | 你可以合成任何东西——包括合成你的世界！";

        if (name == "26w14a") return "2026 | 为什么需要物品栏？让方块们跟着你走吧！";

        return "";
    }

    /// <summary>
    ///     当前按卡片分类的所有版本列表。
    /// </summary>
    public static Dictionary<McInstanceCardType, List<McInstance>> McInstanceList = new();

    #endregion

    #region 实例列表加载

    /// <summary>
    ///     是否要求本次加载强制刷新实例列表。
    /// </summary>
    public static bool McInstanceListForceRefresh;

    /// <summary>
    ///     是否为本次打开 PCL 后第一次加载实例列表。
    ///     这会清理所有 .pclignore 文件，而非跳过这些对应实例。
    /// </summary>
    private static bool _isFirstMcInstanceListLoad = true;

    /// <summary>
    ///     加载 Minecraft 文件夹的实例列表。
    /// </summary>
    public static ModLoader.LoaderTask<string, int> McInstanceListLoader =
        new("Minecraft Instance List", InitMcInstanceList) { ReloadTimeout = 1 };

    private static void InitMcInstanceList(ModLoader.LoaderTask<string, int> loader)
    {
        var path = loader.Input;
        try
        {
            // 初始化
            McInstanceList = new Dictionary<McInstanceCardType, List<McInstance>>();
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
                    throw new Exception($"无法读取实例文件夹，可能是由于没有权限（{versionsPath}）", ex);
                }

            // 如果没有可用实例，清空缓存并跳过后续处理
            if (!folderList.Any())
            {
                LauncherSerialization.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", "");
                McInstanceSelected = null;
                States.Game.SelectedInstance = "";
                LauncherLogger.Log("[Minecraft] 未找到可用 Minecraft 实例");
                return;
            }

            // 根据文件夹名列表生成辨识码
            var folderListHash = LauncherHash.GetHash(McInstanceCacheVersion + "#" + string.Join("#", folderList));
            var folderListCheck = (int)(folderListHash % (int.MaxValue - 1));

            // 尝试使用缓存
            var useCache = !McInstanceListForceRefresh &&
                           MigrationHelpers.Val(LauncherSerialization.ReadIni(Path.Combine(path, "PCL.ini"), "InstanceCache")) ==
                           folderListCheck;

            if (useCache)
            {
                var cachedResult = InitMcInstanceListWithCache(path);
                if (cachedResult != null)
                    McInstanceList = cachedResult;
                else
                    useCache = false; // 缓存无效，需要重载
            }

            // 如果不能使用缓存，重新加载
            if (!useCache)
            {
                McInstanceListForceRefresh = false;
                LauncherLogger.Log("[Minecraft] 文件夹列表变更或缓存无效，重载所有实例");
                LauncherSerialization.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", folderListCheck.ToString());
                McInstanceList = InitMcInstanceListWithoutCache(path);
            }

            _isFirstMcInstanceListLoad = false;

            if (loader.IsAborted)
                return;

            // 尝试读取已储存的选择
            var savedSelection = LauncherSerialization.ReadIni(Path.Combine(path, "PCL.ini"), "Version");
            if (!string.IsNullOrEmpty(savedSelection))
                foreach (var card in McInstanceList)
                foreach (var instance in card.Value)
                    if ((instance.Name ?? "") == savedSelection && instance.State != McInstanceState.Error)
                    {
                        McInstanceSelected = instance;
                        States.Game.SelectedInstance = McInstanceSelected.Name;
                        LauncherLogger.Log("[Minecraft] 选择该文件夹储存的 Minecraft 实例：" + McInstanceSelected.PathInstance);
                        return;
                    }

            // 自动选择第一项
            var firstInstance = McInstanceList
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(i => i.State != McInstanceState.Error);

            if (firstInstance != null)
            {
                McInstanceSelected = firstInstance;
                States.Game.SelectedInstance = McInstanceSelected.Name;
                LauncherLogger.Log("[Launch] 自动选择 Minecraft 实例：" + McInstanceSelected.PathInstance);
            }
            else
            {
                McInstanceSelected = null;
                States.Game.SelectedInstance = "";
                LauncherLogger.Log("[Minecraft] 未找到可用 Minecraft 实例");
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
            LauncherSerialization.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", ""); // 要求下次重新加载
            LauncherLogger.Log(ex, "加载 .minecraft 实例列表失败", LauncherLogger.LogLevel.Feedback);
        }
    }

    // 获取实例列表
    private static Dictionary<McInstanceCardType, List<McInstance>> InitMcInstanceListWithCache(string path)
    {
        var results = new Dictionary<McInstanceCardType, List<McInstance>>();
        try
        {
            var cardCount = Conversions.ToInteger(LauncherSerialization.ReadIni(path + "PCL.ini", "CardCount", (-1).ToString()));
            if (cardCount == -1)
                return null;
            for (int i = 0, loopTo = cardCount - 1; i <= loopTo; i++)
            {
                var cardType =
                    (McInstanceCardType)Conversions.ToInteger(LauncherSerialization.ReadIni(path + "PCL.ini", "CardKey" + (i + 1),
                        ":"));
                var instanceList = new List<McInstance>();

                // 循环读取实例
                foreach (var folder in LauncherSerialization.ReadIni(path + "PCL.ini", "CardValue" + (i + 1), ":").Split(":"))
                {
                    if (string.IsNullOrEmpty(folder))
                        continue;
                    var versionFolder = $@"{path}versions\{folder}\";
                    if (File.Exists(versionFolder + ".pclignore"))
                    {
                        if (_isFirstMcInstanceListLoad)
                        {
                            LauncherLogger.Log("[Minecraft] 清理残留的忽略项目：" + versionFolder); // #2781
                            File.Delete(versionFolder + ".pclignore");
                        }
                        else
                        {
                            LauncherLogger.Log("[Minecraft] 跳过要求忽略的项目：" + versionFolder);
                            continue;
                        }
                    }

                    try
                    {
                        // 读取单个实例
                        var instance = new McInstance(versionFolder);
                        instanceList.Add(instance);
                        var instanceCfg = States.Instance;
                        instance.Desc = instanceCfg.CustomInfo[instance.PathInstance];

                        if (string.IsNullOrEmpty(instance.Desc))
                            instance.Desc = instanceCfg.Info[instance.PathInstance];
                        if (!instanceCfg.LogoPathConfig.IsDefault(instance.PathInstance))
                            instance.Logo = instanceCfg.LogoPath[instance.PathInstance];
                        if (!instanceCfg.ReleaseTimeConfig.IsDefault(instance.PathInstance))
                            instance.ReleaseTime = (dynamic)instanceCfg.ReleaseTime[instance.PathInstance];
                        if (!instanceCfg.StateConfig.IsDefault(instance.PathInstance))
                            instance.State =
                                (McInstanceState)Conversions.ToInteger(instanceCfg.State[instance.PathInstance]);
                        instance.IsStar = instanceCfg.Starred[instance.PathInstance];
                        instance.DisplayType =
                            (McInstanceCardType)Conversions.ToInteger(instanceCfg.CardType[instance.PathInstance]);
                        if (instance.State != McInstanceState.Error &&
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
                                Vanilla = new Version(instanceCfg.VanillaVersion[instance.PathInstance])
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
                        if (instance.State == McInstanceState.Error)
                        {
                            // 重新获取实例错误信息
                            var OldDesc = instance.Desc;
                            instance.State = McInstanceState.Original;
                            instance.Check();
                            // 校验错误原因是否改变
                            var CustomInfo = States.Instance.CustomInfo[instance.PathInstance];
                            if (instance.State == McInstanceState.Original || (string.IsNullOrEmpty(CustomInfo) &&
                                                                               !((OldDesc ?? "") ==
                                                                                   (instance.Desc ?? ""))))
                            {
                                LauncherLogger.Log("[Minecraft] 实例 " + instance.Name + " 的错误状态已变更，新的状态为：" + instance.Desc);
                                return null;
                            }
                        }

                        // 校验未加载的实例
                        if (string.IsNullOrEmpty(instance.Logo))
                        {
                            LauncherLogger.Log("[Minecraft] 实例 " + instance.Name + " 未被加载");
                            return null;
                        }
                    }

                    catch (Exception ex)
                    {
                        LauncherLogger.Log(ex, "读取实例加载缓存失败（" + folder + "）");
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
            LauncherLogger.Log(ex, "读取实例缓存失败");
            return null;
        }
    }

    private static Dictionary<McInstanceCardType, List<McInstance>> InitMcInstanceListWithoutCache(string path)
    {
        var instanceList = new List<McInstance>();

        #region 循环加载每个实例的信息

        foreach (var folder in new DirectoryInfo(path + "versions").GetDirectories())
        {
            if (!folder.Exists || !folder.EnumerateFiles().Any())
            {
                LauncherLogger.Log("[Minecraft] 跳过空文件夹：" + folder.FullName);
                continue;
            }

            if ((folder.Name == "cache" || folder.Name == "BLClient" || folder.Name == "PCL") &&
                !File.Exists(folder.FullName + @"\" + folder.Name + ".json"))
            {
                LauncherLogger.Log("[Minecraft] 跳过可能不是实例文件夹的项目：" + folder.FullName);
                continue;
            }

            var instanceFolder = folder.FullName + @"\";
            if (File.Exists(instanceFolder + ".pclignore"))
            {
                if (_isFirstMcInstanceListLoad)
                {
                    LauncherLogger.Log("[Minecraft] 清理残留的忽略项目：" + instanceFolder); // #2781
                    try
                    {
                        File.Delete(instanceFolder + ".pclignore");
                    }
                    catch (Exception ex)
                    {
                        LauncherLogger.Log(ex, "清理残留的忽略项目失败（" + instanceFolder + "）", LauncherLogger.LogLevel.Hint);
                    }
                }
                else
                {
                    LauncherLogger.Log("[Minecraft] 跳过要求忽略的项目：" + instanceFolder);
                    continue;
                }
            }

            var instance = new McInstance(instanceFolder);
            instanceList.Add(instance);
            instance.Load();
        }

        #endregion

        var results = new Dictionary<McInstanceCardType, List<McInstance>>();

        #region 将实例分类到各个卡片

        try
        {
            // 未经过自定义的实例列表
            var instanceListOriginal = new Dictionary<McInstanceCardType, List<McInstance>>();

            // 单独列出收藏的实例
            var staredInstances = new List<McInstance>();
            foreach (var instance in instanceList.ToList())
            {
                if (!instance.IsStar)
                    continue;
                if (instance.DisplayType == McInstanceCardType.Hidden)
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
            var instanceUseful = new List<McInstance>();
            var instanceRubbish = new List<McInstance>();
            McInstanceFilter(ref instanceList, new[] { McInstanceState.Old }, ref instanceRubbish);

            // 确认最新实例，若为快照则加入常用列表
            var latestInstance = instanceList
                .Where(v => v.State == McInstanceState.Original || v.State == McInstanceState.Snapshot)
                .MaxOrDefault(v => v.ReleaseTime);
            if (latestInstance is not null && latestInstance.State == McInstanceState.Snapshot)
            {
                instanceUseful.Add(latestInstance);
                instanceList.Remove(latestInstance);
            }

            // 将剩余的快照全部拖进不常用列表
            McInstanceFilter(ref instanceList, new[] { McInstanceState.Snapshot }, ref instanceRubbish);

            // 获取每个 Drop 下最新的原版与 OptiFine
            var newerInstance = new Dictionary<string, McInstance>();
            var existDrops = new List<int>();
            foreach (var instance in instanceList)
            {
                if (!instance.Info.Valid)
                    continue;
                if (!existDrops.Contains(instance.Info.Drop))
                    existDrops.Add(instance.Info.Drop);
                var key = instance.Info.Drop + "-" + (int)instance.State;
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
                else if (instance.ReleaseTime > newerInstance[key].ReleaseTime)
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
                var realType = instance.DisplayType == 0 || instancePair.Key == McInstanceCardType.Star
                    ? instancePair.Key
                    : instance.DisplayType;
                if (!results.ContainsKey(realType))
                    results.Add(realType, new List<McInstance>());
                results[realType].Add(instance);
            }
        }

        catch (Exception ex)
        {
            results.Clear();
            LauncherLogger.Log(ex, "分类实例列表失败", LauncherLogger.LogLevel.Feedback);
        }

        #endregion

        #region 对卡片与实例进行排序

        // 卡片排序
        var sortedInstanceList = new Dictionary<McInstanceCardType, List<McInstance>>();
        foreach (var sortRule in new[]
                 {
                     McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike,
                     McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error,
                     McInstanceCardType.Hidden
                 })
            if (results.ContainsKey((McInstanceCardType)Conversions.ToInteger(sortRule)))
                sortedInstanceList.Add((McInstanceCardType)Conversions.ToInteger(sortRule),
                    results[(McInstanceCardType)Conversions.ToInteger(sortRule)]);
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

            int getComponentCode(McInstance instance)
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
                if ((left.ReleaseTime.Year >= 2000 || right.ReleaseTime.Year >= 2000) &&
                    left.ReleaseTime != right.ReleaseTime)
                    return left.ReleaseTime > right.ReleaseTime;
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
                return Operators.CompareString(left.Name, right.Name, false) > 0;
            });
        }

        #endregion

        #region 保存卡片缓存

        LauncherSerialization.WriteIni(path + "PCL.ini", "CardCount", results.Count.ToString());
        for (int i = 0, loopTo = results.Count - 1; i <= loopTo; i++)
        {
            LauncherSerialization.WriteIni(path + "PCL.ini", "CardKey" + (i + 1),
                ((int)results.Keys.ElementAtOrDefault(i)).ToString());
            var Value = "";
            foreach (var Instance in results.Values.ElementAtOrDefault(i))
                Value += Instance.Name + ":";
            LauncherSerialization.WriteIni(path + "PCL.ini", "CardValue" + (i + 1), Value);
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
    private static void McInstanceFilter(ref List<McInstance> instanceList,
        ref Dictionary<McInstanceCardType, List<McInstance>> target, McInstanceState[] formula,
        McInstanceCardType cardType)
    {
        var keepList = instanceList.Where(v => formula.Contains(v.State)).ToList();
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
    private static void McInstanceFilter(ref List<McInstance> instanceList, McInstanceState[] formula,
        ref List<McInstance> keepList)
    {
        keepList.AddRange(instanceList.Where(v => formula.Contains(v.State)));
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
}
