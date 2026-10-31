using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Exts;

namespace PCL;

public static partial class InstanceList
{
    private static Dictionary<McInstanceCardType, List<McInstance>> InitMcInstanceListWithoutCache(
        string path)
    {
        var instanceList = LoadMcInstancesWithoutCache(path);
        var results = new Dictionary<McInstanceCardType, List<McInstance>>();

        try
        {
            ClassifyMcInstances(instanceList, results);
        }
        catch (Exception ex)
        {
            results.Clear();
            Common.Log(
                ex,
                Lang.Text("Select.Instance.Error.Classify"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.Classify"));
        }

        results = new[]
            {
                McInstanceCardType.Star,
                McInstanceCardType.API,
                McInstanceCardType.OriginalLike,
                McInstanceCardType.Rubbish,
                McInstanceCardType.Fool,
                McInstanceCardType.Error,
                McInstanceCardType.Hidden
            }
            .Where(results.ContainsKey)
            .ToDictionary(type => type, type => results[type]);

        SortMcInstanceGroups(results);
        WriteMcInstanceListCache(path, results);

        return results;
    }

    private static List<McInstance> LoadMcInstancesWithoutCache(string path)
    {
        var instances = new List<McInstance>();

        foreach (var folder in new DirectoryInfo($"{path}versions").GetDirectories())
        {
            if (!folder.Exists || !folder.EnumerateFiles().Any())
            {
                Common.Log($"[Minecraft] 跳过空文件夹：{folder.FullName}");
                continue;
            }

            if (folder.Name is "cache" or "BLClient" or "PCL"
                && !File.Exists(Path.Combine(folder.FullName, $"{folder.Name}.json")))
            {
                Common.Log($"[Minecraft] 跳过可能不是实例文件夹的项目：{folder.FullName}");
                continue;
            }

            var instanceFolder = $@"{folder.FullName}\";
            var ignoreFilePath = $"{instanceFolder}.pclignore";

            if (File.Exists(ignoreFilePath))
            {
                if (!_isFirstMcInstanceListLoad)
                {
                    Common.Log($"[Minecraft] 跳过要求忽略的项目：{instanceFolder}");
                    continue;
                }

                Common.Log($"[Minecraft] 清理残留的忽略项目：{instanceFolder}");

                try
                {
                    File.Delete(ignoreFilePath);
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        Lang.Text("Select.Folder.Error.Cleanup", instanceFolder),
                        Common.LogLevel.Hint,
                        userSummary: Lang.Text("Select.Folder.Error.Cleanup", instanceFolder));
                }
            }

            var instance = new McInstance(instanceFolder);
            instances.Add(instance);
            instance.Load();
        }

        return instances;
    }

    private static void ClassifyMcInstances(
        List<McInstance> instanceList,
        Dictionary<McInstanceCardType, List<McInstance>> results)
    {
        var instancesByType = new Dictionary<McInstanceCardType, List<McInstance>>();

        var starredInstances = instanceList
            .Where(instance => instance.IsStar)
            .Where(instance => instance.displayType != McInstanceCardType.Hidden)
            .ToList();

        foreach (var instance in starredInstances)
            instanceList.Remove(instance);

        if (starredInstances.Count != 0)
            instancesByType.Add(McInstanceCardType.Star, starredInstances);

        McInstanceFilter(
            ref instanceList,
            ref instancesByType,
            [McInstanceState.Error],
            McInstanceCardType.Error);

        McInstanceFilter(
            ref instanceList,
            ref instancesByType,
            [McInstanceState.Fool],
            McInstanceCardType.Fool);

        McInstanceFilter(
            ref instanceList,
            ref instancesByType,
            [
                McInstanceState.Forge,
                McInstanceState.NeoForge,
                McInstanceState.LiteLoader,
                McInstanceState.Fabric,
                McInstanceState.LegacyFabric,
                McInstanceState.Quilt,
                McInstanceState.Cleanroom,
                McInstanceState.LabyMod
            ],
            McInstanceCardType.API);

        var usefulInstances = new List<McInstance>();
        var rubbishInstances = new List<McInstance>();

        McInstanceFilter(
            ref instanceList,
            [McInstanceState.Old],
            ref rubbishInstances);

        var latestInstance = instanceList
            .Where(instance => instance.state is
                McInstanceState.Original or McInstanceState.Snapshot)
            .MaxOrDefault(instance => instance.releaseTime);

        if (latestInstance is not null && latestInstance.state == McInstanceState.Snapshot)
        {
            usefulInstances.Add(latestInstance);
            instanceList.Remove(latestInstance);
        }

        McInstanceFilter(
            ref instanceList,
            [McInstanceState.Snapshot],
            ref rubbishInstances);

        var newestInstancesByKey = new Dictionary<string, McInstance>();
        var existingDrops = new List<int>();

        foreach (var instance in instanceList.Where(instance => instance.Info.Valid))
        {
            var drop = instance.Info.Drop;

            if (!existingDrops.Contains(drop))
                existingDrops.Add(drop);

            var key = $"{drop}-{(int)instance.state}";

            if (newestInstancesByKey.TryAdd(key, instance))
                continue;

            var currentInstance = newestInstancesByKey[key];

            if (instance.Info.HasOptiFine)
            {
                if (instance.Info.OptiFineCode > currentInstance.Info.OptiFineCode)
                    newestInstancesByKey[key] = instance;
            }
            else if (instance.releaseTime > currentInstance.releaseTime)
            {
                newestInstancesByKey[key] = instance;
            }
        }

        foreach (var drop in existingDrops)
        {
            var optiFineKey = $"{drop}-{(int)McInstanceState.OptiFine}";
            var originalKey = $"{drop}-{(int)McInstanceState.Original}";

            if (newestInstancesByKey.TryGetValue(optiFineKey, out var optiFineInstance))
            {
                if (newestInstancesByKey.TryGetValue(originalKey, out var vanillaInstance)
                    && vanillaInstance.Info.Drop > optiFineInstance.Info.Drop)
                {
                    usefulInstances.Add(vanillaInstance);
                    instanceList.Remove(vanillaInstance);
                }

                usefulInstances.Add(optiFineInstance);
                instanceList.Remove(optiFineInstance);
            }
            else if (newestInstancesByKey.TryGetValue(originalKey, out var originalInstance))
            {
                usefulInstances.Add(originalInstance);
                instanceList.Remove(originalInstance);
            }
        }

        rubbishInstances.AddRange(instanceList);

        if (usefulInstances.Count != 0)
            instancesByType.Add(McInstanceCardType.OriginalLike, usefulInstances);

        if (rubbishInstances.Count != 0)
            instancesByType.Add(McInstanceCardType.Rubbish, rubbishInstances);

        foreach (var instancePair in instancesByType)
        foreach (var instance in instancePair.Value)
        {
            var realType = instance.displayType == 0 || instancePair.Key == McInstanceCardType.Star
                ? instancePair.Key
                : instance.displayType;

            if (!results.TryGetValue(realType, out var targetInstances))
            {
                targetInstances = [];
                results.Add(realType, targetInstances);
            }

            targetInstances.Add(instance);
        }
    }

    private static void SortMcInstanceGroups(
        Dictionary<McInstanceCardType, List<McInstance>> results)
    {
        foreach (var cardType in new[]
                 {
                     McInstanceCardType.Star,
                     McInstanceCardType.API,
                     McInstanceCardType.OriginalLike,
                     McInstanceCardType.Rubbish,
                     McInstanceCardType.Fool
                 })
        {
            if (!results.TryGetValue(cardType, out var instances))
                continue;

            results[cardType] = SortUtils.Sort(instances, CompareMcInstancesForDisplay);
        }
    }

    private static bool CompareMcInstancesForDisplay(McInstance left, McInstance right)
    {
        if ((left.releaseTime.Year >= 2000 || right.releaseTime.Year >= 2000)
            && left.releaseTime != right.releaseTime)
            return left.releaseTime > right.releaseTime;

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

        var leftComponentCode = GetMcInstanceComponentCode(left);
        var rightComponentCode = GetMcInstanceComponentCode(right);

        if (leftComponentCode != rightComponentCode)
            return leftComponentCode > rightComponentCode;

        return string.CompareOrdinal(left.Name, right.Name) > 0;
    }

    private static int GetMcInstanceComponentCode(McInstance instance)
    {
        return instance.Info.ForgelikeCode > 0
            ? instance.Info.ForgelikeCode
            : instance.Info.HasOptiFine
                ? instance.Info.OptiFineCode
                : 0;
    }

    private static void WriteMcInstanceListCache(
        string path,
        Dictionary<McInstanceCardType, List<McInstance>> results)
    {
        var iniPath = $"{path}PCL.ini";

        Common.WriteIni(iniPath, "CardCount", results.Count.ToString());

        for (var index = 0; index < results.Count; index++)
        {
            Common.WriteIni(
                iniPath,
                $"CardKey{index + 1}",
                ((int)results.Keys.ElementAtOrDefault(index)).ToString());

            var value = results
                .Values
                .ElementAt(index)
                .Aggregate("", (current, instance) => $"{current}{instance.Name}:");

            Common.WriteIni(iniPath, $"CardValue{index + 1}", value);
        }
    }
}
