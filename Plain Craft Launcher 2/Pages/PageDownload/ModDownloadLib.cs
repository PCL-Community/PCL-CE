using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.IO.Net.Http;
using PCL.Core.App.Localization;

namespace PCL;

public static class ModDownloadLib
{
    /// <summary>
    ///     如果 OptiFine 与 Forge 同时开始安装，就会导致 Forge 安装失败。
    /// </summary>
    private static readonly object installSyncLock = new();

    /// <summary>
    ///     如果 OptiFine 与 Forge 同时复制原版 Jar，就会导致复制文件时冲突。
    /// </summary>
    private static readonly object vanillaSyncLock = new();

    #region Minecraft 下载

    /// <summary>
    ///     下载某个 Minecraft 实例，这会创造一个单独的下载任务，失败会跳过执行并要求反馈。
    ///     返回正在下载的任务，若跳过或失败，则返回 Nothing。
    /// </summary>
    /// <param name="Id">所下载的 Minecraft 的版本名。</param>
    /// <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
    public static ModLoader.LoaderCombo<string> McDownloadClient(NetPreDownloadBehaviour behaviour, string id,
        string jsonUrl = null)
    {
        try
        {
            var versionFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", id);

            // 重复任务检查
            foreach (var ongoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if (ongoingLoader.name != Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id))
                    continue;
                if (behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return (ModLoader.LoaderCombo<string>)ongoingLoader;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return (ModLoader.LoaderCombo<string>)ongoingLoader;
            }

            // 已有实例检查
            if (behaviour != NetPreDownloadBehaviour.IgnoreCheck && File.Exists(Path.Combine(versionFolder, id + ".json")) &&
                File.Exists(Path.Combine(versionFolder, id + ".jar")))
            {
                if (behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return null;
                if (ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", id, "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")
                    ) == 1)
                {
                    File.Delete(Path.Combine(versionFolder, id + ".jar"));
                    File.Delete(Path.Combine(versionFolder, id + ".json"));
                }
                else
                {
                    return null;
                }
            }

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id),
                        McDownloadClientLoader(id, jsonUrl))
                    { OnStateChanged = McInstallState };
            loader.Start(versionFolder);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
            return loader;
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
            return null;
        }
    }

    /// <summary>
    ///     保存某个 Minecraft 实例的核心文件（仅 Json 与核心 Jar）。
    /// </summary>
    /// <param name="Id">所下载的 Minecraft 的版本名。</param>
    /// <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
    public static void McDownloadClientCore(string Id, string JsonUrl, NetPreDownloadBehaviour Behaviour)
    {
        try
        {
            var versionFolder = SystemDialogs.SelectFolder();
            if (!versionFolder.Contains(@"\"))
                return;
            versionFolder = Path.Combine(versionFolder, Id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar)
            {
                if ((OngoingLoader.name ?? "") != (Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id) ?? ""))
                    continue;
                if (Behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 Json 文件
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), Path.Combine(versionFolder, Id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 获取支持库文件地址
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 0.5d, show = false });
            // 下载支持库文件
            loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), new List<DownloadFile>())
                    { ProgressWeight = 5d });

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(Id);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 Minecraft 版本的加载器列表。
    ///     它必须安装到 McFolderSelected，但是可以自定义版本名（不过自定义的实例名不会修改 Json 中的 id 项）。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadClientLoader(string id, string jsonUrl = null,
        string instanceName = null)
    {
        instanceName = instanceName ?? id;
        var instanceFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", instanceName);

        var loaders = new List<ModLoader.LoaderBase>();

        // 下载实例 Json 文件
        if (jsonUrl is null)
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainVanillaJsonUrl"), task =>
            {
                var jsonAddress = ModDownload.DlClientListGet(id)?.ToString();
                task.output = new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(jsonAddress), Path.Combine(instanceFolder, instanceName + ".json"))
                };
            })
            {
                ProgressWeight = 2d,
                show = false
            });
        loaders.Add(new LoaderDownload(mcDownloadClientJsonName,
            new List<DownloadFile>
            {
                new(ModDownload.DlSourceLauncherOrMetaGet(jsonUrl ?? ""), Path.Combine(instanceFolder, instanceName + ".json"),
                    new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
            }) { ProgressWeight = 3d });

        // 下载支持库文件
        var loadersLib = new List<ModLoader.LoaderBase>();
        loadersLib.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaLibraries.Side"), task =>
        {
            var jsonPath = Path.Combine(instanceFolder, instanceName + ".json");
            ModBase.WaitForFileReady(jsonPath);
            ModBase.Log("[Download] 开始分析原版支持库文件：" + instanceFolder);
            if (id == "1.16.5" && Config.Download.FixAuthLib) // 1.16.5 Authlib 修复
                try
                {
                    var json = ModBase.ReadFile(jsonPath);
                    json = json.Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                        .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                        .Replace("ad54da276bf59983d02d5ed16fc14541354c71fd", "bbd00ca33b052f73a6312254780fc580d2da3535")
                        .Replace("76328", "87662");
                    ModBase.WriteFile(jsonPath, json);
                }
                catch (Exception ex)
                {
                    ModBase.Log("[Download] 替换 Authlib 版本失败: " + ex.Message);
                }

            task.output = ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(instanceFolder));
        })
        {
            ProgressWeight = 1d,
            show = false
        });
        loadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadVanillaLibraries.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 13d, show = false });
        loaders.Add(new ModLoader.LoaderCombo<string>(mcDownloadClientLibName, loadersLib)
            { block = false, ProgressWeight = 14d });

        // 下载资源文件
        var loadersAssets = new List<ModLoader.LoaderBase>();
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"), task =>
        {
            ModBase.WaitForFileReady(Path.Combine(instanceFolder, instanceName + ".json"));
            try
            {
                var assetIndex = new ModMinecraft.McInstance(instanceFolder);
                task.output = new List<DownloadFile> { ModDownload.DlClientAssetIndexGet(assetIndex) };
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"), ex);
            }

            // 顺手添加 Json 项目
            try
            {
                var versionJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Path.Combine(instanceFolder, instanceName + ".json")));
                versionJson.Add("clientVersion", id);
                ModBase.WriteFile(Path.Combine(instanceFolder, instanceName + ".json"), versionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AddClientVersionFailed"), ex);
            }
        })
        {
            ProgressWeight = 1d,
            show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 3d, show = false });
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"), task =>
        {
            ModLoader.LoaderBase argprogressFeed = task;
            task.output =
                ModMinecraft.McAssetsFixList(new ModMinecraft.McInstance(instanceFolder), true, ref argprogressFeed);
            task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
        })
        {
            ProgressWeight = 0.01d,
            show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 14d, show = false });
        loaders.Add(
            new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                loadersAssets) { block = false, ProgressWeight = 18d });

        return loaders;
    }

    private static readonly string mcDownloadClientLibName = Lang.Text("Minecraft.Download.Stage.VanillaLibrariesDownload");
    private static readonly string mcDownloadClientJsonName = Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload");

    #endregion

    #region Minecraft 下载菜单

    public static MyListItem McDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 确定图标
        string logo = Entry["type"].ToString() switch
        {
            "release" => ModBase.pathImage + "Blocks/Grass.png",
            "snapshot" => ModBase.pathImage + "Blocks/CommandBlock.png",
            "pending" => ModBase.pathImage + "Blocks/CommandBlock.png",
            "special" => ModBase.pathImage + "Blocks/GoldBlock.png",
            _ => ModBase.pathImage + "Blocks/CobbleStone.png"
        };

        // 建立控件
        var formattedVersion = McFormatter.FormatVersion(Entry["id"].ToString()).Replace("_", " ");
        var newItem = new MyListItem
        {
            Logo = logo, SnapsToDevicePixels = true, Title = formattedVersion, Height = 42d,
            Type = MyListItem.CheckType.Clickable, Tag = Entry
        };
        if (Entry["lore"] is null)
        {
            if (formattedVersion != (string)Entry["id"])
                newItem.Info = Lang.Date(Entry["releaseTime"].ToObject<DateTime>(), "g") + " | " +
                               Entry["id"];
            else
                newItem.Info = Lang.Date(Entry["releaseTime"].ToObject<DateTime>(), "g");
        }
        else if (formattedVersion != (string)Entry["id"])
        {
            newItem.Info = Entry["lore"] + " | " + Entry["id"];
        }
        else
        {
            newItem.Info = Entry["lore"].ToString();
        }

        if (Entry["url"].ToString().Contains("unlisted-versions-of-minecraft"))
            newItem.Tags = Lang.Text("Download.Tag.Uvmc");
        newItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            newItem.ContentHandler = McDownloadSaveMenuBuild;
        else
            newItem.ContentHandler = McDownloadMenuBuild;
        // 结束
        return newItem;
    }

    private static void McDownloadSaveMenuBuild(object sender, EventArgs _)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (ss, ee) => McDownloadMenuLog(ss, (dynamic)ee);
        var btnServer = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonServer, ToolTip = Lang.Text("Download.Version.DownloadServer") };
        ToolTipService.SetPlacement(btnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnServer, 30d);
        ToolTipService.SetHorizontalOffset(btnServer, 2d);
        btnServer.Click += (ss, ee) => McDownloadMenuSaveServer(ss, (dynamic)ee);
        ((dynamic)sender).Buttons = new[] { btnServer, btnInfo };
    }

    private static void McDownloadMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (a, b) => McDownloadMenuSave(a, (dynamic)b); // dynamic!
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => McDownloadMenuLog(a, (dynamic)b); // dynamic!
        var btnServer = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonServer, ToolTip = Lang.Text("Download.Version.DownloadServer") };
        ToolTipService.SetPlacement(btnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnServer, 30d);
        ToolTipService.SetHorizontalOffset(btnServer, 2d);
        btnServer.Click += (a, b) => McDownloadMenuSaveServer(a, (dynamic)b); // dynamic!
        ((dynamic)sender).Buttons = new[] { btnSave, btnInfo, btnServer };
    }

    private static void McDownloadMenuLog(object sender, RoutedEventArgs e)
    {
        JsonNode version;
        if (((dynamic)sender).Tag is not null)
            version = (JsonNode)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (JsonNode)((dynamic)sender).Parent.Tag;
        else
            version = (JsonNode)((dynamic)sender).Parent.Parent.Tag;
        McUpdateLogShow(version);
    }

    private static void McDownloadMenuSaveServer(object sender, RoutedEventArgs e)
    {
        MyListItem version;
        if (sender is MyListItem)
            version = (MyListItem)sender;
        else if (((dynamic)sender).Parent is MyListItem)
            version = (MyListItem)((dynamic)sender).Parent;
        else
            version = (MyListItem)((dynamic)sender).Parent.Parent;
        try
        {
            var id = version.Title;
            string jsonUrl = ((dynamic)version.Tag)["url"].ToString();
            var versionFolder = SystemDialogs.SelectFolder();
            if (!versionFolder.Contains(@"\"))
                return;
            versionFolder = Path.Combine(versionFolder, id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.ServerDownloading"), ModMain.HintType.Critical);
                return;
            }

            var loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 JSON 文件
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(jsonUrl), Path.Combine(versionFolder, id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 构建服务端
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.BuildServer"), Task =>
            {
                // 分析服务端 JAR 文件下载地址
                var mcInstance = new ModMinecraft.McInstance(versionFolder);
                if (mcInstance.JsonObject["downloads"] is null ||
                    mcInstance.JsonObject["downloads"]["server"] is null ||
                    mcInstance.JsonObject["downloads"]["server"]["url"] is null)
                {
                    File.Delete(Path.Combine(versionFolder, id + ".json"));
                    if (!new DirectoryInfo(versionFolder).GetFileSystemInfos().Any())
                        Directory.Delete(versionFolder);
                    Task.output = new List<DownloadFile>();
                    ModMain.Hint(Lang.Text("Minecraft.Download.Error.NoOfficialServerDownload", id),
                        ModMain.HintType.Critical);
                    Thread.Sleep(2000); // 等玩家把上一个提示看完
                    Task.Abort();
                    return;
                }

                var jarUrl = (string)mcInstance.JsonObject["downloads"]["server"]["url"];
                var checker = new ModBase.FileChecker(1024L,
                    (long)(mcInstance.JsonObject["downloads"]["server"]["size"] ?? -1),
                    (string)mcInstance.JsonObject["downloads"]["server"]["sha1"]);
                Task.output = new List<DownloadFile>
                    { new(ModDownload.DlSourceLauncherOrMetaGet(jarUrl), Path.Combine(versionFolder, id + "-server.jar"), checker) };
                // 添加启动脚本
                var bat = $"""
                           @echo off
                           title {Lang.Text("Minecraft.Download.ServerBatch.Title", id)}
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionJavaPath")}
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionPclSettings")}
                           echo ------------------------------
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionEula")}
                           echo ------------------------------
                           "java" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar {id}-server.jar nogui
                           echo ----------------------
                           echo {Lang.Text("Minecraft.Download.ServerBatch.ServerStopped")}
                           pause
                           """;
                ModBase.WriteFile(Path.Combine(versionFolder, "Launch Server.bat"), bat.Replace("\n", "\r\n"),
                    Encoding: Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encoding.GetEncoding("GB18030"));
                // 删除实例 JSON
                File.Delete(Path.Combine(versionFolder, id + ".json"));
            })
            {
                ProgressWeight = 0.5d,
                show = false
            });
            // 下载服务端文件
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadServerFile"), [])
                { ProgressWeight = 5d });

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", id),
                        loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(id);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Minecraft 服务端下载失败", ModBase.LogLevel.Feedback);
        }
    }

    public static void McDownloadMenuSave(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        MyListItem version;
        if (element is MyListItem s1) version = s1;
        else if (element.Parent is MyListItem s2) version = s2;
        else version = (MyListItem)((FrameworkElement)element.Parent).Parent;
        try
        {
            var id = version.Title;
            var jsonUrl = ((JsonObject)version.Tag)["url"]!.ToString();
            var versionFolder = SystemDialogs.SelectFolder();
            if (!versionFolder.Contains(@"\"))
                return;
            versionFolder = Path.Combine(versionFolder, id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") != (Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 JSON 文件
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(jsonUrl), Path.Combine(versionFolder, id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 获取支持库文件地址
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    Task => Task.output = new List<DownloadFile>
                        { ModDownload.DlClientJarGet(new ModMinecraft.McInstance(versionFolder), false) })
                { ProgressWeight = 0.5d, show = false });
            // 下载支持库文件
            loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), new List<DownloadFile>())
                    { ProgressWeight = 5d });

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(id);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Minecraft 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     显示某 Minecraft 版本的更新日志。
    /// </summary>
    /// <param name="VersionJson">在 version_manifest.json 中的对应项。</param>
    public static void McUpdateLogShow(JsonNode VersionJson)
    {
        var wikiName = McFormatter.GetWikiUrlSuffix(VersionJson["id"].ToString());
        ModBase.OpenWebsite("https://zh.minecraft.wiki/w/Special:Search?search=" + wikiName);
    }

    #endregion

    #region OptiFine 下载

    public static void McDownloadOptiFine(ModDownload.DlOptiFineListEntry DownloadInfo)
    {
        try
        {
            var id = DownloadInfo.nameVersion;
            var versionFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", id);
            var isNewVersion = ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
            var target = isNewVersion
                ? Path.Combine(ModBase.pathTemp, "Cache", "Code", DownloadInfo.nameVersion + "_" + ModBase.GetUuid())
                : Path.Combine(ModMinecraft.mcFolderSelected, "libraries", "optifine", "OptiFine",
                    DownloadInfo.nameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", ""),
                    DownloadInfo.nameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", ""));

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar)
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.displayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 已有实例检查
            if (File.Exists(Path.Combine(versionFolder, id + ".json")))
            {
                if (ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", id, "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")
                    ) == 1)
                {
                    File.Delete(Path.Combine(versionFolder, id + ".jar"));
                    File.Delete(Path.Combine(versionFolder, id + ".json"));
                }
                else
                {
                    return;
                }
            }

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.displayName),
                    McDownloadOptiFineLoader(DownloadInfo)) { OnStateChanged = McInstallState };
            loader.Start(versionFolder);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 OptiFine 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void McDownloadOptiFineSave(ModDownload.DlOptiFineListEntry DownloadInfo)
    {
        try
        {
            var id = DownloadInfo.nameVersion;
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), DownloadInfo.nameFile, "OptiFine Jar (*.jar)|*.jar");
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.displayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var loader =
                new ModLoader.LoaderCombo<ModDownload.DlOptiFineListEntry>(
                        Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.displayName),
                        McDownloadOptiFineSaveLoader(DownloadInfo, target))
                    { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 OptiFine 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void McDownloadOptiFineInstall(string BaseMcFolderHome, string Target, ModLoader.LoaderTask<List<DownloadFile>, bool> Task, bool UseJavaWrapper)
    {
        // 选择 Java
        JavaEntry java;
        lock (ModJava.javaLock)
        {
            java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 0));
            if (java is null)
            {
                if (!ModJava.JavaDownloadConfirm(Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
                // 开始自动下载
                var javaLoader = ModJava.GetJavaDownloadLoader();
                try
                {
                    javaLoader.Start(17, true);
                    while (javaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                        Thread.Sleep(10);
                }
                finally
                {
                    javaLoader.Abort(); // 确保取消时中止 Java 下载
                }

                // 检查下载结果
                java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                    new Version(1, 8, 0, 0));
                if (Task.IsAborted)
                    return;
                if (java is null)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
            }
        }

        // 添加 Java Wrapper 作为主 Jar
        string arguments;
        if (UseJavaWrapper &&
                                  !(dynamic)Config.Launch.DisableJlw) // dynamic!
            arguments =
                $"-Doolloo.jlw.tmpdir=\"{ModBase.pathPure.TrimEnd('\\')}\" -Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" -jar \"{ModLaunch.ExtractJavaWrapper()}\" optifine.Installer";
        else
            arguments = $"-Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" optifine.Installer";
        if (java.Installation.MajorVersion >= 9)
            arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + arguments;
        // 开始启动
        lock (installSyncLock)
        {
            var info = new ProcessStartInfo
            {
                FileName = java.Installation.JavaExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = ModBase.ShortenPath(BaseMcFolderHome)
            };
            if (info.EnvironmentVariables.ContainsKey("appdata"))
                info.EnvironmentVariables["appdata"] = BaseMcFolderHome;
            else
                info.EnvironmentVariables.Add("appdata", BaseMcFolderHome);
            ModBase.Log("[Download] 开始安装 OptiFine：" + Target);
            var totalLength = 0;
            var process = new Process { StartInfo = info };
            var lastResult = "";
            using (var outputWaitHandle = new AutoResetEvent(false))
            {
                using (var errorWaitHandle = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        try
                        {
                            if (e.Data is null)
                            {
                                outputWaitHandle.Set();
                            }
                            else
                            {
                                lastResult = e.Data;
                                if (ModBase.modeDebug)
                                    ModBase.Log("[Installer] " + lastResult);
                                totalLength += 1;
                                Task.Progress += 0.9d / 7000d;
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "读取 OptiFine 安装器信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log("[Installer] 由于任务取消，已中止 OptiFine 安装");
                                process.Kill();
                            }
                        }
                        catch
                        {
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        try
                        {
                            if (e.Data is null)
                            {
                                errorWaitHandle.Set();
                            }
                            else
                            {
                                lastResult = e.Data;
                                if (ModBase.modeDebug)
                                    ModBase.Log("[Installer] " + lastResult);
                                totalLength += 1;
                                Task.Progress += 0.9d / 7000d;
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "读取 OptiFine 安装器错误信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log("[Installer] 由于任务取消，已中止 OptiFine 安装");
                                process.Kill();
                            }
                        }
                        catch
                        {
                        }
                    };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    // 等待
                    while (!process.HasExited)
                        Thread.Sleep(10);
                    // 输出
                    outputWaitHandle.WaitOne(10000);
                    errorWaitHandle.WaitOne(10000);
                    process.Dispose();
                    if (totalLength < 1000 || lastResult.Contains("at "))
                        throw new Exception(Lang.Text("Minecraft.Download.Error.InstallerFailedLastLine", lastResult));
                }
            }
        }
    }

    /// <summary>
    ///     获取下载某个 OptiFine 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadOptiFineLoader(ModDownload.DlOptiFineListEntry DownloadInfo,
        string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, string ClientFolder = null,
        bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = DownloadInfo.nameVersion;
        var versionFolder = Path.Combine(McFolder, "versions", id);
        var isNewVersion = DownloadInfo.Inherit.Contains("w") || ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
        var target = isNewVersion
            ? $"{ModMain.RequestTaskTempFolder()}OptiFine.jar"
            : $@"{McFolder}libraries\optifine\OptiFine\{DownloadInfo.nameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "")}\{DownloadInfo.nameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")}";
        var loaders = new List<ModLoader.LoaderBase>();

        // 获取下载地址
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainOptiFineUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (ClientDownloadLoader is null)
            {
                if (isCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                    DownloadInfo.Inherit);
            }

            Task.Progress = 0.1d;
            var sources = new List<string>();
            // BMCLAPI 源
            var bmclapiInherit = DownloadInfo.Inherit;
            if (bmclapiInherit == "1.8" || bmclapiInherit == "1.9")
                bmclapiInherit += ".0"; // #4281
            if (DownloadInfo.isPreview)
                sources.Add("https://bmclapi2.bangbang93.com/optifine/" + bmclapiInherit + "/HD_U_" +
                            DownloadInfo.displayName.Replace(DownloadInfo.Inherit + " ", "").Replace(" ", "/"));
            else
                sources.Add("https://bmclapi2.bangbang93.com/optifine/" + bmclapiInherit + "/HD_U/" +
                            DownloadInfo.displayName.Replace(DownloadInfo.Inherit + " ", ""));
            // 官方源
            string pageData;
            try
            {
                using (var resp = HttpRequest
                           .Create("https://optifine.net/adloadx?f=" + DownloadInfo.nameFile)
                           .WithHeader("Accept", "text/html")
                           .WithHeader("Accept-Language", "en-US,en;q=0.5")
                           .WithHeader("X-Requested-With", "XMLHttpRequest")
                           .SendAsync()
                           .GetAwaiter()
                           .GetResult())
                {
                    resp.EnsureSuccessStatusCode();
                    pageData = resp.AsString();
                }
                Task.Progress = 0.8d;
                sources.Add("https://optifine.net/" + pageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                ModBase.Log("[Download] OptiFine " + DownloadInfo.displayName + " 官方下载地址：" + sources.Last());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 OptiFine " + DownloadInfo.displayName + " 官方下载地址失败");
            }

            // 构造文件请求
            Task.output = new List<DownloadFile>
                { new(sources.ToArray(), target, new ModBase.FileChecker(300 * 1024)) };
        })
        {
            ProgressWeight = 8d
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"), [])
            { ProgressWeight = 8d });
        loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
            Lang.Text("Minecraft.Download.Stage.WaitVanillaDownload"), Task =>
        {
            // 等待原版文件下载完成
            if (ClientDownloadLoader is null)
                return;
            var targetLoaders = ClientDownloadLoader.GetLoaderList()
                .Where(l => (l.name ?? "") == mcDownloadClientLibName || (l.name ?? "") == mcDownloadClientJsonName)
                .Where(l => l.State != ModBase.LoadState.Finished).ToList();
            if (targetLoaders.Any())
                ModBase.Log("[Download] OptiFine 安装正在等待原版文件下载完成");
            while (targetLoaders.Any() && !Task.IsAborted)
            {
                targetLoaders = targetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                Thread.Sleep(50);
            }

            if (Task.IsAborted)
                return;
            // 拷贝原版文件
            if (!isCustomFolder)
                return;
            lock (vanillaSyncLock)
            {
                var clientName = ModBase.GetFolderNameFromPath(ClientFolder);
                Directory.CreateDirectory(Path.Combine(McFolder, "versions", DownloadInfo.Inherit));
                if (!File.Exists(Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json")))
                    ModBase.CopyFile($"{ClientFolder}{clientName}.json",
                        $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.json");
                if (!File.Exists(Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar")))
                    ModBase.CopyFile($"{ClientFolder}{clientName}.jar",
                        $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.jar");
            }
        })
        {
            ProgressWeight = 0.1d,
            show = false
        });

        // 安装（新旧方式均需要原版 Jar 和 Json）
        if (isNewVersion)
        {
            ModBase.Log("[Download] 检测为新版 OptiFine：" + DownloadInfo.Inherit);
            loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodA"), Task =>
            {
                var baseMcFolderHome = ModMain.RequestTaskTempFolder();
                var baseMcFolder = Path.Combine(baseMcFolderHome, ".minecraft");
                try
                {
                    // 准备安装环境
                    if (Directory.Exists(Path.Combine(baseMcFolder, "versions", DownloadInfo.Inherit)))
                        ModBase.DeleteDirectory(Path.Combine(baseMcFolder, "versions", DownloadInfo.Inherit));
                    Directory.CreateDirectory(Path.Combine(baseMcFolder, "versions", DownloadInfo.Inherit));
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(baseMcFolder);
                    ModBase.CopyFile(
                        Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json"),
                        Path.Combine(baseMcFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json"));
                    ModBase.CopyFile(
                        Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"),
                        Path.Combine(baseMcFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"));
                    Task.Progress = 0.06d;
                    // 进行安装
                    var useJavaWrapper = ModBase.IsUtf8CodePage();
                    Retry: ;

                    try
                    {
                        McDownloadOptiFineInstall(baseMcFolderHome, target, Task, useJavaWrapper);
                    }
                    catch (Exception ex)
                    {
                        if (!useJavaWrapper)
                        {
                            ModBase.Log(ex, "不使用 JavaWrapper 安装 OptiFine 失败，将使用 JavaWrapper 并重试");
                            useJavaWrapper = true;
                            goto Retry;
                        }

                        throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineInstallerRunFailed"), ex);
                    }

                    Task.Progress = 0.96d;
                    // 复制文件
                    File.Delete(Path.Combine(baseMcFolder, "launcher_profiles.json"));
                    ModBase.CopyDirectory(baseMcFolder, McFolder);
                    Task.Progress = 0.98d;
                    // 清理文件
                    File.Delete(target);
                    ModBase.DeleteDirectory(baseMcFolderHome);
                }
                catch (Exception ex)
                {
                    throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineInstallFailed.MethodA"), ex);
                }
            })
            {
                ProgressWeight = 8d
            });
        }
        else
        {
            ModBase.Log("[Download] 检测为旧版 OptiFine：" + DownloadInfo.Inherit);
            // 新建实例文件夹
            // 复制 Jar 文件
            // 建立 Json 文件
            loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodB"), Task =>
                {
                    try
                    {
                        Directory.CreateDirectory(versionFolder);
                        Task.Progress = 0.1d;
                        if (File.Exists(Path.Combine(versionFolder, id + ".jar"))) File.Delete(Path.Combine(versionFolder, id + ".jar"));
                        ModBase.CopyFile(
                            Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"),
                            Path.Combine(versionFolder, id + ".jar"));
                        Task.Progress = 0.7d;
                        var inheritInstance =
                            new ModMinecraft.McInstance(Path.Combine(McFolder, "versions", DownloadInfo.Inherit));
                        var json = @"{
    ""id"": """ + id + @""",
    ""inheritsFrom"": """ + DownloadInfo.Inherit + @""",
    ""time"": """ +
                                   (string.IsNullOrEmpty(DownloadInfo.releaseTime)
                                       ? inheritInstance.releaseTime.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)
                                       : DownloadInfo.releaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""releaseTime"": """ +
                                   (string.IsNullOrEmpty(DownloadInfo.releaseTime)
                                       ? inheritInstance.releaseTime.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)
                                       : DownloadInfo.releaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""type"": ""release"",
    ""libraries"": [
        {""name"": ""optifine:OptiFine:" +
                                   DownloadInfo.nameFile.Replace("OptiFine_", "").Replace(".jar", "")
                                       .Replace("preview_", "") + // 输出旧版 Json 格式
                                   @"""},
        {""name"": ""net.minecraft:launchwrapper:1.12""}
    ],
    ""mainClass"": ""net.minecraft.launchwrapper.Launch"",";
                        Task.Progress = 0.8d;
                        if (inheritInstance.IsOldJson)
                            json += @"
    ""minimumLauncherVersion"": 18,
    ""minecraftArguments"": """ + inheritInstance.JsonObject["minecraftArguments"] + // 输出新版 Json 格式
                                    @"  --tweakClass optifine.OptiFineTweaker""
}";
                        else
                            json += @"
    ""minimumLauncherVersion"": ""21"",
    ""arguments"": {
        ""game"": [
            ""--tweakClass"",
            ""optifine.OptiFineTweaker""
        ]
    }
}";
                        ModBase.WriteFile(Path.Combine(versionFolder, id + ".json"), json);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineInstallFailed.MethodB"), ex);
                    }
                })
                { ProgressWeight = 1d });
        }

        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeOptiFineLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineLibraries"),
                    new List<DownloadFile>())
                { ProgressWeight = 4d });
        }

        return loaders;
    }

    /// <summary>
    ///     获取保存某个 OptiFine 版本的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadOptiFineSaveLoader(ModDownload.DlOptiFineListEntry downloadInfo,
        string targetFolder)
    {
        var loaders = new List<ModLoader.LoaderBase>();
        // 获取下载地址
        loaders.Add(new ModLoader.LoaderTask<ModDownload.DlOptiFineListEntry, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainOptiFineDownloadUrl"),
            Task =>
            {
                var sources = new List<string>();
                // BMCLAPI 源
                var bmclapiInherit = downloadInfo.Inherit;
                if (bmclapiInherit == "1.8" || bmclapiInherit == "1.9")
                    bmclapiInherit += ".0"; // #4281
                if (downloadInfo.isPreview)
                    sources.Add("https://bmclapi2.bangbang93.com/optifine/" + bmclapiInherit + "/HD_U_" +
                                downloadInfo.displayName.Replace(downloadInfo.Inherit + " ", "").Replace(" ", "/"));
                else
                    sources.Add("https://bmclapi2.bangbang93.com/optifine/" + bmclapiInherit + "/HD_U/" +
                                downloadInfo.displayName.Replace(downloadInfo.Inherit + " ", ""));
                // 官方源
                string pageData;
                try
                {
                    using (var resp = HttpRequest
                            .Create("https://optifine.net/adloadx?f=" + downloadInfo.nameFile)
                            .WithHeader("Accept", "text/html")
                            .WithHeader("Accept-Language", "en-US,en;q=0.5")
                            .WithHeader("X-Requested-With", "XMLHttpRequest")
                            .SendAsync().GetAwaiter().GetResult())
                    {
                        resp.EnsureSuccessStatusCode();
                        pageData = resp.AsString();
                    }
                    Task.Progress = 0.8d;
                    sources.Add("https://optifine.net/" + pageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                    ModBase.Log("[Download] OptiFine " + downloadInfo.displayName + " 官方下载地址：" + sources.Last());
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取 OptiFine " + downloadInfo.displayName + " 官方下载地址失败");
                }

                Task.Progress = 0.9d;
                // 构造文件请求
                Task.output = new List<DownloadFile>
                    { new(sources.ToArray(), targetFolder, new ModBase.FileChecker(64 * 1024)) };
            })
        {
            ProgressWeight = 6d
        });
        // 下载
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"),
                new List<DownloadFile>())
            { ProgressWeight = 10d, block = true });
        return loaders;
    }

    #endregion

    #region OptiFine 下载菜单

    public static MyListItem OptiFineDownloadListItem(ModDownload.DlOptiFineListEntry Entry,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        var infoParts = new List<string>
        {
            Entry.isPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Release")
        };

        if (!string.IsNullOrEmpty(Entry.releaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.releaseTime));

        if (Entry.requiredForgeVersion is null)
            infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(Entry.requiredForgeVersion))
            infoParts.Add(Lang.Text("Download.Version.Optifine.CompatibleForge", Entry.requiredForgeVersion));

        var newItem = new MyListItem
        {
            Title = Entry.displayName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.pathImage + "Blocks/GrassPath.png"
        };

        newItem.Click += OnClick;
        // 建立菜单
        newItem.ContentHandler = IsSaveOnly
            ? OptiFineSaveContMenuBuild
            : OptiFineContMenuBuild;
        // 结束
        return newItem;
    }

    private static void OptiFineSaveContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[] { btnInfo };
    }

    private static void OptiFineContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        //btnSave.Click += () ModDownloadLib.OptiFineSave_Click;
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[] { btnSave, btnInfo };
    }

    private static void OptiFineLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlOptiFineListEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite("https://optifine.net/changelog?f=" + version.nameFile);
    }

    public static void OptiFineSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlOptiFineListEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadOptiFineSave(version);
    }

    #endregion

    #region LiteLoader 下载

    public static void McDownloadLiteLoader(ModDownload.DlLiteLoaderListEntry DownloadInfo)
    {
        try
        {
            var id = DownloadInfo.inherit;
            var target = Path.Combine(ModBase.pathTemp, "Download", id + "-Liteloader.jar");
            var versionName = DownloadInfo.inherit + "-LiteLoader";
            var versionFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", versionName);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") != (Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 已有实例检查
            if (File.Exists(Path.Combine(versionFolder, versionName + ".json")))
            {
                if (ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", versionName, "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")
                    ) == 1)
                {
                    File.Delete(Path.Combine(versionFolder, versionName + ".jar"));
                    File.Delete(Path.Combine(versionFolder, versionName + ".json"));
                }
                else
                {
                    return;
                }
            }

            // 启动
            var loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", id),
                        McDownloadLiteLoaderLoader(DownloadInfo))
                    { OnStateChanged = McInstallState };
            loader.Start(versionFolder);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 LiteLoader 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void McDownloadLiteLoaderSave(ModDownload.DlLiteLoaderListEntry DownloadInfo)
    {
        try
        {
            var id = DownloadInfo.inherit;
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), DownloadInfo.fileName.Replace("-SNAPSHOT", ""),
                Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", "LiteLoader", "jar"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") != (Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var address = new List<string>();
            if (DownloadInfo.isLegacy)
                // 老版本
                switch (DownloadInfo.inherit ?? "")
                {
                    case "1.7.10":
                    {
                        address.Add("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar");
                        break;
                    }
                    case "1.7.2":
                    {
                        address.Add("https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar");
                        break;
                    }
                    case "1.6.4":
                    {
                        address.Add("https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar");
                        break;
                    }
                    case "1.6.2":
                    {
                        address.Add("https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar");
                        break;
                    }
                    case "1.5.2":
                    {
                        address.Add("https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar");
                        break;
                    }

                    default:
                    {
                        throw new NotSupportedException(Lang.Text("Minecraft.Download.Error.UnknownMinecraftVersion",
                            DownloadInfo.inherit));
                    }
                }
            else
                // 官方源
                address.Add("http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" + DownloadInfo.inherit +
                            "/lastSuccessfulBuild/artifact/" +
                            (DownloadInfo.inherit == "1.8" ? "ant/dist/" : "build/libs/") + DownloadInfo.fileName);

            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 1024)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<ModDownload.DlLiteLoaderListEntry>(
                        Lang.Text("Minecraft.Download.Stage.LiteLoaderInstallerDownload", id), loaders)
                    { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 LiteLoader 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 LiteLoader 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadLiteLoaderLoader(ModDownload.DlLiteLoaderListEntry DownloadInfo,
        string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = DownloadInfo.inherit;
        var target = Path.Combine(ModBase.pathTemp, "Download", id + "-Liteloader.jar");
        var versionName = DownloadInfo.inherit + "-LiteLoader";
        var versionFolder = Path.Combine(McFolder, "versions", versionName);
        var loaders = new List<ModLoader.LoaderBase>();

        // 启动依赖实例的下载
        if (ClientDownloadLoader is null)
            loaders.Add(new ModLoader.LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Stage.StartLiteLoaderDependencyDownload"), _ =>
            {
                if (isCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                    DownloadInfo.inherit);
            })
            {
                ProgressWeight = 0.2d,
                show = false,
                block = false
            });
        // 安装
        // 新建实例文件夹
        // 构造实例 Json
        // 输出 Json 文件
        loaders.Add(new ModLoader.LoaderTask<string, string>(Lang.Text("Minecraft.Download.Stage.InstallLiteLoader"),
            _ =>
        {
            try
            {
                Directory.CreateDirectory(versionFolder);
                var versionJson = new JsonObject();
                versionJson.Add("id", versionName);
                versionJson.Add("time",
                    DateTime.ParseExact(DownloadInfo.releaseTime, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
                versionJson.Add("releaseTime",
                    DateTime.ParseExact(DownloadInfo.releaseTime, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
                versionJson.Add("type", "release");
                versionJson.Add("arguments",
                    (JsonNode)ModBase.GetJson("{\"game\":[\"--tweakClass\",\"" + DownloadInfo.jsonToken["tweakClass"] +
                                            "\"]}"));
                versionJson.Add("libraries", DownloadInfo.jsonToken["libraries"]?.DeepClone());
                versionJson["libraries"].AsArray().Add(ModBase.GetJson("{\"name\": \"com.mumfrey:liteloader:" +
                                                                            DownloadInfo.jsonToken["version"] +
                                                                            "\",\"url\": \"https://dl.liteloader.com/versions/\"}"));
                versionJson.Add("mainClass", "net.minecraft.launchwrapper.Launch");
                versionJson.Add("minimumLauncherVersion", 18);
                versionJson.Add("inheritsFrom", DownloadInfo.inherit);
                versionJson.Add("jar", DownloadInfo.inherit);
                ModBase.WriteFile(Path.Combine(versionFolder, versionName + ".json"), versionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.LiteLoaderInstallFailed"), ex);
            }
        }) { ProgressWeight = 1d });
        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLiteLoaderLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLiteLoaderLibraries"),
                    new List<DownloadFile>())
                { ProgressWeight = 6d });
        }

        return loaders;
    }

    #endregion

    #region LiteLoader 下载菜单

    public static MyListItem LiteLoaderDownloadListItem(ModDownload.DlLiteLoaderListEntry Entry,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        var infoParts = new List<string>
        {
            Entry.isPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable")
        };

        if (!string.IsNullOrEmpty(Entry.releaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.releaseTime));

        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.inherit,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.pathImage + "Blocks/Egg.png"
        };

        newItem.Click += OnClick;
        // 建立菜单
        newItem.ContentHandler = IsSaveOnly
            ? LiteLoaderSaveContMenuBuild
            : LiteLoaderContMenuBuild;
        // 结束
        return newItem;
    }

    private static void LiteLoaderSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        if ((bool)((dynamic)sender.Tag).IsLegacy)
        {
            sender.Buttons = Array.Empty<MyIconButton>();
        }
        else
        {
            var btnList = new MyIconButton { Logo = Icon.IconButtonList, ToolTip = Lang.Text("Download.Version.ViewAllVersions"), Tag = sender };
            ToolTipService.SetPlacement(btnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnList, 30d);
            ToolTipService.SetHorizontalOffset(btnList, 2d);
            btnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = new[] { btnList };
        }
    }

    private static void LiteLoaderContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveInstaller"), Tag = sender };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (sender, e) => LiteLoaderSave_Click(sender, (RoutedEventArgs)e);
        if ((bool)((dynamic)sender.Tag).IsLegacy)
        {
            sender.Buttons = [btnSave];
        }
        else
        {
            var btnList = new MyIconButton { Logo = Icon.IconButtonList, ToolTip = Lang.Text("Download.Version.ViewAllVersions"), Tag = sender };
            ToolTipService.SetPlacement(btnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnList, 30d);
            ToolTipService.SetHorizontalOffset(btnList, 2d);
            btnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = [btnSave, btnList];
        }
    }

    private static void LiteLoaderAll_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlLiteLoaderListEntry version;
        if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        ModBase.OpenWebsite("https://jenkins.liteloader.com/view/" + version.inherit);
    }

    public static void LiteLoaderSave_Click(object sender, RoutedEventArgs e)
    {
        // ListItem 与小按钮都会调用这个方法
        ModDownload.DlLiteLoaderListEntry version;
        if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        McDownloadLiteLoaderSave(version);
    }

    #endregion

    #region Forgelike 下载

    public static void McDownloadForgelikeSave(ModDownload.DlForgelikeEntry Info)
    {
        try
        {
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"),
                $"{Info.LoaderName}-{Info.inherit}-{Info.versionName}.{Info.FileExtension}",
                Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", Info.LoaderName, Info.FileExtension));
            var displayName = $"{Info.LoaderName} {Info.inherit} - {Info.versionName}";
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", displayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 获取下载地址
            var files = new List<DownloadFile>();
            if (Info.forgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge)
            {
                // NeoForge
                var neo = (ModDownload.DlNeoForgeListEntry)Info;
                var url = neo.UrlBase + "-installer.jar";
                files.Add(new DownloadFile(
                    new[] { url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), url }, target,
                    new ModBase.FileChecker(64 * 1024)));
            }
            else if (Info.forgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom)
            {
                // Cleanroom
                var clr = (ModDownload.DlCleanroomListEntry)Info;
                var url = clr.UrlBase + "-installer.jar";
                files.Add(new DownloadFile(new[] { url }, target, new ModBase.FileChecker(64 * 1024)));
            }
            else
            {
                // Forge
                var forge = (ModDownload.DlForgeVersionEntry)Info;
                files.Add(new DownloadFile(
                    new[]
                    {
                        $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{forge.inherit}-{forge.fileVersion}/forge-{forge.inherit}-{forge.fileVersion}-{forge.category}.{forge.FileExtension}",
                        $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{forge.inherit}-{forge.fileVersion}/forge-{forge.inherit}-{forge.fileVersion}-{forge.category}.{forge.FileExtension}"
                    }, target, new ModBase.FileChecker(64 * 1024, Hash: forge.hash)));
            }

            // 构造加载器
            var loaders = new List<ModLoader.LoaderBase>();
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"), files)
                { ProgressWeight = 6d });

            // 启动
            var loader =
                new ModLoader.LoaderCombo<ModDownload.DlForgelikeEntry>(
                        Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", displayName), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(Info);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, $"开始 {Info.LoaderName} 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void ForgelikeInjector(string Target, ModLoader.LoaderTask<bool, bool> Task, string McFolder,
        bool UseJavaWrapper, ModDownload.DlForgelikeEntry.ForgelikeType ForgeType)
    {
        // 选择 Java
        JavaEntry java;
        lock (ModJava.javaLock)
        {
            java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (java is null)
            {
                if (!ModJava.JavaDownloadConfirm(Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
                // 开始自动下载
                var javaLoader = ModJava.GetJavaDownloadLoader();
                try
                {
                    javaLoader.Start(17, true);
                    while (javaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                        Thread.Sleep(10);
                }
                finally
                {
                    javaLoader.Abort(); // 确保取消时中止 Java 下载
                }

                // 检查下载结果
                java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                    new Version(1, 8, 0, 60));
                if (Task.IsAborted)
                    return;
                if (java is null)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
            }
        }

        // 添加 Java Wrapper 作为主 Jar
        string arguments;
        if (UseJavaWrapper && !Config.Launch.DisableJlw)
            arguments =
                $@"-Doolloo.jlw.tmpdir=""{ModBase.pathPure.TrimEnd('\\')}"" -cp ""{ModBase.pathTemp}Cache\forge_installer.jar;{Target}"" -jar ""{ModLaunch.ExtractJavaWrapper()}"" com.bangbang93.ForgeInstaller ""{McFolder}";
        else
            arguments =
                $@"-cp ""{ModBase.pathTemp}Cache\forge_installer.jar;{Target}"" com.bangbang93.ForgeInstaller ""{McFolder}";
        if (java.Installation.MajorVersion >= 9)
            arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + arguments;
        // 开始启动
        lock (installSyncLock)
        {
            var info = new ProcessStartInfo
            {
                FileName = java.Installation.JavaExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            string loaderName = ModBase.GetStringFromEnum(ForgeType);
            ModBase.Log($"[Download] 开始安装 {loaderName}：" + arguments);
            var process = new Process { StartInfo = info };
            var lastResults = new Queue<string>();
            using (var outputWaitHandle = new AutoResetEvent(false))
            {
                using (var errorWaitHandle = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        try
                        {
                            if (e.Data is null)
                            {
                                outputWaitHandle.Set();
                            }
                            else
                            {
                                lastResults.Enqueue(e.Data);
                                if (lastResults.Count > 100)
                                    lastResults.Dequeue();
                                ForgelikeInjectorLine(e.Data, Task);
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"读取 {loaderName} 安装器信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log($"[Installer] 由于任务取消，已中止 {loaderName} 安装");
                                process.Kill();
                            }
                        }
                        catch
                        {
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        try
                        {
                            if (e.Data is null)
                            {
                                errorWaitHandle.Set();
                            }
                            else
                            {
                                lastResults.Enqueue(e.Data);
                                if (lastResults.Count > 100)
                                    lastResults.Dequeue();
                                ForgelikeInjectorLine(e.Data, Task);
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"读取 {loaderName} 安装器错误信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log($"[Installer] 由于任务取消，已中止 {loaderName} 安装");
                                process.Kill();
                            }
                        }
                        catch
                        {
                        }
                    };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    // 等待
                    while (!process.HasExited)
                        Thread.Sleep(10);
                    // 输出
                    outputWaitHandle.WaitOne(10000);
                    errorWaitHandle.WaitOne(10000);
                    process.Dispose();
                    // 检查是否安装成功：最后 5 行中是否有 true（true 可能在倒数数行，见 #832）
                    if (lastResults.Reverse().Take(5).Any(l => l == "true"))
                        return;
                    ModBase.Log(lastResults.Join("\r\n"));
                    var lastLines = "";
                    for (int i = Math.Max(0, lastResults.Count - 5), loopTo = lastResults.Count - 1;
                         i <= loopTo;
                         i++) // 最后 5 行
                        lastLines += "\r\n" + lastResults.ElementAtOrDefault(i);
                    throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderInstallerFailedLastLine", loaderName,
                        lastLines));
                }
            }
        }
    }

    private static void ForgelikeInjectorLine(string Content, ModLoader.LoaderTask<bool, bool> Task)
    {
        switch (Content ?? "")
        {
            case "Extracting json":
            {
                ModBase.Log("[Installer] " + Content);
                Task.Progress = 0.07d;
                break;
            }
            case "Downloading libraries":
            {
                ModBase.Log("[Installer] " + Content);
                Task.Progress = 0.08d;
                break;
            }
            case "  File exists: Checksum validated.":
            {
                if (ModBase.modeDebug)
                    ModBase.Log("[Installer] " + Content);
                Task.Progress += 0.003d;
                break;
            }
            case "Building Processors":
            {
                Task.Progress = 0.18d;
                break;
            }
            case "Task: DOWNLOAD_MOJMAPS": // B
            {
                Task.Progress = 0.2d;
                break;
            }
            case "Task: MERGE_MAPPING": // B
            {
                Task.Progress = 0.3d;
                break;
            }
            case "Splitting: ":
            {
                Task.Progress = 0.35d;
                break;
            }
            case "Parameter Annotations": // B
            {
                Task.Progress = 0.4d;
                break;
            }
            case "Processing Complete": // B
            {
                Task.Progress = 0.5d;
                break;
            }
            case "log: null": // new
            {
                Task.Progress = 0.5d;
                break;
            }
            case "Sorting": // new
            {
                Task.Progress = 0.65d;
                break;
            }
            case "Remapping final jar": // A
            {
                Task.Progress = 0.72d;
                break;
            }
            case "Remapping jar... 50%": // A
            {
                Task.Progress = 0.76d;
                break;
            }
            case "Remapping jar... 100%": // A
            {
                Task.Progress = 0.81d;
                break;
            }
            case "Injecting profile":
            {
                Task.Progress = 0.91d;
                break;
            }

            default:
            {
                if (ModBase.modeDebug)
                    ModBase.Log("[Installer] " + Content);
                return;
            }
        }

        ModBase.Log("[Installer] " + Content);
    }

    /// <summary>
    ///     获取下载某个 Forgelike 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType ForgeType, string LoaderVersion,
        string TargetVersion, string Inherit, ModDownload.DlForgelikeEntry Info = null, string McFolder = null, ModLoader.LoaderCombo<string> ClientDownloadLoader = null, string ClientFolder = null)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        if (ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge && Info is null)
        {
            // 需要传入 API Name，但整合包版本可能不以 1.20.1- 开头，所以需要进行特别处理
            if (Inherit == "1.20.1" && !LoaderVersion.StartsWithF("1.20.1-"))
                Info = new ModDownload.DlNeoForgeListEntry("1.20.1-" + LoaderVersion);
            else
                Info = new ModDownload.DlNeoForgeListEntry(LoaderVersion);
        }

        if (ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom && Info is null) Info = new ModDownload.DlCleanroomListEntry(LoaderVersion);
        if (ForgeType != ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge && LoaderVersion.StartsWithF("1.") && LoaderVersion.Contains("-"))
        {
            // 类似 1.19.3-41.2.8 格式，优先使用 Version 中要求的版本而非 Inherit（例如 1.19.3 却使用了 1.19 的 Forge）
            Inherit = LoaderVersion.BeforeFirst("-");
            LoaderVersion = LoaderVersion.AfterLast("-");
        }

        string loaderName = ModBase.GetStringFromEnum(ForgeType);
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var installerAddress = ModMain.RequestTaskTempFolder() + "forge_installer.jar";
        var versionFolder = $@"{McFolder}versions\{TargetVersion}\";
        var displayName = $"{loaderName} {Inherit} - {LoaderVersion}";
        var loaders = new List<ModLoader.LoaderBase>();
        var libVersionFolder = $@"{ModMinecraft.mcFolderSelected}versions\{TargetVersion}\"; // 作为 Lib 文件目标的实例文件夹

        // 获取 Forge 下载信息
        if (Info is null)
            loaders.Add(new ModLoader.LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Stage.ObtainLoaderDetails", loaderName), Task =>
            {
                // 获取 Forge 对应 MC 版本列表
                var forgeLoader =
                    new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>(
                        "McDownloadForgeLoader " + Inherit, ModDownload.DlForgeVersionMain);
                forgeLoader.WaitForExit(Inherit);
                Task.Progress = 0.8d;
                // 查找对应版本
                foreach (var ForgeVersion in forgeLoader.output)
                    if (ModMinecraft.CompareVersion(ForgeVersion.version.ToString(), LoaderVersion) == 0)
                    {
                        Info = ForgeVersion;
                        return;
                    }

                throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderDetailsNotFound", loaderName, Inherit,
                    LoaderVersion));
            })
            {
                ProgressWeight = 3d
            });
        // 下载 Forgelike 主文件
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.PrepareLoaderDownload", loaderName), Task =>
        {
            // 启动依赖实例的下载
            if (ClientDownloadLoader is null)
            {
                if (isCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader =
                    McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, Inherit);
            }

            // 添加主文件下载
            var files = new List<DownloadFile>();
            if (Info.forgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge)
            {
                // NeoForge
                var neo = (ModDownload.DlNeoForgeListEntry)Info;
                var url = neo.UrlBase + "-installer.jar";
                files.Add(new DownloadFile(
                    new[] { url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), url },
                    installerAddress, new ModBase.FileChecker(64 * 1024)));
            }
            else if (Info.forgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom)
            {
                // Cleanroom
                var clr = (ModDownload.DlCleanroomListEntry)Info;
                var url = clr.UrlBase + "-installer.jar";
                files.Add(new DownloadFile(new[] { url }, installerAddress, new ModBase.FileChecker(64 * 1024)));
            }
            else
            {
                // Forge
                var forge = (ModDownload.DlForgeVersionEntry)Info;
                var fileName =
                    $"{forge.inherit.Replace("-", "_")}-{forge.fileVersion}/forge-{forge.inherit.Replace("-", "_")}-{forge.fileVersion}-{forge.category}.{forge.FileExtension}";
                files.Add(new DownloadFile(
                    new[]
                    {
                        $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                        $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{fileName}"
                    }, installerAddress, new ModBase.FileChecker(64 * 1024, Hash: forge.hash)));
            }

            Task.output = files;
        })
        {
            ProgressWeight = 0.5d,
            show = false
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", loaderName),
                new List<DownloadFile>())
            { ProgressWeight = 9d });

        // 安装（仅在新版安装时需要原版 Jar）
        if (ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge || Convert.ToDouble(LoaderVersion.BeforeFirst(".")) >= 20d)
        {
            ModBase.Log($"[Download] 检测为{(ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge ? "新版 Forge" : " " + ForgeType)}：" + LoaderVersion);
            List<ModMinecraft.McLibToken> libs = null;
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeLoaderLibraries", loaderName), Task =>
            {
                Task.output = new List<DownloadFile>();
                ZipArchive installer = null;
                try
                {
                    // 解压并获取、合并两个 Json 的信息
                    ModBase.WaitForFileReady(installerAddress);
                    installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
                    Task.Progress = 0.2d;
                    var json = (JsonObject)ModBase.GetJson(
                        ModBase.ReadFile(installer.GetEntry("install_profile.json").Open()));
                    var json2 = (JsonObject)ModBase.GetJson(ModBase.ReadFile(installer.GetEntry("version.json").Open()));
                    json.Merge(json2);
                    // 如果是 1.16.5 就升级一下 Authlib
                    if (Inherit == "1.16.5" && (bool)Config.Download.FixAuthLib)
                        json = (JsonObject)ModBase.GetJson(json.ToString()
                            .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                            .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                            .Replace("ad54da276bf59983d02d5ed16fc14541354c71fd",
                                "bbd00ca33b052f73a6312254780fc580d2da3535").Replace("76328", "87662"));
                    // 获取 Lib 下载信息
                    libs = ModMinecraft.McLibListGetWithJson(json, true);
                    // 添加 Mappings 下载信息
                    if (json["data"] is not null && json["data"]["MOJMAPS"] is not null)
                    {
                        // 下载原版 Json 文件
                        Task.Progress = 0.4d;
                        var rawJson = (JsonObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(
                            ModDownload.DlSourceLauncherOrMetaGet(
                                ModDownload.DlClientListGet(Inherit)?.ToString()), IsJson: true));
                        // [net.minecraft:client:1.17.1-20210706.113038:mappings@txt] 或 @tsrg]
                        var originalName = json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray())
                            .BeforeFirst("@");
                        var address = ModMinecraft.McLibGet(originalName).Replace(".jar",
                            "-mappings." + json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray())
                                .Split("@")[1]);
                        var clientMappings = rawJson["downloads"]["client_mappings"];
                        libs.Add(new ModMinecraft.McLibToken
                        {
                            isNatives = false,
                            localPath = address,
                            originalName = originalName,
                            Url = (string)clientMappings["url"],
                            size = (long)clientMappings["size"],
                            sha1 = (string)clientMappings["sha1"]
                        });
                        ModBase.Log(
                            $"[Download] 需要下载 Mappings：{clientMappings["url"]} (SHA1: {clientMappings["sha1"]})");
                    }

                    Task.Progress = 0.8d;
                    // 去除其中的原始 Forgelike 项
                    for (int i = 0, loopTo = libs.Count - 1; i <= loopTo; i++)
                        if (libs[i].localPath.EndsWithF($"{loaderName.ToLower()}-{Inherit}-{LoaderVersion}.jar") ||
                            libs[i].localPath.EndsWithF($"{loaderName.ToLower()}-{Inherit}-{LoaderVersion}-client.jar"))
                        {
                            ModBase.Log($"[Download] 已从待下载 {loaderName} 支持库中移除：" + libs[i].localPath,
                                ModBase.LogLevel.Debug);
                            libs.RemoveAt(i);
                            break;
                        }

                    Task.output = ModMinecraft.McLibNetFilesFromTokens(libs);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.LoaderLibraryListFailed",
                            ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge
                                ? Lang.Text("Minecraft.Download.Loader.NewForge")
                                : " " + ForgeType), ex);
                }
                finally
                {
                    // 释放文件
                    if (installer is not null)
                        installer.Dispose();
                }
            })
            {
                ProgressWeight = 2d
            });
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", loaderName),
                    new List<DownloadFile>())
                { ProgressWeight = 12d });
            loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.GetLoaderLibraries", loaderName), Task =>
            {
                #region Forgelike 文件

                if (isCustomFolder)
                    foreach (var LibFile in libs)
                    {
                        var realPath = LibFile.localPath.Replace(ModMinecraft.mcFolderSelected, McFolder);
                        if (!File.Exists(realPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(realPath));
                            ModBase.CopyFile(LibFile.localPath, realPath);
                        }

                        if (ModBase.modeDebug)
                            ModBase.Log($"[Download] 复制的 {loaderName} 支持库文件：" + LibFile.localPath);
                    }

                #endregion

                #region 原版文件

                // 等待原版文件下载完成
                if (ClientDownloadLoader is null)
                    return;
                var targetLoaders = ClientDownloadLoader.GetLoaderList()
                    .Where(l => (l.name ?? "") == mcDownloadClientLibName || (l.name ?? "") == mcDownloadClientJsonName)
                    .Where(l => l.State != ModBase.LoadState.Finished).ToList();
                if (targetLoaders.Any())
                    ModBase.Log($"[Download] {loaderName} 安装正在等待原版文件下载完成");
                while (targetLoaders.Any() && !Task.IsAborted)
                {
                    targetLoaders = targetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                    Thread.Sleep(50);
                }

                if (Task.IsAborted)
                    return;
                // 拷贝原版文件
                if (!isCustomFolder)
                    return;
                lock (vanillaSyncLock)
                {
                    var clientName = ModBase.GetFolderNameFromPath(ClientFolder);
                    Directory.CreateDirectory(Path.Combine(McFolder, "versions", Inherit));
                    if (!File.Exists(Path.Combine(McFolder, "versions", Inherit, Inherit + ".json")))
                        ModBase.CopyFile(Path.Combine(ClientFolder, clientName + ".json"),
                            Path.Combine(McFolder, "versions", Inherit, Inherit + ".json"));
                    if (!File.Exists(Path.Combine(McFolder, "versions", Inherit, Inherit + ".jar")))
                        ModBase.CopyFile(Path.Combine(ClientFolder, clientName + ".jar"),
                            Path.Combine(McFolder, "versions", Inherit, Inherit + ".jar"));
                }

                #endregion
            })
            {
                ProgressWeight = 0.1d,
                show = false
            });
            loaders.Add(new ModLoader.LoaderTask<bool, bool>(
                ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodA")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", ForgeType), Task =>
                {
                    ModBase.WaitForFileReady(installerAddress);
                    var installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
                    try
                    {
                        // 记录当前文件夹列表（在新建目标文件夹之前）
                        ModBase.Log("[Download] 开始进行 Forgelike 安装：" + installerAddress);
                        // 解压并获取信息
                        var oldList = new DirectoryInfo(McFolder + "versions/")
                            .EnumerateDirectories().Select(i => i.FullName).ToList();


                        // 新建目标实例文件夹
                        var json = ModBase.GetJson(ModBase.ReadFile(installer.GetEntry("install_profile.json").Open()));
                        Directory.CreateDirectory(versionFolder);
                        Task.Progress = 0.04d;
                        // 释放 launcher_installer.json
                        ModMinecraft.McFolderLauncherProfilesJsonCreate(McFolder);
                        Task.Progress = 0.05d;
                        // 运行 Forge 安装器
                        var useJavaWrapper = ModBase.IsUtf8CodePage();
                        Retry:

                        try
                        {
                            // 释放 Forge 注入器
                            ModBase.WriteFile(Path.Combine(ModBase.pathTemp, "Cache", "forge_installer.jar"),
                                ModBase.GetResourceStream("Resources/forge-installer.jar"));
                            Task.Progress = 0.06d;
                            // 运行注入器
                            ForgelikeInjector(installerAddress, Task, McFolder, useJavaWrapper, ForgeType);
                            Task.Progress = 0.97d;
                        }
                        catch (Exception ex)
                        {
                            if (!useJavaWrapper)
                            {
                                ModBase.Log(ex, $"不使用 JavaWrapper 安装 {loaderName} 失败，将使用 JavaWrapper 并重试");
                                useJavaWrapper = true;
                                goto Retry;
                            }

                            throw new Exception(
                                Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", loaderName), ex);
                            // 拷贝新增的实例 Json
                        }

                        var deltaList = new DirectoryInfo(McFolder + "versions/").EnumerateDirectories()
                            .SkipWhile(i => oldList.Contains(i.FullName)).ToList();

                        if (deltaList.Count > 1)
                            // 它可能和 OptiFine 安装同时运行，导致增加的文件不止一个（这导致了 #151）
                            // 也可能是因为 Forge 安装器的 Bug，生成了一个名字错误的文件夹，所以需要检查文件夹是否为空
                            deltaList = deltaList
                                .Where(l => l.Name.ContainsF("forge", true) && l.EnumerateFiles().Any())
                                .ToList();
                        // 如果没有新增文件夹，那么预测的文件夹名就是正确的
                        // 如果只新增 1 个文件夹，那么拷贝 Json 文件
                        if (deltaList.Count == 1)
                        {
                            var jsonFile = deltaList[0].EnumerateFiles().First();
                            ModBase.WriteFile(Path.Combine(versionFolder, TargetVersion + ".json"),
                                ModBase.ReadFile(jsonFile.FullName));
                            ModBase.Log(
                                $"[Download] 已拷贝新增的实例 Json 文件：{jsonFile.FullName} -> {versionFolder}{TargetVersion}.json");
                        }
                        else if (deltaList.Count > 1)
                        {
                            // 新增了多个文件夹
                            //Enumerable.Select<string>((IEnumerable<DirectoryInfo>)DeltaList, d => d.Name).Join(";")
                            ModBase.Log(
                                $"[Download] 有多个疑似的新增实例，无法确定：{string.Join(";", deltaList.Select<DirectoryInfo, string>(d => d.Name))}");
                        }
                        else
                        {
                            // 没有新增文件夹
                            ModBase.Log("[Download] 未找到新增的实例文件夹");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderInstallFailed", loaderName), ex);
                    }
                    finally
                    {
                        // 清理文件
                        try
                        {
                            if (installer is not null)
                                installer.Dispose();
                            if (File.Exists(installerAddress))
                                File.Delete(installerAddress);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"安装 {loaderName} 清理文件时出错");
                        }
                    }
                })
            {
                ProgressWeight = 10d
            });
        }
        else
        {
            ModBase.Log("[Download] 检测为非新版 Forge：" + LoaderVersion);
            loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                $"{(ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodB") : Lang.Text("Minecraft.Download.Stage.InstallForgeType", ForgeType))}",
                Task =>
                {
                    ZipArchive installer = null;
                    try
                    {
                        // 解压并获取信息
                        ModBase.WaitForFileReady(installerAddress);
                        installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
                        Task.Progress = 0.2d;
                        var json = (JsonObject)ModBase.GetJson(
                            ModBase.ReadFile(installer.GetEntry("install_profile.json").Open()));
                        Task.Progress = 0.4d;
                        // 新建实例文件夹
                        Directory.CreateDirectory(versionFolder);
                        Task.Progress = 0.5d;
                        if (json["install"] is null)
                        {
                            // 中版：Legacy 方式 1
                            ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 1：" + installerAddress);
                            // 建立 Json 文件
                            var jsonVersion = (JsonObject)ModBase.GetJson(
                                ModBase.ReadFile(installer.GetEntry(json["json"].ToString().TrimStart('/')).Open()));
                            jsonVersion["id"] = TargetVersion;
                            ModBase.WriteFile(Path.Combine(versionFolder, TargetVersion + ".json"), jsonVersion.ToString());
                            Task.Progress = 0.6d;
                            // 解压支持库文件
                            installer.Dispose();
                            var unrarDir = Path.Combine(Path.GetDirectoryName(installerAddress), "_unrar");
                            ModBase.ExtractFile(installerAddress, unrarDir);
                            ModBase.CopyDirectory(Path.Combine(unrarDir, "maven"), Path.Combine(McFolder, "libraries"));
                            ModBase.DeleteDirectory(unrarDir);
                        }
                        else
                        {
                            // 旧版：Legacy 方式 2
                            ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 2：" + installerAddress);
                            // 解压 Jar 文件
                            var jarAddress = ModMinecraft.McLibGet((string)json["install"]["path"],
                                customMcFolder: McFolder);
                            if (File.Exists(jarAddress))
                                File.Delete(jarAddress);
                            ModBase.WriteFile(jarAddress,
                                installer.GetEntry((string)json["install"]["filePath"]).Open());
                            Task.Progress = 0.9d;
                            // 建立 Json 文件
                            json["versionInfo"]["id"] = TargetVersion;
                            if (json["versionInfo"]["inheritsFrom"] is null)
                                ((JsonObject)json["versionInfo"]).Add("inheritsFrom", Inherit);
                            ModBase.WriteFile(Path.Combine(versionFolder, TargetVersion + ".json"), json["versionInfo"].ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Minecraft.Download.Error.ForgeOldInstallFailed"), ex);
                    }
                    finally
                    {
                        try
                        {
                            // 清理文件
                            if (installer is not null)
                                installer.Dispose();
                            if (File.Exists(installerAddress))
                                File.Delete(installerAddress);
                            var unrarDir = Path.Combine(Path.GetDirectoryName(installerAddress), "_unrar");
                            if (Directory.Exists(unrarDir))
                                ModBase.DeleteDirectory(unrarDir);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "非新版方式安装 Forge 清理文件时出错");
                        }
                    }
                })
            {
                ProgressWeight = 1d
            });
        }

        return loaders;
    }

    #endregion

    #region Forge 下载菜单

    public static void ForgeDownloadListItemPreload(StackPanel Stack, List<ModDownload.DlForgeVersionEntry> Entries,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 如果只有一个版本，则不特别列出
        if (Entries.Count == 1)
            return;
        // 获取推荐版本与最新版本
        ModDownload.DlForgeVersionEntry freshVersion = null;
        if (Entries.Any())
            freshVersion = Entries[0];
        else
            ModBase.Log("[System] 未找到可用的 Forge 版本", ModBase.LogLevel.Debug);
        ModDownload.DlForgeVersionEntry recommendedVersion = null;
        foreach (var Entry in Entries)
            if (Entry.isRecommended)
                recommendedVersion = Entry;
        // 若推荐版本与最新版本为同一版本，则仅显示推荐版本
        if (freshVersion is not null && ReferenceEquals(freshVersion, recommendedVersion))
            freshVersion = null;
        // 显示各个版本
        if (recommendedVersion is not null)
        {
            var recommended = ForgeDownloadListItem(recommendedVersion, OnClick, IsSaveOnly);
            recommended.Info = Lang.Text("Download.Version.Type.Recommended") + (string.IsNullOrEmpty(recommended.Info) ? "" : "  |  " + recommended.Info);
            Stack.Children.Add(recommended);
        }

        if (freshVersion is not null)
        {
            var fresh = ForgeDownloadListItem(freshVersion, OnClick, IsSaveOnly);
            fresh.Info = Lang.Text("Download.Version.Latest.Title") + (string.IsNullOrEmpty(fresh.Info) ? "" : "  |  " + fresh.Info);
            Stack.Children.Add(fresh);
        }

        // 添加间隔
        Stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Version.AllVersions", Entries.Count), HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 13d, 0d, 4d)
        });
    }

    public static MyListItem ForgeDownloadListItem(ModDownload.DlForgeVersionEntry Entry,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        var infoParts = new List<string>();

        if (!string.IsNullOrEmpty(Entry.releaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.releaseTime));

        if (ModBase.modeDebug)
            infoParts.Add(Lang.Text("Download.Version.Forge.Type", Entry.category));

        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.versionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.pathImage + "Blocks/Anvil.png"
        };

        newItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            newItem.ContentHandler = ForgeSaveContMenuBuild;
        else
            newItem.ContentHandler = ForgeContMenuBuild;
        // 结束
        return newItem;
    }

    private static void ForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (ss, ee) => ForgeSave_Click(ss, (dynamic)ee);
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (ss, ee) => ForgeLog_Click(ss, (dynamic)ee);
        sender.Buttons = new[] { btnSave, btnInfo };
    }

    private static void ForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (ss, ee) => ForgeLog_Click(ss, (dynamic)e);
        sender.Buttons = new[] { btnInfo };
    }

    private static void ForgeLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlForgeVersionEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(
            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{version.inherit}-{version.versionName}/forge-{version.inherit}-{version.versionName}-changelog.txt");
    }

    public static void ForgeSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlForgeVersionEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(version);
    }

    #endregion

    #region Forge 推荐版本获取

    /// <summary>
    ///     尝试刷新 Forge 推荐版本缓存。
    /// </summary>
    public static void McDownloadForgeRecommendedRefresh()
    {
        if (isForgeRecommendedRefreshed)
            return;
        isForgeRecommendedRefreshed = true;
        // 获取所有推荐版本列表
        // 内容为："1.15.2":"31.2.0"
        // 保存
        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModBase.Log("[Download] 刷新 Forge 推荐版本缓存开始");
                var result = ModNet.NetGetCodeByLoader("https://bmclapi2.bangbang93.com/forge/promos");
                if (result.Length < 1000) throw new Exception(Lang.Text("Minecraft.Download.Error.ForgePromosResultTooShort", result));
                var resultJson = (JsonNode)ModBase.GetJson(result);
                var recommendedList = new List<string>();
                foreach (JsonObject Version in resultJson.AsArray())
                {
                    if (Version["name"] is null || Version["build"] is null) continue;
                    var name = (string)Version["name"];
                    if (!name.EndsWithF("-recommended")) continue;
                    recommendedList.Add("\"" + name.Replace("-recommended",
                        "\":\"" + Version["build"]["version"] + "\""));
                }

                if (recommendedList.Count < 5)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.ForgeRecommendedTooFew", result));
                var cacheJson = "{" + recommendedList.Join(",") + "}";
                ModBase.WriteFile(Path.Combine(ModBase.pathTemp, "Cache", "ForgeRecommendedList.json"), cacheJson);
                ModBase.Log("[Download] 刷新 Forge 推荐版本缓存成功");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新 Forge 推荐版本缓存失败");
            }
        }, "ForgeRecommendedRefresh");
    }

    private static bool isForgeRecommendedRefreshed;

    /// <summary>
    ///     尝试获取某个 MC 版本对应的 Forge 推荐版本。如果不可用会返回 Nothing。
    /// </summary>
    public static string McDownloadForgeRecommendedGet(string McInstance)
    {
        try
        {
            if (McInstance is null)
                return null;
            var list = ModBase.ReadFile(Path.Combine(ModBase.pathTemp, "Cache", "ForgeRecommendedList.json"));
            if (list is null || string.IsNullOrEmpty(list))
            {
                ModBase.Log("[Download] 没有 Forge 推荐版本缓存文件");
                return null;
            }

            var json = (JsonObject)ModBase.GetJson(list);
            if (json is null || !(McInstance ?? "null").Contains(".") || !json.ContainsKey(McInstance))
                return null;
            return (json[McInstance] ?? "").ToString();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 Forge 推荐版本失败（" + (McInstance ?? "null") + "）", ModBase.LogLevel.Feedback);
            return null;
        }
    }

    #endregion

    #region NeoForge 下载菜单

    public static void NeoForgeDownloadListItemPreload(StackPanel Stack, List<ModDownload.DlNeoForgeListEntry> Entries,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 如果只有一个版本，则不特别列出
        if (Entries.Count == 1)
            return;
        // 获取最新稳定版和测试版
        ModDownload.DlNeoForgeListEntry freshStableVersion = null;
        ModDownload.DlNeoForgeListEntry freshBetaVersion = null;
        if (Entries.Any())
            foreach (var Entry in Entries.ToList())
                if (Entry.isBeta)
                {
                    if (freshBetaVersion is null)
                        freshBetaVersion = Entry;
                }
                else
                {
                    freshStableVersion = Entry;
                    break;
                }
        else
            ModBase.Log("[System] 未找到可用的 NeoForge 版本", ModBase.LogLevel.Debug);

        // 显示各个版本
        if (freshStableVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshStableVersion, OnClick, IsSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Stable")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
            Stack.Children.Add(fresh);
        }

        if (freshBetaVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshBetaVersion, OnClick, IsSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Development")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
            Stack.Children.Add(fresh);
        }

        // 添加间隔
        Stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Version.AllVersions", Entries.Count), HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 13d, 0d, 4d)
        });
    }

    public static MyListItem NeoForgeDownloadListItem(ModDownload.DlNeoForgeListEntry Info,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Info.versionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Info,
            Info = Info.isBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.pathImage + "Blocks/NeoForge.png"
        };
        newItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            newItem.ContentHandler = NeoForgeSaveContMenuBuild;
        else
            newItem.ContentHandler = NeoForgeContMenuBuild;
        // 结束
        return newItem;
    }

    private static void NeoForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (sender, e) => NeoForgeSave_Click(sender, (RoutedEventArgs)e);
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { btnSave, btnInfo };
    }

    private static void NeoForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { btnInfo };
    }

    private static void NeoForgeLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlNeoForgeListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(info.UrlBase + "-changelog.txt");
    }

    public static void NeoForgeSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlNeoForgeListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(info);
    }

    #endregion

    #region Cleanroom 下载菜单

    public static void CleanroomDownloadListItemPreload(StackPanel Stack,
        List<ModDownload.DlCleanroomListEntry> Entries, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 获取最新稳定版和测试版
        // Dim FreshStableVersion As DlCleanroomListEntry = Nothing
        ModDownload.DlCleanroomListEntry freshBetaVersion = null;
        if (Entries.Any())
            freshBetaVersion = Entries[0];
        else
            ModBase.Log("[System] 未找到可用的 Cleanroom 版本", ModBase.LogLevel.Debug);
        if (freshBetaVersion is not null)
        {
            var fresh = CleanroomDownloadListItem(freshBetaVersion, OnClick, IsSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info) ? Lang.Text("Download.Version.Fresh.Development") : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
            Stack.Children.Add(fresh);
        }

        // 添加间隔
        Stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Version.AllVersions", Entries.Count), HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 13d, 0d, 4d)
        });
    }

    public static MyListItem CleanroomDownloadListItem(ModDownload.DlCleanroomListEntry Info,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Info.versionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Info,
            Info = Info.isBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.pathImage + "Blocks/Cleanroom.png"
        };
        newItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            newItem.ContentHandler = CleanroomSaveContMenuBuild;
        else
            newItem.ContentHandler = CleanroomContMenuBuild;
        // 结束
        return newItem;
    }

    private static void CleanroomContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (sender, _e) => CleanroomSave_Click(sender, (RoutedEventArgs)e);
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => CleanroomLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { btnSave, btnInfo };
    }

    private static void CleanroomSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => CleanroomLog_Click(a, (dynamic)b);
        sender.Buttons = new[] { btnInfo };
    }

    private static void CleanroomLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlCleanroomListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(info.UrlBase + "-changelog.txt");
    }

    public static void CleanroomSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlCleanroomListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(info);
    }

    #endregion

    #region Fabric 下载

    public static void McDownloadFabricLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var url = DownloadInfo["url"].ToString();
            var fileName = ModBase.GetFileNameFromPath(url);
            var version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), fileName, Lang.Text("Download.Version.Installer.Fabric.Filter"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.FabricInstallerDownload", version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            // BMCLAPI 不支持 Fabric Installer 下载
            var address = new List<string>();
            address.Add(url);
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.FabricInstallerDownload", version), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Fabric 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 Fabric 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadFabricLoader(string FabricVersion, string MinecraftName,
        string McFolder = null, bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = "fabric-loader-" + FabricVersion + "-" + MinecraftName;
        var versionFolder = Path.Combine(McFolder, "versions", id);
        var loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainFabricMainFileUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            Task.Progress = 0.5d;
            
            var safeName = MinecraftName.Replace("∞", "infinite");
            var bmclapiUrl = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{safeName}/{FabricVersion}/profile/json";
            var officialUrl = $"https://meta.fabricmc.net/v2/versions/loader/{safeName}/{FabricVersion}/profile/json";

            string json = null;
            foreach (var url in new[] { bmclapiUrl, officialUrl })
            {
                try
                {
                    json = Requester.FetchString(url, new RequestParam { UseBrowserUserAgent = true, Timeout = 5000, Retries = 2 });
                    if (json is not null) break;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[Download] 从 {url} 下载 Fabric meta 失败");
                }
            }

            if (json is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.FabricMetaDownloadFailed",
                    $"{bmclapiUrl} and {officialUrl}"));

            Directory.CreateDirectory(versionFolder);
            File.WriteAllText(Path.Combine(versionFolder, id + ".json"), json, Encoding.UTF8);
            Task.output = new List<DownloadFile>();
        })
        {
            ProgressWeight = 0.5d
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Fabric"),
            new List<DownloadFile>()) { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeFabricLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLabyModClientJson"),
                    new List<DownloadFile>()) { ProgressWeight = 8d });
        }

        return loaders;
    }

    #endregion

    #region LegacyFabric 下载

    public static void McDownloadLegacyFabricLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var url = DownloadInfo["url"].ToString();
            var fileName = ModBase.GetFileNameFromPath(url);
            var version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), fileName, Lang.Text("Download.Version.Installer.LegacyFabric.Filter"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LegacyFabricInstallerDownload", version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var address = new List<string>();
            address.Add(url);
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.LegacyFabricInstallerDownload", version), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Legacy Fabric 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 LegacyFabric 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadLegacyFabricLoader(string LegacyFabricVersion,
        string MinecraftName, string McFolder = null, bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = "legacy-fabric-loader-" + LegacyFabricVersion + "-" + MinecraftName;
        var versionFolder = Path.Combine(McFolder, "versions", id);
        var loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainLegacyFabricMainFileUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        "https://meta.legacyfabric.net/v2/versions/loader/" + MinecraftName + "/" +
                        LegacyFabricVersion + "/profile/json"
                    }, Path.Combine(versionFolder, id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
        })
        {
            ProgressWeight = 0.5d
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Legacy Fabric"),
                new List<DownloadFile>())
            { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLegacyFabricLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Legacy Fabric"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return loaders;
    }

    #endregion

    #region Fabric 下载菜单

    public static MyListItem FabricDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry["version"].ToString().Replace("+build", ""),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["stable"].ToObject<bool>() ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = ModBase.pathImage + "Blocks/Fabric.png"
        };
        newItem.Click += OnClick;
        newItem.ContentHandler = FabricContMenuBuild;
        // 结束
        return newItem;
    }

    private static void FabricContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => FabricLog_Click(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[] { btnInfo };
    }

    private static void FabricLog_Click(object sender, RoutedEventArgs e)
    {
        ModBase.OpenWebsite("https://fabricmc.net/blog");
    }

    public static MyListItem FabricApiDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.displayName.Split("]")[1].Replace("Fabric API ", "").Replace(" build ", ".").Trim(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.releaseDate, "g")),
            Logo = ModBase.pathImage + "Blocks/Fabric.png"
        };
        newItem.Click += OnClick;
        // 结束
        return newItem;
    }

    public static MyListItem OptiFabricDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.displayName.ToLower().Replace("optifabric-", "").Replace(".jar", "").Trim().TrimStart('v'),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.releaseDate, "g")),
            Logo = ModBase.pathImage + "Blocks/OptiFabric.png"
        };
        newItem.Click += OnClick;
        // 结束
        return newItem;
    }

    #endregion

    #region LegacyFabric 下载菜单

    public static MyListItem LegacyFabricDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry["version"].ToString(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["stable"].ToObject<bool>() ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = ModBase.pathImage + "Blocks/Fabric.png"
        };
        newItem.Click += OnClick;
        // 结束
        return newItem;
    }

    public static MyListItem LegacyFabricApiDownloadListItem(ModComp.CompFile Entry,
        MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.displayName.Replace("Legacy Fabric API ", ""),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.releaseDate, "g")),
            Logo = ModBase.pathImage + "Blocks/Fabric.png"
        };
        newItem.Click += OnClick;
        // 结束
        return newItem;
    }

    #endregion

    #region Quilt 下载

    public static void McDownloadQuiltLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var url = DownloadInfo["url"].ToString();
            var fileName = ModBase.GetFileNameFromPath(url);
            var version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), fileName, Lang.Text("Download.Version.Installer.Quilt.Filter"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar)
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.QuiltInstallerDownload", version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            // TODO: BMCLAPI 不支持 Quilt Installer 下载
            var address = new List<string>();
            address.Add(url);
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.QuiltInstallerDownload", version), loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Quilt 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 Quilt 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadQuiltLoader(string QuiltVersion, string MinecraftName,
        string McFolder = null, bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = "quilt-loader-" + QuiltVersion + "-" + MinecraftName;
        var versionFolder = Path.Combine(McFolder, "versions", id);
        var loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainQuiltMainFileUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        "https://meta.quiltmc.org/v3/versions/loader/" + MinecraftName + "/" + QuiltVersion +
                        "/profile/json"
                    }, Path.Combine(versionFolder, id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
            // 新建 mods 文件夹
            Directory.CreateDirectory($@"{McFolder ?? ModMinecraft.mcFolderSelected}mods\");
        })
        {
            ProgressWeight = 0.5d
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Quilt"),
            new List<DownloadFile>()) { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeQuiltLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Quilt"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return loaders;
    }

    #endregion

    #region Quilt 下载菜单

    public static MyListItem QuiltDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry["version"].ToString(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["maven"].ToString().Contains("installer") ? Lang.Text("Download.Version.Type.Installer") :
                Entry["version"].ToString().Contains("beta") || Entry["version"].ToString().Contains("pre") ? Lang.Text("Download.Version.Type.Preview") :
                Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.pathImage + "Blocks/Quilt.png"
        };
        newItem.Click += OnClick;
        newItem.ContentHandler = QuiltContMenuBuild;
        // 结束
        return newItem;
    }

    private static void QuiltContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => QuiltLog_Click(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[] { btnInfo };
    }

    private static void QuiltLog_Click(object sender, RoutedEventArgs e)
    {
        ModBase.OpenWebsite("https://quiltmc.org/en/blog/1/");
    }

    public static MyListItem QSLDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry.displayName.Split("]")[1].Replace(" build ", ".").Split("+")[0].Trim(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.releaseDate, "g")),
            Logo = ModBase.pathImage + "Blocks/Quilt.png"
        };
        newItem.Click += OnClick;
        // 结束
        return newItem;
    }

    #endregion

    #region LabyMod 下载

    public static void McDownloadLabyModProductionLoaderSave()
    {
        try
        {
            var url = "https://releases.labymod.net/api/v1/installer/production/java";
            var fileName = "LabyMod4ProductionInstaller.jar";
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), fileName, Lang.Text("Download.Version.Installer.LabyMod.Filter"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var address = new List<string>();
            address.Add(url);
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<JsonObject>(Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                        loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 LabyMod 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    public static void McDownloadLabyModSnapshotLoaderSave()
    {
        try
        {
            var url = "https://releases.labymod.net/api/v1/installer/snapshot/java";
            var fileName = "LabyMod4SnapshotInstaller.jar";
            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), fileName, Lang.Text("Download.Version.Installer.LabyMod.Filter"));
            if (!target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.loaderTaskbar.ToList())
            {
                if ((OngoingLoader.name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var address = new List<string>();
            address.Add(url);
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(address.ToArray(), target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var loader =
                new ModLoader.LoaderCombo<JsonObject>(Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                        loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 LabyMod 安装器下载失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下载某个 LabyMod 实例的加载器列表。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadLabyModLoader(string LabyModCommitRef, string LabyModChannel,
        string MinecraftName, string McFolder = null, bool FixLibrary = true)
    {
        // 参数初始化
        McFolder = McFolder ?? ModMinecraft.mcFolderSelected;
        var isCustomFolder = (McFolder ?? "") != (ModMinecraft.mcFolderSelected ?? "");
        var id = "labymod-" + LabyModCommitRef + "-" + MinecraftName;
        var versionFolder = Path.Combine(McFolder, "versions", id);
        var loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainLabyModClientUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName,
                    $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{LabyModChannel}/{MinecraftName}/{LabyModCommitRef}.json");
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{LabyModChannel}/{MinecraftName}/{LabyModCommitRef}.json"
                    }, Path.Combine(versionFolder, id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
            Task.Progress = 1d;
        })
        {
            ProgressWeight = 2d
        });
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Fabric"),
                new List<DownloadFile>())
            { ProgressWeight = 10d });
        // 下载支持库
        if (FixLibrary)
        {
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLabyModLibraries"),
                    Task => Task.output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder)))
                { ProgressWeight = 1d, show = false });
            loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "LabyMod"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return loaders;
    }

    /// <summary>
    ///     获取下载某个 Minecraft 实例的加载器列表。
    ///     它必须安装到 PathMcFolder，但是可以自定义实例名（不过自定义的实例名不会修改 Json 中的 id 项）。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadLabyModClientLoader(string Id, string LabyChannel,
        string LabyCommitRef, string VersionName = null)
    {
        VersionName = VersionName ?? Id;
        var versionFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", VersionName) + @"\";

        var loaders = new List<ModLoader.LoaderBase>();

        // 下载支持库文件
        var loadersLib = new List<ModLoader.LoaderBase>();
        loadersLib.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaAndLabyModLibrariesSide"), Task =>
        {
            ModBase.WaitForFileReady(Path.Combine(versionFolder, VersionName + ".json"));
            ModBase.Log("[Download] 开始分析原版与 LabyMod 支持库文件：" + versionFolder);
            Task.output = ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(versionFolder));
        })
        {
            ProgressWeight = 1d,
            show = false
        });
        loadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAndLabyModLibrariesSide"),
                new List<DownloadFile>())
            { ProgressWeight = 13d, show = false });
        loaders.Add(new ModLoader.LoaderCombo<string>(mcDownloadClientLibName, loadersLib)
            { block = false, ProgressWeight = 14d });

        // 下载资源文件
        var loadersAssets = new List<ModLoader.LoaderBase>();
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"), Task =>
        {
            try
            {
                var version = new ModMinecraft.McInstance(versionFolder);
                Task.output = new List<DownloadFile> { ModDownload.DlClientAssetIndexGet(version) };
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"), ex);
            }

            // 顺手添加 Json 项目
            try
            {
                var versionJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Path.Combine(versionFolder, VersionName + ".json")));
                versionJson.Add("clientVersion", Id);
                ModBase.WriteFile(Path.Combine(versionFolder, VersionName + ".json"), versionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AddClientVersionFailed"), ex);
            }
        })
        {
            ProgressWeight = 1d,
            show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 3d, show = false });
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"), Task =>
        {
            ModLoader.LoaderBase argprogressFeed = Task;
            Task.output =
                ModMinecraft.McAssetsFixList(new ModMinecraft.McInstance(versionFolder), true, ref argprogressFeed);
            Task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
        })
        {
            ProgressWeight = 3d,
            show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 14d, show = false });
        loaders.Add(
            new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                loadersAssets) { block = false, ProgressWeight = 21d });

        return loaders;
    }

    #endregion

    #region LabyMod 下载菜单

    public static MyListItem LabyModDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var newItem = new MyListItem
        {
            Title = Entry["version"] + " " + (Entry["channel"].ToString().Contains("snapshot") ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable")),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["channel"].ToString().Contains("snapshot") ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.pathImage + "Blocks/LabyMod.png"
        };
        newItem.Click += OnClick;
        newItem.ContentHandler = LabyModContMenuBuild;
        // 结束
        return newItem;
    }

    private static void LabyModContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (a, b) => LabyModSave_Click(a, (dynamic)b);
        var btnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => LabyModLog_Click(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[] { btnSave, btnInfo };
    }

    private static void LabyModLog_Click(object sender, RoutedEventArgs e)
    {
        ModBase.OpenWebsite("https://www.labymod.net/zh_Hans/download");
    }

    private static void LabyModSave_Click(object sender, RoutedEventArgs e)
    {
        JsonObject version;
        if (((dynamic)sender).Tag is not null)
            version = (JsonObject)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (JsonObject)((dynamic)sender).Parent.Tag;
        else
            version = (JsonObject)((dynamic)sender).Parent.Parent.Tag;
        if ((string)version["channel"] == "snapshot")
            McDownloadLabyModSnapshotLoaderSave();
        else
            McDownloadLabyModProductionLoaderSave();
    }

    #endregion

    #region 合并安装

    /// <summary>
    ///     安装请求。
    /// </summary>
    public class McInstallRequest
    {
        /// <summary>
        ///     欲下载的 Cleanroom。
        /// </summary>
        public ModDownload.DlCleanroomListEntry cleanroomEntry = null;

        // 若要下载 Cleanroom，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 Cleanroom 版本名。
        /// </summary>
        public string cleanroomVersion;

        /// <summary>
        ///     欲下载的 Fabric API 信息。
        /// </summary>
        public ModComp.CompFile fabricApi = null;

        /// <summary>
        ///     欲下载的 Fabric Loader 版本名。
        /// </summary>
        public string fabricVersion = null;

        /// <summary>
        ///     欲下载的 Forge。
        /// </summary>
        public ModDownload.DlForgeVersionEntry forgeEntry = null;

        // 若要下载 Forge，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
        /// </summary>
        public string forgeVersion;

        /// <summary>
        ///     欲下载的 LabyMod 通道。
        /// </summary>
        public string labyModChannel = null;

        /// <summary>
        ///     欲下载的 LabyMod 版本。
        /// </summary>
        public string labyModCommitRef = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric API 信息。
        /// </summary>
        public ModComp.CompFile legacyFabricApi = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric Loader 版本名。
        /// </summary>
        public string legacyFabricVersion = null;

        /// <summary>
        ///     欲下载的 LiteLoader 详细信息。
        /// </summary>
        public ModDownload.DlLiteLoaderListEntry liteLoaderEntry = null;

        /// <summary>
        ///     可选。欲下载的 Minecraft Json 地址。
        /// </summary>
        public string minecraftJson = null;

        /// <summary>
        ///     必填。欲下载的 Minecraft 的版本名。
        /// </summary>
        public string minecraftName = null;

        /// <summary>
        ///     若 MMC 整合包安装包含特殊参数，则填写此项。
        /// </summary>
        public ModModpack.MMCPackInfo mmcPackInfo = null;

        /// <summary>
        ///     欲下载的 NeoForge。
        /// </summary>
        public ModDownload.DlNeoForgeListEntry neoForgeEntry = null;

        // 若要下载 NeoForge，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 NeoForge 版本名。
        /// </summary>
        public string neoForgeVersion;

        /// <summary>
        ///     欲下载的 OptiFabric 信息。
        /// </summary>
        public ModComp.CompFile optiFabric = null;

        /// <summary>
        ///     欲下载的 OptiFine 详细信息。
        /// </summary>
        public ModDownload.DlOptiFineListEntry optiFineEntry;

        // 若要下载 OptiFine，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
        /// </summary>
        public string optiFineVersion;

        /// <summary>
        ///     欲下载的 Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL) 信息。
        /// </summary>
        public ModComp.CompFile qsl = null;

        /// <summary>
        ///     欲下载的 Quilt Loader 版本名。
        /// </summary>
        public string quiltVersion = null;

        /// <summary>
        ///     必填。安装目标文件夹。
        /// </summary>
        public string targetInstanceFolder;

        /// <summary>
        ///     必填。安装目标实例名称。
        /// </summary>
        public string targetInstanceName;
    }

    /// <summary>
    ///     在加载器状态改变后显示一条提示。
    ///     不会进行任何其他操作。
    /// </summary>
    public static void LoaderStateChangedHintOnly(object Loader)
    {
        var loader = (ModLoader.LoaderBase)Loader;
        switch (loader.State)
        {
            case ModBase.LoadState.Finished:
                ModMain.Hint($"{loader.name}{Lang.Text("Common.Status.Success")}", ModMain.HintType.Finish);
                break;
            case ModBase.LoadState.Failed:
                ModMain.Hint($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}", ModMain.HintType.Critical);
                break;
            case ModBase.LoadState.Aborted:
                ModMain.Hint($"{loader.name}{Lang.Text("Common.Status.Cancelled")}");
                break;
        }
    }

    /// <summary>
    ///     安装加载器状态改变后进行提示和重载文件夹列表的方法。
    /// </summary>
    public static void McInstallState(object Loader)
    {
        var loader = (ModLoader.LoaderBase)Loader;
        var combo = (ModLoader.LoaderCombo)Loader;
        switch (loader.State)
        {
            case ModBase.LoadState.Finished:
            {
                if (Config.Download.AutoSelectInstance)
                {
                    var versionName = loader.name;
                    ModBase.WriteIni(ModMinecraft.mcFolderSelected + "PCL.ini", "Version",
                        versionName.Remove(versionName.Length - 3, 3));
                }

                ModBase.WriteIni(ModMinecraft.mcFolderSelected + "PCL.ini", "InstanceCache",
                    ""); // 清空缓存（合并安装会先生成文件夹，这会在刷新时误判为可以使用缓存）
                ModBase.DeleteDirectory($"{combo.input}PCLInstallBackups\\");
                ModMain.Hint($"{loader.name}{Lang.Text("Common.Status.Success")}",
                    ModMain.HintType.Finish);
                break;
            }
            case ModBase.LoadState.Failed:
            {
                ModMain.Hint(
                    $"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}",
                    ModMain.HintType.Critical);
                break;
            }
            case ModBase.LoadState.Aborted:
            {
                ModMain.Hint($"{loader.name}{Lang.Text("Common.Status.Cancelled")}");
                break;
            }
            case ModBase.LoadState.Loading:
            {
                return; // 不重新加载实例列表
            }
        }

        if (loader.State != ModBase.LoadState.Finished &&
                Directory.Exists(
                    $"{combo.input}PCLInstallBackups\\")) // 实例修改失败回滚
        {
            ModBase.CopyDirectory(
                $"{combo.input}PCLInstallBackups\\",
                (string)combo.input);
            File.Delete($"{combo.input}.pclignore");
            ModBase.DeleteDirectory(
                $"{combo.input}PCLInstallBackups\\");
        }
        else
        {
            McInstallFailedClearFolder(Loader);
        }

        ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
            ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    public static void McInstallFailedClearFolder(object Loader)
    {
        try
        {
            Thread.Sleep(1000); // 防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
            if (((ModLoader.LoaderBase)Loader).State == ModBase.LoadState.Failed ||
                ((ModLoader.LoaderBase)Loader).State == ModBase.LoadState.Aborted)
            {
                // 删除实例文件夹
                if (Directory.Exists(
                        $"{((ModLoader.LoaderCombo)Loader).input}saves\\") ||
                    Directory.Exists(
                        $"{((ModLoader.LoaderCombo)Loader).input}versions\\") ||
                    Directory.Exists(
                        $"{((ModLoader.LoaderCombo)Loader).input}mods\\") ||
                    File.Exists($"{((ModLoader.LoaderCombo)Loader).input}server.dat"))
                {
                    ModBase.Log(
                        $"[Download] 由于实例已被独立启动，不清理实例文件夹：{((ModLoader.LoaderCombo)Loader).input}", ModBase.LogLevel.Developer);
                }
                else
                {
                    ModBase.Log(
                        $"[Download] 由于下载失败或取消，清理实例文件夹：{((ModLoader.LoaderCombo)Loader).input}", ModBase.LogLevel.Developer);
                    ModBase.DeleteDirectory((string)((ModLoader.LoaderCombo)Loader).input);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "下载失败或取消后清理实例文件夹失败");
        }
    }

    private const string mcInstallDefaultType = "安装";

    /// <summary>
    ///     进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    /// </summary>
    public static bool McInstall(McInstallRequest Request, string Type = mcInstallDefaultType)
    {
        try
        {
            var subLoaders = McInstallLoader(Request, IgnoreDump: Type != mcInstallDefaultType);
            if (subLoaders is null)
                return false;
            var loader = new ModLoader.LoaderCombo<string>(Request.targetInstanceName + " " + Type, subLoaders)
                { OnStateChanged = McInstallState };

            // 启动
            loader.Start(Request.targetInstanceFolder);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
            return true;
        }

        catch (ModBase.CancelledException ex)
        {
            return false;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始合并安装失败", ModBase.LogLevel.Feedback);
            return false;
        }
    }

    /// <summary>
    ///     获取合并安装加载器列表，并进行前期的缓存清理与 Java 检查工作。
    /// </summary>
    /// <exception cref="ModBase.CancelledException" />
    public static List<ModLoader.LoaderBase> McInstallLoader(McInstallRequest Request, bool DontFixLibraries = false,
        bool IgnoreDump = false)
    {
        // 获取缓存目录（安装 Mod 加载器的文件夹不能包含空格）
        var tempMcFolder = ModMain.RequestTaskTempFolder(Request.optiFineEntry is not null ||
                                                         Request.forgeEntry is not null ||
                                                         Request.neoForgeEntry is not null);

        // 获取参数
        var instanceFolder = Path.Combine(ModMinecraft.mcFolderSelected, "versions", Request.targetInstanceName);
        if (Directory.Exists(tempMcFolder))
            ModBase.DeleteDirectory(tempMcFolder);
        string optiFineFolder = null;
        if (Request.optiFineVersion is not null)
        {
            if (Request.optiFineVersion.Contains("_HD_U_"))
                Request.optiFineVersion = "HD_U_" + Request.optiFineVersion.AfterLast("_HD_U_"); // #735
            Request.optiFineEntry = new ModDownload.DlOptiFineListEntry
            {
                displayName = Request.minecraftName + " " + Request.optiFineVersion.Replace("HD_U_", "")
                    .Replace("_", "").Replace("pre", " pre"),
                Inherit = Request.minecraftName,
                isPreview = Request.optiFineVersion.ContainsF("pre", true),
                nameVersion = Request.minecraftName + "-OptiFine_" + Request.optiFineVersion,
                nameFile = (Request.optiFineVersion.ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" +
                           Request.minecraftName + "_" + Request.optiFineVersion + ".jar"
            };
        }

        if (Request.optiFineEntry is not null)
            optiFineFolder = Path.Combine(tempMcFolder, "versions", Request.optiFineEntry.nameVersion);
        string forgeFolder = null;
        if (Request.forgeEntry is not null)
            Request.forgeVersion = Request.forgeVersion ?? Request.forgeEntry.versionName;
        if (Request.forgeVersion is not null)
            forgeFolder = Path.Combine(tempMcFolder, "versions", "forge-" + Request.forgeVersion);
        string neoForgeFolder = null;
        if (Request.neoForgeEntry is not null)
            Request.neoForgeVersion = Request.neoForgeVersion ?? Request.neoForgeEntry.versionName;
        if (Request.neoForgeVersion is not null)
            neoForgeFolder = Path.Combine(tempMcFolder, "versions", "neoforge-" + Request.neoForgeVersion);
        string cleanroomFolder = null;
        if (Request.cleanroomEntry is not null)
            Request.cleanroomVersion = Request.cleanroomVersion ?? Request.cleanroomEntry.versionName;
        if (Request.cleanroomVersion is not null)
            cleanroomFolder = Path.Combine(tempMcFolder, "versions", "cleanroom-" + Request.cleanroomVersion);
        string fabricFolder = null;
        if (Request.fabricVersion is not null)
            fabricFolder = Path.Combine(tempMcFolder, "versions", "fabric-loader-" + Request.fabricVersion + "-" +
                           Request.minecraftName);
        string legacyFabricFolder = null;
        if (Request.legacyFabricVersion is not null)
            legacyFabricFolder = Path.Combine(tempMcFolder, "versions", "legacy-fabric-loader-" + Request.legacyFabricVersion + "-" +
                                 Request.minecraftName);
        string quiltFolder = null;
        if (Request.quiltVersion is not null)
            quiltFolder = Path.Combine(tempMcFolder, "versions", "quilt-loader-" + Request.quiltVersion + "-" + Request.minecraftName);
        string labyModFolder = null;
        if (Request.labyModCommitRef is not null)
            labyModFolder = Path.Combine(tempMcFolder, "versions", "labymod-" + Request.labyModCommitRef + "-" +
                            Request.minecraftName);
        string liteLoaderFolder = null;
        if (Request.liteLoaderEntry is not null)
            liteLoaderFolder = Path.Combine(tempMcFolder, "versions", Request.minecraftName + "-LiteLoader");

        // 判断 OptiFine 是否作为 Mod 进行下载
        var modable = Request.fabricVersion is not null || Request.legacyFabricVersion is not null ||
                      Request.forgeEntry is not null || Request.neoForgeEntry is not null ||
                      Request.liteLoaderEntry is not null;
        var modsTempFolder = Path.Combine(tempMcFolder, "mods");
        var optiFineAsMod = Request.optiFineEntry is not null && modable; // 选择了 OptiFine 与任意 Mod 加载器
        if (optiFineAsMod)
        {
            ModBase.Log("[Download] OptiFine 将作为 Mod 进行下载");
            if (Request.liteLoaderEntry is not null)
                optiFineFolder = Path.Combine(modsTempFolder, Request.minecraftName);
            else
                optiFineFolder = modsTempFolder;
        }

        // 记录日志
        if (optiFineFolder is not null)
            ModBase.Log("[Download] OptiFine 缓存：" + optiFineFolder);
        if (forgeFolder is not null)
            ModBase.Log("[Download] Forge 缓存：" + forgeFolder);
        if (neoForgeFolder is not null)
            ModBase.Log("[Download] NeoForge 缓存：" + neoForgeFolder);
        if (cleanroomFolder is not null)
            ModBase.Log("[Download] Cleanroom 缓存：" + cleanroomFolder);
        if (fabricFolder is not null)
            ModBase.Log("[Download] Fabric 缓存：" + fabricFolder);
        if (legacyFabricFolder is not null)
            ModBase.Log("[Download] LegacyFabric 缓存：" + legacyFabricFolder);
        if (quiltFolder is not null)
            ModBase.Log("[Download] Quilt 缓存：" + quiltFolder);
        if (labyModFolder is not null)
            ModBase.Log("[Download] LabyMod 缓存：" + labyModFolder);
        if (liteLoaderFolder is not null)
            ModBase.Log("[Download] LiteLoader 缓存：" + liteLoaderFolder);
        ModBase.Log("[Download] 对应的原版版本：" + Request.minecraftName);

        // 重复实例检查
        if (File.Exists(Path.Combine(instanceFolder, Request.targetInstanceName + ".json")) && !IgnoreDump)
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", Request.targetInstanceName, ""),
                ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        var loaderList = new List<ModLoader.LoaderBase>();
        // 添加忽略标识
        loaderList.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Minecraft.Download.Stage.AddIgnoreFlag"),
                _ => ModBase.WriteFile(Path.Combine(instanceFolder, ".pclignore"), "用于临时地在 PCL 的实例列表中屏蔽此实例。"))
            { show = false, block = false });
        // Fabric API
        if (Request.fabricApi is not null)
            loaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadFabricApi"),
                    new List<DownloadFile> { Request.fabricApi.ToNetFile(modsTempFolder) })
                { ProgressWeight = 3d, block = false });
        // LegacyFabric API
        if (Request.legacyFabricApi is not null)
            loaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLegacyFabricApi"),
                    new List<DownloadFile> { Request.legacyFabricApi.ToNetFile(modsTempFolder) })
                { ProgressWeight = 3d, block = false });
        // Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL)
        if (Request.qsl is not null)
            loaderList.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadQfapiQsl"),
                        new List<DownloadFile> { Request.qsl.ToNetFile(modsTempFolder) })
                    { ProgressWeight = 3d, block = false });
        // OptiFabric
        if (Request.optiFabric is not null)
            loaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFabric"),
                    new List<DownloadFile> { Request.optiFabric.ToNetFile(modsTempFolder) })
                { ProgressWeight = 3d, block = false });
        // LabyMod
        if (Request.labyModCommitRef is not null)
        {
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "LabyMod", Request.labyModCommitRef),
                McDownloadLabyModLoader(Request.labyModCommitRef, Request.labyModChannel, Request.minecraftName,
                    tempMcFolder, false)) { show = false, ProgressWeight = 10d, block = true });
            goto LabyModSkip;
        }

        // 原版
        var clientLoader = new ModLoader.LoaderCombo<string>(
            Lang.Text(
                "Minecraft.Download.Stage.LoaderDownloadCombo",
                Lang.Text("Minecraft.Version.Vanilla"),
                Request.minecraftName
            ),
            McDownloadClientLoader(
                Request.minecraftName, Request.minecraftJson, Request.targetInstanceName
            )
        )
        {
            show = false,
            ProgressWeight = 39d,
            block = Request.forgeVersion is null && Request.neoForgeVersion is null && Request.optiFineEntry is null &&
                    Request.fabricVersion is null && Request.liteLoaderEntry is null && Request.quiltVersion is null &&
                    Request.cleanroomEntry is null && Request.legacyFabricVersion is null
        };
        loaderList.Add(clientLoader);
        // OptiFine
        if (Request.optiFineEntry is not null)
        {
            if (optiFineAsMod)
                loaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "OptiFine",
                        Request.optiFineEntry.displayName),
                    McDownloadOptiFineSaveLoader(Request.optiFineEntry,
                        Path.Combine(optiFineFolder, Request.optiFineEntry.nameFile)))
                {
                    show = false,
                    ProgressWeight = 16d,
                    block = Request.forgeVersion is null && Request.neoForgeVersion is null &&
                            Request.fabricVersion is null && Request.liteLoaderEntry is null
                });
            else
                loaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "OptiFine",
                        Request.optiFineEntry.displayName),
                    McDownloadOptiFineLoader(Request.optiFineEntry, tempMcFolder, clientLoader,
                        Request.targetInstanceFolder, false))
                {
                    show = false,
                    ProgressWeight = 24d,
                    block = Request.forgeVersion is null && Request.neoForgeVersion is null &&
                            Request.fabricVersion is null && Request.liteLoaderEntry is null
                });
        }

        // Forge
        if (Request.forgeVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Forge", Request.forgeVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.Forge, Request.forgeVersion, "forge-" + Request.forgeVersion,
                    Request.minecraftName, Request.forgeEntry, tempMcFolder, clientLoader,
                    Request.targetInstanceFolder))
            {
                show = false, ProgressWeight = 25d,
                block = Request.fabricVersion is null && Request.liteLoaderEntry is null &&
                        Request.neoForgeEntry is null
            });
        // NeoForge
        if (Request.neoForgeVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "NeoForge", Request.neoForgeVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge, Request.neoForgeVersion, "neoforge-" + Request.neoForgeVersion,
                    Request.minecraftName, Request.neoForgeEntry, tempMcFolder, clientLoader,
                    Request.targetInstanceFolder))
            {
                show = false, ProgressWeight = 25d,
                block = Request.forgeEntry is null && Request.fabricVersion is null && Request.liteLoaderEntry is null
            });
        // Cleanroom
        if (Request.cleanroomVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Cleanroom", Request.cleanroomVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom, Request.cleanroomVersion,
                    "cleanroom-" + Request.cleanroomVersion, Request.minecraftName, Request.cleanroomEntry,
                    tempMcFolder, clientLoader, Request.targetInstanceFolder))
            {
                show = false, ProgressWeight = 25d,
                block = Request.forgeEntry is null && Request.fabricVersion is null && Request.liteLoaderEntry is null
            });
        // LiteLoader
        if (Request.liteLoaderEntry is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "LiteLoader", Request.minecraftName),
                McDownloadLiteLoaderLoader(Request.liteLoaderEntry, tempMcFolder, clientLoader, false))
            {
                show = false,
                ProgressWeight = 1d,
                block = Request.fabricVersion is null
            });
        // Fabric
        if (Request.fabricVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Fabric", Request.fabricVersion),
                McDownloadFabricLoader(Request.fabricVersion, Request.minecraftName, tempMcFolder, false))
            {
                show = false,
                ProgressWeight = 2d,
                block = true
            });
        // LegacyFabric
        if (Request.legacyFabricVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Legacy Fabric", Request.legacyFabricVersion),
                McDownloadLegacyFabricLoader(Request.legacyFabricVersion, Request.minecraftName, tempMcFolder, false))
            {
                show = false,
                ProgressWeight = 2d,
                block = true
            });
        // Quilt
        if (Request.quiltVersion is not null)
            loaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Quilt", Request.quiltVersion),
                    McDownloadQuiltLoader(Request.quiltVersion, Request.minecraftName, tempMcFolder, false))
                { show = false, ProgressWeight = 2d, block = true });

        LabyModSkip: ;

        // 合并安装
        loaderList.Add(new ModLoader.LoaderTask<string, string>(Lang.Text("Minecraft.Download.Stage.InstallGame"),
            Task =>
        {
            // 合并 JSON
            MergeJson(instanceFolder, instanceFolder, optiFineFolder, optiFineAsMod, forgeFolder, Request.forgeVersion,
                neoForgeFolder, Request.neoForgeVersion, cleanroomFolder, Request.cleanroomVersion, fabricFolder,
                quiltFolder, labyModFolder, Request.labyModChannel, liteLoaderFolder, Request.mmcPackInfo,
                legacyFabricFolder);
            Task.Progress = 0.2d;
            // 迁移文件
            if (Directory.Exists(Path.Combine(tempMcFolder, "libraries")))
                ModBase.CopyDirectory(Path.Combine(tempMcFolder, "libraries"), Path.Combine(ModMinecraft.mcFolderSelected, "libraries"));
            Task.Progress = 0.8d;
            // 创建 Mod 和资源包文件夹
            var modsFolder = Path.Combine(new ModMinecraft.McInstance(instanceFolder).PathIndie, "mods"); // 版本隔离信息在此时被决定
            if (Directory.Exists(modsTempFolder))
            {
                ModBase.CopyDirectory(modsTempFolder, modsFolder);
            }
            else if (modable)
            {
                Directory.CreateDirectory(modsFolder);
                ModBase.Log("[Download] 自动创建 Mod 文件夹：" + modsFolder);
            }

            var resourcepacksFolder = Path.Combine(new ModMinecraft.McInstance(instanceFolder).PathIndie, "resourcepacks");
            Directory.CreateDirectory(resourcepacksFolder);
            ModBase.Log("[Download] 自动创建资源包文件夹：" + resourcepacksFolder);
        })
        {
            ProgressWeight = 2d,
            block = true
        });
        // 补全文件
        if (!DontFixLibraries && (Request.optiFineEntry is not null ||
                                  (Request.forgeVersion is not null &&
                                   Convert.ToDouble(Request.forgeVersion.BeforeFirst(".")) >= 20d) ||
                                  Request.neoForgeVersion is not null || Request.fabricVersion is not null ||
                                  Request.quiltVersion is not null || Request.cleanroomVersion is not null ||
                                  Request.liteLoaderEntry is not null || Request.labyModCommitRef is not null))
        {
            var loadersLib = new List<ModLoader.LoaderBase>();
            if (Request.labyModCommitRef is not null)
            {
                var labyModClientLoader = new ModLoader.LoaderCombo<string>(
                    Lang.Text(
                        "Minecraft.Download.Stage.LoaderDownloadCombo",
                        Lang.Text("Minecraft.Version.Vanilla"), Request.minecraftName
                    ),
                    McDownloadLabyModClientLoader(
                        Request.minecraftName, Request.labyModChannel,
                        Request.labyModCommitRef, Request.targetInstanceName
                    )
                )
                {
                    show = false, ProgressWeight = 39d, block = false
                };
                loaderList.Add(labyModClientLoader);
            }
            else
            {
                loadersLib.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                        Lang.Text("Minecraft.Download.Stage.AnalyzeGameLibrariesSide"),
                        Task => Task.output =
                            ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(instanceFolder)))
                    { ProgressWeight = 1d, show = false });
                loadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadGameLibrariesSide"),
                        new List<DownloadFile>())
                    { ProgressWeight = 7d, show = false });
                loaderList.Add(
                    new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadGameLibraries"),
                        loadersLib) { ProgressWeight = 8d });
            }
        }

        // 删除忽略标识
        loaderList.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
                _ => File.Delete(Path.Combine(instanceFolder, ".pclignore")))
            { show = false });
        // 总加载器
        return loaderList;
    }

    /// <summary>
    ///     将多个实例 JSON 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。
    /// </summary>
    private static void MergeJson(string OutputFolder, string MinecraftFolder, string OptiFineFolder = null,
        bool OptiFineAsMod = false, string ForgeFolder = null, string ForgeVersion = null, string NeoForgeFolder = null,
        string NeoForgeVersion = null, string CleanroomFolder = null, string CleanroomVersion = null,
        string FabricFolder = null, string QuiltFolder = null, string LabyModFolder = null,
        string LabyModChannel = null, string LiteLoaderFolder = null, ModModpack.MMCPackInfo MMCPackInfo = null,
        string LegacyFabricFolder = null)
    {
        ModBase.Log("[Download] 开始进行实例合并，输出：" + OutputFolder + "，Minecraft：" + MinecraftFolder +
                    (OptiFineFolder is not null ? "，OptiFine：" + OptiFineFolder : "") +
                    (ForgeFolder is not null ? "，Forge：" + ForgeFolder : "") +
                    (NeoForgeFolder is not null ? "，NeoForge：" + NeoForgeFolder : "") +
                    (CleanroomFolder is not null ? "，Cleanroom：" + CleanroomFolder : "") +
                    (LiteLoaderFolder is not null ? "，LiteLoader：" + LiteLoaderFolder : "") +
                    (FabricFolder is not null ? "，Fabric：" + FabricFolder : "") +
                    (LegacyFabricFolder is not null ? "，LegacyFabric：" + LegacyFabricFolder : "") +
                    (QuiltFolder is not null ? "，Quilt：" + QuiltFolder : "") +
                    (LabyModFolder is not null ? "，LabyMod：" + LabyModFolder : ""));
        Directory.CreateDirectory(OutputFolder);

        var hasOptiFine = OptiFineFolder is not null && !OptiFineAsMod;
        var hasForge = ForgeFolder is not null;
        var hasLegacyFabric = LegacyFabricFolder is not null;
        var hasNeoForge = NeoForgeFolder is not null;
        var hasCleanroom = CleanroomFolder is not null;
        var hasLiteLoader = LiteLoaderFolder is not null;
        var hasFabric = FabricFolder is not null;
        var hasQuilt = QuiltFolder is not null;
        var hasLabyMod = LabyModFolder is not null;
        string outputName;
        string minecraftName;
        string optiFineName;
        string forgeName;
        string neoForgeName;
        string cleanroomName;
        string liteLoaderName;
        string fabricName;
        string legacyFabricName;
        string quiltName;
        string labyModName;
        string outputJsonPath;
        string minecraftJsonPath;
        string optiFineJsonPath = null;
        string forgeJsonPath = null;
        string neoForgeJsonPath = null;
        string cleanroomJsonPath = null;
        string liteLoaderJsonPath = null;
        string fabricJsonPath = null;
        string quiltJsonPath = null;
        string labyModJsonPath = null;
        string legacyFabricJsonPath = null;
        string outputJar;
        string minecraftJar;

        #region 初始化路径信息

        if (!OutputFolder.EndsWithF(@"\"))
            OutputFolder += @"\";
        outputName = ModBase.GetFolderNameFromPath(OutputFolder);
        outputJsonPath = Path.Combine(OutputFolder, outputName + ".json");
        outputJar = Path.Combine(OutputFolder, outputName + ".jar");

        if (!MinecraftFolder.EndsWithF(@"\"))
            MinecraftFolder += @"\";
        minecraftName = ModBase.GetFolderNameFromPath(MinecraftFolder);
        minecraftJsonPath = Path.Combine(MinecraftFolder, minecraftName + ".json");
        minecraftJar = Path.Combine(MinecraftFolder, minecraftName + ".jar");

        if (hasOptiFine)
        {
            if (!OptiFineFolder.EndsWithF(@"\"))
                OptiFineFolder += @"\";
            optiFineName = ModBase.GetFolderNameFromPath(OptiFineFolder);
            optiFineJsonPath = Path.Combine(OptiFineFolder, optiFineName + ".json");
        }

        if (hasForge)
        {
            if (!ForgeFolder.EndsWithF(@"\"))
                ForgeFolder += @"\";
            forgeName = ModBase.GetFolderNameFromPath(ForgeFolder);
            forgeJsonPath = Path.Combine(ForgeFolder, forgeName + ".json");
        }

        if (hasNeoForge)
        {
            if (!NeoForgeFolder.EndsWithF(@"\"))
                NeoForgeFolder += @"\";
            neoForgeName = ModBase.GetFolderNameFromPath(NeoForgeFolder);
            neoForgeJsonPath = Path.Combine(NeoForgeFolder, neoForgeName + ".json");
        }

        if (hasCleanroom)
        {
            if (!CleanroomFolder.EndsWithF(@"\"))
                CleanroomFolder += @"\";
            cleanroomName = ModBase.GetFolderNameFromPath(CleanroomFolder);
            cleanroomJsonPath = Path.Combine(CleanroomFolder, cleanroomName + ".json");
        }

        if (hasLiteLoader)
        {
            if (!LiteLoaderFolder.EndsWithF(@"\"))
                LiteLoaderFolder += @"\";
            liteLoaderName = ModBase.GetFolderNameFromPath(LiteLoaderFolder);
            liteLoaderJsonPath = Path.Combine(LiteLoaderFolder, liteLoaderName + ".json");
        }

        if (hasFabric)
        {
            if (!FabricFolder.EndsWithF(@"\"))
                FabricFolder += @"\";
            fabricName = ModBase.GetFolderNameFromPath(FabricFolder);
            fabricJsonPath = Path.Combine(FabricFolder, fabricName + ".json");
        }

        if (hasLegacyFabric)
        {
            if (!LegacyFabricFolder.EndsWithF(@"\"))
                LegacyFabricFolder += @"\";
            legacyFabricName = ModBase.GetFolderNameFromPath(LegacyFabricFolder);
            legacyFabricJsonPath = Path.Combine(LegacyFabricFolder, legacyFabricName + ".json");
        }

        if (hasQuilt)
        {
            if (!QuiltFolder.EndsWithF(@"\"))
                QuiltFolder += @"\";
            quiltName = ModBase.GetFolderNameFromPath(QuiltFolder);
            quiltJsonPath = Path.Combine(QuiltFolder, quiltName + ".json");
        }

        if (hasLabyMod)
        {
            if (!LabyModFolder.EndsWithF(@"\"))
                LabyModFolder += @"\";
            labyModName = ModBase.GetFolderNameFromPath(LabyModFolder);
            labyModJsonPath = Path.Combine(LabyModFolder, labyModName + ".json");
        }

        #endregion

        JsonObject outputJson;
        JsonObject minecraftJson = null;
        JsonObject optiFineJson = null;
        JsonObject forgeJson = null;
        JsonObject neoForgeJson = null;
        JsonObject legacyFabricJson = null;
        JsonObject cleanroomJson = null;
        JsonObject liteLoaderJson = null;
        JsonObject fabricJson = null;
        JsonObject quiltJson = null;
        JsonObject labyModJson = null;

        #region 读取文件并检查文件是否合规

        var minecraftJsonText = ModBase.ReadFile(minecraftJsonPath);
        if (!hasLabyMod)
        {
            if (!minecraftJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Minecraft", minecraftJsonPath,
                    minecraftJsonText.Substring(0, Math.Min(minecraftJsonText.Length, 1000))));
            minecraftJson = (JsonObject)ModBase.GetJson(minecraftJsonText);
        }

        if (hasOptiFine)
        {
            var optiFineJsonText = ModBase.ReadFile(optiFineJsonPath);
            if (!optiFineJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "OptiFine", optiFineJsonPath,
                    optiFineJsonText.Substring(0, Math.Min(optiFineJsonText.Length, 1000))));
            optiFineJson = (JsonObject)ModBase.GetJson(optiFineJsonText);
        }

        if (hasForge)
        {
            var forgeJsonText = ModBase.ReadFile(forgeJsonPath);
            if (!forgeJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Forge", forgeJsonPath,
                    forgeJsonText.Substring(0, Math.Min(forgeJsonText.Length, 1000))));
            forgeJson = (JsonObject)ModBase.GetJson(forgeJsonText);
        }

        if (hasNeoForge)
        {
            var neoForgeJsonText = ModBase.ReadFile(neoForgeJsonPath);
            if (!neoForgeJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "NeoForge", neoForgeJsonPath,
                    neoForgeJsonText.Substring(0, Math.Min(neoForgeJsonText.Length, 1000))));
            neoForgeJson = (JsonObject)ModBase.GetJson(neoForgeJsonText);
        }

        if (hasCleanroom)
        {
            var cleanroomJsonText = ModBase.ReadFile(cleanroomJsonPath);
            if (!cleanroomJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Cleanroom", cleanroomJsonPath,
                    cleanroomJsonText.Substring(0, Math.Min(cleanroomJsonText.Length, 1000))));
            cleanroomJson = (JsonObject)ModBase.GetJson(cleanroomJsonText);
        }

        if (hasLiteLoader)
        {
            var liteLoaderJsonText = ModBase.ReadFile(liteLoaderJsonPath);
            if (!liteLoaderJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "LiteLoader", liteLoaderJsonPath,
                    liteLoaderJsonText.Substring(0, Math.Min(liteLoaderJsonText.Length, 1000))));
            liteLoaderJson = (JsonObject)ModBase.GetJson(liteLoaderJsonText);
        }

        if (hasFabric)
        {
            var fabricJsonText = ModBase.ReadFile(fabricJsonPath);
            if (!fabricJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Fabric", fabricJsonPath,
                    fabricJsonText.Substring(0, Math.Min(fabricJsonText.Length, 1000))));
            fabricJson = (JsonObject)ModBase.GetJson(fabricJsonText);
        }

        if (hasLegacyFabric)
        {
            var legacyFabricJsonText = ModBase.ReadFile(legacyFabricJsonPath);
            if (!legacyFabricJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Legacy Fabric", fabricJsonPath,
                    legacyFabricJsonText.Substring(0, Math.Min(legacyFabricJsonText.Length, 1000))));
            legacyFabricJson = (JsonObject)ModBase.GetJson(legacyFabricJsonText);
        }

        if (hasQuilt)
        {
            var quiltJsonText = ModBase.ReadFile(quiltJsonPath);
            if (!quiltJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Quilt", quiltJsonPath,
                    quiltJsonText.Substring(0, Math.Min(quiltJsonText.Length, 1000))));
            quiltJson = (JsonObject)ModBase.GetJson(quiltJsonText);
        }

        if (hasLabyMod)
        {
            var labyModJsonText = ModBase.ReadFile(labyModJsonPath);
            if (!labyModJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "LabyMod", labyModJsonPath,
                    labyModJsonText.Substring(0, Math.Min(labyModJsonText.Length, 1000))));
            labyModJson = (JsonObject)ModBase.GetJson(labyModJsonText);
        }

        #endregion

        #region 处理 JSON 文件

        // 获取 minecraftArguments
        var allArguments = (minecraftJson is not null ? (minecraftJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (labyModJson is not null ? (labyModJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (optiFineJson is not null ? (optiFineJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " + (forgeJson is not null ? (forgeJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (neoForgeJson is not null ? (neoForgeJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " + (liteLoaderJson is not null
                               ? (liteLoaderJson["minecraftArguments"] ?? " ").ToString()
                               : " ") + " " + (cleanroomJson is not null
                               ? (cleanroomJson["minecraftArguments"] ?? " ").ToString()
                               : " ");
        // 分割参数字符串
        var rawArguments = allArguments.Split(" ").Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Trim()).ToList();
        var splitArguments = new List<string>();
        for (int i = 0, loopTo = rawArguments.Count - 1; i <= loopTo; i++)
            if (rawArguments[i].StartsWithF("-"))
                splitArguments.Add(rawArguments[i]);
            else if (splitArguments.Any() && splitArguments.Last().StartsWithF("-") &&
                     !splitArguments.Last().Contains(" "))
                splitArguments[splitArguments.Count - 1] = splitArguments.Last() + " " + rawArguments[i];
            else
                splitArguments.Add(rawArguments[i]);

        var realArguments = splitArguments.Distinct().ToList().Join(" ");
        // 合并
        // 相关讨论见 #2801
        if (MMCPackInfo is not null)
        {
            if (MMCPackInfo.isMinecraftOverrided)
            {
                ModBase.Log("[Download] 当前实例的 MC 核心已被修改，使用对应的 MMC 整合包参数");
                outputJson = MMCPackInfo.overridedJson;
            }
            else
            {
                ModBase.Log("[Download] 存在无修改 MC 核心文件的 MMC 整合包信息，应用相关参数");
                outputJson = minecraftJson;
                // 合并来自 MultiMC 的 JSON
                outputJson.Merge(MMCPackInfo.overridedJson);
            }
        }
        else
        {
            outputJson = minecraftJson;
        }

        if (hasOptiFine)
        {
            // 合并 OptiFine
            optiFineJson.Remove("releaseTime");
            optiFineJson.Remove("time");
            outputJson.Merge(optiFineJson);
        }

        if (hasForge)
            if (MMCPackInfo is null || !MMCPackInfo.isForgeOverrided)
            {
                // 合并 Forge
                forgeJson.Remove("releaseTime");
                forgeJson.Remove("time");
                outputJson.Merge(forgeJson);
            }

        if (hasNeoForge)
            if (MMCPackInfo is null || !MMCPackInfo.isNeoForgeOverrided)
            {
                // 合并 NeoForge
                neoForgeJson.Remove("releaseTime");
                neoForgeJson.Remove("time");
                outputJson.Merge(neoForgeJson);
            }

        if (hasCleanroom)
            if (MMCPackInfo is null || !MMCPackInfo.isCleanroomOverrided)
            {
                // 合并 Cleanroom
                cleanroomJson.Remove("releaseTime");
                cleanroomJson.Remove("time");
                outputJson.Merge(cleanroomJson);
            }

        if (hasLiteLoader)
        {
            // 合并 LiteLoader
            liteLoaderJson.Remove("releaseTime");
            liteLoaderJson.Remove("time");
            outputJson.Merge(liteLoaderJson);
        }

        if (hasFabric)
            if (MMCPackInfo is null || !MMCPackInfo.isFabricOverrided)
            {
                // 合并 Fabric
                fabricJson.Remove("releaseTime");
                fabricJson.Remove("time");
                outputJson.Merge(fabricJson);
            }

        if (hasLegacyFabric)
            if (MMCPackInfo is null || !MMCPackInfo.isFabricOverrided)
            {
                // 合并 Fabric
                legacyFabricJson.Remove("releaseTime");
                legacyFabricJson.Remove("time");
                outputJson.Merge(legacyFabricJson);
            }

        if (hasQuilt)
            if (MMCPackInfo is null || !MMCPackInfo.isQuiltOverrided)
            {
                // 合并 Quilt
                quiltJson.Remove("releaseTime");
                quiltJson.Remove("time");
                outputJson.Merge(quiltJson);
            }

        if (hasLabyMod)
        {
            // 合并 LabyMod
            labyModJson.Remove("releaseTime");
            labyModJson.Remove("time");
            if (outputJson is null)
                outputJson = new JsonObject();
            outputJson.Merge(labyModJson);

            var labyModLib =
                (JsonObject)Requester.FetchJson(
                    $"https://releases.r2.labymod.net/api/v1/libraries/{LabyModChannel}.json", RequestParam.WithRetry);
            var labyModCore = (JsonObject)Requester.FetchJson(
                $"https://releases.r2.labymod.net/api/v1/manifest/{LabyModChannel}/latest.json", RequestParam.WithRetry);
            var outputLibraries = new JsonArray();
            var isolatedLibraries = new Dictionary<string, bool>();
            var minecraftVersion = labyModJson["_minecraftVersion"];

            foreach (var Library in labyModLib["isolated_libraries"].AsArray())
                if (((JsonArray)Library["versions"]).Contains(minecraftVersion))
                    isolatedLibraries.Add(Library["name"].ToString(), true);

            foreach (var Library in labyModJson["libraries"].AsArray())
            {
                var regexMatchResult = Library["name"].ToString().RegexSeek(RegexPatterns.CatchLwjglInLib);
                if (regexMatchResult is null ||
                    !isolatedLibraries.Contains(new KeyValuePair<string, bool>(regexMatchResult, true)))
                    outputLibraries.Add(Library);
            }

            foreach (var Library in labyModLib["libraries"].AsArray())
            {
                var libraryUrl = Library?["url"]?.ToString() ?? "";
                outputLibraries.Add(new JsonObject
                {
                    ["name"] = Library?["name"]?.ToString(),
                    ["downloads"] = new JsonObject
                    {
                        ["artifact"] = new JsonObject
                        {
                            ["path"] = libraryUrl.Substring(libraryUrl.LastIndexOfF("https://releases.r2.labymod.net/libraries/") + 42),
                            ["sha1"] = Library?["sha1"]?.ToString(),
                            ["size"] = Library?["size"]?.DeepClone(),
                            ["url"] = libraryUrl
                        }
                    }
                });
            }

            var labyModCommitReference = labyModCore["commitReference"]?.ToString() ?? "";
            outputLibraries.Add(new JsonObject
            {
                ["name"] = "net.labymod:LabyMod:4",
                ["downloads"] = new JsonObject
                {
                    ["artifact"] = new JsonObject
                    {
                        ["path"] = "net/labymod/LabyMod/4/LabyMod-4.jar",
                        ["sha1"] = labyModCore["sha1"]?.ToString(),
                        ["size"] = labyModCore["size"]?.DeepClone(),
                        ["url"] = $"https://releases.r2.labymod.net/api/v1/download/labymod4/{LabyModChannel}/{labyModCommitReference}.jar"
                    }
                }
            });
            outputJson["libraries"] = outputLibraries;
            outputJson.Add("labymod_data", new JsonObject
            {
                ["channelType"] = LabyModChannel,
                ["commitReference"] = labyModCommitReference,
                ["version"] = labyModCore["labyModVersion"]?.ToString(),
                ["versionType"] = "release"
            });
        }

        // 修改
        if (realArguments is not null && !string.IsNullOrEmpty(realArguments.Replace(" ", "")))
            outputJson["minecraftArguments"] = realArguments;
        if (MMCPackInfo is not null && MMCPackInfo.isMcArgsEdited)
            outputJson.Remove("minecraftArguments");
        outputJson.Remove("_comment_");
        outputJson.Remove("inheritsFrom");
        outputJson.Remove("jar");
        outputJson["id"] = outputName;

        #endregion

        #region 保存

        ModBase.WriteFile(outputJsonPath, outputJson.ToString());
        if ((minecraftJar ?? "") != (outputJar ?? "")) // 可能是同一个文件
        {
            if (File.Exists(outputJar))
                File.Delete(outputJar);
            ModBase.CopyFile(minecraftJar, outputJar);
        }

        ModBase.Log("[Download] 实例合并 " + outputName + " 完成");

        #endregion
    }

    #endregion
}
