using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class InstanceList
{
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
    public static Loading.LoaderTask<string, int> mcInstanceListLoader = new(
        "Minecraft Instance List",
        InitMcInstanceList)
    {
        reloadTimeout = 1
    };

    private static void InitMcInstanceList(Loading.LoaderTask<string, int> loader)
    {
        var path = loader.input
            ?? throw new InvalidOperationException("实例列表加载器输入为空");
        try
        {
            mcInstanceList = new Dictionary<McInstanceCardType, List<McInstance>>();
            var folderList = _GetInstanceFolderNames(path);
            if (folderList.Count == 0)
            {
                _ClearInstanceSelection(path);
                return;
            }

            var folderListCheck = _GetInstanceCacheCheck(folderList);
            var useCache = !mcInstanceListForceRefresh
                && LauncherText.Val(
                    LauncherCommon.ReadIni(
                        Path.Combine(path, "PCL.ini"),
                        "InstanceCache"))
                == folderListCheck;

            if (useCache)
            {
                var cachedResult = InitMcInstanceListWithCache(path);
                if (cachedResult is not null)
                    mcInstanceList = cachedResult;
                else
                    useCache = false;
            }

            if (!useCache)
            {
                mcInstanceListForceRefresh = false;
                LauncherCommon.Log("[Minecraft] 文件夹列表变更或缓存无效，重载所有实例");
                LauncherCommon.WriteIni(
                    Path.Combine(path, "PCL.ini"),
                    "InstanceCache",
                    folderListCheck.ToString());
                mcInstanceList = InitMcInstanceListWithoutCache(path);
            }

            _isFirstMcInstanceListLoad = false;

            if (loader.IsAborted)
                return;

            _SelectInstanceAfterLoad(path);

            if (Config.Debug.AddRandomDelay)
                Thread.Sleep(RandomUtils.NextInt(200, 3000));
        }
        catch (ThreadInterruptedException)
        {
        }
        catch (Exception ex)
        {
            LauncherCommon.WriteIni(
                Path.Combine(path, "PCL.ini"),
                "InstanceCache",
                "");
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.ListLoad"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.ListLoad"));
        }
    }

    private static List<string> _GetInstanceFolderNames(string path)
    {
        var versionsPath = Path.Combine(path, "versions");
        if (!Directory.Exists(versionsPath))
            return [];

        try
        {
            return new DirectoryInfo(versionsPath)
                .GetDirectories()
                .Select(folder => folder.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Error.CannotReadInstanceFolder", versionsPath),
                ex);
        }
    }

    private static void _ClearInstanceSelection(string path)
    {
        LauncherCommon.WriteIni(Path.Combine(path, "PCL.ini"), "InstanceCache", "");
        McMcInstanceSelected = null;
        States.Game.SelectedInstance = "";
        LauncherCommon.Log("[Minecraft] 未找到可用 Minecraft 实例");
    }

    private static int _GetInstanceCacheCheck(List<string> folderList)
    {
        var folderListHash = LauncherCommon.GetHash(
            $"{McInstanceCacheVersion}#{string.Join("#", folderList)}");
        return folderListHash % (int.MaxValue - 1);
    }

    private static void _SelectInstanceAfterLoad(string path)
    {
        var savedSelection = LauncherCommon.ReadIni(Path.Combine(path, "PCL.ini"), "Version");
        if (!string.IsNullOrEmpty(savedSelection) && _TrySelectSavedInstance(savedSelection))
            return;

        var firstInstance = mcInstanceList
            .SelectMany(kv => kv.Value)
            .FirstOrDefault(i => i.state != McInstanceState.Error);

        if (firstInstance is not null)
        {
            McMcInstanceSelected = firstInstance;
            States.Game.SelectedInstance = McMcInstanceSelected.Name;
            LauncherCommon.Log($"[Launch] 自动选择 Minecraft 实例：{McMcInstanceSelected.PathInstance}");
            return;
        }

        McMcInstanceSelected = null;
        States.Game.SelectedInstance = "";
        LauncherCommon.Log("[Minecraft] 未找到可用 Minecraft 实例");
    }

    private static bool _TrySelectSavedInstance(string savedSelection)
    {
        foreach (
            var instance in
            from card in mcInstanceList
            from instance in card.Value
            where (instance.Name ?? "") == savedSelection && instance.state != McInstanceState.Error
            select instance)
        {
            McMcInstanceSelected = instance;
            States.Game.SelectedInstance = McMcInstanceSelected.Name;
            LauncherCommon.Log(
                $"[Minecraft] 选择该文件夹储存的 Minecraft 实例：{McMcInstanceSelected.PathInstance}");
            return true;
        }

        return false;
    }
}