using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public class McInstance
    {
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
        /// 是否已初始化从 JAR 中读取 version.json。
        /// </summary>
        private bool _jsonVersionInited;

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
        public McInstance(string name)
        {
            PathInstance = (name.Contains(":") ? name : Path.Combine(ModFolder.mcFolderSelected, "versions", name)) + (name.EndsWithF(@"\") ? "" : @"\");
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

                return Config.Instance.IndieV2[PathInstance] ? PathInstance : ModFolder.mcFolderSelected;
            }
        }

        /// <summary>
        ///     该实例的实例文件夹名称。
        /// </summary>
        public string Name
        {
            get
            {
                if (field is null && !string.IsNullOrEmpty(PathInstance))
                    field = ModBase.GetFolderNameFromPath(PathInstance);
                return field;
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
                if (field is not null)
                    return field;
                field = new McInstanceInfo();

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
                            field.VanillaName = "Old";
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
                        field.VanillaName = "pending";
                        goto VersionSearchFinish;
                    }

                    // 从 PCL 下载的版本信息中获取版本号
                    if (JsonObject["clientVersion"] is not null)
                    {
                        field.VanillaName = (string)JsonObject["clientVersion"];
                        goto VersionSearchFinish;
                    }

                    // 从 HMCL 下载的版本信息中获取版本号
                    if (JsonObject["patches"] is not null)
                        foreach (var patchNode in JsonObject["patches"].AsArray()) { var patch = patchNode.AsObject();
                            if ((patch["id"] ?? "").ToString() == "game" && patch["version"] is not null)
                            {
                                field.VanillaName = patch["version"].ToString();
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
                                    field.VanillaName = Argument.ToString();
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
                                    field.VanillaName = regexArgument;
                                    goto VersionSearchFinish;
                                }
                            }
                    }

                    // 从继承实例中获取版本号
                    if (!string.IsNullOrEmpty(InheritInstanceName))
                    {
                        field.VanillaName = (JsonObject["jar"] ?? "").ToString(); // LiteLoader 优先使用 Jar
                        if (string.IsNullOrEmpty(field.VanillaName))
                            field.VanillaName = InheritInstanceName;
                        goto VersionSearchFinish;
                    }

                    // 从下载地址中获取版本号
                    var regex = (JsonObject["downloads"] ?? "").ToString()
                        .RegexSeek(RegexPatterns.MinecraftDownloadUrlVersion);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 Forge 版本中获取版本号
                    var librariesString = JsonObject["libraries"].ToString();
                    regex = librariesString.RegexSeek(RegexPatterns.ForgeLibVersion);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 OptiFine 版本中获取版本号
                    regex = librariesString.RegexSeek(RegexPatterns.OptiFineLibVersion);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 Fabric / Quilt / Legacy Fabric 版本中获取版本号
                    regex = librariesString.RegexSeek(RegexPatterns.FabricLikeLibVersion);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 jar 项中获取版本号
                    if (JsonObject["jar"] is not null)
                    {
                        field.VanillaName = JsonObject["jar"].ToString();
                        goto VersionSearchFinish;
                    }

                    // 从 jar 文件的 version.json 中获取版本号
                    if (JsonVersion?["name"] is not null)
                    {
                        var jsonVerName = JsonVersion["name"].ToString();
                        if (jsonVerName.Length < 32) // 因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                        {
                            field.VanillaName = jsonVerName;
                            ModBase.Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" + jsonVerName);
                            goto VersionSearchFinish;
                        }
                    }

                    // 从 JSON 的 ID 中获取
                    regex = ((string)JsonObject["id"]).RegexSeek(RegexPatterns.MinecraftJsonVersion,
                        RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 非准确的版本判断警告
                    ModBase.Log("[Minecraft] 无法完全确认 MC 版本号的版本：" + Name);
                    field.Reliable = false;
                    // 从文件夹名中获取
                    regex = Name.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 从 JSON 出现的版本号中获取
                    var jsonRaw = (JsonObject)JsonObject.DeepClone();
                    jsonRaw.Remove("libraries");
                    var jsonRawText = jsonRaw.ToString();
                    regex = jsonRawText.RegexSeek(RegexPatterns.MinecraftJsonVersion, RegexOptions.IgnoreCase);
                    if (regex is not null)
                    {
                        field.VanillaName = regex;
                        goto VersionSearchFinish;
                    }

                    // 无法获取
                    field.VanillaName = "Unknown";
                    Desc = Lang.Text("Select.Instance.Description.UnknownMcVersion");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "识别 Minecraft 版本时出错");
                    field.VanillaName = "Unknown";
                    Desc = Lang.Text("Minecraft.Error.Unrecognizable", ex.Message);
                }

                #endregion

                VersionSearchFinish: ;

                if (field.VanillaName.StartsWithF("20.") || field.VanillaName.StartsWithF("21."))
                {
                    field.VanillaName = "1." + field.VanillaName;
                }
                
                field.VanillaName = field.VanillaName.Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
                // 获取版本号
                if (field.VanillaName.StartsWithF("1."))
                {
                    var segments = field.VanillaName.Split(" _-.".ToCharArray());
                    field.vanilla = new Version((int)Math.Round(ModBase.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        0, (int)Math.Round(ModBase.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else if (field.VanillaName.RegexCheck(@"^[2-9][0-9]\."))
                {
                    var segments = field.VanillaName.Split(" _-.".ToCharArray());
                    field.vanilla = new Version((int)Math.Round(ModBase.Val(segments[0])),
                        (int)Math.Round(ModBase.Val(segments.Count() >= 2 ? segments[1] : "0")),
                        (int)Math.Round(ModBase.Val(segments.Count() >= 3 ? segments[2] : "0")));
                }
                else
                {
                    field.vanilla = new Version(9999, 0, 0);
                }

                return field;
            }
            set { field = value; }
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
                if (field is null)
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

                    field = ModBase.ReadFile(jsonPath);
                    // 如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    if (!FastJsonCheck(field))
                    {
                        if (ModBase.RunInUi())
                        {
                            ModBase.Log($"[Minecraft] 实例 JSON 文件为空或有误，将进行短暂重试（{jsonPath}）", ModBase.LogLevel.Debug);
                            Thread.Sleep(200);
                            field = ModBase.ReadFile(jsonPath);
                        }
                        else
                        {
                            ModBase.Log($"[Minecraft] 实例 JSON 文件为空或有误，将在 2s 后重试读取（{jsonPath}）", ModBase.LogLevel.Debug);
                            Thread.Sleep(2000);
                            field = ModBase.ReadFile(jsonPath);
                        }
                        if (!FastJsonCheck(field))
                            ModBase.GetJson(field);
                    }
                }

                return field;
            }
            set => field = value;
        }

        /// <summary>
        ///     该实例的 JSON 对象。
        ///     若 JSON 存在问题，在获取该属性时即会抛出异常。
        /// </summary>
        public JsonObject JsonObject
        {
            get
            {
                if (field is null)
                {
                    var text = JsonText; // 触发 JsonText 的 Get 事件
                    try
                    {
                        field = (JsonObject)ModBase.GetJson(text);
                        // 转换 HMCL 关键项
                        if (field.ContainsKey("patches") && !field.ContainsKey("time"))
                        {
                            IsHmclFormatJson = true;
                            // 合并 JSON
                            // Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            JsonObject currentObject = null;
                            var subjsonList = new List<JsonObject>();
                            foreach (var SubjsonNode in field["patches"].AsArray()) { var subjson = SubjsonNode.AsObject();
                                subjsonList.Add(subjson); }
                            subjsonList.Sort((left, right) =>
                            {
                                var leftVal = ModBase.Val((left["priority"] ?? "0").ToString());
                                var rightVal = ModBase.Val((right["priority"] ?? "0").ToString());
                                return leftVal.CompareTo(rightVal);
                            });
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

                            field = currentObject;
                            // 修改附加项
                            field["id"] = Name;
                            if (field.ContainsKey("inheritsFrom"))
                                field.Remove("inheritsFrom");
                        }

                        // 与继承实例合并
                        object inheritInstanceName = null;
                        do
                        {
                            try
                            {
                                inheritInstanceName = field["inheritsFrom"] is null
                                    ? ""
                                    : field["inheritsFrom"].ToString();
                                if (Equals(inheritInstanceName, Name))
                                {
                                    ModBase.Log("[Minecraft] 自引用的继承实例：" + Name, ModBase.LogLevel.Debug);
                                    inheritInstanceName = "";
                                    break;
                                }

                                Recheck: ;

                                if (!Equals(inheritInstanceName, ""))
                                {
                                    var inheritInstance = new McInstance(inheritInstanceName?.ToString() ?? "");
                                    // 继续循环
                                    if (Equals(inheritInstance.InheritInstanceName,
                                            inheritInstanceName))
                                        throw new Exception(Lang.Text("Minecraft.Error.DependencyRecursion",
                                            inheritInstanceName));
                                    inheritInstanceName = inheritInstance.InheritInstanceName;
                                    // 合并
                                    inheritInstance.JsonObject.Merge(field);
                                    field = inheritInstance.JsonObject;
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

                return field;
            }
            set => field = value;
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
                if (!_jsonVersionInited)
                {
                    _jsonVersionInited = true;
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
                                        field = (JsonObject)ModBase.GetJson(versionJsonStream.ReadToEnd());
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"从实例 JAR 中读取 version.json 失败 ({PathInstance}{Name}.jar)");
                        }
                    } while (false);
                }

                return field;
            }
        }

        /// <summary>
        ///     该实例的依赖实例。若无依赖实例则为空字符串。
        /// </summary>
        public string InheritInstanceName
        {
            get
            {
                if (field is null)
                {
                    field = (JsonObject["inheritsFrom"] ?? "").ToString();
                    // 由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承实例
                    // 此外，由于这里的加载早于实例种类判断，所以需要手动判断是否为 LiteLoader
                    // 如果实例提供了不同的 JAR，代表所需的 JAR 可能已被更改，则跳过 Inherit 替换
                    if (JsonText.Contains("liteloader") && (Info.VanillaName ?? "") != (Name ?? "") &&
                        !JsonText.Contains("logging"))
                        if (((JsonObject["jar"] ?? Info.VanillaName).ToString() ?? "") == (Info.VanillaName ?? ""))
                            field = Info.VanillaName;
                    // HMCL 实例无 JSON
                    if (IsHmclFormatJson)
                        field = "";
                }

                return field;
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