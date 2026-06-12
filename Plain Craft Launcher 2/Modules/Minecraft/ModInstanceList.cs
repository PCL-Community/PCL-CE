using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Network;

namespace PCL;

public static class ModInstanceList
{
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
}
