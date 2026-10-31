using PCL.Core.Utils;
using static PCL.Comp;
using static PCL.Loading;

namespace PCL;

public static partial class LocalComp
{
    public static readonly LoaderTask<CompLocalLoaderData, int> CompUpdateDetailLoader =
        new("Comp List Detail Loader", CompUpdateDetailLoad);

    private static void CompUpdateDetailLoad(LoaderTask<CompLocalLoaderData, int> loader)
    {
        var data = loader.input ?? throw new InvalidOperationException("资源更新加载器输入为空");
        var mods = data.detailInfo.Key;
        var cache = data.detailInfo.Value;
        var modLoaders = data.loaders;
        var compType = data.compType;
        var mcInstance = data.gameVersion.Info.VanillaName;

        LauncherCommon.Log($"[Mod] 目标加载器：{string.Join("/", modLoaders)}，版本：{mcInstance}");
        var endedThreadCount = 0;
        var isFailed = false;
        var currentTaskId = Task.CurrentId ?? -1;

        StartUpdateProvider(
            "Mod List Detail Loader Modrinth",
            () =>
                _LoadModrinthUpdates(
                    mods,
                    mcInstance,
                    modLoaders,
                    compType,
                    loader,
                    currentTaskId));
        StartUpdateProvider(
            "Mod List Detail Loader CurseForge",
            () =>
                _LoadCurseForgeUpdates(
                    mods,
                    mcInstance,
                    modLoaders,
                    compType,
                    loader,
                    currentTaskId));

        while (endedThreadCount < 2)
        {
            if (loader.IsAborted)
                return;
            Thread.Sleep(10);
        }

        if (!_SaveUpdateCache(cache, mods, mcInstance, modLoaders, isFailed))
            return;

        _RefreshUpdateList();
        return;

        void StartUpdateProvider(string name, Func<bool> load)
        {
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        if (load())
                            isFailed = true;
                    }
                    finally
                    {
                        Interlocked.Increment(ref endedThreadCount);
                    }
                },
                name);
        }
    }

    private static void _RefreshUpdateList()
    {
        CompLocalUi.Current?.RefreshModList();
    }

    private static bool _SaveUpdateCache(
        JsonObject cache,
        List<LocalCompFile> mods,
        string mcInstance,
        List<CompLoaderType> modLoaders,
        bool isFailed)
    {
        var cachedMods = mods.Where(m => m.Comp is not null).ToList();
        LauncherCommon.Log($"[Mod] 联网获取本地 Mod 信息完成，为 {cachedMods.Count} 个 Mod 更新缓存");
        if (cachedMods.Count == 0)
            return false;

        foreach (var entry in cachedMods)
        {
            entry.CompLoaded = !isFailed;
            cache[entry.ModrinthHash + mcInstance + string.Join("", modLoaders)] = entry.ToJson();
        }

        LauncherCommon.WriteFile(
            Path.Combine(LauncherPaths.Temp, "Cache", "LocalComp.json"),
            cache.ToJsonString(
                LauncherCommon.ModeDebug
                    ? new JsonSerializerOptions(JsonCompat.SerializerOptions)
                    {
                        WriteIndented = true
                    }
                    : null));

        return true;
    }

    private static bool _LoadModrinthUpdates(
        List<LocalCompFile> mods,
        string mcInstance,
        List<CompLoaderType> modLoaders,
        CompType compType,
        LoaderTask<CompLocalLoaderData, int> loader,
        int currentTaskId)
    {
        try
        {
            var modrinthVersion = _FetchModrinthVersionMap(mods);
            LauncherCommon.Log($"[Mod] 从 Modrinth 获取到 {modrinthVersion.Count} 个本地 Mod 的对应信息");

            if (modrinthVersion.Count == 0)
                return false;
            var modrinthMapping = _MapModrinthLocalFiles(mods, modrinthVersion);

            if (loader.IsAbortedWithThread(currentTaskId))
                return false;
            LauncherCommon.Log($"[Mod] 需要从 Modrinth 获取 {modrinthMapping.Count} 个本地 Mod 的工程信息");

            if (modrinthMapping.Count == 0)
                return false;
            _ApplyModrinthProjects(modrinthMapping);

            LauncherCommon.Log("[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息");
            var modrinthUpdate = _FetchModrinthUpdateMap(
                modrinthMapping,
                mcInstance,
                modLoaders,
                compType);

            foreach (var entry in mods)
                _ApplyModrinthUpdate(entry, modrinthUpdate, mcInstance);

            LauncherCommon.Log("[Mod] 从 Modrinth 获取本地 Mod 信息结束");
            return false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
            return true;
        }
    }

    private static JsonObject _FetchModrinthVersionMap(List<LocalCompFile> mods)
    {
        var hashes = string.Join("\",\"", mods.Select(m => m.ModrinthHash));
        var body = $$"""{"hashes": ["{{hashes}}"], "algorithm": "sha1"}""";
        return (JsonObject)
            LauncherCommon.GetJson(
                GameDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files",
                    "POST",
                    body,
                    "application/json"));
    }

    private static void _ApplyModrinthProjects(
        Dictionary<string, List<LocalCompFile>> modrinthMapping)
    {
        var modrinthProject = (JsonArray)
            LauncherCommon.GetJson(
                GameDownload.DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{string.Join("\",\"", modrinthMapping.Keys)}\"]",
                    "GET",
                    "",
                    "application/json"));

        foreach (var projectJson in modrinthProject)
        {
            if (projectJson is not JsonObject projectObject)
                continue;
            var project = new CompProject(projectObject);
            if (project.Id is null)
                continue;
            foreach (var entry in modrinthMapping[project.Id])
                entry.Comp = project;
        }
    }

    private static JsonObject _FetchModrinthUpdateMap(
        Dictionary<string, List<LocalCompFile>> modrinthMapping,
        string mcInstance,
        List<CompLoaderType> modLoaders,
        CompType compType)
    {
        var targetLoaders =
            compType == CompType.DataPack
                ? "datapack"
                : string.Join("\",\"", modLoaders).ToLower();
        var updateHashes = string.Join(
            "\",\"",
            modrinthMapping.SelectMany(l =>
                l.Value.Select(m => m.ModrinthHash)));
        var body = $$"""
                     {
                       "hashes": ["{{updateHashes}}"],
                       "algorithm": "sha1",
                       "loaders": ["{{targetLoaders}}"],
                       "game_versions": ["{{mcInstance}}"]
                     }
                     """;
        return (JsonObject)
            LauncherCommon.GetJson(
                GameDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files/update",
                    "POST",
                    body,
                    "application/json"));
    }

    private static Dictionary<string, List<LocalCompFile>> _MapModrinthLocalFiles(
        List<LocalCompFile> mods,
        JsonObject modrinthVersion)
    {
        var modrinthMapping = new Dictionary<string, List<LocalCompFile>>();
        foreach (var entry in mods)
        {
            if (modrinthVersion[entry.ModrinthHash] is not JsonObject versionJson)
                continue;
            if (versionJson["files"]?[0]?["hashes"]?["sha1"]?.ToString() != entry.ModrinthHash)
                continue;

            var projectId = versionJson["project_id"]?.ToString();
            if (projectId is null)
                continue;
            if (compProjectCache.TryGetValue(projectId, out var value)
                && entry.Comp is null)
                entry.Comp = value;

            if (!modrinthMapping.ContainsKey(projectId))
                modrinthMapping[projectId] = [];
            modrinthMapping[projectId].Add(entry);

            var fileInfo = new CompFile(versionJson, CompType.Mod);
            if (entry.CompFile is null || entry.CompFile.ReleaseDate < fileInfo.ReleaseDate)
                entry.CompFile = fileInfo;
        }

        return modrinthMapping;
    }

    private static void _ApplyModrinthUpdate(
        LocalCompFile entry,
        JsonObject modrinthUpdate,
        string mcInstance)
    {
        if (modrinthUpdate[entry.ModrinthHash] is not JsonObject updateJson
            || entry.CompFile is null)
            return;

        var updateFile = new CompFile(updateJson, CompType.Mod);
        if (!updateFile.Available)
            return;

        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log(
                $"[Mod] 本地文件 {entry.CompFile.FileName} 在 Modrinth 上的最新版为 {updateFile.FileName}");
        if (entry.CompFile.ReleaseDate >= updateFile.ReleaseDate
            || entry.CompFile.Hash == updateFile.Hash)
            return;

        var changelogUrl =
            $"https://modrinth.com/mod/{updateJson["project_id"]}/changelog?g={mcInstance}";

        if (entry.UpdateFile is not null
            && updateFile.Hash == entry.UpdateFile.Hash)
        {
            entry.ChangelogUrls.Add(changelogUrl);
            entry.UpdateFile.DownloadUrls.AddRange(updateFile.DownloadUrls);
            entry.UpdateFile = updateFile;
        }
        else if (entry.UpdateFile is null
                 || updateFile.ReleaseDate >= entry.UpdateFile.ReleaseDate)
        {
            entry.ChangelogUrls = [changelogUrl];
            entry.UpdateFile = updateFile;
        }
    }

    private static bool _LoadCurseForgeUpdates(
        List<LocalCompFile> mods,
        string mcInstance,
        List<CompLoaderType> modLoaders,
        CompType compType,
        LoaderTask<CompLocalLoaderData, int> loader,
        int currentTaskId)
    {
        try
        {
            var curseForgeRaw = _FetchCurseForgeMatches(mods, loader, currentTaskId);
            if (curseForgeRaw is null)
                return false;
            LauncherCommon.Log($"[Mod] 从 CurseForge 获取到 {curseForgeRaw.Count} 个本地 Mod 的对应信息");

            if (curseForgeRaw.Count == 0)
                return false;
            var curseForgeMapping = _MapCurseForgeLocalFiles(mods, curseForgeRaw);

            if (loader.IsAbortedWithThread(currentTaskId))
                return false;
            LauncherCommon.Log(
                $"[Mod] 需要从 CurseForge 获取 {curseForgeMapping.Count} 个本地 Mod 的工程信息");

            if (curseForgeMapping.Count == 0)
                return false;
            var curseForgeProject = _FetchCurseForgeProjects(curseForgeMapping);

            var (updateFileIds, fileIdToProjectSlug) = _CollectCurseForgeUpdateFiles(
                curseForgeProject,
                curseForgeMapping,
                mcInstance,
                modLoaders,
                compType);

            if (loader.IsAbortedWithThread(currentTaskId))
                return false;
            LauncherCommon.Log(
                $"[Mod] 已从 CurseForge 获取本地 Mod 信息，需要获取 {updateFileIds.Count} 个用于检查更新的文件信息");

            if (updateFileIds.Count == 0)
                return false;
            var curseForgeFiles = _FetchCurseForgeFiles(updateFileIds);

            var updateFiles = _SelectCurseForgeUpdateFiles(curseForgeFiles, updateFileIds);

            foreach (var (entry, updateFile) in updateFiles)
                _ApplyCurseForgeUpdate(entry, updateFile, fileIdToProjectSlug);

            LauncherCommon.Log("[Mod] 从 CurseForge 获取 Mod 更新信息结束");
            return false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
            return true;
        }
    }

    private static JsonArray? _FetchCurseForgeMatches(
        List<LocalCompFile> mods,
        LoaderTask<CompLocalLoaderData, int> loader,
        int currentTaskId)
    {
        var hashes = new List<uint>();
        foreach (var entry in mods)
        {
            hashes.Add(entry.CurseForgeHash);
            if (loader.IsAbortedWithThread(currentTaskId))
                return null;
        }

        var body = $$"""{"fingerprints": [{{string.Join(",", hashes)}}]}""";
        var response = (JsonObject)
            LauncherCommon.GetJson(
                GameDownload.DlModRequest(
                    "https://api.curseforge.com/v1/fingerprints/432",
                    "POST",
                    body,
                    "application/json"));
        return response["data"]?["exactMatches"] as JsonArray ?? [];
    }

    private static JsonArray _FetchCurseForgeProjects(
        Dictionary<string, List<LocalCompFile>> curseForgeMapping)
    {
        var body = $$"""{"modIds": [{{string.Join(",", curseForgeMapping.Keys)}}]}""";
        var response = (JsonObject)LauncherCommon.GetJson(
            GameDownload.DlModRequest(
                "https://api.curseforge.com/v1/mods",
                "POST",
                body,
                "application/json"));
        return response["data"] as JsonArray ?? [];
    }

    private static JsonArray _FetchCurseForgeFiles(
        Dictionary<int, List<LocalCompFile>> updateFileIds)
    {
        var body = $$"""{"fileIds": [{{string.Join(",", updateFileIds.Keys)}}]}""";
        var response = (JsonObject)LauncherCommon.GetJson(
            GameDownload.DlModRequest(
                "https://api.curseforge.com/v1/mods/files",
                "POST",
                body,
                "application/json"));
        return response["data"] as JsonArray ?? [];
    }

    private static Dictionary<string, List<LocalCompFile>> _MapCurseForgeLocalFiles(
        List<LocalCompFile> mods,
        JsonArray curseForgeRaw)
    {
        var curseForgeMapping = new Dictionary<string, List<LocalCompFile>>();
        foreach (var project in curseForgeRaw)
        {
            if (project is null)
                continue;

            var projectId = project["id"]?.ToString();
            if (projectId is null)
                continue;

            var hash = project["file"]?["fileFingerprint"]?.GetValue<uint>() ?? 0;
            foreach (var entry in mods.Where(entry => entry.CurseForgeHash == hash))
            {
                if (compProjectCache.TryGetValue(projectId, out var value)
                    && entry.Comp is null)
                    entry.Comp = value;

                if (!curseForgeMapping.ContainsKey(projectId))
                    curseForgeMapping[projectId] = [];
                curseForgeMapping[projectId].Add(entry);

                if (project["file"] is not JsonObject projectFile)
                    continue;
                var fileInfo = new CompFile(projectFile, CompType.Mod);
                if (entry.CompFile is null || entry.CompFile.ReleaseDate < fileInfo.ReleaseDate)
                    entry.CompFile = fileInfo;
            }
        }

        return curseForgeMapping;
    }

    private static (
        Dictionary<int, List<LocalCompFile>> UpdateFileIds,
        Dictionary<int, string> FileIdToProjectSlug
        ) _CollectCurseForgeUpdateFiles(
            JsonArray curseForgeProject,
            Dictionary<string, List<LocalCompFile>> curseForgeMapping,
            string mcInstance,
            List<CompLoaderType> modLoaders,
            CompType compType)
    {
        var updateFileIds = new Dictionary<int, List<LocalCompFile>>();
        var fileIdToProjectSlug = new Dictionary<int, string>();

        foreach (var projectJson in curseForgeProject)
        {
            if (projectJson is not JsonObject projectObject)
                continue;

            var isAvailable = projectObject["isAvailable"];
            if (isAvailable is not null && !isAvailable.ToObject<bool>())
                continue;

            var project = new CompProject(projectObject);
            if (project.Id is null
                || !curseForgeMapping.TryGetValue(project.Id, out var value))
                continue;

            foreach (var entry in value
                         .Where(entry => entry.Comp is null || entry.Comp.FromCurseForge))
                entry.Comp = project;
            if (modLoaders.Count != 1)
                continue;

            var newestFileIds = _GetLatestCurseForgeFileIds(
                projectJson,
                mcInstance,
                modLoaders.Single(),
                compType);

            foreach (var fileId in newestFileIds)
            {
                if (!updateFileIds.ContainsKey(fileId))
                    updateFileIds[fileId] = [];
                updateFileIds[fileId].AddRange(curseForgeMapping[project.Id]);
                fileIdToProjectSlug[fileId] = project.Slug;
            }
        }

        return (updateFileIds, fileIdToProjectSlug);
    }

    private static List<int> _GetLatestCurseForgeFileIds(
        JsonNode projectJson,
        string mcInstance,
        CompLoaderType modLoader,
        CompType compType)
    {
        string? newestVersion = null;
        var newestFileIds = new List<int>();
        var latestFilesIndexes = projectJson["latestFilesIndexes"] as JsonArray ?? [];
        foreach (var indexEntry in latestFilesIndexes)
        {
            var loaderNode = indexEntry?["modLoader"];
            if (compType != CompType.DataPack
                && (loaderNode is null || (int)modLoader != loaderNode.ToObject<int>()))
                continue;

            var indexVersion = indexEntry?["gameVersion"]?.ToString();
            if (indexVersion is null
                || indexVersion != mcInstance)
                continue;

            if (newestVersion is not null
                && McVersionComparer.CompareVersion(newestVersion, indexVersion) > -1)
                continue;

            var fileIdNode = indexEntry?["fileId"];
            if (fileIdNode is null)
                continue;

            if (newestVersion != indexVersion)
            {
                newestVersion = indexVersion;
                newestFileIds.Clear();
            }

            newestFileIds.Add(fileIdNode.ToObject<int>());
        }

        return newestFileIds;
    }

    private static Dictionary<LocalCompFile, CompFile> _SelectCurseForgeUpdateFiles(
        JsonArray curseForgeFiles,
        Dictionary<int, List<LocalCompFile>> updateFileIds)
    {
        var updateFiles = new Dictionary<LocalCompFile, CompFile>();
        foreach (var fileJson in curseForgeFiles)
        {
            if (fileJson is not JsonObject fileObject)
                continue;

            var updateFile = new CompFile(fileObject, CompType.Mod);
            if (!updateFile.Available)
                continue;

            if (!int.TryParse(updateFile.Id, out var fileId)
                || !updateFileIds.TryGetValue(fileId, out var id))
                continue;

            foreach (var entry in id.Where(entry =>
                         !updateFiles.ContainsKey(entry)
                         || updateFiles[entry].ReleaseDate < updateFile.ReleaseDate))
                updateFiles[entry] = updateFile;
        }

        return updateFiles;
    }

    private static void _ApplyCurseForgeUpdate(
        LocalCompFile entry,
        CompFile updateFile,
        Dictionary<int, string> fileIdToProjectSlug)
    {
        if (entry.CompFile is null)
            return;
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log(
                $"[Mod] 本地文件 {entry.CompFile.FileName} 在 CurseForge 上的最新版为 {updateFile.FileName}");
        if (entry.CompFile.ReleaseDate >= updateFile.ReleaseDate
            || entry.CompFile.Hash == updateFile.Hash)
            return;

        var changelogUrl =
            "https://www.curseforge.com/minecraft/mc-mods/"
            + $"{fileIdToProjectSlug[int.Parse(updateFile.Id)]}/files/{updateFile.Id}";

        if (entry.UpdateFile is not null
            && updateFile.Hash == entry.UpdateFile.Hash)
        {
            entry.ChangelogUrls.Add(changelogUrl);
            entry.UpdateFile.DownloadUrls.AddRange(updateFile.DownloadUrls);
        }
        else if (entry.UpdateFile is null
                 || updateFile.ReleaseDate > entry.UpdateFile.ReleaseDate)
        {
            entry.ChangelogUrls = [changelogUrl];
            entry.UpdateFile = updateFile;
        }
    }
}