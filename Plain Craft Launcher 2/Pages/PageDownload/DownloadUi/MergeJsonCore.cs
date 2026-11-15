using PCL.Core.Utils;

namespace PCL;

public static partial class DownloadUi
{
    private static List<string> _SplitMergeArguments(string allArguments)
    {
        var rawArguments = allArguments
            .Split(" ")
            .Where(l => !string.IsNullOrEmpty(l))
            .Select(l => l.Trim())
            .ToList();
        var splitArguments = new List<string>();
        for (int i = 0, loopTo = rawArguments.Count - 1; i <= loopTo; i++)
            if (rawArguments[i].StartsWithF("-"))
                splitArguments.Add(rawArguments[i]);
            else if (splitArguments.Count != 0
                     && splitArguments.Last().StartsWithF("-")
                     && !splitArguments.Last().Contains(' '))
                splitArguments[^1] = $"{splitArguments.Last()} {rawArguments[i]}";
            else
                splitArguments.Add(rawArguments[i]);

        return splitArguments;
    }

    private static JsonObject? _CreateMergedOutputJson(
        JsonObject? minecraftJson,
        MmcPackInfo? mmcPackInfo)
    {
        if (mmcPackInfo is null)
            return minecraftJson;

        if (!mmcPackInfo.isMinecraftOverrided)
        {
            Common.Log("[Download] 存在无修改 MC 核心文件的 MMC 整合包信息，应用相关参数");
            minecraftJson?.Merge(mmcPackInfo.overridedJson);
            return minecraftJson;
        }

        Common.Log("[Download] 当前实例的 MC 核心已被修改，使用对应的 MMC 整合包参数");
        var overriddenJson = minecraftJson is not null
            ? (JsonObject)minecraftJson.DeepClone()
            : new JsonObject();
        foreach (var patchProperty in mmcPackInfo.overridedJson)
            overriddenJson[patchProperty.Key] = patchProperty.Value?.DeepClone();
        return overriddenJson;
    }

    private static void _MergeLoaderJson(
        JsonObject outputJson,
        JsonObject? loaderJson,
        bool isOverridden)
    {
        if (loaderJson is null || isOverridden)
            return;

        loaderJson.Remove("releaseTime");
        loaderJson.Remove("time");
        outputJson.Merge(loaderJson);
    }
}