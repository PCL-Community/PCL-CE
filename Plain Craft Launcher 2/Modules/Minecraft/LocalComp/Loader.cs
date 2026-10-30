using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using static PCL.Comp;
using static PCL.Loading;

namespace PCL;

public static partial class LocalComp
{
    public static readonly LoaderTask<CompLocalLoaderData, List<LocalCompFile>>
        CompResourceListLoader =
            new("Comp Resource List Loader", CompResourceListLoad);

    [ThreadStatic] internal static bool deferJijOnMiss;

    [ThreadStatic] internal static JarInJar.ScanContext? jijScanContext;

    internal static volatile bool compJijResolved = true;
    private static int _jijGeneration;

    internal static string? GetPreferredLoader(McInstance? instance)
    {
        if (instance?.Info is null) return null;
        if (instance.Info.HasNeoForge) return "NeoForge";
        if (instance.Info.HasForge || instance.Info.HasCleanroom) return "Forge";
        if (instance.Info.HasFabric) return "Fabric";
        if (instance.Info.HasQuilt) return "Quilt";
        return null;
    }

    private static void _ResolveJijPendingBackground(
        List<LocalCompFile> pending,
        List<string> allModPaths,
        string instancePath,
        int loaderTaskId,
        int generation)
    {
        JarInJarCache.UseInstance(instancePath);
        jijScanContext = new JarInJar.ScanContext(() =>
            CompResourceListLoader.IsAbortedWithThread(loaderTaskId));
        var allResolved = false;

        try
        {
            foreach (var mod in pending)
            {
                if (CompResourceListLoader.IsAbortedWithThread(loaderTaskId))
                    return;

                mod.ResolveJijNow();
            }

            if (CompResourceListLoader.IsAbortedWithThread(loaderTaskId))
                return;

            allResolved = pending.All(mod => !mod.JijPending);
            if (allResolved)
                JarInJarCache.Prune(allModPaths);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "Jar-in-Jar 后台补解析失败", Common.LogLevel.Developer);
        }
        finally
        {
            JarInJarCache.Flush();
            jijScanContext = null;
            JarInJarCache.UseInstance(null);

            if (generation == Volatile.Read(ref _jijGeneration))
            {
                compJijResolved = allResolved;
                Common.RunInUi(() => Launcher.frmInstanceModJarInJar?.OnJijResolved());
            }
        }
    }

    private static void CompResourceListLoad(
        LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader)
    {
        var data = loader.input
            ?? throw new InvalidOperationException("资源列表加载器输入为空");

        try
        {
            Common.RunInUiWait(() => { data.frm?.Load.ShowProgress = false; });

            if (!WaitForResourceUpdate(loader, data))
                return;

            var modList = LoadResourceListFiles(data);
            UpdateResourceListProgress(loader, data, modList.Count);

            var cache = LoadLocalModInfoCache();
            var modUpdateList = new List<LocalCompFile>();
            var jijEnabled = data.compType == CompType.Mod;
            var jijInstancePath = jijEnabled ? data.gameVersion.PathInstance : null;
            var preferredLoader = jijEnabled ? GetPreferredLoader(data.gameVersion) : null;
            var jijGeneration = 0;
            Action? startJijResolve = null;

            JarInJarCache.UseInstance(jijInstancePath);
            if (jijEnabled)
            {
                jijGeneration = Interlocked.Increment(ref _jijGeneration);
                compJijResolved = false;
                deferJijOnMiss = true;
            }

            if (!LoadModInfoForResourceList(
                    loader,
                    data,
                    modList,
                    cache,
                    modUpdateList,
                    jijEnabled,
                    preferredLoader))
                return;

            if (jijEnabled)
                startJijResolve = PrepareJarInJarResolution(
                    modList,
                    jijInstancePath,
                    jijGeneration);

            loader.Progress = 0.99d;
            Common.Log(
                $"[Mod] 共有 {modList.Count} 个 Mod，"
                + $"其中 {modUpdateList.Count(mod => mod.Comp is null)} 个需要联网获取信息，"
                + $"{modUpdateList.Count(mod => mod.Comp is not null)} 个需要更新信息");

            modList.Sort(CompareLocalCompFiles);

            CompleteResourceListLoad(
                loader,
                data,
                modList,
                modUpdateList,
                cache,
                startJijResolve);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "Mod 列表加载失败");
            throw;
        }
        finally
        {
            JarInJarCache.UseInstance(null);
            deferJijOnMiss = false;
        }
    }

    private static bool WaitForResourceUpdate(
        LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader,
        CompLocalLoaderData data)
    {
        if (!PageInstanceCompResource.updatingVersions.Contains(data.compPath))
            return true;

        Common.Log($"[Mod] 等待资源更新完成后才能继续加载资源列表：{data.compPath}");

        try
        {
            Common.RunInUiWait(() =>
            {
                data.frm?.Load.Text = Lang.Text("Instance.Resource.Update.WaitingForUpdate");
            });

            while (PageInstanceCompResource.updatingVersions.Contains(data.compPath))
            {
                if (loader.IsAborted)
                    return false;

                Thread.Sleep(100);
            }
        }
        finally
        {
            Common.RunInUiWait(() =>
            {
                data.frm?.Load.Text = Lang.Text("Instance.Resource.List.Loading");
            });
        }

        data.frm?.LoaderRun(LoaderFolderRunType.UpdateOnly);
        return true;
    }

    private static List<LocalCompFile> LoadResourceListFiles(CompLocalLoaderData data)
    {
        var modList = new List<LocalCompFile>();
        if (!Directory.Exists(data.compPath))
            return modList;

        var rawName = data.compPath.ToLower();
        if (data.compType == CompType.Schematic)
            EnumerateSchematicResourceFiles(data, modList);
        else
            EnumerateRegularResourceFiles(data, rawName, modList);

        return modList;
    }

    private static void EnumerateSchematicResourceFiles(
        CompLocalLoaderData data,
        List<LocalCompFile> modList)
    {
        var currentFolderPath = data.frm is not null
            ? data.frm.CurrentFolderPath
            : "";
        var searchPath = string.IsNullOrEmpty(currentFolderPath)
            ? data.compPath
            : currentFolderPath;

        try
        {
            var directoryInfo = new DirectoryInfo(searchPath);
            modList.AddRange(
                directoryInfo
                    .EnumerateDirectories("*", SearchOption.AllDirectories)
                    .Select(directory =>
                        new LocalCompFile(Path.Combine(directory.FullName, "__FOLDER__"))));

            foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                try
                {
                    if (LocalCompFile.IsCompFile(file.FullName, data.compType))
                        modList.Add(new LocalCompFile(file.FullName));
                }
                catch (Exception ex)
                {
                    Common.Log(ex, $"处理文件失败：{file.FullName}");
                }
        }
        catch (Exception ex)
        {
            Common.Log(ex, $"枚举文件失败：{searchPath}");
        }
    }

    private static void EnumerateRegularResourceFiles(
        CompLocalLoaderData data,
        string rawName,
        List<LocalCompFile> modList)
    {
        try
        {
            foreach (var file in Common.EnumerateFiles(data.compPath))
                try
                {
                    if (ShouldSkipResourceFile(file, rawName))
                        continue;

                    if (LocalCompFile.IsCompFile(file.FullName, data.compType))
                        modList.Add(new LocalCompFile(file.FullName));
                }
                catch (Exception ex)
                {
                    Common.Log(ex, $"处理文件失败：{file.FullName}");
                }
        }
        catch (Exception ex)
        {
            Common.Log(ex, $"枚举文件夹失败：{data.compPath}");
        }
    }

    private static bool ShouldSkipResourceFile(FileInfo file, string rawName)
    {
        if ((file.DirectoryName ?? "").Equals(rawName, StringComparison.CurrentCultureIgnoreCase))
            return false;

        return !(PageInstanceLeft.mcInstance is not null
            && PageInstanceLeft.mcInstance.Info.HasForge
            && PageInstanceLeft.mcInstance.Info.Drop < 130
            && (file.Directory?.Name ?? "")
            == (PageInstanceLeft.mcInstance.Info.VanillaName ?? ""));
    }

    private static void UpdateResourceListProgress(
        LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader,
        CompLocalLoaderData data,
        int modCount)
    {
        loader.Progress = 0.05d;
        if (modCount > 50)
            Common.RunInUi(() => { data.frm?.Load.ShowProgress = true; });
    }

    private static JsonObject LoadLocalModInfoCache()
    {
        var cachePath = $@"{Common.pathTemp}Cache\LocalComp.json";
        var cache = new JsonObject();

        try
        {
            var cacheContent = Common.ReadFile(cachePath);
            if (!string.IsNullOrWhiteSpace(cacheContent))
            {
                cache = (JsonObject)Common.GetJson(cacheContent);
                if (!cache.ContainsKey("version")
                    || cache["version"].ToObject<int>() != LocalModCacheVersion)
                {
                    Common.Log(
                        "[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息",
                        Common.LogLevel.Debug);
                    cache = new JsonObject();
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "读取本地 Mod 信息缓存失败，已重置");
            cache = new JsonObject();
        }

        cache["version"] = LocalModCacheVersion;
        return cache;
    }

    private static bool LoadModInfoForResourceList(
        LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader,
        CompLocalLoaderData data,
        List<LocalCompFile> modList,
        JsonObject cache,
        List<LocalCompFile> modUpdateList,
        bool jijEnabled,
        string? preferredLoader)
    {
        foreach (var modEntry in modList)
        {
            loader.Progress += 0.94d / modList.Count;
            if (loader.IsAborted)
                return false;

            if (modEntry.IsFolder)
                continue;

            if (jijEnabled)
                modEntry.PreferredLoader = preferredLoader;

            if (data.compType == CompType.Schematic)
                modEntry.LoadBasicInfo();
            else
                modEntry.Load();

            if (modEntry.State == LocalCompFile.LocalFileStatus.Unavailable)
                continue;

            var cacheKey = modEntry.ModrinthHash
                + data.gameVersion.Info.VanillaName
                + data.loaders.Join("");

            if (cache[cacheKey] is JsonObject cachedJson)
            {
                modEntry.FromJson(cachedJson);
                var cacheTime = cachedJson["Comp"]?["CacheTime"];

                if (modEntry.CompLoaded
                    && cacheTime is not null
                    && DateTime.Now - cacheTime.ToObject<DateTime>() < new TimeSpan(6, 0, 0))
                    continue;
            }

            modUpdateList.Add(modEntry);
        }

        return true;
    }

    private static Action? PrepareJarInJarResolution(
        List<LocalCompFile> modList,
        string? jijInstancePath,
        int jijGeneration)
    {
        deferJijOnMiss = false;

        var allModPaths = modList
            .Where(mod => !mod.IsFolder)
            .Select(mod => mod.path)
            .ToList();
        var pending = modList
            .Where(mod => mod is { IsFolder: false, JijPending: true })
            .ToList();

        if (pending.Count == 0)
        {
            JarInJarCache.Prune(allModPaths);
            JarInJarCache.Flush();

            if (jijGeneration == Volatile.Read(ref _jijGeneration))
                compJijResolved = true;

            return null;
        }

        if (jijInstancePath is null)
            throw new InvalidOperationException("无法确定实例路径");

        var loaderTaskId = Task.CurrentId ?? -1;
        return () => _ResolveJijPendingBackground(
            pending,
            allModPaths,
            jijInstancePath,
            loaderTaskId,
            jijGeneration);
    }

    private static int CompareLocalCompFiles(LocalCompFile left, LocalCompFile right)
    {
        var leftUnavailable = left.State == LocalCompFile.LocalFileStatus.Unavailable;
        var rightUnavailable = right.State == LocalCompFile.LocalFileStatus.Unavailable;

        if (leftUnavailable != rightUnavailable)
            return leftUnavailable ? 1 : -1;

        return string.Compare(right.FileName, left.FileName, StringComparison.Ordinal);
    }

    private static void CompleteResourceListLoad(
        LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader,
        CompLocalLoaderData data,
        List<LocalCompFile> modList,
        List<LocalCompFile> modUpdateList,
        JsonObject cache,
        Action? startJijResolve)
    {
        if (loader.IsAborted)
            return;

        loader.output = modList;

        if (startJijResolve is not null)
            Common.RunInNewThread(startJijResolve, "Jar-in-Jar 后台解析");

        if (modUpdateList.Count == 0)
            return;

        data.detailInfo = new KeyValuePair<List<LocalCompFile>, JsonObject>(modUpdateList, cache);
        CompUpdateDetailLoader.Start(loader.input, true);
    }

    public class CompLocalLoaderData
    {
        public string compPath = "";
        public CompType compType;

        public KeyValuePair<List<LocalCompFile>, JsonObject> detailInfo;
        public PageInstanceCompResource? frm;
        public McInstance gameVersion = null!;
        public List<CompLoaderType> loaders = [];
    }
}