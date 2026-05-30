using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;
using PCL.Core.Utils;

namespace PCL;

public static class ModModpack
{
    // 触发整合包安装的外部接口
    /// <summary>
    ///     弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
    public static void ModpackInstall()
    {
        var File = SystemDialogs.SelectFile(Lang.Text("Minecraft.Download.Modpack.FileDialog.Filter"),
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Title")); // 选择整合包文件
        if (string.IsNullOrEmpty(File))
            return;
        ModBase.RunInThread(() =>
        {
            try
            {
                ModpackInstall(File);
            }
            catch (ModBase.CancelledException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "手动安装整合包失败", ModBase.LogLevel.Msgbox);
            }
        });
    }

    /// <summary>
    ///     构建并启动安装给定的整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    ///     必须在工作线程执行。
    /// </summary>
    /// <exception cref="ModBase.CancelledException" />
    public static LoaderCombo<string> ModpackInstall(string File, string InstanceName = null, string Logo = null,
        string resourceId = null, bool isOnlineInstall = false)
    {
        ModBase.Log("[ModPack] 整合包安装请求：" + (File ?? "null"));
        ZipArchive Archive = null;
        var ArchiveBaseFolder = "";
        try
        {
            // 字符校验
            var TargetFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\";
            if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
            {
                ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", TargetFolder),
                    ModMain.HintType.Critical);
                throw new ModBase.CancelledException();
            }

            // 获取整合包种类与关键 Json
            var PackType = -1;
            do
            {
                try
                {
                    Archive = new ZipArchive(new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.Read));
                    if (Archive.Entries.Any(e => e.IsEncrypted))
                        throw new Exception(Lang.Text("Minecraft.Download.Modpack.EncryptedArchiveUnsupported"));
                    // 从根目录判断整合包类型
                    if (Archive.GetEntry("mcbbs.packmeta") is not null)
                    {
                        PackType = 3;
                        break;
                    } // MCBBS 整合包（优先于 manifest.json 判断）

                    if (Archive.GetEntry("mmc-pack.json") is not null)
                    {
                        PackType = 2;
                        break;
                    } // MMC 整合包（优先于 manifest.json 判断，#4194）

                    if (Archive.GetEntry("modrinth.index.json") is not null)
                    {
                        PackType = 4;
                        break;
                    } // Modrinth 整合包

                    if (Archive.GetEntry("manifest.json") is not null)
                    {
                        var Json = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Archive.GetEntry("manifest.json").Open(),
                            Encoding.UTF8));
                        if (Json["addons"] is null)
                        {
                            PackType = 0;
                            break; // CurseForge 整合包
                        }

                        PackType = 3;
                        break;
                        // MCBBS 整合包
                    }

                    if (Archive.GetEntry("modpack.json") is not null)
                    {
                        PackType = 1;
                        break;
                    } // HMCL 整合包

                    if (Archive.GetEntry("modpack.zip") is not null || Archive.GetEntry("modpack.mrpack") is not null)
                    {
                        PackType = 9;
                        break;
                    } // 带启动器的压缩包

                    // 从一级目录判断整合包类型
                    var exitTry = false;
                    foreach (var Entry in Archive.Entries)
                    {
                        var FullNames = Entry.FullName.Split("/");
                        ArchiveBaseFolder = FullNames[0] + "/";
                        // 确定为一级目录下
                        if (FullNames.Count() != 2)
                            continue;
                        // 判断是否为关键文件
                        if (FullNames[1] == "mcbbs.packmeta")
                        {
                            PackType = 3;
                            exitTry = true;
                            break;
                        } // MCBBS 整合包（优先于 manifest.json 判断）

                        if (FullNames[1] == "mmc-pack.json")
                        {
                            PackType = 2;
                            exitTry = true;
                            break;
                        } // MMC 整合包（优先于 manifest.json 判断，#4194）

                        if (FullNames[1] == "modrinth.index.json")
                        {
                            PackType = 4;
                            exitTry = true;
                            break;
                        } // Modrinth 整合包

                        if (FullNames[1] == "manifest.json")
                        {
                            var Json = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Entry.Open(), Encoding.UTF8));
                            if (Json["addons"] is null)
                            {
                                PackType = 0;
                                exitTry = true;
                                break; // CurseForge 整合包
                            }

