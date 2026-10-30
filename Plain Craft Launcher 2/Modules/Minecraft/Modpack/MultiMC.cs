using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackMmc(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder)
    {
        JsonObject packJson;
        string packInstance;
        MmcPackInfo? packInfo;
        try
        {
            packJson = _ReadMmcPackJson(archive, archiveBaseFolder, out packInstance);
            packInfo = _ParseMmcJsonPatches(archive, archiveBaseFolder, packJson);
        }
        catch (Exception ex)
        {
            throw new Exception("MMC 整合包安装信息存在问题", ex);
        }

        var instanceName = _GetMmcInstanceName(packInstance);
        var installTemp = Launcher.RequestTaskTempFolder();
        var versionFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\";
        var installLoaders = _CreateMmcInstallLoaders(
            fileAddress,
            installTemp,
            archiveBaseFolder,
            instanceName,
            versionFolder);

        if (packJson["components"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));

        var request = _CreateMmcInstallRequest(instanceName, packJson);
        if (packInfo is not null)
            request.mmcPackInfo = packInfo;

        var mergeLoaders = DownloadUi.McInstallLoader(request);
        var loaders = _CreateModpackInstallLoaderList(installLoaders, mergeLoaders);
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.MmcInstall", instanceName);
        if (loaderTaskbar.Any(l => (l.name ?? "") == (loaderName ?? "")))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), HintType.Error);
            throw new Common.CancelledException();
        }

        var loader = new LoaderCombo<string>(loaderName, loaders)
        {
            OnStateChanged = DownloadUi.McInstallState
        };
        loader.Start(request.targetInstanceFolder);
        LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        Common.RunInUi(() => Launcher.frmMain.PageChange(FormMain.PageType.TaskManager));
        return loader;
    }

    private static JsonObject _ReadMmcPackJson(
        ZipArchive archive,
        string archiveBaseFolder,
        out string packInstance)
    {
        var packJson = (JsonObject)
            Common.GetJson(
                Common.ReadFile(
                    archive.GetEntry($"{archiveBaseFolder}mmc-pack.json")!.Open(),
                    Encoding.UTF8));

        var instanceCfgEntry = archive.GetEntry($"{archiveBaseFolder}instance.cfg");
        packInstance = instanceCfgEntry is null
            ? ""
            : Common.ReadFile(instanceCfgEntry.Open(), Encoding.UTF8);

        return packJson;
    }

    private static MmcPackInfo? _ParseMmcJsonPatches(
        ZipArchive archive,
        string archiveBaseFolder,
        JsonObject packJson)
    {
        if (!_HasMmcJsonPatches(archive, archiveBaseFolder))
            return null;

        try
        {
            Common.Log("[ModPack] 安装的 MultiMC 整合包存在 JSON Patches");
            var patches = _LoadMmcJsonPatches(archive, archiveBaseFolder);
            var components = packJson["components"]?.AsArray() ?? [];
            var componentUids = components.Select(c => c?["uid"]?.ToString()).ToHashSet();
            patches =
            [
                .. patches
                    .Where(p => componentUids.Contains(p.Key["uid"]?.ToString()))
                    .OrderBy(p => p.Value)
            ];

            var packInfo = new MmcPackInfo();
            var state = new MmcPatchState();
            foreach (var patch in patches)
                _ApplyMmcPatch(packInfo, patch.Key, state);

            _BuildMmcOverrideJson(packInfo, state);
            return packInfo;
        }
        catch (Exception ex)
        {
            throw new Exception("应用 MMC JSON-Patches 失败", ex);
        }
    }

    private static bool _HasMmcJsonPatches(ZipArchive archive, string archiveBaseFolder)
    {
        return archive.Entries.Any(e =>
            !e.FullName.EndsWith("/")
            && e.FullName.StartsWith(
                $"{archiveBaseFolder}patches/",
                StringComparison.OrdinalIgnoreCase));
    }

    private static List<KeyValuePair<JsonObject, int>> _LoadMmcJsonPatches(
        ZipArchive archive,
        string archiveBaseFolder)
    {
        return
        [
            ..
            from entry in archive.Entries
            where
                !entry.FullName.EndsWith("/")
                && entry.FullName.StartsWith(
                    $"{archiveBaseFolder}patches/",
                    StringComparison.OrdinalIgnoreCase)
            select (JsonObject)
                Common.GetJson(
                    Common.ReadFile(archive.GetEntry(entry.FullName)!.Open(), Encoding.UTF8)
                )
            into patch
            select new KeyValuePair<JsonObject, int>(patch, (int)(patch["order"] ?? 0))
        ];
    }

    private static void _ApplyMmcPatch(
        MmcPackInfo packInfo,
        JsonObject patchJson,
        MmcPatchState state)
    {
        _ApplyMmcPatchType(packInfo, patchJson);

        if (patchJson["+jvmArgs"] is not null)
        {
            state.JvmArguments.Merge(patchJson["+jvmArgs"]);
            Common.Log($"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 JVM 参数");
        }

        if (patchJson["libraries"] is not null || patchJson["+libraries"] is not null)
            _ApplyMmcPatchLibraries(patchJson, state);

        if (patchJson["+tweakers"] is not null)
            _ApplyMmcPatchTweakers(patchJson, state);

        if (patchJson["assetIndex"] is not null)
            _ApplyMmcPatchAssetIndex(patchJson, state);

        if (patchJson["minecraftArguments"] is not null)
            _ApplyMmcPatchGameArguments(packInfo, patchJson, state);

        if (patchJson["mainClass"] is not null)
            _ApplyMmcPatchMainClass(patchJson, state);

        if (patchJson["compatibleJavaMajors"] is JsonArray { Count: > 0 } javaMajors)
            _ApplyMmcPatchJavaMajors(patchJson, state, javaMajors);
    }

    private static void _ApplyMmcPatchType(MmcPackInfo packInfo, JsonObject patchJson)
    {
        switch (patchJson["uid"]?.ToString())
        {
            case "net.minecraft":
                packInfo.isMinecraftOverrided = true;
                break;
            case "net.minecraftforge":
                if (patchJson["version"]?.ToString()?.StartsWithF("0.") == true)
                    packInfo.isCleanroomOverrided = true;
                else
                    packInfo.isForgeOverrided = true;
                break;
            case "net.neoforged":
                packInfo.isNeoForgeOverrided = true;
                break;
            case "net.fabricmc.fabric-loader":
                packInfo.isFabricOverrided = true;
                break;
            case "org.quiltmc.quilt-loader":
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
        }
    }

    private static void _ApplyMmcPatchLibraries(JsonObject patchJson, MmcPatchState state)
    {
        var libs = new JsonArray();
        _AppendMmcPatchLibraries(libs, patchJson["libraries"]);
        _AppendMmcPatchLibraries(libs, patchJson["+libraries"]);
        state.Libraries.Merge(libs);
        Common.Log($"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 Libraries");
    }

    private static void _ApplyMmcPatchTweakers(JsonObject patchJson, MmcPatchState state)
    {
        state.Tweakers = patchJson["+tweakers"]![0]?.ToString();
        Common.Log($"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 Tweakers");
    }

    private static void _ApplyMmcPatchAssetIndex(JsonObject patchJson, MmcPatchState state)
    {
        state.AssetIndex = patchJson["assetIndex"]?.DeepClone().AsObject();
        Common.Log($"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 AssetIndex");
    }

    private static void _ApplyMmcPatchGameArguments(
        MmcPackInfo packInfo,
        JsonObject patchJson,
        MmcPatchState state)
    {
        foreach (var arg in patchJson["minecraftArguments"]!.ToString().Split(" "))
            state.GameArguments.Add(arg);
        packInfo.isMcArgsEdited = true;
        Common.Log(
            $"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 minecraftArguments 至 arguments.game");
    }

    private static void _ApplyMmcPatchMainClass(JsonObject patchJson, MmcPatchState state)
    {
        state.MainClass = patchJson["mainClass"]?.ToString();
        Common.Log($"[ModPack] 已应用 JSON-patch {patchJson["uid"]} 的 mainClass");
    }

    private static void _ApplyMmcPatchJavaMajors(
        JsonObject patchJson,
        MmcPatchState state,
        JsonArray javaMajors)
    {
        var majors = javaMajors
            .Select(java => (int)Math.Round(Common.Val(java?.ToString() ?? "")))
            .Where(major => major > 0)
            .Distinct()
            .OrderBy(major => major)
            .ToList();
        if (majors.Count == 0)
            return;

        state.JavaMajorsSet = _MergeJavaMajors(
            state.JavaMajorsSet,
            majors,
            patchJson["uid"]?.ToString() ?? "");
        Common.Log(
            $"[ModPack] JSON-patch {patchJson["uid"]} 兼容的 Java 主版本：{string.Join(", ", majors)}");
    }

    private static void _AppendMmcPatchLibraries(JsonArray libs, JsonNode? source)
    {
        if (source is null)
            return;

        foreach (var library in source.AsArray())
        {
            if (library is not JsonObject libraryObj)
                continue;

            var libJson = libraryObj.DeepClone().AsObject();
            if (libJson["MMC-hint"] is not null)
            {
                libJson.Add("hint", libJson["MMC-hint"]?.DeepClone());
                libJson.Remove("MMC-hint");
            }

            libs.Add(libJson);
        }
    }

    private static List<int> _MergeJavaMajors(
        List<int>? currentMajors,
        List<int> majors,
        string patchUid)
    {
        if (currentMajors is null)
            return majors;

        var intersect = majors.Where(currentMajors.Contains).ToList();
        if (intersect.Count != 0)
            return intersect;
        Common.Log(
            $"[ModPack] JSON-patch {patchUid} 要求的 Java 主版本（{string.Join(", ", majors)}）"
            + $"与此前的要求（{string.Join(", ", currentMajors)}）无交集，已使用当前补丁的要求");
        return majors;
    }

    private static void _BuildMmcOverrideJson(MmcPackInfo packInfo, MmcPatchState state)
    {
        var javaVersionJson = _CreateMmcJavaVersionJson(
            state.JavaMajorsSet,
            out var javaMajorsJson);

        if (!string.IsNullOrWhiteSpace(state.Tweakers))
        {
            state.GameArguments.Add("--tweakClass");
            state.GameArguments.Add(state.Tweakers);
        }

        JsonObject? jsonArguments = null;
        if (state.GameArguments is not null || state.JvmArguments is not null)
        {
            state.JvmArguments.Insert(0, "-Djava.library.path=${natives_directory}");
            state.JvmArguments.Insert(1, "-Dminecraft.launcher.brand=${launcher_name}");
            state.JvmArguments.Insert(2, "-Dminecraft.launcher.version=${launcher_version}");
            state.JvmArguments.Insert(3, "-cp");
            state.JvmArguments.Insert(4, "${classpath}");
            jsonArguments = new JsonObject
            {
                {
                    "game", state.GameArguments
                },
                {
                    "jvm", state.JvmArguments
                }
            };
        }

        packInfo.overridedJson = new JsonObject();
        if (jsonArguments is not null)
            packInfo.overridedJson.Add("arguments", jsonArguments);
        if (state.MainClass is not null)
            packInfo.overridedJson.Add("mainClass", state.MainClass);
        if (state.AssetIndex is not null)
            packInfo.overridedJson.Add("assetIndex", state.AssetIndex);
        if (javaVersionJson is not null)
            packInfo.overridedJson.Add("javaVersion", javaVersionJson);
        if (javaMajorsJson is not null)
            packInfo.overridedJson.Add("compatibleJavaMajors", javaMajorsJson);
        if (state.Libraries is not null)
            packInfo.overridedJson.Add("libraries", state.Libraries);
    }

    private static JsonObject? _CreateMmcJavaVersionJson(
        List<int>? javaMajorsSet,
        out JsonArray? javaMajorsJson)
    {
        javaMajorsJson = null;
        if (javaMajorsSet is null || javaMajorsSet.Count == 0)
            return null;

        var javaMajorMin = javaMajorsSet.Min();
        var javaComponent = javaMajorMin switch
        {
            8 => "jre-legacy",
            17 => "java-runtime-gamma",
            21 => "java-runtime-delta",
            25 => "java-runtime-epsilon",
            _ => null
        };
        var javaVersionJson = new JsonObject
        {
            {
                "majorVersion", javaMajorMin
            }
        };
        if (javaComponent is not null)
            javaVersionJson.Add("component", javaComponent);

        javaMajorsJson = [];
        foreach (var major in javaMajorsSet)
            javaMajorsJson.Add(major);

        Common.Log($"[ModPack] 整合包要求的 Java 主版本集合：{string.Join(", ", javaMajorsSet)}");
        return javaVersionJson;
    }

    private static string _GetMmcInstanceName(string packInstance)
    {
        var instanceName = MmcInstanceNameRegex().Match(packInstance) is { Success: true } match
            ? match.Value
            : "";
        return _ResolveModpackInstanceName(null, instanceName);
    }

    [GeneratedRegex(@"(?<=\nname\=)[^\n]+")]
    private static partial Regex MmcInstanceNameRegex();

    private static List<LoaderBase> _CreateMmcInstallLoaders(
        string fileAddress,
        string installTemp,
        string archiveBaseFolder,
        string instanceName,
        string versionFolder)
    {
        var installLoaders = new List<LoaderBase>
        {
            new LoaderTask<string, int>(
                Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
                task =>
                    _ExtractMmcModpack(
                        task,
                        fileAddress,
                        installTemp,
                        archiveBaseFolder,
                        instanceName,
                        versionFolder))
            {
                ProgressWeight = new FileInfo(fileAddress).Length / 1024d / 1024d / 6d,
                block = false
            }
        };
        return installLoaders;
    }

    private static void _ExtractMmcModpack(
        LoaderTask<string, int> task,
        string fileAddress,
        string installTemp,
        string archiveBaseFolder,
        string instanceName,
        string versionFolder)
    {
        ExtractModpackFiles(installTemp, fileAddress, task, 0.55d);
        CopyOverrideDirectory(
            Path.Combine(installTemp, archiveBaseFolder, "libraries"),
            Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName, "libraries"),
            task,
            0.2d);
        CopyOverrideDirectory(
            Path.Combine(installTemp, archiveBaseFolder, ".minecraft"),
            Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName),
            task,
            0.2d);

        try
        {
            _MigrateMmcSettings(installTemp, archiveBaseFolder, instanceName, versionFolder);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"读取 MMC 配置文件失败（{installTemp}{archiveBaseFolder}instance.cfg）");
        }

        (
            (DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)
        ).InvalidateCache(versionFolder);
    }

    private static void _MigrateMmcSettings(
        string installTemp,
        string archiveBaseFolder,
        string instanceName,
        string versionFolder)
    {
        var mmcSetupFile = Path.Combine(installTemp, archiveBaseFolder, "instance.cfg");
        if (!File.Exists(mmcSetupFile))
            return;

        var lines = (
            from line in Common
                .ReadFile(mmcSetupFile)
                .Split(["\r", "\n"], StringSplitOptions.RemoveEmptyEntries)
            where line.Contains('=')
            select $"{line.BeforeFirst("=")}:{line.AfterFirst("=")}"
        ).ToList();

        Common.WriteFile(mmcSetupFile, lines.Join("\r\n"));

        _MigrateMmcPreLaunchCommand(mmcSetupFile, versionFolder);
        _MigrateMmcJoinServer(mmcSetupFile, versionFolder);
        _MigrateMmcJavaCompatibility(mmcSetupFile, versionFolder);
        _MigrateMmcLogo(mmcSetupFile, installTemp, archiveBaseFolder, instanceName, versionFolder);
        _MigrateMmcJvmArgs(mmcSetupFile, versionFolder);
    }

    private static void _MigrateMmcPreLaunchCommand(string mmcSetupFile, string versionFolder)
    {
        if (!Convert.ToBoolean(Common.ReadIni(mmcSetupFile, "OverrideCommands", false.ToString())))
            return;

        var preLaunchCommand = Common.ReadIni(mmcSetupFile, "PreLaunchCommand");
        if (string.IsNullOrEmpty(preLaunchCommand))
            return;

        preLaunchCommand = preLaunchCommand
            .Replace(@"\""", "\"")
            .Replace("$INST_JAVA", "{java}java.exe")
            .Replace(@"$INST_MC_DIR\", "{minecraft}")
            .Replace("$INST_MC_DIR", "{minecraft}")
            .Replace(@"$INST_DIR\", "{verpath}")
            .Replace("$INST_DIR", "{verpath}")
            .Replace("$INST_ID", "{name}")
            .Replace("$INST_NAME", "{name}");
        Config.Instance.PreLaunchCommand[versionFolder] = preLaunchCommand;
        Common.Log($"[ModPack] 迁移 MultiMC 实例独立设置：启动前执行命令：{preLaunchCommand}");
    }

    private static void _MigrateMmcJoinServer(string mmcSetupFile, string versionFolder)
    {
        if (!Convert.ToBoolean(
                Common.ReadIni(mmcSetupFile, "JoinServerOnLaunch", false.ToString())))
            return;

        var serverAddress = Common
            .ReadIni(mmcSetupFile, "JoinServerOnLaunchAddress")
            .Replace(@"\""", "\"");
        Config.Instance.ServerToEnter[versionFolder] = serverAddress;
        Common.Log($"[ModPack] 迁移 MultiMC 实例独立设置：自动进入服务器：{serverAddress}");
    }

    private static void _MigrateMmcJavaCompatibility(string mmcSetupFile, string versionFolder)
    {
        if (!Convert.ToBoolean(
                Common.ReadIni(mmcSetupFile, "IgnoreJavaCompatibility", false.ToString())))
            return;

        Config.Instance.IgnoreJavaCompatibility[versionFolder] = true;
        Common.Log("[ModPack] 迁移 MultiMC 实例独立设置：忽略 Java 兼容性警告");
    }

    private static void _MigrateMmcLogo(
        string mmcSetupFile,
        string installTemp,
        string archiveBaseFolder,
        string instanceName,
        string versionFolder)
    {
        var logo = Path.GetFileName(Common.ReadIni(mmcSetupFile, "iconKey"));
        if (string.IsNullOrEmpty(logo)
            || !File.Exists($"{installTemp}{archiveBaseFolder}{logo}.png"))
            return;

        States.Instance.IsLogoCustom[versionFolder] = true;
        States.Instance.LogoPath[versionFolder] = @"PCL\Logo.png";
        Common.CopyFile(
            $"{installTemp}{archiveBaseFolder}{logo}.png",
            $@"{GameFolders.mcFolderSelected}versions\{instanceName}\PCL\Logo.png");
        Common.Log($"[ModPack] 迁移 MultiMC 实例独立设置：实例图标（{logo}.png）");
    }

    private static void _MigrateMmcJvmArgs(string mmcSetupFile, string versionFolder)
    {
        var jvmArgs = Common.ReadIni(mmcSetupFile, "JvmArgs");
        if (string.IsNullOrEmpty(jvmArgs))
            return;

        if (Convert.ToBoolean(Common.ReadIni(mmcSetupFile, "OverrideJavaArgs", false.ToString())))
        {
            Config.Instance.JvmArgs[versionFolder] = jvmArgs;
            Common.Log($"[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（覆盖）：{jvmArgs}");
        }
        else
        {
            jvmArgs = $"{jvmArgs} {Config.Launch.JvmArgs}";
            Config.Instance.JvmArgs[versionFolder] = jvmArgs;
            Common.Log($"[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（追加）：{jvmArgs}");
        }
    }

    private static DownloadUi.McInstallRequest _CreateMmcInstallRequest(
        string instanceName,
        JsonObject packJson)
    {
        var request = new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\"
        };
        foreach (var component in packJson["components"]!.AsArray())
            _ApplyMmcRequestComponent(request, component);

        return request;
    }

    private static void _ApplyMmcRequestComponent(
        DownloadUi.McInstallRequest request,
        JsonNode? component)
    {
        switch ((component?["uid"] ?? "").ToString() ?? "")
        {
            case "org.lwjgl":
                Common.Log("[ModPack] 已跳过 LWJGL 项");
                break;
            case "net.minecraft":
                request.minecraftName = component!["version"]!.ToString();
                break;
            case "net.minecraftforge":
                if (component!["version"]!.ToString().StartsWithF("0."))
                    request.cleanroomVersion = component["version"]!.ToString();
                else
                    request.forgeVersion = component["version"]!.ToString();
                break;
            case "net.neoforged":
                request.neoForgeVersion = component!["version"]!.ToString();
                break;
            case "net.fabricmc.fabric-loader":
                request.fabricVersion = component!["version"]!.ToString();
                break;
            case "org.quiltmc.quilt-loader":
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
        }
    }

    public class MmcPackInfo
    {
        public JsonObject additionalJson = new();
        public bool isCleanroomOverrided;
        public bool isFabricOverrided;
        public bool isForgeOverrided;
        public bool isMcArgsEdited;
        public bool isMinecraftOverrided;
        public bool isNeoForgeOverrided;
        public JsonArray jvmArgs = [];
        public JsonArray libraries = [];
        public JsonObject overridedJson = new();
        public string? tweakers = null;
    }

    private sealed class MmcPatchState
    {
        public readonly JsonArray GameArguments = [];
        public readonly JsonArray JvmArguments = [];
        public readonly JsonArray Libraries = [];
        public JsonObject? AssetIndex;
        public List<int>? JavaMajorsSet;
        public string? MainClass;
        public string? Tweakers;
    }
}