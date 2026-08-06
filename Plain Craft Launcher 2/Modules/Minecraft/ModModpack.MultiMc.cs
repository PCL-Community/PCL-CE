using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.UI;
using PCL.Core.Utils;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region MultiMC

    /// <summary>
    ///     MultiMC 整合包的安装覆盖信息，由 <c>patches/</c> 目录下的 JSON 补丁构建，
    ///     在合并实例版本 JSON 时使用。
    /// </summary>
    public class MMCPackInfo
    {
        /// <summary>
        ///     MC 核心已被补丁修改，<see cref="OverriddenJson" /> 将整体替换原版 JSON。
        /// </summary>
        public bool IsMinecraftOverridden;

        /// <summary>
        ///     Forge JSON 已被补丁覆盖，跳过 Forge 合并。
        /// </summary>
        public bool IsForgeOverridden;

        /// <summary>
        ///     NeoForge JSON 已被补丁覆盖，跳过 NeoForge 合并。
        /// </summary>
        public bool IsNeoForgeOverridden;

        /// <summary>
        ///     Cleanroom JSON 已被补丁覆盖，跳过 Cleanroom 合并。
        /// </summary>
        public bool IsCleanroomOverridden;

        /// <summary>
        ///     Fabric / LegacyFabric JSON 已被补丁覆盖，跳过合并。
        /// </summary>
        public bool IsFabricOverridden;

        /// <summary>
        ///     游戏参数已通过补丁改写为 arguments 形式，合并时删除原 minecraftArguments 字段。
        /// </summary>
        public bool IsMcArgsEdited;

        /// <summary>
        ///     需要覆盖或合并到最终版本 JSON 的字段集合。
        /// </summary>
        public JsonObject OverriddenJson = new();
    }

    private static LoaderCombo<string> _InstallMultiMc(string fileAddress, ZipArchive archive,
        string archiveBaseFolder)
    {
        // 读取 Json 文件
        JsonObject packJson;
        string packInstance;
        try
        {
            packJson = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile(archive.GetEntry(archiveBaseFolder + "mmc-pack.json").Open(), Encoding.UTF8));
            packInstance = ModBase.ReadFile(archive.GetEntry(archiveBaseFolder + "instance.cfg").Open(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new Exception("MMC 整合包安装信息存在问题", ex);
        }

        // 构建 JSON Patches 覆盖信息
        var packInfo = _BuildMultiMcPackInfo(archive, archiveBaseFolder, packJson);

        // 获取实例名
        var instanceName = _PromptInstanceName(MultiMcManifest.ParseInstanceName(packInstance) ?? "");

        // 解压
        var installTemp = ModMain.RequestTaskTempFolder();
        var versionFolder = $@"{ModFolder.mcFolderSelected}versions\{instanceName}";
        var installLoaders = new List<LoaderBase>();
        installLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
        {
            _ExtractModpackFiles(installTemp, fileAddress, task, 0.55d);
            _CopyOverrideDirectory(Path.Combine(installTemp, archiveBaseFolder, "libraries"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName, "libraries"), task, 0.2d);
            _CopyOverrideDirectory(Path.Combine(installTemp, archiveBaseFolder, ".minecraft"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName), task, 0.2d);

            _MigrateMultiMcInstanceCfg(Path.Combine(installTemp, archiveBaseFolder, "instance.cfg"), versionFolder,
                installTemp, archiveBaseFolder, instanceName);
        })
        {
            ProgressWeight = new FileInfo(fileAddress).Length / 1024d / 1024d / 6d,
            block = false
        }); // 每 6M 需要 1s
        // 构造实例安装请求
        var manifest = MultiMcManifest.Parse(packJson);
        if (manifest is null || manifest.Components is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = _GetVersionFolder(instanceName)
        };
        foreach (var component in manifest.Components)
            switch (component.Uid ?? "")
            {
                case "org.lwjgl":
                {
                    ModBase.Log("[ModPack] 已跳过 LWJGL 项");
                    break;
                }
                case "net.minecraft":
                {
                    request.minecraftName = component.Version;
                    break;
                }
                case "net.minecraftforge":
                {
                    if ((component.Version ?? "").StartsWithF("0."))
                        request.cleanroomVersion = component.Version;
                    else
                        request.forgeVersion = component.Version;

                    break;
                }
                case "net.neoforged":
                {
                    request.neoForgeVersion = component.Version;
                    break;
                }
                case "net.fabricmc.fabric-loader":
                {
                    request.fabricVersion = component.Version;
                    break;
                }
                case "org.quiltmc.quilt-loader":
                {
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
                }
            }

        if (request.minecraftName is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));

        if (packInfo is not null)
            request.mmcPackInfo = packInfo;
        // 构造加载器
        var mergeLoaders = ModDownloadLib.McInstallLoader(request);
        // 构造总加载器
        var loaders = new List<LoaderBase>();
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                installLoaders)
            { show = false, block = false, ProgressWeight = installLoaders.Sum(l => l.ProgressWeight) });
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), mergeLoaders)
            { show = false, ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight) });

        // 启动
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.MmcInstall", instanceName);
        return _StartInstall(loaderName, loaders, request.targetInstanceFolder, false);
    }

    /// <summary>
    ///     解析 <c>patches/</c> 目录下的 JSON 补丁，构建 <see cref="MMCPackInfo" /> 覆盖信息。
    ///     参考 https://github.com/MultiMC/Launcher/wiki/JSON-Patches
    /// </summary>
    private static MMCPackInfo? _BuildMultiMcPackInfo(ZipArchive archive, string archiveBaseFolder,
        JsonObject packJson)
    {
        MMCPackInfo packInfo = null;
        try
        {
            // 部分压缩工具不会写入 "patches/" 目录条目，因此不能只检查目录项，
            // 而应检查是否存在任何位于 patches/ 下的文件条目。
            if (!archive.Entries.Any(e => !e.FullName.EndsWith("/") &&
                                          e.FullName.StartsWith(archiveBaseFolder + "patches/")))
                return null;
            ModBase.Log("[ModPack] 安装的 MultiMC 整合包存在 JSON Patches");
            // 排序预处理
            var patches = new List<KeyValuePair<JsonObject, int>>();
            foreach (var entry in archive.Entries)
                if (!entry.FullName.EndsWith("/") && entry.FullName.StartsWith(archiveBaseFolder + "patches/"))
                {
                    var patch = (JsonObject)ModBase.GetJson(ModBase.ReadFile(
                        archive.GetEntry(entry.FullName).Open(), Encoding.UTF8));
                    patches.Add(new KeyValuePair<JsonObject, int>(patch,
                        (int)(patch["order"] is not null ? patch["order"] : 0)));
                }

            var components = (JsonArray)packJson["components"];
            var componentUids = components
                .Select(c => c["uid"]?.ToString())
                .ToHashSet();

            patches = patches
                .Where(p => componentUids.Contains(p.Key["uid"]?.ToString()))
                .OrderBy(p => p.Value)
                .ToList();
            // 应用 Patches
            packInfo = new MMCPackInfo();

            string tweakers = null;
            JsonObject assetIndex = null;
            JsonObject javaVerJson = null;
            string mainClass = null;
            var gameArguments = new JsonArray();
            var jvmArguments = new JsonArray();
            var libJson = new JsonArray();
            var addLibJson = new JsonArray();
            foreach (var patch in patches)
            {
                var patchJson = patch.Key;
                if ((string)patchJson["uid"] == "net.minecraft")
                {
                    packInfo.IsMinecraftOverridden = true;
                }
                else if ((string)patchJson["uid"] == "net.minecraftforge")
                {
                    if (patchJson["version"].ToString().StartsWithF("0."))
                        packInfo.IsCleanroomOverridden = true;
                    else
                        packInfo.IsForgeOverridden = true;
                }
                else if ((string)patchJson["uid"] == "net.neoforged")
                {
                    packInfo.IsNeoForgeOverridden = true;
                }
                else if ((string)patchJson["uid"] == "net.fabricmc.fabric-loader")
                {
                    packInfo.IsFabricOverridden = true;
                }
                else if ((string)patchJson["uid"] == "org.quiltmc.quilt-loader")
                {
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
                }

                // JVM 参数
                if (patchJson["+jvmArgs"] is not null)
                {
                    jvmArguments.Merge(patchJson["+jvmArgs"]);
                    ModBase.Log($"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 JVM 参数");
                }

                // Libraries
                if (patchJson["libraries"] is not null || patchJson["+libraries"] is not null)
                {
                    var libs = new JsonArray();
                    if (patchJson["libraries"] is not null)
                        foreach (var library in patchJson["libraries"].AsArray())
                        {
                            if (library is not JsonObject libraryObj) continue;
                            var libObj = libraryObj.DeepClone().AsObject();
                            if (libObj["MMC-hint"] is not null)
                            {
                                libObj.Add("hint", libObj["MMC-hint"]?.DeepClone());
                                libObj.Remove("MMC-hint");
                            }

                            libs.Add(libObj);
                        }

                    if (patchJson["+libraries"] is not null)
                        foreach (var library in patchJson["+libraries"].AsArray())
                        {
                            if (library is not JsonObject libraryObj) continue;
                            var libObj = libraryObj.DeepClone().AsObject();
                            if (libObj["MMC-hint"] is not null)
                            {
                                libObj.Add("hint", libObj["MMC-hint"]?.DeepClone());
                                libObj.Remove("MMC-hint");
                            }

                            libs.Add(libObj);
                        }

                    libJson.Merge(libs);
                    ModBase.Log($"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 Libraries");
                }

                // Tweakers
                if (patchJson["+tweakers"] is not null)
                {
                    tweakers = (string)patchJson["+tweakers"][0];
                    ModBase.Log($"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 Tweakers");
                }

                // AssetIndex
                if (patchJson["assetIndex"] is not null)
                {
                    assetIndex = patchJson["assetIndex"]?.DeepClone().AsObject();
                    ModBase.Log($"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 AssetIndex");
                }

                // minecraftArguments -> arguments.game
                if (patchJson["minecraftArguments"] is not null)
                {
                    foreach (var arg in patchJson["minecraftArguments"].ToString().Split(" "))
                        gameArguments.Add(arg);
                    packInfo.IsMcArgsEdited = true;
                    ModBase.Log(
                        $"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 minecraftArguments 至 arguments.game");
                }

                // mainClass
                if (patchJson["mainClass"] is not null)
                {
                    mainClass = (string)patchJson["mainClass"];
                    ModBase.Log($"[ModPack] 已应用 JSON-Patch {patchJson["uid"]} 的 mainClass");
                }

                // Java 版本要求
                if (patchJson["compatibleJavaMajors"] is not null)
                {
                    var javaVersion = 0;
                    string javaComponent = null;
                    var javaMajors = (JsonArray)patchJson["compatibleJavaMajors"];
                    foreach (var java in javaMajors)
                    {
                        if (javaVersion > ModBase.Val(java))
                            continue;
                        // 优先选择主要的版本
                        if (ModBase.Val(java) == 21d)
                        {
                            javaVersion = 21;
                            javaComponent = "java-runtime-delta";
                        }
                        else if (ModBase.Val(java) == 17d)
                        {
                            javaVersion = 17;
                            javaComponent = "java-runtime-gamma";
                        }
                        else if (ModBase.Val(java) == 11d)
                        {
                            javaVersion = 11;
                            javaComponent = null;
                        }
                        else if (ModBase.Val(java) == 8d)
                        {
                            javaVersion = 8;
                            javaComponent = "jre-legacy";
                        }
                    }

                    if (javaVersion == 0)
                    {
                        javaVersion = (int)javaMajors[0];
                        javaComponent = null;
                    }

                    javaVerJson = new JsonObject { { "majorVersion", javaVersion } };
                    if (javaComponent is not null) javaVerJson.Add("component", javaComponent);
                    ModBase.Log($"[ModPack] JSON-Patch {patchJson["uid"]} 要求 Java 版本: " + javaVersion);
                }
            }

            JsonObject jsonArguments = null;
            if (!string.IsNullOrWhiteSpace(tweakers))
            {
                gameArguments.Add("--tweakClass");
                gameArguments.Add(tweakers);
            }

            if (gameArguments is not null || jvmArguments is not null)
            {
                jvmArguments.Insert(0, "-Djava.library.path=${natives_directory}");
                jvmArguments.Insert(1, "-Dminecraft.launcher.brand=${launcher_name}");
                jvmArguments.Insert(2, "-Dminecraft.launcher.version=${launcher_version}");
                jvmArguments.Insert(3, "-cp");
                jvmArguments.Insert(4, "${classpath}");
                jsonArguments = new JsonObject { { "game", gameArguments }, { "jvm", jvmArguments } };
            }

            packInfo.OverriddenJson = new JsonObject();
            if (jsonArguments is not null)
                packInfo.OverriddenJson.Add("arguments", jsonArguments);
            if (mainClass is not null)
                packInfo.OverriddenJson.Add("mainClass", mainClass);
            if (assetIndex is not null)
                packInfo.OverriddenJson.Add("assetIndex", assetIndex);
            if (javaVerJson is not null)
                packInfo.OverriddenJson.Add("javaVersion", javaVerJson);
            if (libJson is not null)
                packInfo.OverriddenJson.Add("libraries", libJson);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "应用 MMC JSON-Patches 失败");
        }

        return packInfo;
    }

    /// <summary>
    ///     迁移 MultiMC 实例的 instance.cfg 中可由 PCL 承接的设置（#2655）。
    /// </summary>
    private static void _MigrateMultiMcInstanceCfg(string mmcSetupFile, string versionFolder, string installTemp,
        string archiveBaseFolder, string instanceName)
    {
        try
        {
            // 将其中的等号替换为冒号，以符合 ini 文件格式
            if (!File.Exists(mmcSetupFile))
                return;
            List<string> lines = [];
            foreach (var line in ModBase.ReadFile(mmcSetupFile).Split(new[] { "\r", "\n" },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.Contains("="))
                    continue;
                lines.Add(line.BeforeFirst("=") + ":" + line.AfterFirst("="));
            }

            ModBase.WriteFile(mmcSetupFile, lines.Join("\r\n"));
            // 读取文件
            if (Convert.ToBoolean(ModBase.ReadIni(mmcSetupFile, "OverrideCommands",
                    false.ToString())))
            {
                var preLaunchCommand = ModBase.ReadIni(mmcSetupFile, "PreLaunchCommand");
                if (!string.IsNullOrEmpty(preLaunchCommand))
                {
                    preLaunchCommand = preLaunchCommand.Replace(@"\""", "\"")
                        .Replace("$INST_JAVA", "{java}java.exe").Replace(@"$INST_MC_DIR\", "{minecraft}")
                        .Replace("$INST_MC_DIR", "{minecraft}").Replace(@"$INST_DIR\", "{verpath}")
                        .Replace("$INST_DIR", "{verpath}").Replace("$INST_ID", "{name}")
                        .Replace("$INST_NAME", "{name}");
                    Config.Instance.PreLaunchCommand[versionFolder] = preLaunchCommand;
                    ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：启动前执行命令：" + preLaunchCommand);
                }
            }

            if (Convert.ToBoolean(ModBase.ReadIni(mmcSetupFile, "JoinServerOnLaunch",
                    false.ToString())))
            {
                var serverAddress = ModBase.ReadIni(mmcSetupFile, "JoinServerOnLaunchAddress")
                    .Replace(@"\""", "\"");
                Config.Instance.ServerToEnter[versionFolder] = serverAddress;
                ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：自动进入服务器：" + serverAddress);
            }

            if (Convert.ToBoolean(ModBase.ReadIni(mmcSetupFile, "IgnoreJavaCompatibility",
                    false.ToString())))
            {
                Config.Instance.IgnoreJavaCompatibility[versionFolder] = true;
                ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：忽略 Java 兼容性警告");
            }

            var logo = Path.GetFileName(ModBase.ReadIni(mmcSetupFile, "iconKey"));
            if (!string.IsNullOrEmpty(logo) && File.Exists($"{installTemp}{archiveBaseFolder}{logo}.png"))
            {
                States.Instance.IsLogoCustom[versionFolder] = true;
                States.Instance.LogoPath[versionFolder] = @"PCL\Logo.png";
                ModBase.CopyFile($"{installTemp}{archiveBaseFolder}{logo}.png",
                    $@"{ModFolder.mcFolderSelected}versions\{instanceName}\PCL\Logo.png");
                ModBase.Log($"[ModPack] 迁移 MultiMC 实例独立设置：实例图标（{logo}.png）");
            }

            // JVM 参数
            var jvmArgs = ModBase.ReadIni(mmcSetupFile, "JvmArgs");
            if (!string.IsNullOrEmpty(jvmArgs))
            {
                if (Convert.ToBoolean(ModBase.ReadIni(mmcSetupFile, "OverrideJavaArgs",
                        false.ToString())))
                {
                    Config.Instance.JvmArgs[versionFolder] = jvmArgs;
                    ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（覆盖）：" + jvmArgs);
                }
                else
                {
                    jvmArgs = jvmArgs + " " + Config.Launch.JvmArgs;
                    Config.Instance.JvmArgs[versionFolder] = jvmArgs;
                    ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（追加）：" + jvmArgs);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"读取 MMC 配置文件失败（{mmcSetupFile}）");
        }
    }

    #endregion
}
