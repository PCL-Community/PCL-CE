using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region CurseForge

    private static LoaderCombo<string> _InstallCurseForge(string sourcePath, IModpackArchiveReader archive,
        string archiveBaseFolder, string instanceName = null, string logo = null, string resourceId = null,
        bool isOnlineInstall = false)
    {
        // 读取 Json 文件
        JsonObject json;
        try
        {
            json = (JsonObject)ModBase.GetJson(archive.ReadEntryText(archiveBaseFolder + "manifest.json"));
        }
        catch (Exception ex)
        {
            throw new Exception("CurseForge 整合包安装信息存在问题", ex);
        }

        var manifest = CurseForgeManifest.Parse(json);
        if (manifest is null || manifest.Minecraft is null || manifest.Minecraft.Version is null)
            throw new Exception("CurseForge 整合包未提供 Minecraft 版本信息");

        // 获取实例名
        if (instanceName is null)
            instanceName = _PromptInstanceName(manifest.Name ?? "");

        // 推荐内存询问：清单声明了推荐内存时，先校验其是否适合用户的电脑
        // （使用推荐 → 推荐内存模式；取消 → 跟随全局设置；不适合时自动 → 自动配置）
        var recommendedRam = manifest.RecommendedRamEffective ?? 0;
        var useRecommendedRam = false; // 最终是否使用推荐内存模式
        var useAutoRam = false; // 最终是否使用自动配置模式（推荐内存不适合时）
        if (recommendedRam > 0)
        {
            var totalMemoryMb = KernelInterop.GetPhysicalMemoryBytes().Total / 1024d / 1024d;
            if (recommendedRam > totalMemoryMb)
            {
                useAutoRam = true;
            }
            else
            {
                // 阈值 = max(总内存*80%, 总内存-6GB)，推荐内存超过阈值时警告
                var threshold = Math.Max(totalMemoryMb * 0.8d, totalMemoryMb - 6d * 1024d);
                if (recommendedRam > threshold)
                {
                    // 推荐内存不合理
                    useRecommendedRam = ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRamUnfit.Message",(recommendedRam / 1024d).ToString("0.#")),
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRamUnfit.Title"),
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRamUnfit.UseAuto"),
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRamUnfit.KeepRecommended")) == 2;
                    if (!useRecommendedRam)
                        useAutoRam = true;
                }
                else
                {
                    // 推荐内存合理
                    useRecommendedRam = ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRam.Message",
                            (recommendedRam / 1024d).ToString("0.#")),
                        Lang.Text("Minecraft.Download.Modpack.RecommendedRam.Title"),
                        Lang.Text("Common.Action.Confirm"),
                        Lang.Text("Common.Action.Cancel")) == 1;
                }
            }
        }

        // 获取 Mod API 版本信息
        string forgeVersion = null;
        string neoForgeVersion = null;
        string fabricVersion = null;
        var modLoader = ModComp.CompLoaderType.Any;
        foreach (var loader in manifest.Minecraft.ModLoaders ?? [])
        {
            var id = (loader.Id ?? "").ToLower();
            if (id.StartsWithF("forge-"))
            {
                // Forge 指定
                if (id.Contains("recommended"))
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.TooOldUnsupported"));
                ModBase.Log("[ModPack] 整合包 Forge 版本：" + id);
                forgeVersion = id.Replace("forge-", "");
                modLoader = ModComp.CompLoaderType.Forge;
            }
            else if (id.StartsWithF("neoforge-"))
            {
                // NeoForge 指定
                ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + id);
                neoForgeVersion = id.Replace("neoforge-", "");
                modLoader = ModComp.CompLoaderType.NeoForge;
            }
            else if (id.StartsWithF("fabric-"))
            {
                // Fabric 指定
                try
                {
                    ModBase.Log("[ModPack] 整合包 Fabric 版本：" + id);
                    fabricVersion = id.Replace("fabric-", "");
                    modLoader = ModComp.CompLoaderType.Fabric;
                    break;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取整合包 Fabric 版本失败：" + id);
                }
            }
            else if (id.StartsWithF("quilt-"))
            {
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
            }
        }

        // 解压
        var installTemp = ModMain.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>();
        // overrides 字段缺失时按规范默认为 "overrides"，避免整包不解压导致覆写内容（mods/config/saves 等）丢失
        var overrideHome = manifest.Overrides ?? "overrides";
        if (!string.IsNullOrEmpty(overrideHome))
            installLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
                task =>
            {
                _ExtractModpackFiles(installTemp, sourcePath, task, 0.6d);
                _CopyOverrideDirectory(
                    Path.Combine(installTemp, archiveBaseFolder, overrideHome == "." || overrideHome == "./" ? "" : overrideHome),
                    _GetVersionFolder(instanceName), task, 0.4d);
            })
            {
                ProgressWeight = _GetModpackProgressWeight(sourcePath),
                block = false
            }); // 每 6M 需要 1s
        // 获取 Mod 列表
        var modList = new List<int>();
        var modOptionalList = new List<int>();
        // 此处直接读取原始节点：跳过文件时提示需要展示原始条目内容，以保持提示文案不变。
        foreach (var modNode in json["files"]?.AsArray() ?? [])
        {
            if (modNode["projectID"] is null || modNode["fileID"] is null)
            {
                HintService.Hint(Lang.Text("Minecraft.Download.Modpack.ModMissingRequiredInfoSkipped", modNode));
                continue;
            }

            modList.Add((int)modNode["fileID"]);
            if (modNode["required"] is JsonNode requiredNode && !requiredNode.ToObject<bool>())
                modOptionalList.Add((int)modNode["fileID"]);
        }

        if (modList.Any())
        {
            var modDownloadLoaders = new List<LoaderBase>();
            // 获取 Mod 下载信息
            modDownloadLoaders.Add(new LoaderTask<int, JsonArray>(
                Lang.Text("Minecraft.Download.Modpack.Stage.PrepareModsDownloadInfo"), task =>
            {
                var allowMirror = true;
                JsonArray ret;
                var tryCount = 0;
                do
                {
                    tryCount += 1;
                    ret = (JsonArray)((JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                        "https://api.curseforge.com/v1/mods/files",
                        "POST", "{\"fileIds\": [" + modList.Join(",") + "]}", "application/json",
                        allowMirror)))["data"];
                    if (modList.Count <= ret.Count)
                    {
                        ModBase.Log("[Modpack] 已获取到的模组数量足够，开始进行下一步");
                        break;
                    }

                    allowMirror = false;
                    ModBase.Log($"[Modpack] 获取模组数量不达标，设置镜像源允许状态为: {allowMirror}");
                    if (tryCount > 3) throw new Exception(Lang.Text("Minecraft.Download.Modpack.SomeModsDeleted"));
                } while (true);

                task.output = ret;
            })
            {
                ProgressWeight = modList.Count / 10d
            }); // 每 10 Mod 需要 1s
            // 构造 NetFile
            // 地图（World）文件：下载到临时目录，随后解压成 saves\地图名\ 文件夹
            var worldExtracts = new List<(string archivePath, string targetDirectory)>();
            modDownloadLoaders.Add(new LoaderTask<JsonArray, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Modpack.Stage.BuildModsDownloadInfo"), task =>
            {
                var fileList = new Dictionary<int, DownloadFile>();
                foreach (var modJson in task.input)
                {
                    var id = modJson["id"].ToObject<int>();
                    // 跳过重复的 Mod（疑似 CurseForge Bug）
                    if (fileList.ContainsKey(id))
                        continue;
                    // 可选 Mod 提示
                    if (modOptionalList.Contains(id))
                        if (ModMain.MyMsgBox(
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Message", modJson["displayName"]),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")
                            ) == 2)
                            continue;

                    // 根据 modules、文件名后缀与游戏版本需求判断资源类型
                    string targetFolder;
                    ModComp.CompType type;
                    if (modJson["modules"].AsArray().Any()) // modules 可能返回 null（#1006）
                    {
                        var moduleNames = ((JsonArray)modJson["modules"]).Select(l => l["name"].ToString()).ToList();
                        var fileName = modJson?["FileName"]?.ToString() ?? "";
                        var gameVersions = ((JsonArray)modJson?["gameVersions"])?.Select(v => v.ToString()).ToList() ?? [];
                        if (moduleNames.Contains("META-INF") || moduleNames.Contains("mcmod.info") ||
                            fileName.EndsWithF(".jar", true))
                        {
                            targetFolder = "mods";
                            type = ModComp.CompType.Mod;
                        }
                        else if (moduleNames.Contains("pack.mcmeta"))
                        {
                            targetFolder = "resourcepacks";
                            type = ModComp.CompType.ResourcePack;
                        }
                        // 光影包：含 shaders 目录 / .placebo 文件，或声明了 OptiFine/Iris 需求
                        else if (moduleNames.Contains("shaders") || moduleNames.Any(name => name.EndsWithF(".placebo", true)) ||
                                 gameVersions.Contains("OptiFine", StringComparer.OrdinalIgnoreCase) ||
                                 gameVersions.Contains("Iris", StringComparer.OrdinalIgnoreCase))
                        {
                            targetFolder = "shaderpacks";
                            type = ModComp.CompType.Shader;
                        }
                        // 地图存档：level.dat 可能位于一层父文件夹内（如 MyMap/level.dat），需按路径段判断
                        else if (moduleNames.Any(name =>
                                     name.Split("/").Last().Equals("level.dat", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetFolder = "saves";
                            type = ModComp.CompType.World;
                        }
                        // 地图存档（包裹式）：modules 仅有一个无扩展名的顶层条目时，是“zip 内再包一层存档文件夹”的地图
                        else if (moduleNames.Count == 1 && !Path.HasExtension(moduleNames[0]))
                        {
                            targetFolder = "saves";
                            type = ModComp.CompType.World;
                        }
                        else
                        {
                            targetFolder = "shaderpacks";
                            type = ModComp.CompType.Shader;
                        }
                    }
                    else
                    {
                        targetFolder = "mods";
                        type = ModComp.CompType.Mod;
                    }

                    // 建立 CompFile
                    var file = new ModComp.CompFile((JsonObject)modJson, type);
                    if (!file.Available)
                        continue;
                    if (type == ModComp.CompType.World)
                    {
                        // 地图：先下载到临时目录，下载完成后解压成 saves\地图名\ 文件夹（压缩包本身无法被游戏直接读取）
                        var archivePath = Path.Combine(installTemp, "world_" + worldExtracts.Count + ".tmp");
                        fileList.Add(id,
                            file.ToNetFile(archivePath,
                                ModComp.DownloadReason.ModPack, manifest.Minecraft.Version, modLoader));
                        worldExtracts.Add((archivePath,
                            _GetVersionFolder(instanceName) + "saves\\" +
                            ModBase.GetFileNameWithoutExtentionFromPath(file.FileName) + "\\"));
                    }
                    else
                    {
                        // 实际的添加
                        fileList.Add(id,
                            file.ToNetFile(_GetVersionFolder(instanceName) + targetFolder + @"\",
                                ModComp.DownloadReason.ModPack, manifest.Minecraft.Version, modLoader));
                    }
                    task.Progress += 1d / (1 + modList.Count);
                }

                task.output = fileList.Values.ToList();
            })
            {
                ProgressWeight = modList.Count / 200d,
                show = false
            }); // 每 200 Mod 需要 1s
            // 下载 Mod 文件
            modDownloadLoaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods"), [])
                { ProgressWeight = modList.Count * 1.5d }); // 每个 Mod 需要 1.5s
            // 解压地图存档（下载到临时目录的地图，解压成 saves\地图名\ 文件夹）
            modDownloadLoaders.Add(new LoaderTask<string, int>(
                Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"), task =>
            {
                foreach (var (archivePath, targetDirectory) in worldExtracts)
                    _ExtractArchiveToDirectory(archivePath, targetDirectory);
            })
            { show = false, ProgressWeight = 1d });
            // 构造加载器
            installLoaders.Add(
                new LoaderCombo<int>(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods.MainLoader"),
                        modDownloadLoaders)
                { show = false, ProgressWeight = modDownloadLoaders.Sum(l => l.ProgressWeight) });
        }

        // 构造加载器
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = _GetVersionFolder(instanceName),
            minecraftName = manifest.Minecraft.Version,
            forgeVersion = forgeVersion,
            neoForgeVersion = neoForgeVersion,
            fabricVersion = fabricVersion,
        };
        var mergeLoaders = ModDownloadLib.McInstallLoader(request);
        // 构造总加载器
        var loaders = new List<LoaderBase>();
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                installLoaders)
            { show = false, block = false, ProgressWeight = installLoaders.Sum(l => l.ProgressWeight) });
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), mergeLoaders)
            { show = false, ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight) });
        loaders.Add(new LoaderTask<string, string>(Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"), task =>
        {
            var versionFolder = _GetVersionFolder(instanceName);
            _FinalizeInstance(versionFolder, sourcePath, logo, "CurseForge",
                manifest.Version, resourceId, manifest.RecommendedRamEffective ?? 0);
            // 应用推荐内存选择：仅当清单声明了推荐内存时写配置
            // （使用推荐 → 推荐内存模式；不适合时自动 → 自动配置；正常取消 → 跟随全局设置）
            // MemorySolution 注册了 UI 观察者（ModSetup.VersionRamType），需在 UI 线程写入，
            // 避免在后台安装线程触发观察者导致跨线程访问 UI 控件。
            if (recommendedRam > 0)
            {
                var memorySolution = useRecommendedRam ? 3 : (useAutoRam ? 0 : 2);
                ModBase.RunInUi(() => Config.Instance.MemorySolution[versionFolder] = memorySolution);
            }
            // 本地安装没有外部 logo 时，尝试使用整合包内嵌的图标（manifest.json 的 image 字段）
            if (logo is null && manifest.Image is not null)
            {
                var iconPath = Path.Combine(installTemp, archiveBaseFolder.Replace("/", @"\"),
                    manifest.Image.Replace("/", @"\"));
                _SetInstanceIcon(versionFolder, iconPath);
            }
        })
        {
            ProgressWeight = 0.1d,
            show = false
        });

        // 启动
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.CurseForgeInstall", instanceName);
        return _StartInstall(loaderName, loaders, request.targetInstanceFolder, isOnlineInstall);
    }

    #endregion
}
