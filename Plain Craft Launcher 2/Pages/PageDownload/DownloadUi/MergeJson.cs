using System.IO;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     将多个实例 JSON 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。
    /// </summary>
    private static void MergeJson(
        string outputFolder,
        string minecraftFolder,
        string? optiFineFolder = null,
        bool optiFineAsMod = false,
        string? forgeFolder = null,
        string? forgeVersion = null,
        string? neoForgeFolder = null,
        string? neoForgeVersion = null,
        string? cleanroomFolder = null,
        string? cleanroomVersion = null,
        string? fabricFolder = null,
        string? labyModFolder = null,
        string? labyModChannel = null,
        string? liteLoaderFolder = null,
        Modpack.MmcPackInfo? mmcPackInfo = null,
        string? legacyFabricFolder = null
    )
    {
        Common.Log(
            $"[Download] 开始进行实例合并，输出：{outputFolder}，Minecraft：{minecraftFolder}{(optiFineFolder is not null ? $"，OptiFine：{optiFineFolder}" : "")}{(forgeFolder is not null ? $"，Forge：{forgeFolder}" : "")}{(neoForgeFolder is not null ? $"，NeoForge：{neoForgeFolder}" : "")}{(cleanroomFolder is not null ? $"，Cleanroom：{cleanroomFolder}" : "")}{(liteLoaderFolder is not null ? $"，LiteLoader：{liteLoaderFolder}" : "")}{(fabricFolder is not null ? $"，Fabric：{fabricFolder}" : "")}{(legacyFabricFolder is not null ? $"，LegacyFabric：{legacyFabricFolder}" : "")}{(labyModFolder is not null ? $"，LabyMod：{labyModFolder}" : "")}"
        );
        Directory.CreateDirectory(outputFolder);

        var paths = _PrepareMergeJsonPaths(
            outputFolder,
            minecraftFolder,
            optiFineAsMod ? null : optiFineFolder,
            forgeFolder,
            neoForgeFolder,
            cleanroomFolder,
            liteLoaderFolder,
            fabricFolder,
            legacyFabricFolder,
            labyModFolder
        );
        var jsons = _LoadMergeJsonFiles(paths);

        var allArguments = $"{_GetMinecraftArguments(jsons.Minecraft)} "
            + $"{_GetMinecraftArguments(jsons.LabyMod)} "
            + $"{_GetMinecraftArguments(jsons.OptiFine)} "
            + $"{_GetMinecraftArguments(jsons.Forge)} "
            + $"{_GetMinecraftArguments(jsons.NeoForge)} "
            + $"{_GetMinecraftArguments(jsons.LiteLoader)} "
            + $"{_GetMinecraftArguments(jsons.Cleanroom)}";
        var realArguments = _SplitMergeArguments(allArguments).Distinct().ToList().Join(" ");

        var outputJson = _CreateMergedOutputJson(jsons.Minecraft, mmcPackInfo) ?? new JsonObject();

        _MergeLoaderJson(outputJson, jsons.OptiFine, false);
        _MergeLoaderJson(outputJson, jsons.Forge, mmcPackInfo?.isForgeOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.NeoForge, mmcPackInfo?.isNeoForgeOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.Cleanroom, mmcPackInfo?.isCleanroomOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.LiteLoader, false);
        _MergeLoaderJson(outputJson, jsons.Fabric, mmcPackInfo?.isFabricOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.LegacyFabric, mmcPackInfo?.isFabricOverrided ?? false);
        if (jsons.LabyMod is not null)
            outputJson = _ApplyLabyModJson(outputJson, jsons.LabyMod, labyModChannel ?? "");

        _RemoveConflictingLwjgl2(outputJson);

        if (realArguments is not null && !string.IsNullOrEmpty(realArguments.Replace(" ", "")))
            outputJson["minecraftArguments"] = realArguments;
        if (mmcPackInfo is not null && mmcPackInfo.isMcArgsEdited)
            outputJson.Remove("minecraftArguments");
        outputJson.Remove("_comment_");
        outputJson.Remove("inheritsFrom");
        outputJson.Remove("jar");
        outputJson["id"] = paths.OutputName;

        Common.WriteFile(paths.OutputJsonPath, outputJson.ToString());
        if ((paths.MinecraftJar ?? "") != (paths.OutputJar ?? ""))
        {
            if (File.Exists(paths.OutputJar))
                File.Delete(paths.OutputJar);
            Common.CopyFile(
                paths.MinecraftJar ?? throw new InvalidOperationException("Minecraft Jar 路径无效"),
                paths.OutputJar ?? throw new InvalidOperationException("输出 Jar 路径无效"));
        }

        Common.Log($"[Download] 实例合并 {paths.OutputName} 完成");
    }
}