                            PackType = 3;
                            ArchiveBaseFolder = "overrides/";
                            exitTry = true;
                            break;
                            // MCBBS 整合包
                        }

                        if (FullNames[1] == "modpack.json")
                        {
                            PackType = 1;
                            exitTry = true;
                            break;
                        } // HMCL 整合包

                        if (FullNames[1] == "modpack.zip" || FullNames[1] == "modpack.mrpack")
                        {
                            PackType = 9;
                            exitTry = true;
                            break;
                        } // 带启动器的压缩包
                    }

                    if (exitTry) break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Error.WinIOError"))
                        throw new Exception(Lang.Text("Minecraft.Download.Modpack.OpenFailed"), ex);
                    else if (File.EndsWithF(".rar", true))
                        throw new Exception(Lang.Text("Minecraft.Download.Modpack.RarUnsupported"), ex);
                    else
                        throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnsupportedArchive"), ex);
                }
            } while (false);

            // 执行对应的安装方法
            switch (PackType)
            {
                case 0:
                {
                    ModBase.Log("[ModPack] 整合包种类：CurseForge");
                    return InstallPackCurseForge(File, Archive, ArchiveBaseFolder, InstanceName, Logo, resourceId,
                        isOnlineInstall);
                }
                case 1:
                {
                    ModBase.Log("[ModPack] 整合包种类：HMCL");
                    return InstallPackHMCL(File, Archive, ArchiveBaseFolder);
                }
                case 2:
                {
                    ModBase.Log("[ModPack] 整合包种类：MMC");
                    return InstallPackMMC(File, Archive, ArchiveBaseFolder);
                }
                case 3:
                {
                    ModBase.Log("[ModPack] 整合包种类：MCBBS");
                    return InstallPackMCBBS(File, Archive, ArchiveBaseFolder, InstanceName);
                }
                case 4:
                {
                    ModBase.Log("[ModPack] 整合包种类：Modrinth");
                    return InstallPackModrinth(File, Archive, ArchiveBaseFolder, InstanceName, Logo, resourceId,
                        isOnlineInstall);
                }
                case 9:
                {
                    ModBase.Log("[ModPack] 整合包种类：带启动器的压缩包");
                    return InstallPackLauncherPack(File, Archive, ArchiveBaseFolder);
                }

                default:
                {
                    ModBase.Log("[ModPack] 整合包种类：未能识别，假定为压缩包");
                    return InstallPackCompress(File, Archive);
                }
            }
        }
        finally
        {
            if (Archive is not null)
                Archive.Dispose();
        }
    }

    private static void ExtractModpackFiles(string installTemp, string fileAddress, LoaderBase loader,
        double progressIncrement)
    {
        // 解压文件
        var retryCount = 1;
        var encode = Encoding.GetEncoding("GB18030");
        var initialProgress = loader.Progress;

        while (retryCount <= 5)
            try
            {
                loader.Progress = initialProgress;

                // 删除旧目录
                ModBase.DeleteDirectory(installTemp);

                // 解压文件，ProgressIncrementHandler 通过 Lambda 更新进度
                ModBase.ExtractFile(fileAddress, installTemp, encode,
                    delta => loader.Progress += delta * progressIncrement);

                // 解压成功，更新进度并退出循环
                loader.Progress = initialProgress + progressIncrement;
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"第 {retryCount} 次解压尝试失败");

                if (ex is ArgumentException || ex is IOException)
                {
                    encode = Encoding.UTF8;
                    ModBase.Log("[ModPack] 已切换压缩包解压编码为 UTF8");
                }

                // 检查加载器状态，决定是否中止
                if (loader is not null && loader.LoadingState != MyLoading.MyLoadingState.Run)
                    return;

                // 增加重试次数
                retryCount++;

                if (retryCount <= 5)
                    // 等待一段时间再重试
                    Thread.Sleep((retryCount - 1) * 2000);
                else
                    throw new Exception("解压整合包文件失败", ex);
            }
    }

    /// <summary>
    ///     从整合包的 override 目录复制文件，同时设置 PCL 的配置文件与版本隔离。
    ///     对路径末尾是否为 \ 没有要求。
    /// </summary>
    private static void CopyOverrideDirectory(string OverridesFolder, string VersionFolder, LoaderBase Loader,
        double ProgressIncrement)
    {
        if (!OverridesFolder.EndsWithF(@"\"))
            OverridesFolder += @"\";
        if (!VersionFolder.EndsWithF(@"\"))
            VersionFolder += @"\";
        // 复制文件
        if (Directory.Exists(OverridesFolder))
        {
            ModBase.Log($"[ModPack] 处理整合包覆写文件夹：{OverridesFolder} → {VersionFolder}");
            ModBase.CopyDirectory(OverridesFolder, VersionFolder,
                Delta => Loader.Progress += Delta * ProgressIncrement);
        }
        else
        {
            ModBase.Log($"[ModPack] 整合包中没有覆写文件夹：{OverridesFolder}");
            Loader.Progress += ProgressIncrement;
        }

        // 设置 ini
        var OverridesIni = $@"{OverridesFolder}PCL\Setup.ini";
        var VersionIni = $@"{VersionFolder}PCL\Setup.ini";
        if (File.Exists(OverridesIni))
        {
            ModBase.WriteIni(OverridesIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
            ModBase.WriteIni(OverridesIni, "VersionArgumentIndieV2", true.ToString());
            ModBase.CopyFile(OverridesIni, VersionIni); // 覆写已有的 ini
        }
        else
        {
            ModBase.WriteIni(VersionIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
            ModBase.WriteIni(VersionIni, "VersionArgumentIndieV2", true.ToString());
        }

        ModBase.IniClearCache(VersionIni); // 重置缓存，避免被安装过程中写入的 ini 覆盖
    }

    #region CurseForge

    private static LoaderCombo<string> InstallPackCurseForge(string FileAddress, ZipArchive Archive,
        string ArchiveBaseFolder, string InstanceName = null, string Logo = null, string resourceId = null,
        bool isOnlineInstall = false)
    {
        // 读取 Json 文件
        JsonObject Json;
        try
        {
            Json = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "manifest.json").Open()));
        }
        catch (Exception ex)
        {
            throw new Exception("CurseForge 整合包安装信息存在问题", ex);
        }

        if (Json["minecraft"] is null || Json["minecraft"]["version"] is null)
            throw new Exception("CurseForge 整合包未提供 Minecraft 版本信息");

        // 获取实例名
        if (InstanceName is null)
        {
            InstanceName = (string)(Json["name"] ?? "");
            var Validate = new FolderNameValidator(Path.Combine(ModMinecraft.McFolderSelected, "versions"));
            if (!Validate.Validate(InstanceName).IsValid)
                InstanceName = "";
            if (string.IsNullOrEmpty(InstanceName))
                InstanceName = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                    [Validate]);
            if (string.IsNullOrEmpty(InstanceName))
                throw new ModBase.CancelledException();
        }

        // 获取 Mod API 版本信息
        string ForgeVersion = null;
        string NeoForgeVersion = null;
        string FabricVersion = null;
        string QuiltVersion = null;
        foreach (var Entry in (dynamic)Json["minecraft"]["modLoaders"] ?? Array.Empty<JsonNode>())
        {
            string Id = (Entry["id"] ?? "").ToString().ToLower();
            if (Id.StartsWithF("forge-"))
            {
                // Forge 指定
                if (Id.Contains("recommended"))
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.TooOldUnsupported"));
                ModBase.Log("[ModPack] 整合包 Forge 版本：" + Id);
                ForgeVersion = Id.Replace("forge-", "");
            }
            else if (Id.StartsWithF("neoforge-"))
            {
                // NeoForge 指定
                ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + Id);
                NeoForgeVersion = Id.Replace("neoforge-", "");
            }
            else if (Id.StartsWithF("fabric-"))
            {
                // Fabric 指定
                try
                {
                    ModBase.Log("[ModPack] 整合包 Fabric 版本：" + Id);
                    FabricVersion = Id.Replace("fabric-", "");
                    break;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取整合包 Fabric 版本失败：" + Id);
                }
            }
            else if (Id.StartsWithF("quilt-"))
            {
                // Quilt 指定
                try
                {
                    ModBase.Log("[ModPack] 整合包 Quilt 版本：" + Id);
                    QuiltVersion = Id.Replace("quilt-", "");
                    break;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取整合包 Quilt 版本失败：" + Id);
                }
            }
        }

        // 解压
        var InstallTemp = ModMain.RequestTaskTempFolder();
        var InstallLoaders = new List<LoaderBase>();
        var OverrideHome = (string)(Json["overrides"] ?? "");
        if (!string.IsNullOrEmpty(OverrideHome))
            InstallLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
                Task =>
            {
                ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
                CopyOverrideDirectory(
                    Path.Combine(InstallTemp, ArchiveBaseFolder, OverrideHome == "." || OverrideHome == "./" ? "" : OverrideHome),
                    $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}", Task, 0.4d);
            })
            {
                ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
        // 获取 Mod 列表
        var ModList = new List<int>();
        var ModOptionalList = new List<int>();
        foreach (var ModEntry in (dynamic)Json["files"] ?? Array.Empty<JsonNode>())
        {
            if (ModEntry["projectID"] is null || ModEntry["fileID"] is null)
            {
                ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.ModMissingRequiredInfoSkipped", ModEntry));
                continue;
            }

            ModList.Add((int)ModEntry["fileID"]);
            if (ModEntry["required"] is JsonNode requiredNode && !requiredNode.ToObject<bool>())
                ModOptionalList.Add((int)ModEntry["fileID"]);
        }

        if (ModList.Any())
        {
            var ModDownloadLoaders = new List<LoaderBase>();
            // 获取 Mod 下载信息
            ModDownloadLoaders.Add(new LoaderTask<int, JsonArray>(
                Lang.Text("Minecraft.Download.Modpack.Stage.PrepareModsDownloadInfo"), Task =>
            {
                var allowMirror = true;
                JsonArray ret;
                var tryCount = 0;
                do
                {
                    tryCount += 1;
                    ret = (JsonArray)((JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                        "https://api.curseforge.com/v1/mods/files",
                        "POST", "{\"fileIds\": [" + ModList.Join(",") + "]}", "application/json",
                        allowMirror)))["data"];
                    if (ModList.Count <= ret.Count)
                    {
                        ModBase.Log("[Modpack] 已获取到的模组数量足够，开始进行下一步");
                        break;
                    }

                    allowMirror = false;
                    ModBase.Log($"[Modpack] 获取模组数量不达标，设置镜像源允许状态为: {allowMirror}");
                    if (tryCount > 3) throw new Exception(Lang.Text("Minecraft.Download.Modpack.SomeModsDeleted"));
                } while (true);

                Task.Output = ret;
            })
            {
                ProgressWeight = ModList.Count / 10d
            }); // 每 10 Mod 需要 1s
            // 构造 NetFile
            ModDownloadLoaders.Add(new LoaderTask<JsonArray, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Modpack.Stage.BuildModsDownloadInfo"), Task =>
            {
                var FileList = new Dictionary<int, DownloadFile>();
                foreach (var ModJson in Task.Input)
                {
                    var Id = ModJson["id"].ToObject<int>();
                    // 跳过重复的 Mod（疑似 CurseForge Bug）
                    if (FileList.ContainsKey(Id))
                        continue;
                    // 可选 Mod 提示
                    if (ModOptionalList.Contains(Id))
                        if (ModMain.MyMsgBox(
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Message", ModJson["displayName"]),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")
                            ) == 2)
                            continue;

                    // 根据 modules 和文件名后缀判断资源类型
                    string TargetFolder;
                    ModComp.CompType Type;
                    if (ModJson["modules"].AsArray().Any()) // modules 可能返回 null（#1006）
                    {
                        var ModuleNames = ((JsonArray)ModJson["modules"]).Select(l => l["name"].ToString()).ToList();
                        if (ModuleNames.Contains("META-INF") || ModuleNames.Contains("mcmod.info") ||
                            (ModJson?["FileName"]?.ToString()?.EndsWithF(".jar", true)).GetValueOrDefault())
                        {
                            TargetFolder = "mods";
                            Type = ModComp.CompType.Mod;
                        }
                        else if (ModuleNames.Contains("pack.mcmeta"))
                        {
                            TargetFolder = "resourcepacks";
                            Type = ModComp.CompType.ResourcePack;
                        }
                        else if (ModuleNames.Contains("level.dat"))
                        {
                            TargetFolder = "saves";
                            Type = ModComp.CompType.World;
                        }
                        else
                        {
                            TargetFolder = "shaderpacks";
                            Type = ModComp.CompType.Shader;
                        }
                    }
                    else
                    {
                        TargetFolder = "mods";
                        Type = ModComp.CompType.Mod;
                    }

                    // 建立 CompFile
                    var File = new ModComp.CompFile((JsonObject)ModJson, Type);
                    if (!File.Available)
                        continue;
                    // 实际的添加
                    FileList.Add(Id,
                        File.ToNetFile($@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\{TargetFolder}\"));
                    Task.Progress += 1d / (1 + ModList.Count);
                }

                Task.Output = FileList.Values.ToList();
            })
            {
                ProgressWeight = ModList.Count / 200d,
                Show = false
            }); // 每 200 Mod 需要 1s
            // 下载 Mod 文件
            ModDownloadLoaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods"), [])
                { ProgressWeight = ModList.Count * 1.5d }); // 每个 Mod 需要 1.5s
            // 构造加载器
            InstallLoaders.Add(
                new LoaderCombo<int>(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods.MainLoader"),
                        ModDownloadLoaders)
                { Show = false, ProgressWeight = ModDownloadLoaders.Sum(l => l.ProgressWeight) });
        }

        // 构造加载器
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = InstanceName,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\",
            MinecraftName = Json["minecraft"]["version"].ToString(),
            ForgeVersion = ForgeVersion,
            NeoForgeVersion = NeoForgeVersion,
            FabricVersion = FabricVersion,
            QuiltVersion = QuiltVersion
        };
        var MergeLoaders = ModDownloadLib.McInstallLoader(Request);
        // 构造总加载器
        var Loaders = new List<LoaderBase>();
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                InstallLoaders)
            { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), MergeLoaders)
            { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
        Loaders.Add(new LoaderTask<string, string>(Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"), Task =>
        {
            // 设置图标
            var VersionFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\";
            if (Logo is not null && File.Exists(Logo))
            {
                File.Copy(Logo, Path.Combine(VersionFolder, "PCL", "Logo.png"), true);
                States.Instance.LogoPath[VersionFolder] = @"PCL\Logo.png";
                States.Instance.IsLogoCustom[VersionFolder] = true;
                ModBase.Log("[ModPack] 已设置整合包 Logo：" + Logo);
            }

            // 删除原始整合包文件
            foreach (var Target in new[] { Path.Combine(VersionFolder, "原始整合包.zip"), Path.Combine(VersionFolder, "原始整合包.mrpack") })
                if (File.Exists(Target))
                {
                    ModBase.Log("[ModPack] 删除原始整合包文件：" + Target);
                    File.Delete(Target);
                }

            if (File.Exists(FileAddress) && ModBase.GetFileNameWithoutExtentionFromPath(FileAddress) == "modpack")
            {
                ModBase.Log("[ModPack] 删除安装整合包文件：" + FileAddress);
                File.Delete(FileAddress);
            }

            // 整合包版本
            if (Json["version"] is not null) States.Instance.ModpackVersion[VersionFolder] = Json["version"].ToString();
            States.Instance.ModpackSource[VersionFolder] = "CurseForge";
            States.Instance.ModpackId[VersionFolder] = resourceId;
            do
            {
                try
                {
                    var projects = ModComp.CompRequest.GetCompProjectsByIds([resourceId]);
                    if (projects.Count == 0)
                        break;
                    States.Instance.CustomInfo[VersionFolder] = projects.First().Description;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[ModPack] 获取整合包描述文本失败");
                }
            } while (false);
        })
        {
            ProgressWeight = 0.1d,
            Show = false
        });

        // 重复任务检查
        var LoaderName = "CurseForge 整合包安装：" + InstanceName + " ";
        if (LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 启动
        var Loader = new LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
        Loader.Start(Request.TargetInstanceFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        if (!isOnlineInstall)
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager));
        return Loader;
    }

    #endregion

    #region Modrinth

    private static LoaderCombo<string> InstallPackModrinth(string FileAddress, ZipArchive Archive,
        string ArchiveBaseFolder, string InstanceName = null, string Logo = null, string resourceId = null,
        bool isOnlineInstall = false)
    {
        // 读取 Json 文件
        JsonObject Json;
        try
        {
            Json = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "modrinth.index.json").Open()));
        }
        catch (Exception ex)
        {
            throw new Exception("Modrinth 整合包安装信息存在问题", ex);
        }

        if (Json["dependencies"] is null || Json["dependencies"]["minecraft"] is null)
            throw new Exception("Modrinth 整合包未提供 Minecraft 版本信息");
        // 获取 Mod API 版本信息
        string MinecraftVersion = null;
        string ForgeVersion = null;
        string NeoForgeVersion = null;
        string FabricVersion = null;
        string QuiltVersion = null;
        foreach (var Entry in Json["dependencies"]?.AsObject() ?? new JsonObject())
            switch (Entry.Key.ToLower() ?? "")
            {
                case "minecraft":
                {
                    MinecraftVersion = Entry.Value?.ToObject<string>();
                    break;
                }
                case "forge": // eg. 14.23.5.2859 / 1.19-41.1.0
                {
                    ForgeVersion = Entry.Value?.ToObject<string>();
                    ModBase.Log("[ModPack] 整合包 Forge 版本：" + ForgeVersion);
                    break;
                }
                case "neoforge":
                case "neo-forge": // eg. 20.6.98-beta
                {
                    NeoForgeVersion = Entry.Value?.ToObject<string>();
                    ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + NeoForgeVersion);
                    break;
                }
                case "fabric-loader": // eg. 0.14.14
                {
                    FabricVersion = Entry.Value?.ToObject<string>();
                    ModBase.Log("[ModPack] 整合包 Fabric 版本：" + FabricVersion);
                    break;
                }
                case "quilt-loader": // eg. 0.26.0
                {
                    QuiltVersion = Entry.Value?.ToObject<string>();
                    ModBase.Log("[ModPack] 整合包 Quilt 版本：" + QuiltVersion);
                    break;
                }

                default:
                {
                    ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.UnknownLoader", Entry.Key, Entry.Value),
                        ModMain.HintType.Critical);
                    break;
                }
            }

        // 获取实例名
        if (InstanceName is null)
        {
            InstanceName = (string)(Json["name"] ?? "");
            var Validate = new FolderNameValidator(Path.Combine(ModMinecraft.McFolderSelected, "versions"));
            if (!Validate.Validate(InstanceName).IsValid)
                InstanceName = "";
            if (string.IsNullOrEmpty(InstanceName))
                InstanceName = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                    [Validate]);
            if (string.IsNullOrEmpty(InstanceName))
                throw new ModBase.CancelledException();
        }

        // 解压
        var InstallTemp = ModMain.RequestTaskTempFolder();
        var InstallLoaders = new List<LoaderBase>();
        InstallLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            Task =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.5d);
            CopyOverrideDirectory(Path.Combine(InstallTemp, ArchiveBaseFolder, "overrides"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName), Task, 0.4d);
            CopyOverrideDirectory(Path.Combine(InstallTemp, ArchiveBaseFolder, "client-overrides"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName), Task, 0.1d);
        })
        {
            ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
            Block = false
        }); // 每 6M 需要 1s
        // 获取下载文件列表
        var FileList = new List<DownloadFile>();
        foreach (var File in (dynamic)Json["files"] ?? Array.Empty<JsonNode>())
        {
            // 检查是否需要该文件
            if (File["env"] is not null)
                switch (File["env"]["client"].ToString() ?? "")
                {
                    case "optional":
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Message",
                                    ModBase.GetFileNameFromPath(File["path"].ToString())),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")
                            ) == 2) continue;

                        break;
                    }
                    case "unsupported":
                    {
                        continue;
                    }
                }

            // 添加下载文件
            var Urls = ((JsonArray)File["downloads"])
                .OfType<JsonNode>()
                .Select(x => ModComp.CompFile.HandleCurseForgeDownloadUrls(x.ToString()))
                .ToList();
            // 镜像源
            Urls = Urls.SelectMany(x => ModDownload.DlSourceModDownloadGet(x)).ToList();
            var TargetPath = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\{File["path"]}";
            if (!Path.GetFullPath(TargetPath)
                    .StartsWithF($@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\", true))
            {
                ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Message", TargetPath),
                    Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Title"), IsWarn: true);
                throw new ModBase.CancelledException();
            }

            FileList.Add(new DownloadFile(Urls, TargetPath,
                new ModBase.FileChecker(ActualSize: ((JsonNode)File["fileSize"]).ToObject<long>(),
                    Hash: File["hashes"]["sha1"].ToString()), true));
        }

        if (FileList.Any())
            InstallLoaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadAdditions"), FileList)
                { ProgressWeight = FileList.Count * 1.5d }); // 每个 Mod 需要 1.5s

        // 构造加载器
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = InstanceName,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\",
            MinecraftName = MinecraftVersion,
            ForgeVersion = ForgeVersion,
            NeoForgeVersion = NeoForgeVersion,
            FabricVersion = FabricVersion,
            QuiltVersion = QuiltVersion
        };
        var MergeLoaders = ModDownloadLib.McInstallLoader(Request);
        // 构造总加载器
        var Loaders = new List<LoaderBase>();
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                InstallLoaders)
            { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), MergeLoaders)
            { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
        Loaders.Add(new LoaderTask<string, string>(Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"), Task =>
        {
            // 设置图标
            var VersionFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\";
            if (Logo is not null && File.Exists(Logo))
            {
                File.Copy(Logo, Path.Combine(VersionFolder, "PCL", "Logo.png"), true);
                States.Instance.LogoPath[VersionFolder] = @"PCL\Logo.png";
                States.Instance.IsLogoCustom[VersionFolder] = true;
                ModBase.Log("[ModPack] 已设置整合包 Logo：" + Logo);
            }

            // 删除原始整合包文件
            foreach (var Target in new[] { Path.Combine(VersionFolder, "原始整合包.zip"), Path.Combine(VersionFolder, "原始整合包.mrpack") })
                if (File.Exists(Target))
                {
                    ModBase.Log("[ModPack] 删除原始整合包文件：" + Target);
                    File.Delete(Target);
                }

            if (File.Exists(FileAddress) && ModBase.GetFileNameWithoutExtentionFromPath(FileAddress) == "modpack")
            {
                ModBase.Log("[ModPack] 删除安装整合包文件：" + FileAddress);
                File.Delete(FileAddress);
            }

            // 整合包版本
            if (Json["versionId"] is not null)
                States.Instance.ModpackVersion[VersionFolder] = Json["versionId"].ToString();
            States.Instance.ModpackSource[VersionFolder] = "Modrinth";
            States.Instance.ModpackId[VersionFolder] = resourceId;
            do
            {
                try
                {
                    var projects = ModComp.CompRequest.GetCompProjectsByIds([resourceId]);
                    if (projects.Count == 0)
                        break;
                    States.Instance.CustomInfo[VersionFolder] = projects.First().Description;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[ModPack] 获取整合包描述文本失败");
                }
            } while (false);
        })
        {
            ProgressWeight = 0.1d,
            Show = false
        });

        // 重复任务检查
        var LoaderName = $"Modrinth 整合包安装：{InstanceName} ";
        if (LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 启动
        var Loader = new LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
        Loader.Start(Request.TargetInstanceFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        if (!isOnlineInstall)
            ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager));
        return Loader;
    }

    #endregion

    #region HMCL

    private static LoaderCombo<string> InstallPackHMCL(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
    {
        // 读取 Json 文件
        JsonObject Json;
        try
        {
            Json = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "modpack.json").Open(), Encoding.UTF8));
        }
        catch (Exception ex)
        {
            throw new Exception("HMCL 整合包安装信息存在问题", ex);
        }

        // 获取实例名
        var InstanceName = (string)(Json["name"] ?? "");
        var Validate = new FolderNameValidator(Path.Combine(ModMinecraft.McFolderSelected, "versions"));
        if (!Validate.Validate(InstanceName).IsValid)
            InstanceName = "";
        if (string.IsNullOrEmpty(InstanceName))
            InstanceName = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                [Validate]);
        if (string.IsNullOrEmpty(InstanceName))
            throw new ModBase.CancelledException();
        // 解压
        var InstallTemp = ModMain.RequestTaskTempFolder();
        var InstallLoaders = new List<LoaderBase>();
        InstallLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            Task =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6d);
            CopyOverrideDirectory(Path.Combine(InstallTemp, ArchiveBaseFolder, "minecraft"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName), Task, 0.4d);
        })
        {
            ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
            Block = false
        }); // 每 6M 需要 1s
        // 构造游戏本体安装加载器
        if (Json["gameVersion"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Hmcl"));
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = InstanceName,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\",
            MinecraftName = Json["gameVersion"].ToString()
        };
        var MergeLoaders = ModDownloadLib.McInstallLoader(Request);
        // 构造总加载器
        var Loaders = new List<LoaderBase>
        {
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"), InstallLoaders)
                { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) },
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), MergeLoaders)
                { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) }
        };
        // 重复任务检查
        var LoaderName = "HMCL 整合包安装：" + InstanceName + " ";
        if (LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 启动
        var Loader = new LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
        Loader.Start(Request.TargetInstanceFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager));
        return Loader;
    }

    #endregion

    #region MCBBS

    private static LoaderCombo<string> InstallPackMCBBS(string FileAddress, ZipArchive Archive,
        string ArchiveBaseFolder, string InstanceName = null)
    {
        // 读取 Json 文件
        JsonObject Json;
        try
        {
            // VB 的 If(a, b) 在 C# 中如果是 null 合并则用 ??，如果是三元运算则用 ?:
            var Entry = Archive.GetEntry(ArchiveBaseFolder + "mcbbs.packmeta") ??
                        Archive.GetEntry(ArchiveBaseFolder + "manifest.json");
            using (var stream = Entry.Open())
            {
                Json = (JsonObject)ModBase.GetJson(ModBase.ReadFile(stream, Encoding.UTF8));
            }
        }
        catch (Exception ex)
        {
            throw new Exception("MCBBS 整合包安装信息存在问题", ex);
        }

        // 获取实例名
        if (InstanceName is null)
        {
            InstanceName = Json["name"]?.ToString() ?? "";
            var Validate = new FolderNameValidator(Path.Combine(ModMinecraft.McFolderSelected, "versions"));

            if (!Validate.Validate(InstanceName).IsValid) InstanceName = "";

            if (string.IsNullOrEmpty(InstanceName))
                InstanceName = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                    [Validate]);

            if (string.IsNullOrEmpty(InstanceName)) throw new ModBase.CancelledException();
        }

        // 解压与路径准备
        var InstallTemp = ModMain.RequestTaskTempFolder();
        var VersionFolder = $"{ModMinecraft.McFolderSelected}versions\\{InstanceName}";
        var InstallLoaders = new List<LoaderBase>();

        // 解压整合包文件任务
        var unzipTask = new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            Task =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.6);
            CopyOverrideDirectory(
                Path.Combine(InstallTemp, ArchiveBaseFolder, "overrides"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName),
                Task, 0.4);

            // JVM 参数处理
            if (Json["launchInfo"] is not null)
            {
                var LaunchInfo = (JsonObject)Json["launchInfo"];
                Config.Instance.JvmArgs[VersionFolder] = string.Join(" ", LaunchInfo["javaArgument"]);
                Config.Instance.GameArgs[VersionFolder] = string.Join(" ", LaunchInfo["launchArgument"]);
            }

            // 整合包版本
            if (Json["version"] is not null) States.Instance.ModpackVersion[VersionFolder] = Json["version"].ToString();
        });

        unzipTask.ProgressWeight = new FileInfo(FileAddress).Length / 1024.0 / 1024.0 / 6.0; // 每 6M 需要 1s
        unzipTask.Block = false;
        InstallLoaders.Add(unzipTask);

        // 构造加载器
        if (Json["addons"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.McbbsAddons"));

        var Addons = new Dictionary<string, string>();
        foreach (var EntryNode in Json["addons"].AsArray()) { var Entry = EntryNode.AsObject(); Addons.Add(Entry["id"].ToString(), Entry["version"].ToString()); }

        if (!Addons.ContainsKey("game"))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"), ModMain.HintType.Critical);
            return null;
        }

        // 构造安装请求
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = InstanceName,
            TargetInstanceFolder = $"{ModMinecraft.McFolderSelected}versions\\{InstanceName}\\",
            MinecraftName = Addons["game"],
            OptiFineVersion = Addons.ContainsKey("optifine") ? Addons["optifine"] : null,
            ForgeVersion = Addons.ContainsKey("forge") ? Addons["forge"] : null,
            NeoForgeVersion = Addons.ContainsKey("neoforge") ? Addons["neoforge"] : null,
            FabricVersion = Addons.ContainsKey("fabric") ? Addons["fabric"] : null,
            QuiltVersion = Addons.ContainsKey("quilt") ? Addons["quilt"] : null
        };

        var MergeLoaders = ModDownloadLib.McInstallLoader(Request);

        // 构造总加载器
        var Loaders = new List<LoaderBase>();
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
            InstallLoaders)
        {
            Show = false,
            Block = false,
            ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight)
        });
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), MergeLoaders)
        {
            Show = false,
            ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight)
        });

        // 重复任务检查
        var LoaderName = "MCBBS 整合包安装：" + InstanceName + " ";
        if (LoaderTaskbar.Any(l => l.Name == LoaderName))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 启动任务
        var Loader = new LoaderCombo<string>(LoaderName, Loaders);
        Loader.OnStateChanged = ModDownloadLib.McInstallState;

        Loader.Start(Request.TargetInstanceFolder);
        LoaderTaskbarAdd(Loader);

        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager));

        return Loader;
    }

    #endregion

    #region 带启动器的压缩包

    private static LoaderCombo<string> InstallPackLauncherPack(string FileAddress, ZipArchive Archive,
        string ArchiveBaseFolder)
    {
        // 获取解压路径
        ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Lang.Text("Common.Action.Install"), Lang.Text("Common.Action.Continue"), ForceWait: true);
        var TargetFolder = SystemDialogs.SelectFolder(Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(TargetFolder))
            throw new ModBase.CancelledException();
        if (Directory.GetFileSystemEntries(TargetFolder).Length > 0)
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 解压
        var Loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), Task =>
            {
                ExtractModpackFiles(TargetFolder, FileAddress, Task, 0.9d);
                Thread.Sleep(400); // 避免文件争用
                // 查找解压后的 exe 文件
                string Launcher = null;
                foreach (var ExeFile in Directory.GetFiles(TargetFolder, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    var Info = FileVersionInfo.GetVersionInfo(ExeFile);
                    ModBase.Log($"[Modpack] 文件 {ExeFile} 的产品名标识为 {Info.ProductName}");
                    if (Info.ProductName == "Plain Craft Launcher")
                    {
                        Launcher = ExeFile;
                        ModBase.Log($"[Modpack] 发现整合包附带的 PCL 启动器：{ExeFile}");
                    }
                    else if ((Info.ProductName.ContainsF("Launcher", true) || Info.ProductName.ContainsF("启动", true)) &&
                             !(Info.ProductName == "Plain Craft Launcher Admin Manager"))
                    {
                        if (Launcher is null)
                        {
                            Launcher = ExeFile;
                            ModBase.Log($"[Modpack] 发现整合包附带的疑似第三方启动器：{ExeFile}");
                        }
                    }
                }

                Task.Progress = 0.95d;
                // 尝试使用附带的启动器打开
                if (Launcher is not null)
                {
                    ModBase.Log("[Modpack] 找到压缩包中附带的启动器：" + Launcher);
                    if (ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Message", Launcher),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Title"),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.UseBundled"),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.DoNotUse")
                        ) == 1)
                    {
                        ModBase.OpenExplorer(TargetFolder);
                        ModBase.ShellOnly(Launcher, "--wait"); // 要求等待已有的 PCL 退出
                        ModBase.Log("[Modpack] 为换用整合包中的启动器启动，强制结束程序");
                        ModMain.FrmMain.EndProgram(false);
                        return;
                    }
                }
                else
                {
                    ModBase.Log("[Modpack] 未找到压缩包中附带的启动器");
                }

                ModBase.OpenExplorer(TargetFolder);
                // 加入文件夹列表
                var InstanceName = ModBase.GetFolderNameFromPath(TargetFolder);
                Directory.CreateDirectory(Path.Combine(TargetFolder, ".minecraft"));
                PageSelectLeft.AddFolder(
                    Path.Combine(TargetFolder, ".minecraft", ArchiveBaseFolder.Replace("/", @"\").TrimStart('\\')), InstanceName,
                    false); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
                // 调用 modpack 文件进行安装
                var ModpackFile = Directory.GetFiles(TargetFolder, "modpack.*", SearchOption.AllDirectories).First();
                ModBase.Log("[Modpack] 调用 modpack 文件继续安装：" + ModpackFile);
                ModpackInstall(ModpackFile);
            })
        });
        Loader.Start(TargetFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
        return Loader;
    }

    #endregion

    #region 普通压缩包

    private static LoaderCombo<string> InstallPackCompress(string FileAddress, ZipArchive Archive)
    {
        // 尝试定位 .minecraft 文件夹：寻找形如 “/versions/XXX/XXX.json” 的路径
        Match Match = null;
        var Regex = new Regex(@"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)", RegexOptions.IgnoreCase);
        foreach (var Entry in Archive.Entries)
        {
            var EntryMatch = Regex.Match("/" + Entry.FullName);
            if (EntryMatch.Success)
            {
                Match = EntryMatch;
                break;
            }
        }

        if (Match is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnknownArchiveStructure")); // 没有匹配
        var ArchiveBaseFolder = Match.Value.Replace("/", @"\").TrimStart('\\'); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
        var InstanceName = Match.Groups[1].Value;
        ModBase.Log("[ModPack] 检测到压缩包的 .minecraft 根目录：" + ArchiveBaseFolder + "，命中的实例名：" + InstanceName);
        // 获取解压路径
        ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Lang.Text("Common.Action.Install"), Lang.Text("Common.Action.Continue"), ForceWait: true);
        var TargetFolder = SystemDialogs.SelectFolder(Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(TargetFolder))
            throw new ModBase.CancelledException();
        if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", TargetFolder),
                ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        if (Directory.GetFileSystemEntries(TargetFolder).Length > 0)
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 解压
        var Loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), Task =>
            {
                ExtractModpackFiles(TargetFolder, FileAddress, Task, 0.95d);
                // 加入文件夹列表
                PageSelectLeft.AddFolder(Path.Combine(TargetFolder, ArchiveBaseFolder), ModBase.GetFolderNameFromPath(TargetFolder),
                    false);
                Thread.Sleep(400); // 避免文件争用
                ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSelect));
            })
        })
        {
            OnStateChanged = ModDownloadLib.McInstallState
        };
        Loader.Start(TargetFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
        return Loader;
    }

    #endregion

    #region MultiMC

    public class MMCPackInfo
    {
        public JsonObject AdditionalJson = new();
        public bool IsCleanroomOverrided;
        public bool IsFabricOverrided;
        public bool IsForgeOverrided;
        public bool IsMcArgsEdited;
        public bool IsMinecraftOverrided;
        public bool IsNeoForgeOverrided;
        public bool IsQuiltOverrided;
        public JsonArray JvmArgs = new();
        public JsonArray Libraries = new();
        public JsonObject OverridedJson = new();
        public string Tweakers = null;
    }

    private static LoaderCombo<string> InstallPackMMC(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
    {
        // 读取 Json 文件
        JsonObject PackJson;
        string PackInstance;
        MMCPackInfo PackInfo = null;
        try
        {
            PackJson = (JsonObject)ModBase.GetJson(
                ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "mmc-pack.json").Open(), Encoding.UTF8));
            PackInstance = ModBase.ReadFile(Archive.GetEntry(ArchiveBaseFolder + "instance.cfg").Open(), Encoding.UTF8);

            #region JSON Patches

            // 参考 https://github.com/MultiMC/Launcher/wiki/JSON-Patches
            do
            {
                try
                {
                    if (!Archive.Entries.Any(e =>
                            e.FullName.Equals(ArchiveBaseFolder + "patches/", StringComparison.OrdinalIgnoreCase)))
                        break;
                    ModBase.Log("[ModPack] 安装的 MultiMC 整合包存在 JSON Patches");
                    // 排序预处理
                    var Patches = new List<KeyValuePair<JsonObject, int>>();
                    foreach (var entry in Archive.Entries)
                        if (!entry.FullName.EndsWith("/") && entry.FullName.StartsWith(ArchiveBaseFolder + "patches/"))
                        {
                            var Patch = (JsonObject)ModBase.GetJson(ModBase.ReadFile(
                                Archive.GetEntry(entry.FullName).Open(), Encoding.UTF8));
                            Patches.Add(new KeyValuePair<JsonObject, int>(Patch,
                                (int)(Patch["order"] is not null ? Patch["order"] : 0)));
                        }

                    var Components = (JsonArray)PackJson["components"];
                    foreach (var Patch in Patches)
                    {
                        // 检查 Patch 是否在 mmc-pack.json 中
                        var IsContainedInPackJson = false;
                        foreach (var Component in Components)
                            if ((Component["uid"].ToString() ?? "") == (Patch.Key["uid"].ToString() ?? ""))
                            {
                                IsContainedInPackJson = true;
                                break;
                            }

                        if (!IsContainedInPackJson)
                        {
                            ModBase.Log($"[ModPack] JSON-Patch {Patch.Key["uid"]} 未包含于 mmc-pack.json, 跳过该 Patch");
                            Patches.Remove(Patch);
                        }
                    }

                    Patches.Sort((x, y) => x.Value.CompareTo(y.Value));
                    // 应用 Patches
                    PackInfo = new MMCPackInfo();

                    string Tweakers = null;
                    JsonObject AssetIndex = null;
                    JsonObject JavaVerJson = null;
                    string MainClass = null;
                    var GameArguments = new JsonArray();
                    var JvmArguments = new JsonArray();
                    var LibJson = new JsonArray();
                    var AddLibJson = new JsonArray();
                    foreach (var Patch in Patches)
                    {
                        var PatchJson = Patch.Key;
                        if ((string)PatchJson["uid"] == "net.minecraft")
                        {
                            PackInfo.IsMinecraftOverrided = true;
                        }
                        else if ((string)PatchJson["uid"] == "net.minecraftforge")
                        {
                            if (PatchJson["version"].ToString().StartsWithF("0."))
                                PackInfo.IsCleanroomOverrided = true;
                            else
                                PackInfo.IsForgeOverrided = true;
                        }
                        else if ((string)PatchJson["uid"] == "net.neoforged")
                        {
                            PackInfo.IsNeoForgeOverrided = true;
                        }
                        else if ((string)PatchJson["uid"] == "net.fabricmc.fabric-loader")
                        {
                            PackInfo.IsFabricOverrided = true;
                        }
                        else if ((string)PatchJson["uid"] == "org.quiltmc.quilt-loader")
                        {
                            PackInfo.IsQuiltOverrided = true;
                        }

                        // JVM 参数
                        if (PatchJson["+jvmArgs"] is not null)
                        {
                            JvmArguments.Merge(PatchJson["+jvmArgs"]);
                            ModBase.Log($"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 JVM 参数");
                        }

                        // Libraries
                        if (PatchJson["libraries"] is not null || PatchJson["+libraries"] is not null)
                        {
                            var Libs = new JsonArray();
                            if (PatchJson["libraries"] is not null)
                                foreach (var Library in PatchJson["libraries"].AsArray())
                                {
                                    if (Library is not JsonObject LibraryObj) continue;
                                    var LibJobj = LibraryObj.DeepClone().AsObject();
                                    if (LibJobj["MMC-hint"] is not null)
                                    {
                                        LibJobj.Add("hint", LibJobj["MMC-hint"]?.DeepClone());
                                        LibJobj.Remove("MMC-hint");
                                    }

                                    Libs.Add(LibJobj);
                                }

                            if (PatchJson["+libraries"] is not null)
                                foreach (var Library in PatchJson["+libraries"].AsArray()) // TODO: 此处处理不严谨，但也能用吧
                                {
                                    if (Library is not JsonObject LibraryObj) continue;
                                    var LibJobj = LibraryObj.DeepClone().AsObject();
                                    if (LibJobj["MMC-hint"] is not null)
                                    {
                                        LibJobj.Add("hint", LibJobj["MMC-hint"]?.DeepClone());
                                        LibJobj.Remove("MMC-hint");
                                    }

                                    Libs.Add(LibJobj);
                                }

                            LibJson.Merge(Libs);
                            ModBase.Log($"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 Libraries");
                        }

                        // Tweakers
                        if (PatchJson["+tweakers"] is not null)
                        {
                            Tweakers = (string)PatchJson["+tweakers"][0];
                            ModBase.Log($"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 Tweakers");
                        }

                        // AssetIndex
                        if (PatchJson["assetIndex"] is not null)
                        {
                            AssetIndex = PatchJson["assetIndex"]?.DeepClone().AsObject();
                            ModBase.Log($"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 AssetIndex");
                        }

                        // minecraftArguments -> arguments.game
                        if (PatchJson["minecraftArguments"] is not null)
                        {
                            foreach (var Arg in PatchJson["minecraftArguments"].ToString().Split(" "))
                                GameArguments.Add(Arg);
                            PackInfo.IsMcArgsEdited = true;
                            ModBase.Log(
                                $"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 minecraftArguments 至 arguments.game");
                        }

                        // mainClass
                        if (PatchJson["mainClass"] is not null)
                        {
                            MainClass = (string)PatchJson["mainClass"];
                            ModBase.Log($"[ModPack] 已应用 JSON-Patch {PatchJson["uid"]} 的 mainClass");
                        }

                        // Java 版本要求
                        if (PatchJson["compatibleJavaMajors"] is not null)
                        {
                            var JavaVersion = 0;
                            string JavaComponent = null;
                            var JavaMajors = (JsonArray)PatchJson["compatibleJavaMajors"];
                            foreach (var Java in JavaMajors)
                            {
                                if (JavaVersion > ModBase.Val(Java))
                                    continue;
                                // 优先选择主要的版本
                                if (ModBase.Val(Java) == 21d)
                                {
                                    JavaVersion = 21;
                                    JavaComponent = "java-runtime-delta";
                                }
                                else if (ModBase.Val(Java) == 17d)
                                {
                                    JavaVersion = 17;
                                    JavaComponent = "java-runtime-gamma";
                                }
                                else if (ModBase.Val(Java) == 11d)
                                {
                                    JavaVersion = 11;
                                    JavaComponent = null;
                                }
                                else if (ModBase.Val(Java) == 8d)
                                {
                                    JavaVersion = 8;
                                    JavaComponent = "jre-legacy";
                                }
                            }

                            if (JavaVersion == 0)
                            {
                                JavaVersion = (int)JavaMajors[0];
                                JavaComponent = null;
                            }

                            JavaVerJson = new JsonObject { { "majorVersion", JavaVersion } };
                            if (JavaComponent is not null) JavaVerJson.Add("component", JavaComponent);
                            ModBase.Log($"[ModPack] JSON-Patch {PatchJson["uid"]} 要求 Java 版本: " + JavaVersion);
                        }
                    }

                    JsonObject JsonArguments = null;
                    if (!string.IsNullOrWhiteSpace(Tweakers))
                    {
                        GameArguments.Add("--tweakClass");
                        GameArguments.Add(Tweakers);
                    }

                    if (GameArguments is not null || JvmArguments is not null)
                    {
                        JvmArguments.Insert(0, "-Djava.library.path=${natives_directory}");
                        JvmArguments.Insert(1, "-Dminecraft.launcher.brand=${launcher_name}");
                        JvmArguments.Insert(2, "-Dminecraft.launcher.version=${launcher_version}");
                        JvmArguments.Insert(3, "-cp");
                        JvmArguments.Insert(4, "${classpath}");
                        JsonArguments = new JsonObject { { "game", GameArguments }, { "jvm", JvmArguments } };
                    }

                    PackInfo.OverridedJson = new JsonObject();
                    if (JsonArguments is not null)
                        PackInfo.OverridedJson.Add("arguments", JsonArguments);
                    if (MainClass is not null)
                        PackInfo.OverridedJson.Add("mainClass", MainClass);
                    if (AssetIndex is not null)
                        PackInfo.OverridedJson.Add("assetIndex", AssetIndex);
                    if (JavaVerJson is not null)
                        PackInfo.OverridedJson.Add("javaVersion", JavaVerJson);
                    if (LibJson is not null)
                        PackInfo.OverridedJson.Add("libraries", LibJson);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "应用 MMC JSON-Patches 失败");
                }
            } while (false);
        }

        #endregion

        catch (Exception ex)
        {
            throw new Exception("MMC 整合包安装信息存在问题", ex);
        }

        // 获取实例名
        var InstanceName = PackInstance.RegexSeek(@"(?<=\nname\=)[^\n]+") ?? "";
        var Validate = new FolderNameValidator(Path.Combine(ModMinecraft.McFolderSelected, "versions"));
        if (!Validate.Validate(InstanceName).IsValid)
            InstanceName = "";
        if (string.IsNullOrEmpty(InstanceName))
            InstanceName = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                [Validate]);
        if (string.IsNullOrEmpty(InstanceName))
            throw new ModBase.CancelledException();
        // 解压
        var InstallTemp = ModMain.RequestTaskTempFolder();
        var VersionFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}";
        var InstallLoaders = new List<LoaderBase>();
        InstallLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            Task =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task, 0.55d);
            CopyOverrideDirectory(Path.Combine(InstallTemp, ArchiveBaseFolder, "libraries"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName, "libraries"), Task, 0.2d);
            CopyOverrideDirectory(Path.Combine(InstallTemp, ArchiveBaseFolder, ".minecraft"),
                Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName), Task, 0.2d);

            #region instance.cfg

            // 读取 MMC 设置文件（#2655）
            try
            {
                var MMCSetupFile = Path.Combine(InstallTemp, ArchiveBaseFolder, "instance.cfg");
                // 将其中的等号替换为冒号，以符合 ini 文件格式
                if (File.Exists(MMCSetupFile))
                {
                    List<string> Lines = [];
                    foreach (var Line in ModBase.ReadFile(MMCSetupFile).Split(new[] { "\r", "\n" },
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!Line.Contains("="))
                            continue;
                        Lines.Add(Line.BeforeFirst("=") + ":" + Line.AfterFirst("="));
                    }

                    ModBase.WriteFile(MMCSetupFile, Lines.Join("\r\n"));
                    // 读取文件
                    if (Convert.ToBoolean(ModBase.ReadIni(MMCSetupFile, "OverrideCommands",
                            false.ToString())))
                    {
                        var PreLaunchCommand = ModBase.ReadIni(MMCSetupFile, "PreLaunchCommand");
                        if (!string.IsNullOrEmpty(PreLaunchCommand))
                        {
                            PreLaunchCommand = PreLaunchCommand.Replace(@"\""", "\"")
                                .Replace("$INST_JAVA", "{java}java.exe").Replace(@"$INST_MC_DIR\", "{minecraft}")
                                .Replace("$INST_MC_DIR", "{minecraft}").Replace(@"$INST_DIR\", "{verpath}")
                                .Replace("$INST_DIR", "{verpath}").Replace("$INST_ID", "{name}")
                                .Replace("$INST_NAME", "{name}");
                            Config.Instance.PreLaunchCommand[VersionFolder] = PreLaunchCommand;
                            ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：启动前执行命令：" + PreLaunchCommand);
                        }
                    }

                    if (Convert.ToBoolean(ModBase.ReadIni(MMCSetupFile, "JoinServerOnLaunch",
                            false.ToString())))
                    {
                        var ServerAddress = ModBase.ReadIni(MMCSetupFile, "JoinServerOnLaunchAddress")
                            .Replace(@"\""", "\"");
                        Config.Instance.ServerToEnter[VersionFolder] = ServerAddress;
                        ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：自动进入服务器：" + ServerAddress);
                    }

                    if (Convert.ToBoolean(ModBase.ReadIni(MMCSetupFile, "IgnoreJavaCompatibility",
                            false.ToString())))
                    {
                        Config.Instance.IgnoreJavaCompatibility[VersionFolder] = true;
                        ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：忽略 Java 兼容性警告");
                    }

                    var Logo = Path.GetFileName(ModBase.ReadIni(MMCSetupFile, "iconKey"));
                    if (!string.IsNullOrEmpty(Logo) && File.Exists($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png"))
                    {
                        States.Instance.IsLogoCustom[VersionFolder] = true;
                        States.Instance.LogoPath[VersionFolder] = @"PCL\Logo.png";
                        ModBase.CopyFile($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png",
                            $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\PCL\Logo.png");
                        ModBase.Log($"[ModPack] 迁移 MultiMC 实例独立设置：实例图标（{Logo}.png）");
                    }

                    // JVM 参数
                    var JvmArgs = ModBase.ReadIni(MMCSetupFile, "JvmArgs");
                    if (!string.IsNullOrEmpty(JvmArgs))
                    {
                        if (Convert.ToBoolean(ModBase.ReadIni(MMCSetupFile, "OverrideJavaArgs",
                                false.ToString())))
                        {
                            Config.Instance.JvmArgs[VersionFolder] = JvmArgs;
                            ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（覆盖）：" + JvmArgs);
                        }
                        else
                        {
                            JvmArgs = JvmArgs +
                                                           " " +
                                                               Config.Launch.JvmArgs;
                            Config.Instance.JvmArgs[VersionFolder] = JvmArgs;
                            ModBase.Log("[ModPack] 迁移 MultiMC 实例独立设置：JVM 参数（追加）：" + JvmArgs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"读取 MMC 配置文件失败（{InstallTemp}{ArchiveBaseFolder}instance.cfg）");
            }

            #endregion
        })
        {
            ProgressWeight = new FileInfo(FileAddress).Length / 1024d / 1024d / 6d,
            Block = false
        }); // 每 6M 需要 1s
        // 构造实例安装请求
        if (PackJson["components"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = InstanceName,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\"
        };
        foreach (var Component in PackJson["components"].AsArray())
            switch ((Component["uid"] ?? "").ToString() ?? "")
            {
                case "org.lwjgl":
                {
                    ModBase.Log("[ModPack] 已跳过 LWJGL 项");
                    break;
                }
                case "net.minecraft":
                {
                    Request.MinecraftName = (string)Component["version"];
                    break;
                }
                case "net.minecraftforge":
                {
                    if (Component["version"].ToString().StartsWithF("0."))
                        Request.CleanroomVersion = (string)Component["version"];
                    else
                        Request.ForgeVersion = (string)Component["version"];

                    break;
                }
                case "net.neoforged":
                {
                    Request.NeoForgeVersion = (string)Component["version"];
                    break;
                }
                case "net.fabricmc.fabric-loader":
                {
                    Request.FabricVersion = (string)Component["version"];
                    break;
                }
                case "org.quiltmc.quilt-loader":
                {
                    Request.QuiltVersion = (string)Component["version"];
                    break;
                }
            }

        if (PackInfo is not null)
            Request.MMCPackInfo = PackInfo;
        // 构造加载器
        var MergeLoaders = ModDownloadLib.McInstallLoader(Request);
        // 构造总加载器
        var Loaders = new List<LoaderBase>();
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                InstallLoaders)
            { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
        Loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), MergeLoaders)
            { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });

        // 重复任务检查
        var LoaderName = "MMC 整合包安装：" + InstanceName + " ";
        if (LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        // 启动
        var Loader = new LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
        Loader.Start(Request.TargetInstanceFolder);
        LoaderTaskbarAdd(Loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModBase.RunInUi(() => ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager));
        return Loader;
    }

    #endregion
}
