using System.IO;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    private static MergeJsonPaths _PrepareMergeJsonPaths(
        string outputFolder,
        string minecraftFolder,
        string? optiFineFolder,
        string? forgeFolder,
        string? neoForgeFolder,
        string? cleanroomFolder,
        string? liteLoaderFolder,
        string? fabricFolder,
        string? legacyFabricFolder,
        string? labyModFolder
    )
    {
        var paths = new MergeJsonPaths();

        outputFolder = _NormalizeVersionFolder(outputFolder);
        paths.OutputName = Common.GetFolderNameFromPath(outputFolder);
        paths.OutputJsonPath = Path.Combine(outputFolder, $"{paths.OutputName}.json");
        paths.OutputJar = Path.Combine(outputFolder, $"{paths.OutputName}.jar");

        minecraftFolder = _NormalizeVersionFolder(minecraftFolder);
        var minecraftName = Common.GetFolderNameFromPath(minecraftFolder);
        paths.MinecraftJsonPath = Path.Combine(minecraftFolder, $"{minecraftName}.json");
        paths.MinecraftJar = Path.Combine(minecraftFolder, $"{minecraftName}.jar");

        if (optiFineFolder is not null)
            paths.OptiFineJsonPath = _PrepareVersionFolder(ref optiFineFolder);

        if (forgeFolder is not null)
            paths.ForgeJsonPath = _PrepareVersionFolder(ref forgeFolder);

        if (neoForgeFolder is not null)
            paths.NeoForgeJsonPath = _PrepareVersionFolder(ref neoForgeFolder);

        if (cleanroomFolder is not null)
            paths.CleanroomJsonPath = _PrepareVersionFolder(ref cleanroomFolder);

        if (liteLoaderFolder is not null)
            paths.LiteLoaderJsonPath = _PrepareVersionFolder(ref liteLoaderFolder);

        if (fabricFolder is not null)
            paths.FabricJsonPath = _PrepareVersionFolder(ref fabricFolder);

        if (legacyFabricFolder is not null)
            paths.LegacyFabricJsonPath = _PrepareVersionFolder(ref legacyFabricFolder);

        if (labyModFolder is not null)
            paths.LabyModJsonPath = _PrepareVersionFolder(ref labyModFolder);

        return paths;
    }

    private static MergeJsonFiles _LoadMergeJsonFiles(MergeJsonPaths paths)
    {
        var files = new MergeJsonFiles();

        if (paths.LabyModJsonPath is null)
            files.Minecraft = _ReadMergeJson(paths.MinecraftJsonPath, "Minecraft");

        if (paths.OptiFineJsonPath is not null)
            files.OptiFine = _ReadMergeJson(paths.OptiFineJsonPath, "OptiFine");

        if (paths.ForgeJsonPath is not null)
            files.Forge = _ReadMergeJson(paths.ForgeJsonPath, "Forge");

        if (paths.NeoForgeJsonPath is not null)
            files.NeoForge = _ReadMergeJson(paths.NeoForgeJsonPath, "NeoForge");

        if (paths.CleanroomJsonPath is not null)
            files.Cleanroom = _ReadMergeJson(paths.CleanroomJsonPath, "Cleanroom");

        if (paths.LiteLoaderJsonPath is not null)
            files.LiteLoader = _ReadMergeJson(paths.LiteLoaderJsonPath, "LiteLoader");

        if (paths.FabricJsonPath is not null)
            files.Fabric = _ReadMergeJson(paths.FabricJsonPath, "Fabric");

        if (paths.LegacyFabricJsonPath is not null)
            files.LegacyFabric = _ReadMergeJson(
                paths.LegacyFabricJsonPath,
                "Legacy Fabric",
                paths.FabricJsonPath);

        if (paths.LabyModJsonPath is not null)
            files.LabyMod = _ReadMergeJson(paths.LabyModJsonPath, "LabyMod");

        return files;
    }

    private static string _NormalizeVersionFolder(string folder)
    {
        return folder.EndsWithF(@"\") ? folder : $@"{folder}\";
    }

    private static string _PrepareVersionFolder(ref string folder)
    {
        folder = _NormalizeVersionFolder(folder);
        var folderName = Common.GetFolderNameFromPath(folder);
        return Path.Combine(folder, $"{folderName}.json");
    }

    private static JsonObject _ReadMergeJson(
        string jsonPath,
        string sourceName,
        string? reportedPath = null)
    {
        var jsonText = Common.ReadFile(jsonPath);
        if (!jsonText.StartsWithF("{"))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.JsonInvalid",
                    sourceName,
                    reportedPath ?? jsonPath,
                    jsonText.Substring(0, Math.Min(jsonText.Length, 1000))
                )
            );
        return (JsonObject)Common.GetJson(jsonText);
    }

    private static string _GetMinecraftArguments(JsonObject? json)
    {
        return (json?["minecraftArguments"] ?? " ").ToString();
    }

    private sealed class MergeJsonPaths
    {
        public string? CleanroomJsonPath;
        public string? FabricJsonPath;
        public string? ForgeJsonPath;
        public string? LabyModJsonPath;
        public string? LegacyFabricJsonPath;
        public string? LiteLoaderJsonPath;
        public string MinecraftJar = "";
        public string MinecraftJsonPath = "";
        public string? NeoForgeJsonPath;
        public string? OptiFineJsonPath;
        public string OutputJar = "";
        public string OutputJsonPath = "";
        public string OutputName = "";
    }

    private sealed class MergeJsonFiles
    {
        public JsonObject? Cleanroom;
        public JsonObject? Fabric;
        public JsonObject? Forge;
        public JsonObject? LabyMod;
        public JsonObject? LegacyFabric;
        public JsonObject? LiteLoader;
        public JsonObject? Minecraft;
        public JsonObject? NeoForge;
        public JsonObject? OptiFine;
    }
}