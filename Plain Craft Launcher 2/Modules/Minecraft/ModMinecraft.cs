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

    #region 实例处理

    public const int mcInstanceCacheVersion = 30;

    private static object _McInstanceSelected_mcInstanceSelectedLast = 0; // 为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化

    /// <summary>
    ///     当前的 Minecraft 版本。
    /// </summary>
    public static McInstance McMcInstanceSelected
    {
        get => field;
        set
        {
            if (ReferenceEquals(_McInstanceSelected_mcInstanceSelectedLast, value))
                return;
            field = value; // 由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
            _McInstanceSelected_mcInstanceSelectedLast = value;
            if (value is null)
                return;
            // 重置缓存的 Mod 文件夹
            PageDownloadCompDetail.cachedFolder.Clear();
        }
    }

    internal static bool _JsonVersion_jsonVersionInited;
    



    /// <summary>
    ///     根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    /// </summary>
    /// <summary>
    ///     当前按卡片分类的所有版本列表。
    /// </summary>
    public static Dictionary<McInstanceCardType, List<PCL.McInstance>> mcInstanceList = new();

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
            mcInstanceList = new Dictionary<McInstanceCardType, List<PCL.McInstance>>();
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
                McMcInstanceSelected = null;
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
                        McMcInstanceSelected = instance;
                        States.Game.SelectedInstance = McMcInstanceSelected.Name;
                        ModBase.Log("[Minecraft] 选择该文件夹储存的 Minecraft 实例：" + McMcInstanceSelected.PathInstance);
                        return;
                    }

            // 自动选择第一项
            var firstInstance = mcInstanceList
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(i => i.state != McInstanceState.Error);

            if (firstInstance is not null)
            {
                McMcInstanceSelected = firstInstance;
                States.Game.SelectedInstance = McMcInstanceSelected.Name;
                ModBase.Log("[Launch] 自动选择 Minecraft 实例：" + McMcInstanceSelected.PathInstance);
            }
            else
            {
                McMcInstanceSelected = null;
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
    private static Dictionary<McInstanceCardType, List<PCL.McInstance>> InitMcInstanceListWithCache(string path)
    {
        var results = new Dictionary<McInstanceCardType, List<PCL.McInstance>>();
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
                var instanceList = new List<PCL.McInstance>();

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
                        var instance = new PCL.McInstance(versionFolder);
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

    private static Dictionary<McInstanceCardType, List<PCL.McInstance>> InitMcInstanceListWithoutCache(string path)
    {
        var instanceList = new List<PCL.McInstance>();

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

            var instance = new PCL.McInstance(instanceFolder);
            instanceList.Add(instance);
            instance.Load();
        }

        #endregion

        var results = new Dictionary<McInstanceCardType, List<PCL.McInstance>>();

        #region 将实例分类到各个卡片

        try
        {
            // 未经过自定义的实例列表
            var instanceListOriginal = new Dictionary<McInstanceCardType, List<PCL.McInstance>>();

            // 单独列出收藏的实例
            var staredInstances = new List<PCL.McInstance>();
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
            var instanceUseful = new List<PCL.McInstance>();
            var instanceRubbish = new List<PCL.McInstance>();
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
            var newerInstance = new Dictionary<string, PCL.McInstance>();
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
                    results.Add(realType, new List<PCL.McInstance>());
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
        var sortedInstanceList = new Dictionary<McInstanceCardType, List<PCL.McInstance>>();
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

            int getComponentCode(PCL.McInstance instance)
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
    private static void McInstanceFilter(ref List<PCL.McInstance> instanceList,
        ref Dictionary<McInstanceCardType, List<PCL.McInstance>> target, McInstanceState[] formula,
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
    private static void McInstanceFilter(ref List<PCL.McInstance> instanceList, McInstanceState[] formula,
        ref List<McInstance> keepList)
    {
        keepList.AddRange(instanceList.Where(v => formula.Contains(v.state)));
        // 加入实例列表，并从剩余中删除
        if (keepList.Any()) instanceList = instanceList.Except(keepList).ToList();
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
    public static List<McLibToken> McLibListGet(McInstance mcInstance, bool includeInstanceJar)
    {
        // 获取当前支持库列表
        ModBase.Log("[Minecraft] 获取支持库列表：" + mcInstance.Name);
        var result = McLibListGetWithJson(mcInstance.JsonObject, targetMcInstance: mcInstance);

        // 需要添加原版 Jar
        if (includeInstanceJar)
        {
            McInstance realMcInstance;
            var requiredJar = mcInstance.JsonObject["jar"]?.ToString();
            if (mcInstance.IsHmclFormatJson || requiredJar is null)
            {
                // HMCL 项直接使用自身的 Jar
                // 根据 Inherit 获取最深层实例
                var originalInstance = mcInstance;
                // 1.17+ 的 Forge 不寻找 Inherit
                if (!((mcInstance.Info.HasForge || mcInstance.Info.HasNeoForge) && mcInstance.Info.Drop >= 170))
                    while (!string.IsNullOrEmpty(originalInstance.InheritInstanceName))
                    {
                        if ((originalInstance.InheritInstanceName ?? "") == (originalInstance.Name ?? ""))
                            break;
                        originalInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", originalInstance.InheritInstanceName));
                    }

                // 需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                // 复现：启动一个 Snapshot 实例
                realMcInstance = new McInstance(originalInstance.PathInstance);
            }
            else
            {
                // Json 已提供 Jar 字段，使用该字段的信息
                realMcInstance = new McInstance(requiredJar);
            }

            string clientUrl;
            string clientSHA1;
            // 判断需求的实例是否存在
            // 不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            if (!File.Exists(realMcInstance.PathInstance + realMcInstance.Name + ".json"))
            {
                realMcInstance = mcInstance;
                ModBase.Log("[Minecraft] 可能缺少前置实例 " + realMcInstance.Name + "，找不到对应的 JSON 文件", ModBase.LogLevel.Debug);
            }

            // 获取详细下载信息
            if (realMcInstance.JsonObject["downloads"] is not null &&
                realMcInstance.JsonObject["downloads"]["client"] is not null)
            {
                clientUrl = (string)realMcInstance.JsonObject["downloads"]["client"]["url"];
                clientSHA1 = (string)realMcInstance.JsonObject["downloads"]["client"]["sha1"];
            }
            else
            {
                clientUrl = null;
                clientSHA1 = null;
            }

            // 把所需的原版 Jar 添加进去
            result.Add(new McLibToken
            {
                LocalPath = realMcInstance.PathInstance + realMcInstance.Name + ".jar", size = 0L, IsNatives = false,
                Url = clientUrl, Sha1 = clientSHA1
            });
        }

        return result;
    }

    /// <summary>
    ///     获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
    public static List<McLibToken> McLibListGetWithJson(JsonObject jsonObject,
        bool keepSameNameDifferentVersionResult = false, string customMcFolder = null, McInstance targetMcInstance = null)
    {
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
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
                if (isLocal && targetMcInstance is not null) // 纯本地项
                    localPath = targetMcInstance.PathInstance + @"libraries\" +
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
                    if (McVersionComparer.CompareVersionGe(basicArrayVersion, resultArrayVersion)) resultArray[key] = basicArray[i];
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
    public static List<DownloadFile> McLibNetFilesFromInstance(McInstance mcInstance)
    {
        if (!mcInstance.IsLoaded)
            mcInstance.Load();
        var result = new List<DownloadFile>();

        // 更新此方法时需要同步更新 Forge 新版自动安装方法！

        // 主 Jar 文件
        try
        {
            var mainJar = ModDownload.DlClientJarGet(mcInstance, true);
            if (mainJar is not null)
                result.Add(mainJar);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "实例缺失主 Jar 文件所必须的信息", ModBase.LogLevel.Developer);
        }

        // Library 文件
        result.AddRange(McLibNetFilesFromTokens(McLibListGet(mcInstance, false)));

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
        if (McMcInstanceSelected is not null)
            renderer = Config.Instance.Renderer[McMcInstanceSelected?.PathInstance] - 1;
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
        if (mcInstance.Info.HasLabyMod)
        {
            if ((mcInstance.PathIndie ?? "") == (mcInstance.PathInstance ?? ""))
            {
                if (Directory.Exists(Path.Combine(mcInstance.PathInstance, "labymod-neo")))
                    Directory.Delete(Path.Combine(mcInstance.PathInstance, "labymod-neo"), true);
                ModBase.CreateSymbolicLink(Path.Combine(mcInstance.PathInstance, "labymod-neo"), Path.Combine(ModFolder.mcFolderSelected, "labymod-neo"),
                    0x2);
            }

            try
            {
                var channelType = mcInstance.JsonObject["labymod_data"]["channelType"].ToString();
                Directory.CreateDirectory($@"{ModFolder.mcFolderSelected}labymod-neo\libraries");
                ModBase.Log("[Minecraft] 开始获取 LabyMod 信息");
                var labyManifest = (JsonObject)ModNet.NetGetCodeByRequestRetry(
                    $"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json", isJson: true);
                var labyAssets = (JsonObject)labyManifest["assets"];
                var labyModCommitRef = labyManifest["commitReference"].ToString();
                foreach (var Asset in labyAssets)
                {
                    var assetName = Asset.Key;
                    var assetSHA1 = Asset.Value.ToString();
                    var assetPath = $@"{ModFolder.mcFolderSelected}labymod-neo\assets\{assetName}.jar";
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
        if (ShouldIgnoreFileCheck(mcInstance))
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
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
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
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
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
    public static bool ShouldIgnoreFileCheck(McInstance version)
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
    public static JsonNode McAssetsGetIndex(McInstance mcInstance, bool returnLegacyOnError = false,
        bool checkURLEmpty = false)
    {
        string assetsName;
        try
        {
            while (true)
            {
                var index = mcInstance.JsonObject["assetIndex"];
                if (index is not null && index["id"] is not null)
                    return index;
                if (mcInstance.JsonObject["assets"] is not null)
                    assetsName = mcInstance.JsonObject["assets"].ToString();
                if (checkURLEmpty && index["url"] is not null)
                    return index;
                // 下一个实例
                if (string.IsNullOrEmpty(mcInstance.InheritInstanceName))
                    break;
                mcInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", mcInstance.InheritInstanceName));
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
    public static string McAssetsGetIndexName(McInstance mcInstance)
    {
        try
        {
            while (true)
            {
                if (mcInstance.JsonObject["assetIndex"] is not null &&
                    mcInstance.JsonObject["assetIndex"]["id"] is not null)
                    return mcInstance.JsonObject["assetIndex"]["id"].ToString();
                if (mcInstance.JsonObject["assets"] is not null) return mcInstance.JsonObject["assets"].ToString();
                if (string.IsNullOrEmpty(mcInstance.InheritInstanceName))
                    break;
                mcInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", mcInstance.InheritInstanceName));
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
    private static List<McAssetsToken> McAssetsListGet(McInstance mcInstance)
    {
        var indexName = McAssetsGetIndexName(mcInstance);
        try
        {
            // 初始化
            if (!File.Exists($@"{ModFolder.mcFolderSelected}assets\indexes\{indexName}.json"))
                throw new FileNotFoundException(Lang.Text("Minecraft.Error.AssetIndexNotFound"),
                    Path.Combine(ModFolder.mcFolderSelected, "assets", "indexes", indexName + ".json"));
            var result = new List<McAssetsToken>();
            var json = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile($@"{ModFolder.mcFolderSelected}assets\indexes\{indexName}.json"));

            // 读取列表
            foreach (var file in json["objects"].AsObject())
            {
                string localPath;
                var hash = file.Value["hash"].ToString();
                if (json["map_to_resources"] is not null && json["map_to_resources"].ToObject<bool>())
                    // Remap
                    localPath = Path.Combine(mcInstance.PathIndie, "resources", file.Key.Replace("/", @"\"));
                else if (json["virtual"] is not null && json["virtual"].ToObject<bool>())
                    // Virtual
                    localPath = Path.Combine(ModFolder.mcFolderSelected, "assets", "virtual", "legacy", file.Key.Replace("/", @"\"));
                else
                {
                    // 正常
                    localPath = Path.Combine(ModFolder.mcFolderSelected, "assets", "objects", McAssetsHashPrefix(hash), hash);
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
    public static List<DownloadFile> McAssetsFixList(McInstance mcInstance, bool checkHash,
        [Optional] ref ModLoader.LoaderBase progressFeed)
    {
        // 如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
        if (checkHash)
            return McAssetsListGet(mcInstance).Select(token =>
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
            assetsList = McAssetsListGet(mcInstance);
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
