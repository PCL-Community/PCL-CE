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
    private static readonly object InstallSyncLock = new();

    /// <summary>
    ///     如果 OptiFine 与 Forge 同时复制原版 Jar，就会导致复制文件时冲突。
    /// </summary>
    private static readonly object VanillaSyncLock = new();

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
            var versionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", id);

            // 重复任务检查
            foreach (var ongoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if (ongoingLoader.Name != Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id))
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
            var Loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id),
                        McDownloadClientLoader(id, jsonUrl))
                    { OnStateChanged = McInstallState };
            Loader.Start(versionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            return Loader;
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
            var VersionFolder = SystemDialogs.SelectFolder();
            if (!VersionFolder.Contains(@"\"))
                return;
            VersionFolder = Path.Combine(VersionFolder, Id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
            {
                if ((OngoingLoader.Name ?? "") != (Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id) ?? ""))
                    continue;
                if (Behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 Json 文件
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), Path.Combine(VersionFolder, Id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 获取支持库文件地址
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 0.5d, Show = false });
            // 下载支持库文件
            Loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), new List<DownloadFile>())
                    { ProgressWeight = 5d });

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(Id);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        var instanceFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", instanceName);

        var loaders = new List<ModLoader.LoaderBase>();

        // 下载实例 Json 文件
        if (jsonUrl is null)
            loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainVanillaJsonUrl"), task =>
            {
                var jsonAddress = ModDownload.DlClientListGet(id)?.ToString();
                task.Output = new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(jsonAddress), Path.Combine(instanceFolder, instanceName + ".json"))
                };
            })
            {
                ProgressWeight = 2d,
                Show = false
            });
        loaders.Add(new LoaderDownload(McDownloadClientJsonName,
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

            task.Output = ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(instanceFolder));
        })
        {
            ProgressWeight = 1d,
            Show = false
        });
        loadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadVanillaLibraries.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 13d, Show = false });
        loaders.Add(new ModLoader.LoaderCombo<string>(McDownloadClientLibName, loadersLib)
            { Block = false, ProgressWeight = 14d });

        // 下载资源文件
        var loadersAssets = new List<ModLoader.LoaderBase>();
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"), task =>
        {
            ModBase.WaitForFileReady(Path.Combine(instanceFolder, instanceName + ".json"));
            try
            {
                var assetIndex = new ModMinecraft.McInstance(instanceFolder);
                task.Output = new List<DownloadFile> { ModDownload.DlClientAssetIndexGet(assetIndex) };
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
            Show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 3d, Show = false });
        loadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"), task =>
        {
            ModLoader.LoaderBase argprogressFeed = task;
            task.Output =
                ModMinecraft.McAssetsFixList(new ModMinecraft.McInstance(instanceFolder), true, ref argprogressFeed);
            task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
        })
        {
            ProgressWeight = 0.01d,
            Show = false
        });
        loadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 14d, Show = false });
        loaders.Add(
            new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                loadersAssets) { Block = false, ProgressWeight = 18d });

        return loaders;
    }

    private static readonly string McDownloadClientLibName = Lang.Text("Minecraft.Download.Stage.VanillaLibrariesDownload");
    private static readonly string McDownloadClientJsonName = Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload");

    #endregion

    #region Minecraft 下载菜单

    public static MyListItem McDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 确定图标
        string Logo = Entry["type"].ToString() switch
        {
            "release" => ModBase.PathImage + "Blocks/Grass.png",
            "snapshot" => ModBase.PathImage + "Blocks/CommandBlock.png",
            "pending" => ModBase.PathImage + "Blocks/CommandBlock.png",
            "special" => ModBase.PathImage + "Blocks/GoldBlock.png",
            _ => ModBase.PathImage + "Blocks/CobbleStone.png"
        };

        // 建立控件
        var FormattedVersion = McFormatter.FormatVersion(Entry["id"].ToString()).Replace("_", " ");
        var NewItem = new MyListItem
        {
            Logo = Logo, SnapsToDevicePixels = true, Title = FormattedVersion, Height = 42d,
            Type = MyListItem.CheckType.Clickable, Tag = Entry
        };
        if (Entry["lore"] is null)
        {
            if (FormattedVersion != (string)Entry["id"])
                NewItem.Info = Lang.Date(Entry["releaseTime"].ToObject<DateTime>(), "g") + " | " +
                               Entry["id"];
            else
                NewItem.Info = Lang.Date(Entry["releaseTime"].ToObject<DateTime>(), "g");
        }
        else if (FormattedVersion != (string)Entry["id"])
        {
            NewItem.Info = Entry["lore"] + " | " + Entry["id"];
        }
        else
        {
            NewItem.Info = Entry["lore"].ToString();
        }

        if (Entry["url"].ToString().Contains("unlisted-versions-of-minecraft"))
            NewItem.Tags = Lang.Text("Download.Tag.Uvmc");
        NewItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            NewItem.ContentHandler = McDownloadSaveMenuBuild;
        else
            NewItem.ContentHandler = McDownloadMenuBuild;
        // 结束
        return NewItem;
    }

    private static void McDownloadSaveMenuBuild(object sender, EventArgs _)
    {
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (ss, ee) => McDownloadMenuLog(ss, (dynamic)ee);
        var BtnServer = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonServer, ToolTip = Lang.Text("Download.Version.DownloadServer") };
        ToolTipService.SetPlacement(BtnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnServer, 30d);
        ToolTipService.SetHorizontalOffset(BtnServer, 2d);
        BtnServer.Click += (ss, ee) => McDownloadMenuSaveServer(ss, (dynamic)ee);
        ((dynamic)sender).Buttons = new[] { BtnServer, BtnInfo };
    }

    private static void McDownloadMenuBuild(object sender, EventArgs e)
    {
        var BtnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(BtnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnSave, 30d);
        ToolTipService.SetHorizontalOffset(BtnSave, 2d);
        BtnSave.Click += (a, b) => McDownloadMenuSave(a, (dynamic)b); // dynamic!
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (a, b) => McDownloadMenuLog(a, (dynamic)b); // dynamic!
        var BtnServer = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonServer, ToolTip = Lang.Text("Download.Version.DownloadServer") };
        ToolTipService.SetPlacement(BtnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnServer, 30d);
        ToolTipService.SetHorizontalOffset(BtnServer, 2d);
        BtnServer.Click += (a, b) => McDownloadMenuSaveServer(a, (dynamic)b); // dynamic!
        ((dynamic)sender).Buttons = new[] { BtnSave, BtnInfo, BtnServer };
    }

    private static void McDownloadMenuLog(object sender, RoutedEventArgs e)
    {
        JsonNode Version;
        if (((dynamic)sender).Tag is not null)
            Version = (JsonNode)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Version = (JsonNode)((dynamic)sender).Parent.Tag;
        else
            Version = (JsonNode)((dynamic)sender).Parent.Parent.Tag;
        McUpdateLogShow(Version);
    }

    private static void McDownloadMenuSaveServer(object sender, RoutedEventArgs e)
    {
        MyListItem Version;
        if (sender is MyListItem)
            Version = (MyListItem)sender;
        else if (((dynamic)sender).Parent is MyListItem)
            Version = (MyListItem)((dynamic)sender).Parent;
        else
            Version = (MyListItem)((dynamic)sender).Parent.Parent;
        try
        {
            var Id = Version.Title;
            string JsonUrl = ((dynamic)Version.Tag)["url"].ToString();
            var VersionFolder = SystemDialogs.SelectFolder();
            if (!VersionFolder.Contains(@"\"))
                return;
            VersionFolder = Path.Combine(VersionFolder, Id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", Id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.ServerDownloading"), ModMain.HintType.Critical);
                return;
            }

            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 JSON 文件
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), Path.Combine(VersionFolder, Id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 构建服务端
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.BuildServer"), Task =>
            {
                // 分析服务端 JAR 文件下载地址
                var McInstance = new ModMinecraft.McInstance(VersionFolder);
                if (McInstance.JsonObject["downloads"] is null ||
                    McInstance.JsonObject["downloads"]["server"] is null ||
                    McInstance.JsonObject["downloads"]["server"]["url"] is null)
                {
                    File.Delete(Path.Combine(VersionFolder, Id + ".json"));
                    if (!new DirectoryInfo(VersionFolder).GetFileSystemInfos().Any())
                        Directory.Delete(VersionFolder);
                    Task.Output = new List<DownloadFile>();
                    ModMain.Hint(Lang.Text("Minecraft.Download.Error.NoOfficialServerDownload", Id),
                        ModMain.HintType.Critical);
                    Thread.Sleep(2000); // 等玩家把上一个提示看完
                    Task.Abort();
                    return;
                }

                var JarUrl = (string)McInstance.JsonObject["downloads"]["server"]["url"];
                var Checker = new ModBase.FileChecker(1024L,
                    (long)(McInstance.JsonObject["downloads"]["server"]["size"] ?? -1),
                    (string)McInstance.JsonObject["downloads"]["server"]["sha1"]);
                Task.Output = new List<DownloadFile>
                    { new(ModDownload.DlSourceLauncherOrMetaGet(JarUrl), Path.Combine(VersionFolder, Id + "-server.jar"), Checker) };
                // 添加启动脚本
                var Bat = $"""
                           @echo off
                           title {Lang.Text("Minecraft.Download.ServerBatch.Title", Id)}
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionJavaPath")}
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionPclSettings")}
                           echo ------------------------------
                           echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionEula")}
                           echo ------------------------------
                           "java" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar {Id}-server.jar nogui
                           echo ----------------------
                           echo {Lang.Text("Minecraft.Download.ServerBatch.ServerStopped")}
                           pause
                           """;
                ModBase.WriteFile(Path.Combine(VersionFolder, "Launch Server.bat"), Bat.Replace("\n", "\r\n"),
                    Encoding: Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encoding.GetEncoding("GB18030"));
                // 删除实例 JSON
                File.Delete(Path.Combine(VersionFolder, Id + ".json"));
            })
            {
                ProgressWeight = 0.5d,
                Show = false
            });
            // 下载服务端文件
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadServerFile"), [])
                { ProgressWeight = 5d });

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", Id),
                        Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(Id);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 Minecraft 服务端下载失败", ModBase.LogLevel.Feedback);
        }
    }

    public static void McDownloadMenuSave(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        MyListItem Version;
        if (element is MyListItem s1) Version = s1;
        else if (element.Parent is MyListItem s2) Version = s2;
        else Version = (MyListItem)((FrameworkElement)element.Parent).Parent;
        try
        {
            var Id = Version.Title;
            var JsonUrl = ((JsonObject)Version.Tag)["url"]!.ToString();
            var VersionFolder = SystemDialogs.SelectFolder();
            if (!VersionFolder.Contains(@"\"))
                return;
            VersionFolder = Path.Combine(VersionFolder, Id);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") != (Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载实例 JSON 文件
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                new List<DownloadFile>
                {
                    new(ModDownload.DlSourceLauncherOrMetaGet(JsonUrl), Path.Combine(VersionFolder, Id + ".json"),
                        new ModBase.FileChecker(CanUseExistsFile: false, IsJson: true))
                }) { ProgressWeight = 2d });
            // 获取支持库文件地址
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    Task => Task.Output = new List<DownloadFile>
                        { ModDownload.DlClientJarGet(new ModMinecraft.McInstance(VersionFolder), false) })
                { ProgressWeight = 0.5d, Show = false });
            // 下载支持库文件
            Loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), new List<DownloadFile>())
                    { ProgressWeight = 5d });

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.MinecraftDownload", Id), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(Id);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
            var Id = DownloadInfo.NameVersion;
            var VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", Id);
            var IsNewVersion = ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
            var Target = IsNewVersion
                ? Path.Combine(ModBase.PathTemp, "Cache", "Code", DownloadInfo.NameVersion + "_" + ModBase.GetUuid())
                : Path.Combine(ModMinecraft.McFolderSelected, "libraries", "optifine", "OptiFine",
                    DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", ""),
                    DownloadInfo.NameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", ""));

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.DisplayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 已有实例检查
            if (File.Exists(Path.Combine(VersionFolder, Id + ".json")))
            {
                if (ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", Id, "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")
                    ) == 1)
                {
                    File.Delete(Path.Combine(VersionFolder, Id + ".jar"));
                    File.Delete(Path.Combine(VersionFolder, Id + ".json"));
                }
                else
                {
                    return;
                }
            }

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.DisplayName),
                    McDownloadOptiFineLoader(DownloadInfo)) { OnStateChanged = McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
            var Id = DownloadInfo.NameVersion;
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), DownloadInfo.NameFile, "OptiFine Jar (*.jar)|*.jar");
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.DisplayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            var Loader =
                new ModLoader.LoaderCombo<ModDownload.DlOptiFineListEntry>(
                        Lang.Text("Minecraft.Download.Stage.OptiFineDownload", DownloadInfo.DisplayName),
                        McDownloadOptiFineSaveLoader(DownloadInfo, Target))
                    { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始 OptiFine 下载失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void McDownloadOptiFineInstall(string BaseMcFolderHome, string Target, ModLoader.LoaderTask<List<DownloadFile>, bool> Task, bool UseJavaWrapper)
    {
        // 选择 Java
        JavaEntry Java;
        lock (ModJava.JavaLock)
        {
            Java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 0));
            if (Java is null)
            {
                if (!ModJava.JavaDownloadConfirm(Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
                // 开始自动下载
                var JavaLoader = ModJava.GetJavaDownloadLoader();
                try
                {
                    JavaLoader.Start(17, true);
                    while (JavaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                        Thread.Sleep(10);
                }
                finally
                {
                    JavaLoader.Abort(); // 确保取消时中止 Java 下载
                }

                // 检查下载结果
                Java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                    new Version(1, 8, 0, 0));
                if (Task.IsAborted)
                    return;
                if (Java is null)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
            }
        }

        // 添加 Java Wrapper 作为主 Jar
        string Arguments;
        if (UseJavaWrapper &&
                                  !(dynamic)Config.Launch.DisableJlw) // dynamic!
            Arguments =
                $"-Doolloo.jlw.tmpdir=\"{ModBase.PathPure.TrimEnd('\\')}\" -Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" -jar \"{ModLaunch.ExtractJavaWrapper()}\" optifine.Installer";
        else
            Arguments = $"-Duser.home=\"{BaseMcFolderHome.TrimEnd('\\')}\" -cp \"{Target}\" optifine.Installer";
        if (Java.Installation.MajorVersion >= 9)
            Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + Arguments;
        // 开始启动
        lock (InstallSyncLock)
        {
            var Info = new ProcessStartInfo
            {
                FileName = Java.Installation.JavaExePath,
                Arguments = Arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = ModBase.ShortenPath(BaseMcFolderHome)
            };
            if (Info.EnvironmentVariables.ContainsKey("appdata"))
                Info.EnvironmentVariables["appdata"] = BaseMcFolderHome;
            else
                Info.EnvironmentVariables.Add("appdata", BaseMcFolderHome);
            ModBase.Log("[Download] 开始安装 OptiFine：" + Target);
            var TotalLength = 0;
            var process = new Process { StartInfo = Info };
            var LastResult = "";
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
                                LastResult = e.Data;
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Installer] " + LastResult);
                                TotalLength += 1;
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
                                LastResult = e.Data;
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Installer] " + LastResult);
                                TotalLength += 1;
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
                    if (TotalLength < 1000 || LastResult.Contains("at "))
                        throw new Exception(Lang.Text("Minecraft.Download.Error.InstallerFailedLastLine", LastResult));
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = DownloadInfo.NameVersion;
        var VersionFolder = Path.Combine(McFolder, "versions", Id);
        var IsNewVersion = DownloadInfo.Inherit.Contains("w") || ModBase.Val(DownloadInfo.Inherit.Split(".")[1]) >= 14d;
        var Target = IsNewVersion
            ? $"{ModMain.RequestTaskTempFolder()}OptiFine.jar"
            : $@"{McFolder}libraries\optifine\OptiFine\{DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "")}\{DownloadInfo.NameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")}";
        var Loaders = new List<ModLoader.LoaderBase>();

        // 获取下载地址
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainOptiFineUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (ClientDownloadLoader is null)
            {
                if (IsCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                    DownloadInfo.Inherit);
            }

            Task.Progress = 0.1d;
            var Sources = new List<string>();
            // BMCLAPI 源
            var BmclapiInherit = DownloadInfo.Inherit;
            if (BmclapiInherit == "1.8" || BmclapiInherit == "1.9")
                BmclapiInherit += ".0"; // #4281
            if (DownloadInfo.IsPreview)
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U_" +
                            DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit + " ", "").Replace(" ", "/"));
            else
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U/" +
                            DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit + " ", ""));
            // 官方源
            string PageData;
            try
            {
                using (var resp = HttpRequest
                           .Create("https://optifine.net/adloadx?f=" + DownloadInfo.NameFile)
                           .WithHeader("Accept", "text/html")
                           .WithHeader("Accept-Language", "en-US,en;q=0.5")
                           .WithHeader("X-Requested-With", "XMLHttpRequest")
                           .SendAsync()
                           .GetAwaiter()
                           .GetResult())
                {
                    resp.EnsureSuccessStatusCode();
                    PageData = resp.AsString();
                }
                Task.Progress = 0.8d;
                Sources.Add("https://optifine.net/" + PageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                ModBase.Log("[Download] OptiFine " + DownloadInfo.DisplayName + " 官方下载地址：" + Sources.Last());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 OptiFine " + DownloadInfo.DisplayName + " 官方下载地址失败");
            }

            // 构造文件请求
            Task.Output = new List<DownloadFile>
                { new(Sources.ToArray(), Target, new ModBase.FileChecker(300 * 1024)) };
        })
        {
            ProgressWeight = 8d
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"), [])
            { ProgressWeight = 8d });
        Loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
            Lang.Text("Minecraft.Download.Stage.WaitVanillaDownload"), Task =>
        {
            // 等待原版文件下载完成
            if (ClientDownloadLoader is null)
                return;
            var TargetLoaders = ClientDownloadLoader.GetLoaderList()
                .Where(l => (l.Name ?? "") == McDownloadClientLibName || (l.Name ?? "") == McDownloadClientJsonName)
                .Where(l => l.State != ModBase.LoadState.Finished).ToList();
            if (TargetLoaders.Any())
                ModBase.Log("[Download] OptiFine 安装正在等待原版文件下载完成");
            while (TargetLoaders.Any() && !Task.IsAborted)
            {
                TargetLoaders = TargetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                Thread.Sleep(50);
            }

            if (Task.IsAborted)
                return;
            // 拷贝原版文件
            if (!IsCustomFolder)
                return;
            lock (VanillaSyncLock)
            {
                var ClientName = ModBase.GetFolderNameFromPath(ClientFolder);
                Directory.CreateDirectory(Path.Combine(McFolder, "versions", DownloadInfo.Inherit));
                if (!File.Exists(Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json")))
                    ModBase.CopyFile($"{ClientFolder}{ClientName}.json",
                        $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.json");
                if (!File.Exists(Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar")))
                    ModBase.CopyFile($"{ClientFolder}{ClientName}.jar",
                        $@"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.jar");
            }
        })
        {
            ProgressWeight = 0.1d,
            Show = false
        });

        // 安装（新旧方式均需要原版 Jar 和 Json）
        if (IsNewVersion)
        {
            ModBase.Log("[Download] 检测为新版 OptiFine：" + DownloadInfo.Inherit);
            Loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodA"), Task =>
            {
                var BaseMcFolderHome = ModMain.RequestTaskTempFolder();
                var BaseMcFolder = Path.Combine(BaseMcFolderHome, ".minecraft");
                try
                {
                    // 准备安装环境
                    if (Directory.Exists(Path.Combine(BaseMcFolder, "versions", DownloadInfo.Inherit)))
                        ModBase.DeleteDirectory(Path.Combine(BaseMcFolder, "versions", DownloadInfo.Inherit));
                    Directory.CreateDirectory(Path.Combine(BaseMcFolder, "versions", DownloadInfo.Inherit));
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(BaseMcFolder);
                    ModBase.CopyFile(
                        Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json"),
                        Path.Combine(BaseMcFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".json"));
                    ModBase.CopyFile(
                        Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"),
                        Path.Combine(BaseMcFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"));
                    Task.Progress = 0.06d;
                    // 进行安装
                    var UseJavaWrapper = ModBase.IsUtf8CodePage();
                    Retry: ;

                    try
                    {
                        McDownloadOptiFineInstall(BaseMcFolderHome, Target, Task, UseJavaWrapper);
                    }
                    catch (Exception ex)
                    {
                        if (!UseJavaWrapper)
                        {
                            ModBase.Log(ex, "不使用 JavaWrapper 安装 OptiFine 失败，将使用 JavaWrapper 并重试");
                            UseJavaWrapper = true;
                            goto Retry;
                        }

                        throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineInstallerRunFailed"), ex);
                    }

                    Task.Progress = 0.96d;
                    // 复制文件
                    File.Delete(Path.Combine(BaseMcFolder, "launcher_profiles.json"));
                    ModBase.CopyDirectory(BaseMcFolder, McFolder);
                    Task.Progress = 0.98d;
                    // 清理文件
                    File.Delete(Target);
                    ModBase.DeleteDirectory(BaseMcFolderHome);
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
            Loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodB"), Task =>
                {
                    try
                    {
                        Directory.CreateDirectory(VersionFolder);
                        Task.Progress = 0.1d;
                        if (File.Exists(Path.Combine(VersionFolder, Id + ".jar"))) File.Delete(Path.Combine(VersionFolder, Id + ".jar"));
                        ModBase.CopyFile(
                            Path.Combine(McFolder, "versions", DownloadInfo.Inherit, DownloadInfo.Inherit + ".jar"),
                            Path.Combine(VersionFolder, Id + ".jar"));
                        Task.Progress = 0.7d;
                        var InheritInstance =
                            new ModMinecraft.McInstance(Path.Combine(McFolder, "versions", DownloadInfo.Inherit));
                        var Json = @"{
    ""id"": """ + Id + @""",
    ""inheritsFrom"": """ + DownloadInfo.Inherit + @""",
    ""time"": """ +
                                   (string.IsNullOrEmpty(DownloadInfo.ReleaseTime)
                                       ? InheritInstance.ReleaseTime.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)
                                       : DownloadInfo.ReleaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""releaseTime"": """ +
                                   (string.IsNullOrEmpty(DownloadInfo.ReleaseTime)
                                       ? InheritInstance.ReleaseTime.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)
                                       : DownloadInfo.ReleaseTime.Replace("/", "-")) + @"T23:33:33+08:00"",
    ""type"": ""release"",
    ""libraries"": [
        {""name"": ""optifine:OptiFine:" +
                                   DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "")
                                       .Replace("preview_", "") + // 输出旧版 Json 格式
                                   @"""},
        {""name"": ""net.minecraft:launchwrapper:1.12""}
    ],
    ""mainClass"": ""net.minecraft.launchwrapper.Launch"",";
                        Task.Progress = 0.8d;
                        if (InheritInstance.IsOldJson)
                            Json += @"
    ""minimumLauncherVersion"": 18,
    ""minecraftArguments"": """ + InheritInstance.JsonObject["minecraftArguments"] + // 输出新版 Json 格式
                                    @"  --tweakClass optifine.OptiFineTweaker""
}";
                        else
                            Json += @"
    ""minimumLauncherVersion"": ""21"",
    ""arguments"": {
        ""game"": [
            ""--tweakClass"",
            ""optifine.OptiFineTweaker""
        ]
    }
}";
                        ModBase.WriteFile(Path.Combine(VersionFolder, Id + ".json"), Json);
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
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeOptiFineLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineLibraries"),
                    new List<DownloadFile>())
                { ProgressWeight = 4d });
        }

        return Loaders;
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
                var BmclapiInherit = downloadInfo.Inherit;
                if (BmclapiInherit == "1.8" || BmclapiInherit == "1.9")
                    BmclapiInherit += ".0"; // #4281
                if (downloadInfo.IsPreview)
                    sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U_" +
                                downloadInfo.DisplayName.Replace(downloadInfo.Inherit + " ", "").Replace(" ", "/"));
                else
                    sources.Add("https://bmclapi2.bangbang93.com/optifine/" + BmclapiInherit + "/HD_U/" +
                                downloadInfo.DisplayName.Replace(downloadInfo.Inherit + " ", ""));
                // 官方源
                string PageData;
                try
                {
                    using (var resp = HttpRequest
                            .Create("https://optifine.net/adloadx?f=" + downloadInfo.NameFile)
                            .WithHeader("Accept", "text/html")
                            .WithHeader("Accept-Language", "en-US,en;q=0.5")
                            .WithHeader("X-Requested-With", "XMLHttpRequest")
                            .SendAsync().GetAwaiter().GetResult())
                    {
                        resp.EnsureSuccessStatusCode();
                        PageData = resp.AsString();
                    }
                    Task.Progress = 0.8d;
                    sources.Add("https://optifine.net/" + PageData.RegexSearch(@"downloadx\?f=[^""']+")[0]);
                    ModBase.Log("[Download] OptiFine " + downloadInfo.DisplayName + " 官方下载地址：" + sources.Last());
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取 OptiFine " + downloadInfo.DisplayName + " 官方下载地址失败");
                }

                Task.Progress = 0.9d;
                // 构造文件请求
                Task.Output = new List<DownloadFile>
                    { new(sources.ToArray(), targetFolder, new ModBase.FileChecker(64 * 1024)) };
            })
        {
            ProgressWeight = 6d
        });
        // 下载
        loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"),
                new List<DownloadFile>())
            { ProgressWeight = 10d, Block = true });
        return loaders;
    }

    #endregion

    #region OptiFine 下载菜单

    public static MyListItem OptiFineDownloadListItem(ModDownload.DlOptiFineListEntry Entry,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        var infoParts = new List<string>
        {
            Entry.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Release")
        };

        if (!string.IsNullOrEmpty(Entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.ReleaseTime));

        if (Entry.RequiredForgeVersion is null)
            infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(Entry.RequiredForgeVersion))
            infoParts.Add(Lang.Text("Download.Version.Optifine.CompatibleForge", Entry.RequiredForgeVersion));

        var NewItem = new MyListItem
        {
            Title = Entry.DisplayName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.PathImage + "Blocks/GrassPath.png"
        };

        NewItem.Click += OnClick;
        // 建立菜单
        NewItem.ContentHandler = IsSaveOnly
            ? OptiFineSaveContMenuBuild
            : OptiFineContMenuBuild;
        // 结束
        return NewItem;
    }

    private static void OptiFineSaveContMenuBuild(object sender, EventArgs e)
    {
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[] { BtnInfo };
    }

    private static void OptiFineContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        //btnSave.Click += () ModDownloadLib.OptiFineSave_Click;
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[] { btnSave, BtnInfo };
    }

    private static void OptiFineLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlOptiFineListEntry Version;
        if (((dynamic)sender).Tag is not null)
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite("https://optifine.net/changelog?f=" + Version.NameFile);
    }

    public static void OptiFineSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlOptiFineListEntry Version;
        if (((dynamic)sender).Tag is not null)
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            Version = (ModDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadOptiFineSave(Version);
    }

    #endregion

    #region LiteLoader 下载

    public static void McDownloadLiteLoader(ModDownload.DlLiteLoaderListEntry DownloadInfo)
    {
        try
        {
            var Id = DownloadInfo.Inherit;
            var Target = Path.Combine(ModBase.PathTemp, "Download", Id + "-Liteloader.jar");
            var VersionName = DownloadInfo.Inherit + "-LiteLoader";
            var VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", VersionName);

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") != (Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", Id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 已有实例检查
            if (File.Exists(Path.Combine(VersionFolder, VersionName + ".json")))
            {
                if (ModMain.MyMsgBox(
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", VersionName, "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")
                    ) == 1)
                {
                    File.Delete(Path.Combine(VersionFolder, VersionName + ".jar"));
                    File.Delete(Path.Combine(VersionFolder, VersionName + ".json"));
                }
                else
                {
                    return;
                }
            }

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", Id),
                        McDownloadLiteLoaderLoader(DownloadInfo))
                    { OnStateChanged = McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
            var Id = DownloadInfo.Inherit;
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), DownloadInfo.FileName.Replace("-SNAPSHOT", ""),
                Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", "LiteLoader", "jar"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") != (Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", Id) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var Address = new List<string>();
            if (DownloadInfo.IsLegacy)
                // 老版本
                switch (DownloadInfo.Inherit ?? "")
                {
                    case "1.7.10":
                    {
                        Address.Add("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar");
                        break;
                    }
                    case "1.7.2":
                    {
                        Address.Add("https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar");
                        break;
                    }
                    case "1.6.4":
                    {
                        Address.Add("https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar");
                        break;
                    }
                    case "1.6.2":
                    {
                        Address.Add("https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar");
                        break;
                    }
                    case "1.5.2":
                    {
                        Address.Add("https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar");
                        break;
                    }

                    default:
                    {
                        throw new NotSupportedException(Lang.Text("Minecraft.Download.Error.UnknownMinecraftVersion",
                            DownloadInfo.Inherit));
                    }
                }
            else
                // 官方源
                Address.Add("http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" + DownloadInfo.Inherit +
                            "/lastSuccessfulBuild/artifact/" +
                            (DownloadInfo.Inherit == "1.8" ? "ant/dist/" : "build/libs/") + DownloadInfo.FileName);

            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 1024)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<ModDownload.DlLiteLoaderListEntry>(
                        Lang.Text("Minecraft.Download.Stage.LiteLoaderInstallerDownload", Id), Loaders)
                    { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = DownloadInfo.Inherit;
        var Target = Path.Combine(ModBase.PathTemp, "Download", Id + "-Liteloader.jar");
        var VersionName = DownloadInfo.Inherit + "-LiteLoader";
        var VersionFolder = Path.Combine(McFolder, "versions", VersionName);
        var Loaders = new List<ModLoader.LoaderBase>();

        // 启动依赖实例的下载
        if (ClientDownloadLoader is null)
            Loaders.Add(new ModLoader.LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Stage.StartLiteLoaderDependencyDownload"), _ =>
            {
                if (IsCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                    DownloadInfo.Inherit);
            })
            {
                ProgressWeight = 0.2d,
                Show = false,
                Block = false
            });
        // 安装
        // 新建实例文件夹
        // 构造实例 Json
        // 输出 Json 文件
        Loaders.Add(new ModLoader.LoaderTask<string, string>(Lang.Text("Minecraft.Download.Stage.InstallLiteLoader"),
            _ =>
        {
            try
            {
                Directory.CreateDirectory(VersionFolder);
                var VersionJson = new JsonObject();
                VersionJson.Add("id", VersionName);
                VersionJson.Add("time",
                    DateTime.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
                VersionJson.Add("releaseTime",
                    DateTime.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
                VersionJson.Add("type", "release");
                VersionJson.Add("arguments",
                    (JsonNode)ModBase.GetJson("{\"game\":[\"--tweakClass\",\"" + DownloadInfo.JsonToken["tweakClass"] +
                                            "\"]}"));
                VersionJson.Add("libraries", DownloadInfo.JsonToken["libraries"]?.DeepClone());
                VersionJson["libraries"].AsArray().Add(ModBase.GetJson("{\"name\": \"com.mumfrey:liteloader:" +
                                                                            DownloadInfo.JsonToken["version"] +
                                                                            "\",\"url\": \"https://dl.liteloader.com/versions/\"}"));
                VersionJson.Add("mainClass", "net.minecraft.launchwrapper.Launch");
                VersionJson.Add("minimumLauncherVersion", 18);
                VersionJson.Add("inheritsFrom", DownloadInfo.Inherit);
                VersionJson.Add("jar", DownloadInfo.Inherit);
                ModBase.WriteFile(Path.Combine(VersionFolder, VersionName + ".json"), VersionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.LiteLoaderInstallFailed"), ex);
            }
        }) { ProgressWeight = 1d });
        // 下载支持库
        if (FixLibrary)
        {
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLiteLoaderLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLiteLoaderLibraries"),
                    new List<DownloadFile>())
                { ProgressWeight = 6d });
        }

        return Loaders;
    }

    #endregion

    #region LiteLoader 下载菜单

    public static MyListItem LiteLoaderDownloadListItem(ModDownload.DlLiteLoaderListEntry Entry,
        MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        var infoParts = new List<string>
        {
            Entry.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable")
        };

        if (!string.IsNullOrEmpty(Entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.ReleaseTime));

        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry.Inherit,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.PathImage + "Blocks/Egg.png"
        };

        NewItem.Click += OnClick;
        // 建立菜单
        NewItem.ContentHandler = IsSaveOnly
            ? LiteLoaderSaveContMenuBuild
            : LiteLoaderContMenuBuild;
        // 结束
        return NewItem;
    }

    private static void LiteLoaderSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        if ((bool)((dynamic)sender.Tag).IsLegacy)
        {
            sender.Buttons = Array.Empty<MyIconButton>();
        }
        else
        {
            var BtnList = new MyIconButton { Logo = Icon.IconButtonList, ToolTip = Lang.Text("Download.Version.ViewAllVersions"), Tag = sender };
            ToolTipService.SetPlacement(BtnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnList, 30d);
            ToolTipService.SetHorizontalOffset(BtnList, 2d);
            BtnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = new[] { BtnList };
        }
    }

    private static void LiteLoaderContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveInstaller"), Tag = sender };
        ToolTipService.SetPlacement(BtnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnSave, 30d);
        ToolTipService.SetHorizontalOffset(BtnSave, 2d);
        BtnSave.Click += (sender, e) => LiteLoaderSave_Click(sender, (RoutedEventArgs)e);
        if ((bool)((dynamic)sender.Tag).IsLegacy)
        {
            sender.Buttons = [BtnSave];
        }
        else
        {
            var BtnList = new MyIconButton { Logo = Icon.IconButtonList, ToolTip = Lang.Text("Download.Version.ViewAllVersions"), Tag = sender };
            ToolTipService.SetPlacement(BtnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnList, 30d);
            ToolTipService.SetHorizontalOffset(BtnList, 2d);
            BtnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = [BtnSave, BtnList];
        }
    }

    private static void LiteLoaderAll_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlLiteLoaderListEntry Version;
        if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        ModBase.OpenWebsite("https://jenkins.liteloader.com/view/" + Version.Inherit);
    }

    public static void LiteLoaderSave_Click(object sender, RoutedEventArgs e)
    {
        // ListItem 与小按钮都会调用这个方法
        ModDownload.DlLiteLoaderListEntry Version;
        if (((dynamic)sender).Tag is ModDownload.DlLiteLoaderListEntry)
            Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            Version = (ModDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        McDownloadLiteLoaderSave(Version);
    }

    #endregion

    #region Forgelike 下载

    public static void McDownloadForgelikeSave(ModDownload.DlForgelikeEntry Info)
    {
        try
        {
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"),
                $"{Info.LoaderName}-{Info.Inherit}-{Info.VersionName}.{Info.FileExtension}",
                Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", Info.LoaderName, Info.FileExtension));
            var DisplayName = $"{Info.LoaderName} {Info.Inherit} - {Info.VersionName}";
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", DisplayName) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 获取下载地址
            var Files = new List<DownloadFile>();
            if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge)
            {
                // NeoForge
                var Neo = (ModDownload.DlNeoForgeListEntry)Info;
                var Url = Neo.UrlBase + "-installer.jar";
                Files.Add(new DownloadFile(
                    new[] { Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url }, Target,
                    new ModBase.FileChecker(64 * 1024)));
            }
            else if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom)
            {
                // Cleanroom
                var Clr = (ModDownload.DlCleanroomListEntry)Info;
                var Url = Clr.UrlBase + "-installer.jar";
                Files.Add(new DownloadFile(new[] { Url }, Target, new ModBase.FileChecker(64 * 1024)));
            }
            else
            {
                // Forge
                var Forge = (ModDownload.DlForgeVersionEntry)Info;
                Files.Add(new DownloadFile(
                    new[]
                    {
                        $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}",
                        $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}"
                    }, Target, new ModBase.FileChecker(64 * 1024, Hash: Forge.Hash)));
            }

            // 构造加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"), Files)
                { ProgressWeight = 6d });

            // 启动
            var Loader =
                new ModLoader.LoaderCombo<ModDownload.DlForgelikeEntry>(
                        Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", DisplayName), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(Info);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        JavaEntry Java;
        lock (ModJava.JavaLock)
        {
            Java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (Java is null)
            {
                if (!ModJava.JavaDownloadConfirm(Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
                // 开始自动下载
                var JavaLoader = ModJava.GetJavaDownloadLoader();
                try
                {
                    JavaLoader.Start(17, true);
                    while (JavaLoader.State == ModBase.LoadState.Loading && !Task.IsAborted)
                        Thread.Sleep(10);
                }
                finally
                {
                    JavaLoader.Abort(); // 确保取消时中止 Java 下载
                }

                // 检查下载结果
                Java = ModJava.JavaSelect(Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                    new Version(1, 8, 0, 60));
                if (Task.IsAborted)
                    return;
                if (Java is null)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
            }
        }

        // 添加 Java Wrapper 作为主 Jar
        string Arguments;
        if (UseJavaWrapper && !Config.Launch.DisableJlw)
            Arguments =
                $@"-Doolloo.jlw.tmpdir=""{ModBase.PathPure.TrimEnd('\\')}"" -cp ""{ModBase.PathTemp}Cache\forge_installer.jar;{Target}"" -jar ""{ModLaunch.ExtractJavaWrapper()}"" com.bangbang93.ForgeInstaller ""{McFolder}";
        else
            Arguments =
                $@"-cp ""{ModBase.PathTemp}Cache\forge_installer.jar;{Target}"" com.bangbang93.ForgeInstaller ""{McFolder}";
        if (Java.Installation.MajorVersion >= 9)
            Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + Arguments;
        // 开始启动
        lock (InstallSyncLock)
        {
            var Info = new ProcessStartInfo
            {
                FileName = Java.Installation.JavaExePath,
                Arguments = Arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            string LoaderName = ModBase.GetStringFromEnum(ForgeType);
            ModBase.Log($"[Download] 开始安装 {LoaderName}：" + Arguments);
            var process = new Process { StartInfo = Info };
            var LastResults = new Queue<string>();
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
                                LastResults.Enqueue(e.Data);
                                if (LastResults.Count > 100)
                                    LastResults.Dequeue();
                                ForgelikeInjectorLine(e.Data, Task);
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"读取 {LoaderName} 安装器信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装");
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
                                LastResults.Enqueue(e.Data);
                                if (LastResults.Count > 100)
                                    LastResults.Dequeue();
                                ForgelikeInjectorLine(e.Data, Task);
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"读取 {LoaderName} 安装器错误信息失败");
                        }

                        try
                        {
                            if (Task.State == ModBase.LoadState.Aborted && !process.HasExited)
                            {
                                ModBase.Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装");
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
                    if (LastResults.Reverse().Take(5).Any(l => l == "true"))
                        return;
                    ModBase.Log(LastResults.Join("\r\n"));
                    var LastLines = "";
                    for (int i = Math.Max(0, LastResults.Count - 5), loopTo = LastResults.Count - 1;
                         i <= loopTo;
                         i++) // 最后 5 行
                        LastLines += "\r\n" + LastResults.ElementAtOrDefault(i);
                    throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderInstallerFailedLastLine", LoaderName,
                        LastLines));
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
                if (ModBase.ModeDebug)
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
                if (ModBase.ModeDebug)
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
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

        string LoaderName = ModBase.GetStringFromEnum(ForgeType);
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var InstallerAddress = ModMain.RequestTaskTempFolder() + "forge_installer.jar";
        var VersionFolder = $@"{McFolder}versions\{TargetVersion}\";
        var DisplayName = $"{LoaderName} {Inherit} - {LoaderVersion}";
        var Loaders = new List<ModLoader.LoaderBase>();
        var LibVersionFolder = $@"{ModMinecraft.McFolderSelected}versions\{TargetVersion}\"; // 作为 Lib 文件目标的实例文件夹

        // 获取 Forge 下载信息
        if (Info is null)
            Loaders.Add(new ModLoader.LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Stage.ObtainLoaderDetails", LoaderName), Task =>
            {
                // 获取 Forge 对应 MC 版本列表
                var ForgeLoader =
                    new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>(
                        "McDownloadForgeLoader " + Inherit, ModDownload.DlForgeVersionMain);
                ForgeLoader.WaitForExit(Inherit);
                Task.Progress = 0.8d;
                // 查找对应版本
                foreach (var ForgeVersion in ForgeLoader.Output)
                    if (ModMinecraft.CompareVersion(ForgeVersion.Version.ToString(), LoaderVersion) == 0)
                    {
                        Info = ForgeVersion;
                        return;
                    }

                throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderDetailsNotFound", LoaderName, Inherit,
                    LoaderVersion));
            })
            {
                ProgressWeight = 3d
            });
        // 下载 Forgelike 主文件
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.PrepareLoaderDownload", LoaderName), Task =>
        {
            // 启动依赖实例的下载
            if (ClientDownloadLoader is null)
            {
                if (IsCustomFolder)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                ClientDownloadLoader =
                    McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, Inherit);
            }

            // 添加主文件下载
            var Files = new List<DownloadFile>();
            if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge)
            {
                // NeoForge
                var Neo = (ModDownload.DlNeoForgeListEntry)Info;
                var Url = Neo.UrlBase + "-installer.jar";
                Files.Add(new DownloadFile(
                    new[] { Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url },
                    InstallerAddress, new ModBase.FileChecker(64 * 1024)));
            }
            else if (Info.ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom)
            {
                // Cleanroom
                var Clr = (ModDownload.DlCleanroomListEntry)Info;
                var Url = Clr.UrlBase + "-installer.jar";
                Files.Add(new DownloadFile(new[] { Url }, InstallerAddress, new ModBase.FileChecker(64 * 1024)));
            }
            else
            {
                // Forge
                var Forge = (ModDownload.DlForgeVersionEntry)Info;
                var FileName =
                    $"{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}/forge-{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}";
                Files.Add(new DownloadFile(
                    new[]
                    {
                        $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{FileName}",
                        $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{FileName}"
                    }, InstallerAddress, new ModBase.FileChecker(64 * 1024, Hash: Forge.Hash)));
            }

            Task.Output = Files;
        })
        {
            ProgressWeight = 0.5d,
            Show = false
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", LoaderName),
                new List<DownloadFile>())
            { ProgressWeight = 9d });

        // 安装（仅在新版安装时需要原版 Jar）
        if (ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge || Convert.ToDouble(LoaderVersion.BeforeFirst(".")) >= 20d)
        {
            ModBase.Log($"[Download] 检测为{(ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge ? "新版 Forge" : " " + ForgeType)}：" + LoaderVersion);
            List<ModMinecraft.McLibToken> Libs = null;
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeLoaderLibraries", LoaderName), Task =>
            {
                Task.Output = new List<DownloadFile>();
                ZipArchive Installer = null;
                try
                {
                    // 解压并获取、合并两个 Json 的信息
                    ModBase.WaitForFileReady(InstallerAddress);
                    Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                    Task.Progress = 0.2d;
                    var Json = (JsonObject)ModBase.GetJson(
                        ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                    var Json2 = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("version.json").Open()));
                    Json.Merge(Json2);
                    // 如果是 1.16.5 就升级一下 Authlib
                    if (Inherit == "1.16.5" && (bool)Config.Download.FixAuthLib)
                        Json = (JsonObject)ModBase.GetJson(Json.ToString()
                            .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                            .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                            .Replace("ad54da276bf59983d02d5ed16fc14541354c71fd",
                                "bbd00ca33b052f73a6312254780fc580d2da3535").Replace("76328", "87662"));
                    // 获取 Lib 下载信息
                    Libs = ModMinecraft.McLibListGetWithJson(Json, true);
                    // 添加 Mappings 下载信息
                    if (Json["data"] is not null && Json["data"]["MOJMAPS"] is not null)
                    {
                        // 下载原版 Json 文件
                        Task.Progress = 0.4d;
                        var RawJson = (JsonObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(
                            ModDownload.DlSourceLauncherOrMetaGet(
                                ModDownload.DlClientListGet(Inherit)?.ToString()), IsJson: true));
                        // [net.minecraft:client:1.17.1-20210706.113038:mappings@txt] 或 @tsrg]
                        var OriginalName = Json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray())
                            .BeforeFirst("@");
                        var Address = ModMinecraft.McLibGet(OriginalName).Replace(".jar",
                            "-mappings." + Json["data"]["MOJMAPS"]["client"].ToString().Trim("[]".ToCharArray())
                                .Split("@")[1]);
                        var ClientMappings = RawJson["downloads"]["client_mappings"];
                        Libs.Add(new ModMinecraft.McLibToken
                        {
                            IsNatives = false,
                            LocalPath = Address,
                            OriginalName = OriginalName,
                            Url = (string)ClientMappings["url"],
                            Size = (long)ClientMappings["size"],
                            SHA1 = (string)ClientMappings["sha1"]
                        });
                        ModBase.Log(
                            $"[Download] 需要下载 Mappings：{ClientMappings["url"]} (SHA1: {ClientMappings["sha1"]})");
                    }

                    Task.Progress = 0.8d;
                    // 去除其中的原始 Forgelike 项
                    for (int i = 0, loopTo = Libs.Count - 1; i <= loopTo; i++)
                        if (Libs[i].LocalPath.EndsWithF($"{LoaderName.ToLower()}-{Inherit}-{LoaderVersion}.jar") ||
                            Libs[i].LocalPath.EndsWithF($"{LoaderName.ToLower()}-{Inherit}-{LoaderVersion}-client.jar"))
                        {
                            ModBase.Log($"[Download] 已从待下载 {LoaderName} 支持库中移除：" + Libs[i].LocalPath,
                                ModBase.LogLevel.Debug);
                            Libs.RemoveAt(i);
                            break;
                        }

                    Task.Output = ModMinecraft.McLibNetFilesFromTokens(Libs);
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
                    if (Installer is not null)
                        Installer.Dispose();
                }
            })
            {
                ProgressWeight = 2d
            });
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", LoaderName),
                    new List<DownloadFile>())
                { ProgressWeight = 12d });
            Loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.GetLoaderLibraries", LoaderName), Task =>
            {
                #region Forgelike 文件

                if (IsCustomFolder)
                    foreach (var LibFile in Libs)
                    {
                        var RealPath = LibFile.LocalPath.Replace(ModMinecraft.McFolderSelected, McFolder);
                        if (!File.Exists(RealPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(RealPath));
                            ModBase.CopyFile(LibFile.LocalPath, RealPath);
                        }

                        if (ModBase.ModeDebug)
                            ModBase.Log($"[Download] 复制的 {LoaderName} 支持库文件：" + LibFile.LocalPath);
                    }

                #endregion

                #region 原版文件

                // 等待原版文件下载完成
                if (ClientDownloadLoader is null)
                    return;
                var TargetLoaders = ClientDownloadLoader.GetLoaderList()
                    .Where(l => (l.Name ?? "") == McDownloadClientLibName || (l.Name ?? "") == McDownloadClientJsonName)
                    .Where(l => l.State != ModBase.LoadState.Finished).ToList();
                if (TargetLoaders.Any())
                    ModBase.Log($"[Download] {LoaderName} 安装正在等待原版文件下载完成");
                while (TargetLoaders.Any() && !Task.IsAborted)
                {
                    TargetLoaders = TargetLoaders.Where(l => l.State != ModBase.LoadState.Finished).ToList();
                    Thread.Sleep(50);
                }

                if (Task.IsAborted)
                    return;
                // 拷贝原版文件
                if (!IsCustomFolder)
                    return;
                lock (VanillaSyncLock)
                {
                    var ClientName = ModBase.GetFolderNameFromPath(ClientFolder);
                    Directory.CreateDirectory(Path.Combine(McFolder, "versions", Inherit));
                    if (!File.Exists(Path.Combine(McFolder, "versions", Inherit, Inherit + ".json")))
                        ModBase.CopyFile(Path.Combine(ClientFolder, ClientName + ".json"),
                            Path.Combine(McFolder, "versions", Inherit, Inherit + ".json"));
                    if (!File.Exists(Path.Combine(McFolder, "versions", Inherit, Inherit + ".jar")))
                        ModBase.CopyFile(Path.Combine(ClientFolder, ClientName + ".jar"),
                            Path.Combine(McFolder, "versions", Inherit, Inherit + ".jar"));
                }

                #endregion
            })
            {
                ProgressWeight = 0.1d,
                Show = false
            });
            Loaders.Add(new ModLoader.LoaderTask<bool, bool>(
                ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodA")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", ForgeType), Task =>
                {
                    ModBase.WaitForFileReady(InstallerAddress);
                    var Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                    try
                    {
                        // 记录当前文件夹列表（在新建目标文件夹之前）
                        ModBase.Log("[Download] 开始进行 Forgelike 安装：" + InstallerAddress);
                        // 解压并获取信息
                        var OldList = new DirectoryInfo(McFolder + "versions/")
                            .EnumerateDirectories().Select(i => i.FullName).ToList();


                        // 新建目标实例文件夹
                        var Json = ModBase.GetJson(ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                        Directory.CreateDirectory(VersionFolder);
                        Task.Progress = 0.04d;
                        // 释放 launcher_installer.json
                        ModMinecraft.McFolderLauncherProfilesJsonCreate(McFolder);
                        Task.Progress = 0.05d;
                        // 运行 Forge 安装器
                        var UseJavaWrapper = ModBase.IsUtf8CodePage();
                        Retry:

                        try
                        {
                            // 释放 Forge 注入器
                            ModBase.WriteFile(Path.Combine(ModBase.PathTemp, "Cache", "forge_installer.jar"),
                                ModBase.GetResourceStream("Resources/forge-installer.jar"));
                            Task.Progress = 0.06d;
                            // 运行注入器
                            ForgelikeInjector(InstallerAddress, Task, McFolder, UseJavaWrapper, ForgeType);
                            Task.Progress = 0.97d;
                        }
                        catch (Exception ex)
                        {
                            if (!UseJavaWrapper)
                            {
                                ModBase.Log(ex, $"不使用 JavaWrapper 安装 {LoaderName} 失败，将使用 JavaWrapper 并重试");
                                UseJavaWrapper = true;
                                goto Retry;
                            }

                            throw new Exception(
                                Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", LoaderName), ex);
                            // 拷贝新增的实例 Json
                        }

                        var DeltaList = new DirectoryInfo(McFolder + "versions/").EnumerateDirectories()
                            .SkipWhile(i => OldList.Contains(i.FullName)).ToList();

                        if (DeltaList.Count > 1)
                            // 它可能和 OptiFine 安装同时运行，导致增加的文件不止一个（这导致了 #151）
                            // 也可能是因为 Forge 安装器的 Bug，生成了一个名字错误的文件夹，所以需要检查文件夹是否为空
                            DeltaList = DeltaList
                                .Where(l => l.Name.ContainsF("forge", true) && l.EnumerateFiles().Any())
                                .ToList();
                        // 如果没有新增文件夹，那么预测的文件夹名就是正确的
                        // 如果只新增 1 个文件夹，那么拷贝 Json 文件
                        if (DeltaList.Count == 1)
                        {
                            var JsonFile = DeltaList[0].EnumerateFiles().First();
                            ModBase.WriteFile(Path.Combine(VersionFolder, TargetVersion + ".json"),
                                ModBase.ReadFile(JsonFile.FullName));
                            ModBase.Log(
                                $"[Download] 已拷贝新增的实例 Json 文件：{JsonFile.FullName} -> {VersionFolder}{TargetVersion}.json");
                        }
                        else if (DeltaList.Count > 1)
                        {
                            // 新增了多个文件夹
                            //Enumerable.Select<string>((IEnumerable<DirectoryInfo>)DeltaList, d => d.Name).Join(";")
                            ModBase.Log(
                                $"[Download] 有多个疑似的新增实例，无法确定：{string.Join(";", DeltaList.Select<DirectoryInfo, string>(d => d.Name))}");
                        }
                        else
                        {
                            // 没有新增文件夹
                            ModBase.Log("[Download] 未找到新增的实例文件夹");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Minecraft.Download.Error.LoaderInstallFailed", LoaderName), ex);
                    }
                    finally
                    {
                        // 清理文件
                        try
                        {
                            if (Installer is not null)
                                Installer.Dispose();
                            if (File.Exists(InstallerAddress))
                                File.Delete(InstallerAddress);
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"安装 {LoaderName} 清理文件时出错");
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
            Loaders.Add(new ModLoader.LoaderTask<List<DownloadFile>, bool>(
                $"{(ForgeType == ModDownload.DlForgelikeEntry.ForgelikeType.Forge ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodB") : Lang.Text("Minecraft.Download.Stage.InstallForgeType", ForgeType))}",
                Task =>
                {
                    ZipArchive Installer = null;
                    try
                    {
                        // 解压并获取信息
                        ModBase.WaitForFileReady(InstallerAddress);
                        Installer = new ZipArchive(new FileStream(InstallerAddress, FileMode.Open));
                        Task.Progress = 0.2d;
                        var Json = (JsonObject)ModBase.GetJson(
                            ModBase.ReadFile(Installer.GetEntry("install_profile.json").Open()));
                        Task.Progress = 0.4d;
                        // 新建实例文件夹
                        Directory.CreateDirectory(VersionFolder);
                        Task.Progress = 0.5d;
                        if (Json["install"] is null)
                        {
                            // 中版：Legacy 方式 1
                            ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 1：" + InstallerAddress);
                            // 建立 Json 文件
                            var JsonVersion = (JsonObject)ModBase.GetJson(
                                ModBase.ReadFile(Installer.GetEntry(Json["json"].ToString().TrimStart('/')).Open()));
                            JsonVersion["id"] = TargetVersion;
                            ModBase.WriteFile(Path.Combine(VersionFolder, TargetVersion + ".json"), JsonVersion.ToString());
                            Task.Progress = 0.6d;
                            // 解压支持库文件
                            Installer.Dispose();
                            var unrarDir = Path.Combine(Path.GetDirectoryName(InstallerAddress), "_unrar");
                            ModBase.ExtractFile(InstallerAddress, unrarDir);
                            ModBase.CopyDirectory(Path.Combine(unrarDir, "maven"), Path.Combine(McFolder, "libraries"));
                            ModBase.DeleteDirectory(unrarDir);
                        }
                        else
                        {
                            // 旧版：Legacy 方式 2
                            ModBase.Log("[Download] 开始进行 Forge 安装，Legacy 方式 2：" + InstallerAddress);
                            // 解压 Jar 文件
                            var JarAddress = ModMinecraft.McLibGet((string)Json["install"]["path"],
                                customMcFolder: McFolder);
                            if (File.Exists(JarAddress))
                                File.Delete(JarAddress);
                            ModBase.WriteFile(JarAddress,
                                Installer.GetEntry((string)Json["install"]["filePath"]).Open());
                            Task.Progress = 0.9d;
                            // 建立 Json 文件
                            Json["versionInfo"]["id"] = TargetVersion;
                            if (Json["versionInfo"]["inheritsFrom"] is null)
                                ((JsonObject)Json["versionInfo"]).Add("inheritsFrom", Inherit);
                            ModBase.WriteFile(Path.Combine(VersionFolder, TargetVersion + ".json"), Json["versionInfo"].ToString());
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
                            if (Installer is not null)
                                Installer.Dispose();
                            if (File.Exists(InstallerAddress))
                                File.Delete(InstallerAddress);
                            var unrarDir = Path.Combine(Path.GetDirectoryName(InstallerAddress), "_unrar");
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

        return Loaders;
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
        ModDownload.DlForgeVersionEntry FreshVersion = null;
        if (Entries.Any())
            FreshVersion = Entries[0];
        else
            ModBase.Log("[System] 未找到可用的 Forge 版本", ModBase.LogLevel.Debug);
        ModDownload.DlForgeVersionEntry RecommendedVersion = null;
        foreach (var Entry in Entries)
            if (Entry.IsRecommended)
                RecommendedVersion = Entry;
        // 若推荐版本与最新版本为同一版本，则仅显示推荐版本
        if (FreshVersion is not null && ReferenceEquals(FreshVersion, RecommendedVersion))
            FreshVersion = null;
        // 显示各个版本
        if (RecommendedVersion is not null)
        {
            var Recommended = ForgeDownloadListItem(RecommendedVersion, OnClick, IsSaveOnly);
            Recommended.Info = Lang.Text("Download.Version.Type.Recommended") + (string.IsNullOrEmpty(Recommended.Info) ? "" : "  |  " + Recommended.Info);
            Stack.Children.Add(Recommended);
        }

        if (FreshVersion is not null)
        {
            var Fresh = ForgeDownloadListItem(FreshVersion, OnClick, IsSaveOnly);
            Fresh.Info = Lang.Text("Download.Version.Latest.Title") + (string.IsNullOrEmpty(Fresh.Info) ? "" : "  |  " + Fresh.Info);
            Stack.Children.Add(Fresh);
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

        if (!string.IsNullOrEmpty(Entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", Entry.ReleaseTime));

        if (ModBase.ModeDebug)
            infoParts.Add(Lang.Text("Download.Version.Forge.Type", Entry.Category));

        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = string.Join("  |  ", infoParts),
            Logo = ModBase.PathImage + "Blocks/Anvil.png"
        };

        NewItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            NewItem.ContentHandler = ForgeSaveContMenuBuild;
        else
            NewItem.ContentHandler = ForgeContMenuBuild;
        // 结束
        return NewItem;
    }

    private static void ForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(BtnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnSave, 30d);
        ToolTipService.SetHorizontalOffset(BtnSave, 2d);
        BtnSave.Click += (ss, ee) => ForgeSave_Click(ss, (dynamic)ee);
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (ss, ee) => ForgeLog_Click(ss, (dynamic)ee);
        sender.Buttons = new[] { BtnSave, BtnInfo };
    }

    private static void ForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (ss, ee) => ForgeLog_Click(ss, (dynamic)e);
        sender.Buttons = new[] { BtnInfo };
    }

    private static void ForgeLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlForgeVersionEntry Version;
        if (((dynamic)sender).Tag is not null)
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(
            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Version.Inherit}-{Version.VersionName}/forge-{Version.Inherit}-{Version.VersionName}-changelog.txt");
    }

    public static void ForgeSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlForgeVersionEntry Version;
        if (((dynamic)sender).Tag is not null)
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            Version = (ModDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(Version);
    }

    #endregion

    #region Forge 推荐版本获取

    /// <summary>
    ///     尝试刷新 Forge 推荐版本缓存。
    /// </summary>
    public static void McDownloadForgeRecommendedRefresh()
    {
        if (IsForgeRecommendedRefreshed)
            return;
        IsForgeRecommendedRefreshed = true;
        // 获取所有推荐版本列表
        // 内容为："1.15.2":"31.2.0"
        // 保存
        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModBase.Log("[Download] 刷新 Forge 推荐版本缓存开始");
                var Result = ModNet.NetGetCodeByLoader("https://bmclapi2.bangbang93.com/forge/promos");
                if (Result.Length < 1000) throw new Exception(Lang.Text("Minecraft.Download.Error.ForgePromosResultTooShort", Result));
                var ResultJson = (JsonNode)ModBase.GetJson(Result);
                var RecommendedList = new List<string>();
                foreach (JsonObject Version in ResultJson.AsArray())
                {
                    if (Version["name"] is null || Version["build"] is null) continue;
                    var Name = (string)Version["name"];
                    if (!Name.EndsWithF("-recommended")) continue;
                    RecommendedList.Add("\"" + Name.Replace("-recommended",
                        "\":\"" + Version["build"]["version"] + "\""));
                }

                if (RecommendedList.Count < 5)
                    throw new Exception(Lang.Text("Minecraft.Download.Error.ForgeRecommendedTooFew", Result));
                var CacheJson = "{" + RecommendedList.Join(",") + "}";
                ModBase.WriteFile(Path.Combine(ModBase.PathTemp, "Cache", "ForgeRecommendedList.json"), CacheJson);
                ModBase.Log("[Download] 刷新 Forge 推荐版本缓存成功");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新 Forge 推荐版本缓存失败");
            }
        }, "ForgeRecommendedRefresh");
    }

    private static bool IsForgeRecommendedRefreshed;

    /// <summary>
    ///     尝试获取某个 MC 版本对应的 Forge 推荐版本。如果不可用会返回 Nothing。
    /// </summary>
    public static string McDownloadForgeRecommendedGet(string McInstance)
    {
        try
        {
            if (McInstance is null)
                return null;
            var List = ModBase.ReadFile(Path.Combine(ModBase.PathTemp, "Cache", "ForgeRecommendedList.json"));
            if (List is null || string.IsNullOrEmpty(List))
            {
                ModBase.Log("[Download] 没有 Forge 推荐版本缓存文件");
                return null;
            }

            var Json = (JsonObject)ModBase.GetJson(List);
            if (Json is null || !(McInstance ?? "null").Contains(".") || !Json.ContainsKey(McInstance))
                return null;
            return (Json[McInstance] ?? "").ToString();
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
        ModDownload.DlNeoForgeListEntry FreshStableVersion = null;
        ModDownload.DlNeoForgeListEntry FreshBetaVersion = null;
        if (Entries.Any())
            foreach (var Entry in Entries.ToList())
                if (Entry.IsBeta)
                {
                    if (FreshBetaVersion is null)
                        FreshBetaVersion = Entry;
                }
                else
                {
                    FreshStableVersion = Entry;
                    break;
                }
        else
            ModBase.Log("[System] 未找到可用的 NeoForge 版本", ModBase.LogLevel.Debug);

        // 显示各个版本
        if (FreshStableVersion is not null)
        {
            var Fresh = NeoForgeDownloadListItem(FreshStableVersion, OnClick, IsSaveOnly);
            Fresh.Info = string.IsNullOrEmpty(Fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Stable")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + Fresh.Info;
            Stack.Children.Add(Fresh);
        }

        if (FreshBetaVersion is not null)
        {
            var Fresh = NeoForgeDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly);
            Fresh.Info = string.IsNullOrEmpty(Fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Development")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + Fresh.Info;
            Stack.Children.Add(Fresh);
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
        var NewItem = new MyListItem
        {
            Title = Info.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Info,
            Info = Info.IsBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.PathImage + "Blocks/NeoForge.png"
        };
        NewItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            NewItem.ContentHandler = NeoForgeSaveContMenuBuild;
        else
            NewItem.ContentHandler = NeoForgeContMenuBuild;
        // 结束
        return NewItem;
    }

    private static void NeoForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(BtnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnSave, 30d);
        ToolTipService.SetHorizontalOffset(BtnSave, 2d);
        BtnSave.Click += (sender, e) => NeoForgeSave_Click(sender, (RoutedEventArgs)e);
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { BtnSave, BtnInfo };
    }

    private static void NeoForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { BtnInfo };
    }

    private static void NeoForgeLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlNeoForgeListEntry Info;
        if (((dynamic)sender).Tag is not null)
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(Info.UrlBase + "-changelog.txt");
    }

    public static void NeoForgeSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlNeoForgeListEntry Info;
        if (((dynamic)sender).Tag is not null)
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            Info = (ModDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(Info);
    }

    #endregion

    #region Cleanroom 下载菜单

    public static void CleanroomDownloadListItemPreload(StackPanel Stack,
        List<ModDownload.DlCleanroomListEntry> Entries, MyListItem.ClickEventHandler OnClick, bool IsSaveOnly)
    {
        // 获取最新稳定版和测试版
        // Dim FreshStableVersion As DlCleanroomListEntry = Nothing
        ModDownload.DlCleanroomListEntry FreshBetaVersion = null;
        if (Entries.Any())
            FreshBetaVersion = Entries[0];
        else
            ModBase.Log("[System] 未找到可用的 Cleanroom 版本", ModBase.LogLevel.Debug);
        if (FreshBetaVersion is not null)
        {
            var Fresh = CleanroomDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly);
            Fresh.Info = string.IsNullOrEmpty(Fresh.Info) ? Lang.Text("Download.Version.Fresh.Development") : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + Fresh.Info;
            Stack.Children.Add(Fresh);
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
        var NewItem = new MyListItem
        {
            Title = Info.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Info,
            Info = Info.IsBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.PathImage + "Blocks/Cleanroom.png"
        };
        NewItem.Click += OnClick;
        // 建立菜单
        if (IsSaveOnly)
            NewItem.ContentHandler = CleanroomSaveContMenuBuild;
        else
            NewItem.ContentHandler = CleanroomContMenuBuild;
        // 结束
        return NewItem;
    }

    private static void CleanroomContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnSave = new MyIconButton { Logo = Icon.IconButtonSave, ToolTip = Lang.Text("Download.Version.SaveAs") };
        ToolTipService.SetPlacement(BtnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnSave, 30d);
        ToolTipService.SetHorizontalOffset(BtnSave, 2d);
        BtnSave.Click += (sender, _e) => CleanroomSave_Click(sender, (RoutedEventArgs)e);
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (sender, e) => CleanroomLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = new[] { BtnSave, BtnInfo };
    }

    private static void CleanroomSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        var BtnInfo = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Download.Version.Changelog") };
        ToolTipService.SetPlacement(BtnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnInfo, 30d);
        ToolTipService.SetHorizontalOffset(BtnInfo, 2d);
        BtnInfo.Click += (a, b) => CleanroomLog_Click(a, (dynamic)b);
        sender.Buttons = new[] { BtnInfo };
    }

    private static void CleanroomLog_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlCleanroomListEntry Info;
        if (((dynamic)sender).Tag is not null)
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        ModBase.OpenWebsite(Info.UrlBase + "-changelog.txt");
    }

    public static void CleanroomSave_Click(object sender, RoutedEventArgs e)
    {
        ModDownload.DlCleanroomListEntry Info;
        if (((dynamic)sender).Tag is not null)
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            Info = (ModDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(Info);
    }

    #endregion

    #region Fabric 下载

    public static void McDownloadFabricLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var Url = DownloadInfo["url"].ToString();
            var FileName = ModBase.GetFileNameFromPath(Url);
            var Version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), FileName, Lang.Text("Download.Version.Installer.Fabric.Filter"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.FabricInstallerDownload", Version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            // BMCLAPI 不支持 Fabric Installer 下载
            var Address = new List<string>();
            Address.Add(Url);
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.FabricInstallerDownload", Version), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = "fabric-loader-" + FabricVersion + "-" + MinecraftName;
        var VersionFolder = Path.Combine(McFolder, "versions", Id);
        var Loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
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

            Directory.CreateDirectory(VersionFolder);
            File.WriteAllText(Path.Combine(VersionFolder, Id + ".json"), json, Encoding.UTF8);
            Task.Output = new List<DownloadFile>();
        })
        {
            ProgressWeight = 0.5d
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Fabric"),
            new List<DownloadFile>()) { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeFabricLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLabyModClientJson"),
                    new List<DownloadFile>()) { ProgressWeight = 8d });
        }

        return Loaders;
    }

    #endregion

    #region LegacyFabric 下载

    public static void McDownloadLegacyFabricLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var Url = DownloadInfo["url"].ToString();
            var FileName = ModBase.GetFileNameFromPath(Url);
            var Version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), FileName, Lang.Text("Download.Version.Installer.LegacyFabric.Filter"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LegacyFabricInstallerDownload", Version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var Address = new List<string>();
            Address.Add(Url);
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.LegacyFabricInstallerDownload", Version), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = "legacy-fabric-loader-" + LegacyFabricVersion + "-" + MinecraftName;
        var VersionFolder = Path.Combine(McFolder, "versions", Id);
        var Loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainLegacyFabricMainFileUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.Output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        "https://meta.legacyfabric.net/v2/versions/loader/" + MinecraftName + "/" +
                        LegacyFabricVersion + "/profile/json"
                    }, Path.Combine(VersionFolder, Id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
        })
        {
            ProgressWeight = 0.5d
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Legacy Fabric"),
                new List<DownloadFile>())
            { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLegacyFabricLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Legacy Fabric"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return Loaders;
    }

    #endregion

    #region Fabric 下载菜单

    public static MyListItem FabricDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry["version"].ToString().Replace("+build", ""),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["stable"].ToObject<bool>() ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = ModBase.PathImage + "Blocks/Fabric.png"
        };
        NewItem.Click += OnClick;
        NewItem.ContentHandler = FabricContMenuBuild;
        // 结束
        return NewItem;
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
        var NewItem = new MyListItem
        {
            Title = Entry.DisplayName.Split("]")[1].Replace("Fabric API ", "").Replace(" build ", ".").Trim(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.ReleaseDate, "g")),
            Logo = ModBase.PathImage + "Blocks/Fabric.png"
        };
        NewItem.Click += OnClick;
        // 结束
        return NewItem;
    }

    public static MyListItem OptiFabricDownloadListItem(ModComp.CompFile Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry.DisplayName.ToLower().Replace("optifabric-", "").Replace(".jar", "").Trim().TrimStart('v'),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.ReleaseDate, "g")),
            Logo = ModBase.PathImage + "Blocks/OptiFabric.png"
        };
        NewItem.Click += OnClick;
        // 结束
        return NewItem;
    }

    #endregion

    #region LegacyFabric 下载菜单

    public static MyListItem LegacyFabricDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry["version"].ToString(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["stable"].ToObject<bool>() ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = ModBase.PathImage + "Blocks/Fabric.png"
        };
        NewItem.Click += OnClick;
        // 结束
        return NewItem;
    }

    public static MyListItem LegacyFabricApiDownloadListItem(ModComp.CompFile Entry,
        MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry.DisplayName.Replace("Legacy Fabric API ", ""),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.ReleaseDate, "g")),
            Logo = ModBase.PathImage + "Blocks/Fabric.png"
        };
        NewItem.Click += OnClick;
        // 结束
        return NewItem;
    }

    #endregion

    #region Quilt 下载

    public static void McDownloadQuiltLoaderSave(JsonObject DownloadInfo)
    {
        try
        {
            var Url = DownloadInfo["url"].ToString();
            var FileName = ModBase.GetFileNameFromPath(Url);
            var Version = ModBase.GetFileNameFromPath(DownloadInfo["version"].ToString());
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), FileName, Lang.Text("Download.Version.Installer.Quilt.Filter"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.QuiltInstallerDownload", Version) ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            // TODO: BMCLAPI 不支持 Quilt Installer 下载
            var Address = new List<string>();
            Address.Add(Url);
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<JsonObject>(
                        Lang.Text("Minecraft.Download.Stage.QuiltInstallerDownload", Version), Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start(DownloadInfo);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = "quilt-loader-" + QuiltVersion + "-" + MinecraftName;
        var VersionFolder = Path.Combine(McFolder, "versions", Id);
        var Loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainQuiltMainFileUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName);
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.Output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        "https://meta.quiltmc.org/v3/versions/loader/" + MinecraftName + "/" + QuiltVersion +
                        "/profile/json"
                    }, Path.Combine(VersionFolder, Id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
            // 新建 mods 文件夹
            Directory.CreateDirectory($@"{McFolder ?? ModMinecraft.McFolderSelected}mods\");
        })
        {
            ProgressWeight = 0.5d
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Quilt"),
            new List<DownloadFile>()) { ProgressWeight = 2.5d });

        // 下载支持库
        if (FixLibrary)
        {
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeQuiltLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Quilt"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return Loaders;
    }

    #endregion

    #region Quilt 下载菜单

    public static MyListItem QuiltDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry["version"].ToString(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["maven"].ToString().Contains("installer") ? Lang.Text("Download.Version.Type.Installer") :
                Entry["version"].ToString().Contains("beta") || Entry["version"].ToString().Contains("pre") ? Lang.Text("Download.Version.Type.Preview") :
                Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.PathImage + "Blocks/Quilt.png"
        };
        NewItem.Click += OnClick;
        NewItem.ContentHandler = QuiltContMenuBuild;
        // 结束
        return NewItem;
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
        var NewItem = new MyListItem
        {
            Title = Entry.DisplayName.Split("]")[1].Replace(" build ", ".").Split("+")[0].Trim(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry.StatusDescription + Lang.Text("Download.Version.ReleaseDate", Lang.Date(Entry.ReleaseDate, "g")),
            Logo = ModBase.PathImage + "Blocks/Quilt.png"
        };
        NewItem.Click += OnClick;
        // 结束
        return NewItem;
    }

    #endregion

    #region LabyMod 下载

    public static void McDownloadLabyModProductionLoaderSave()
    {
        try
        {
            var Url = "https://releases.labymod.net/api/v1/installer/production/java";
            var FileName = "LabyMod4ProductionInstaller.jar";
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), FileName, Lang.Text("Download.Version.Installer.LabyMod.Filter"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var Address = new List<string>();
            Address.Add(Url);
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<JsonObject>(Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                        Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start();
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
            var Url = "https://releases.labymod.net/api/v1/installer/snapshot/java";
            var FileName = "LabyMod4SnapshotInstaller.jar";
            var Target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Version.SelectSaveLocation"), FileName, Lang.Text("Download.Version.Installer.LabyMod.Filter"));
            if (!Target.Contains(@"\"))
                return;

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar.ToList())
            {
                if ((OngoingLoader.Name ?? "") !=
                    (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), ModMain.HintType.Critical);
                return;
            }

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            // 下载
            var Address = new List<string>();
            Address.Add(Url);
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    new List<DownloadFile> { new(Address.ToArray(), Target, new ModBase.FileChecker(1024 * 64)) })
                { ProgressWeight = 15d });
            // 启动
            var Loader =
                new ModLoader.LoaderCombo<JsonObject>(Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                        Loaders)
                { OnStateChanged = LoaderStateChangedHintOnly };
            Loader.Start();
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        McFolder = McFolder ?? ModMinecraft.McFolderSelected;
        var IsCustomFolder = (McFolder ?? "") != (ModMinecraft.McFolderSelected ?? "");
        var Id = "labymod-" + LabyModCommitRef + "-" + MinecraftName;
        var VersionFolder = Path.Combine(McFolder, "versions", Id);
        var Loaders = new List<ModLoader.LoaderBase>();

        // 下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.ObtainLabyModClientUrl"), Task =>
        {
            // 启动依赖实例的下载
            if (FixLibrary)
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName,
                    $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{LabyModChannel}/{MinecraftName}/{LabyModCommitRef}.json");
            Task.Progress = 0.5d;
            // 构造文件请求
            Task.Output = new List<DownloadFile>
            {
                new(
                    new[]
                    {
                        $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{LabyModChannel}/{MinecraftName}/{LabyModCommitRef}.json"
                    }, Path.Combine(VersionFolder, Id + ".json"), new ModBase.FileChecker(IsJson: true))
            };
            Task.Progress = 1d;
        })
        {
            ProgressWeight = 2d
        });
        Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Fabric"),
                new List<DownloadFile>())
            { ProgressWeight = 10d });
        // 下载支持库
        if (FixLibrary)
        {
            Loaders.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLabyModLibraries"),
                    Task => Task.Output =
                        ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder)))
                { ProgressWeight = 1d, Show = false });
            Loaders.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "LabyMod"),
                    new List<DownloadFile>())
                { ProgressWeight = 8d });
        }

        return Loaders;
    }

    /// <summary>
    ///     获取下载某个 Minecraft 实例的加载器列表。
    ///     它必须安装到 PathMcFolder，但是可以自定义实例名（不过自定义的实例名不会修改 Json 中的 id 项）。
    /// </summary>
    private static List<ModLoader.LoaderBase> McDownloadLabyModClientLoader(string Id, string LabyChannel,
        string LabyCommitRef, string VersionName = null)
    {
        VersionName = VersionName ?? Id;
        var VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", VersionName) + @"\";

        var Loaders = new List<ModLoader.LoaderBase>();

        // 下载支持库文件
        var LoadersLib = new List<ModLoader.LoaderBase>();
        LoadersLib.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaAndLabyModLibrariesSide"), Task =>
        {
            ModBase.WaitForFileReady(Path.Combine(VersionFolder, VersionName + ".json"));
            ModBase.Log("[Download] 开始分析原版与 LabyMod 支持库文件：" + VersionFolder);
            Task.Output = ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(VersionFolder));
        })
        {
            ProgressWeight = 1d,
            Show = false
        });
        LoadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAndLabyModLibrariesSide"),
                new List<DownloadFile>())
            { ProgressWeight = 13d, Show = false });
        Loaders.Add(new ModLoader.LoaderCombo<string>(McDownloadClientLibName, LoadersLib)
            { Block = false, ProgressWeight = 14d });

        // 下载资源文件
        var LoadersAssets = new List<ModLoader.LoaderBase>();
        LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"), Task =>
        {
            try
            {
                var Version = new ModMinecraft.McInstance(VersionFolder);
                Task.Output = new List<DownloadFile> { ModDownload.DlClientAssetIndexGet(Version) };
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"), ex);
            }

            // 顺手添加 Json 项目
            try
            {
                var VersionJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(Path.Combine(VersionFolder, VersionName + ".json")));
                VersionJson.Add("clientVersion", Id);
                ModBase.WriteFile(Path.Combine(VersionFolder, VersionName + ".json"), VersionJson.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(Lang.Text("Minecraft.Download.Error.AddClientVersionFailed"), ex);
            }
        })
        {
            ProgressWeight = 1d,
            Show = false
        });
        LoadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 3d, Show = false });
        LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"), Task =>
        {
            ModLoader.LoaderBase argprogressFeed = Task;
            Task.Output =
                ModMinecraft.McAssetsFixList(new ModMinecraft.McInstance(VersionFolder), true, ref argprogressFeed);
            Task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
        })
        {
            ProgressWeight = 3d,
            Show = false
        });
        LoadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"),
                new List<DownloadFile>())
            { ProgressWeight = 14d, Show = false });
        Loaders.Add(
            new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                LoadersAssets) { Block = false, ProgressWeight = 21d });

        return Loaders;
    }

    #endregion

    #region LabyMod 下载菜单

    public static MyListItem LabyModDownloadListItem(JsonObject Entry, MyListItem.ClickEventHandler OnClick)
    {
        // 建立控件
        var NewItem = new MyListItem
        {
            Title = Entry["version"] + " " + (Entry["channel"].ToString().Contains("snapshot") ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable")),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = Entry,
            Info = Entry["channel"].ToString().Contains("snapshot") ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable"),
            Logo = ModBase.PathImage + "Blocks/LabyMod.png"
        };
        NewItem.Click += OnClick;
        NewItem.ContentHandler = LabyModContMenuBuild;
        // 结束
        return NewItem;
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
        public ModDownload.DlCleanroomListEntry CleanroomEntry = null;

        // 若要下载 Cleanroom，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 Cleanroom 版本名。
        /// </summary>
        public string CleanroomVersion;

        /// <summary>
        ///     欲下载的 Fabric API 信息。
        /// </summary>
        public ModComp.CompFile FabricApi = null;

        /// <summary>
        ///     欲下载的 Fabric Loader 版本名。
        /// </summary>
        public string FabricVersion = null;

        /// <summary>
        ///     欲下载的 Forge。
        /// </summary>
        public ModDownload.DlForgeVersionEntry ForgeEntry = null;

        // 若要下载 Forge，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
        /// </summary>
        public string ForgeVersion;

        /// <summary>
        ///     欲下载的 LabyMod 通道。
        /// </summary>
        public string LabyModChannel = null;

        /// <summary>
        ///     欲下载的 LabyMod 版本。
        /// </summary>
        public string LabyModCommitRef = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric API 信息。
        /// </summary>
        public ModComp.CompFile LegacyFabricApi = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric Loader 版本名。
        /// </summary>
        public string LegacyFabricVersion = null;

        /// <summary>
        ///     欲下载的 LiteLoader 详细信息。
        /// </summary>
        public ModDownload.DlLiteLoaderListEntry LiteLoaderEntry = null;

        /// <summary>
        ///     可选。欲下载的 Minecraft Json 地址。
        /// </summary>
        public string MinecraftJson = null;

        /// <summary>
        ///     必填。欲下载的 Minecraft 的版本名。
        /// </summary>
        public string MinecraftName = null;

        /// <summary>
        ///     若 MMC 整合包安装包含特殊参数，则填写此项。
        /// </summary>
        public ModModpack.MMCPackInfo MMCPackInfo = null;

        /// <summary>
        ///     欲下载的 NeoForge。
        /// </summary>
        public ModDownload.DlNeoForgeListEntry NeoForgeEntry = null;

        // 若要下载 NeoForge，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 NeoForge 版本名。
        /// </summary>
        public string NeoForgeVersion;

        /// <summary>
        ///     欲下载的 OptiFabric 信息。
        /// </summary>
        public ModComp.CompFile OptiFabric = null;

        /// <summary>
        ///     欲下载的 OptiFine 详细信息。
        /// </summary>
        public ModDownload.DlOptiFineListEntry OptiFineEntry;

        // 若要下载 OptiFine，则需要在下面两项中完成至少一项
        /// <summary>
        ///     欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
        /// </summary>
        public string OptiFineVersion;

        /// <summary>
        ///     欲下载的 Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL) 信息。
        /// </summary>
        public ModComp.CompFile QSL = null;

        /// <summary>
        ///     欲下载的 Quilt Loader 版本名。
        /// </summary>
        public string QuiltVersion = null;

        /// <summary>
        ///     必填。安装目标文件夹。
        /// </summary>
        public string TargetInstanceFolder;

        /// <summary>
        ///     必填。安装目标实例名称。
        /// </summary>
        public string TargetInstanceName;
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
                ModMain.Hint($"{loader.Name}{Lang.Text("Common.Status.Success")}", ModMain.HintType.Finish);
                break;
            case ModBase.LoadState.Failed:
                ModMain.Hint($"{loader.Name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}", ModMain.HintType.Critical);
                break;
            case ModBase.LoadState.Aborted:
                ModMain.Hint($"{loader.Name}{Lang.Text("Common.Status.Cancelled")}");
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
                    var versionName = loader.Name;
                    ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version",
                        versionName.Remove(versionName.Length - 3, 3));
                }

                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache",
                    ""); // 清空缓存（合并安装会先生成文件夹，这会在刷新时误判为可以使用缓存）
                ModBase.DeleteDirectory($"{combo.Input}PCLInstallBackups\\");
                ModMain.Hint($"{loader.Name}{Lang.Text("Common.Status.Success")}",
                    ModMain.HintType.Finish);
                break;
            }
            case ModBase.LoadState.Failed:
            {
                ModMain.Hint(
                    $"{loader.Name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}",
                    ModMain.HintType.Critical);
                break;
            }
            case ModBase.LoadState.Aborted:
            {
                ModMain.Hint($"{loader.Name}{Lang.Text("Common.Status.Cancelled")}");
                break;
            }
            case ModBase.LoadState.Loading:
            {
                return; // 不重新加载实例列表
            }
        }

        if (loader.State != ModBase.LoadState.Finished &&
                Directory.Exists(
                    $"{combo.Input}PCLInstallBackups\\")) // 实例修改失败回滚
        {
            ModBase.CopyDirectory(
                $"{combo.Input}PCLInstallBackups\\",
                (string)combo.Input);
            File.Delete($"{combo.Input}.pclignore");
            ModBase.DeleteDirectory(
                $"{combo.Input}PCLInstallBackups\\");
        }
        else
        {
            McInstallFailedClearFolder(Loader);
        }

        ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
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
                        $"{((ModLoader.LoaderCombo)Loader).Input}saves\\") ||
                    Directory.Exists(
                        $"{((ModLoader.LoaderCombo)Loader).Input}versions\\") ||
                    Directory.Exists(
                        $"{((ModLoader.LoaderCombo)Loader).Input}mods\\") ||
                    File.Exists($"{((ModLoader.LoaderCombo)Loader).Input}server.dat"))
                {
                    ModBase.Log(
                        $"[Download] 由于实例已被独立启动，不清理实例文件夹：{((ModLoader.LoaderCombo)Loader).Input}", ModBase.LogLevel.Developer);
                }
                else
                {
                    ModBase.Log(
                        $"[Download] 由于下载失败或取消，清理实例文件夹：{((ModLoader.LoaderCombo)Loader).Input}", ModBase.LogLevel.Developer);
                    ModBase.DeleteDirectory((string)((ModLoader.LoaderCombo)Loader).Input);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "下载失败或取消后清理实例文件夹失败");
        }
    }

    private const string McInstallDefaultType = "安装";

    /// <summary>
    ///     进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    /// </summary>
    public static bool McInstall(McInstallRequest Request, string Type = McInstallDefaultType)
    {
        try
        {
            var SubLoaders = McInstallLoader(Request, IgnoreDump: Type != McInstallDefaultType);
            if (SubLoaders is null)
                return false;
            var Loader = new ModLoader.LoaderCombo<string>(Request.TargetInstanceName + " " + Type, SubLoaders)
                { OnStateChanged = McInstallState };

            // 启动
            Loader.Start(Request.TargetInstanceFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        var TempMcFolder = ModMain.RequestTaskTempFolder(Request.OptiFineEntry is not null ||
                                                         Request.ForgeEntry is not null ||
                                                         Request.NeoForgeEntry is not null);

        // 获取参数
        var InstanceFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", Request.TargetInstanceName);
        if (Directory.Exists(TempMcFolder))
            ModBase.DeleteDirectory(TempMcFolder);
        string OptiFineFolder = null;
        if (Request.OptiFineVersion is not null)
        {
            if (Request.OptiFineVersion.Contains("_HD_U_"))
                Request.OptiFineVersion = "HD_U_" + Request.OptiFineVersion.AfterLast("_HD_U_"); // #735
            Request.OptiFineEntry = new ModDownload.DlOptiFineListEntry
            {
                DisplayName = Request.MinecraftName + " " + Request.OptiFineVersion.Replace("HD_U_", "")
                    .Replace("_", "").Replace("pre", " pre"),
                Inherit = Request.MinecraftName,
                IsPreview = Request.OptiFineVersion.ContainsF("pre", true),
                NameVersion = Request.MinecraftName + "-OptiFine_" + Request.OptiFineVersion,
                NameFile = (Request.OptiFineVersion.ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" +
                           Request.MinecraftName + "_" + Request.OptiFineVersion + ".jar"
            };
        }

        if (Request.OptiFineEntry is not null)
            OptiFineFolder = Path.Combine(TempMcFolder, "versions", Request.OptiFineEntry.NameVersion);
        string ForgeFolder = null;
        if (Request.ForgeEntry is not null)
            Request.ForgeVersion = Request.ForgeVersion ?? Request.ForgeEntry.VersionName;
        if (Request.ForgeVersion is not null)
            ForgeFolder = Path.Combine(TempMcFolder, "versions", "forge-" + Request.ForgeVersion);
        string NeoForgeFolder = null;
        if (Request.NeoForgeEntry is not null)
            Request.NeoForgeVersion = Request.NeoForgeVersion ?? Request.NeoForgeEntry.VersionName;
        if (Request.NeoForgeVersion is not null)
            NeoForgeFolder = Path.Combine(TempMcFolder, "versions", "neoforge-" + Request.NeoForgeVersion);
        string CleanroomFolder = null;
        if (Request.CleanroomEntry is not null)
            Request.CleanroomVersion = Request.CleanroomVersion ?? Request.CleanroomEntry.VersionName;
        if (Request.CleanroomVersion is not null)
            CleanroomFolder = Path.Combine(TempMcFolder, "versions", "cleanroom-" + Request.CleanroomVersion);
        string FabricFolder = null;
        if (Request.FabricVersion is not null)
            FabricFolder = Path.Combine(TempMcFolder, "versions", "fabric-loader-" + Request.FabricVersion + "-" +
                           Request.MinecraftName);
        string LegacyFabricFolder = null;
        if (Request.LegacyFabricVersion is not null)
            LegacyFabricFolder = Path.Combine(TempMcFolder, "versions", "legacy-fabric-loader-" + Request.LegacyFabricVersion + "-" +
                                 Request.MinecraftName);
        string QuiltFolder = null;
        if (Request.QuiltVersion is not null)
            QuiltFolder = Path.Combine(TempMcFolder, "versions", "quilt-loader-" + Request.QuiltVersion + "-" + Request.MinecraftName);
        string LabyModFolder = null;
        if (Request.LabyModCommitRef is not null)
            LabyModFolder = Path.Combine(TempMcFolder, "versions", "labymod-" + Request.LabyModCommitRef + "-" +
                            Request.MinecraftName);
        string LiteLoaderFolder = null;
        if (Request.LiteLoaderEntry is not null)
            LiteLoaderFolder = Path.Combine(TempMcFolder, "versions", Request.MinecraftName + "-LiteLoader");

        // 判断 OptiFine 是否作为 Mod 进行下载
        var Modable = Request.FabricVersion is not null || Request.LegacyFabricVersion is not null ||
                      Request.ForgeEntry is not null || Request.NeoForgeEntry is not null ||
                      Request.LiteLoaderEntry is not null;
        var ModsTempFolder = Path.Combine(TempMcFolder, "mods");
        var OptiFineAsMod = Request.OptiFineEntry is not null && Modable; // 选择了 OptiFine 与任意 Mod 加载器
        if (OptiFineAsMod)
        {
            ModBase.Log("[Download] OptiFine 将作为 Mod 进行下载");
            if (Request.LiteLoaderEntry is not null)
                OptiFineFolder = Path.Combine(ModsTempFolder, Request.MinecraftName);
            else
                OptiFineFolder = ModsTempFolder;
        }

        // 记录日志
        if (OptiFineFolder is not null)
            ModBase.Log("[Download] OptiFine 缓存：" + OptiFineFolder);
        if (ForgeFolder is not null)
            ModBase.Log("[Download] Forge 缓存：" + ForgeFolder);
        if (NeoForgeFolder is not null)
            ModBase.Log("[Download] NeoForge 缓存：" + NeoForgeFolder);
        if (CleanroomFolder is not null)
            ModBase.Log("[Download] Cleanroom 缓存：" + CleanroomFolder);
        if (FabricFolder is not null)
            ModBase.Log("[Download] Fabric 缓存：" + FabricFolder);
        if (LegacyFabricFolder is not null)
            ModBase.Log("[Download] LegacyFabric 缓存：" + LegacyFabricFolder);
        if (QuiltFolder is not null)
            ModBase.Log("[Download] Quilt 缓存：" + QuiltFolder);
        if (LabyModFolder is not null)
            ModBase.Log("[Download] LabyMod 缓存：" + LabyModFolder);
        if (LiteLoaderFolder is not null)
            ModBase.Log("[Download] LiteLoader 缓存：" + LiteLoaderFolder);
        ModBase.Log("[Download] 对应的原版版本：" + Request.MinecraftName);

        // 重复实例检查
        if (File.Exists(Path.Combine(InstanceFolder, Request.TargetInstanceName + ".json")) && !IgnoreDump)
        {
            ModMain.Hint(Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", Request.TargetInstanceName, ""),
                ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        var LoaderList = new List<ModLoader.LoaderBase>();
        // 添加忽略标识
        LoaderList.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Minecraft.Download.Stage.AddIgnoreFlag"),
                _ => ModBase.WriteFile(Path.Combine(InstanceFolder, ".pclignore"), "用于临时地在 PCL 的实例列表中屏蔽此实例。"))
            { Show = false, Block = false });
        // Fabric API
        if (Request.FabricApi is not null)
            LoaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadFabricApi"),
                    new List<DownloadFile> { Request.FabricApi.ToNetFile(ModsTempFolder) })
                { ProgressWeight = 3d, Block = false });
        // LegacyFabric API
        if (Request.LegacyFabricApi is not null)
            LoaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLegacyFabricApi"),
                    new List<DownloadFile> { Request.LegacyFabricApi.ToNetFile(ModsTempFolder) })
                { ProgressWeight = 3d, Block = false });
        // Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL)
        if (Request.QSL is not null)
            LoaderList.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadQfapiQsl"),
                        new List<DownloadFile> { Request.QSL.ToNetFile(ModsTempFolder) })
                    { ProgressWeight = 3d, Block = false });
        // OptiFabric
        if (Request.OptiFabric is not null)
            LoaderList.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFabric"),
                    new List<DownloadFile> { Request.OptiFabric.ToNetFile(ModsTempFolder) })
                { ProgressWeight = 3d, Block = false });
        // LabyMod
        if (Request.LabyModCommitRef is not null)
        {
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "LabyMod", Request.LabyModCommitRef),
                McDownloadLabyModLoader(Request.LabyModCommitRef, Request.LabyModChannel, Request.MinecraftName,
                    TempMcFolder, false)) { Show = false, ProgressWeight = 10d, Block = true });
            goto LabyModSkip;
        }

        // 原版
        var ClientLoader = new ModLoader.LoaderCombo<string>(
            Lang.Text(
                "Minecraft.Download.Stage.LoaderDownloadCombo",
                Lang.Text("Minecraft.Version.Vanilla"),
                Request.MinecraftName
            ),
            McDownloadClientLoader(
                Request.MinecraftName, Request.MinecraftJson, Request.TargetInstanceName
            )
        )
        {
            Show = false,
            ProgressWeight = 39d,
            Block = Request.ForgeVersion is null && Request.NeoForgeVersion is null && Request.OptiFineEntry is null &&
                    Request.FabricVersion is null && Request.LiteLoaderEntry is null && Request.QuiltVersion is null &&
                    Request.CleanroomEntry is null && Request.LegacyFabricVersion is null
        };
        LoaderList.Add(ClientLoader);
        // OptiFine
        if (Request.OptiFineEntry is not null)
        {
            if (OptiFineAsMod)
                LoaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "OptiFine",
                        Request.OptiFineEntry.DisplayName),
                    McDownloadOptiFineSaveLoader(Request.OptiFineEntry,
                        Path.Combine(OptiFineFolder, Request.OptiFineEntry.NameFile)))
                {
                    Show = false,
                    ProgressWeight = 16d,
                    Block = Request.ForgeVersion is null && Request.NeoForgeVersion is null &&
                            Request.FabricVersion is null && Request.LiteLoaderEntry is null
                });
            else
                LoaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "OptiFine",
                        Request.OptiFineEntry.DisplayName),
                    McDownloadOptiFineLoader(Request.OptiFineEntry, TempMcFolder, ClientLoader,
                        Request.TargetInstanceFolder, false))
                {
                    Show = false,
                    ProgressWeight = 24d,
                    Block = Request.ForgeVersion is null && Request.NeoForgeVersion is null &&
                            Request.FabricVersion is null && Request.LiteLoaderEntry is null
                });
        }

        // Forge
        if (Request.ForgeVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Forge", Request.ForgeVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.Forge, Request.ForgeVersion, "forge-" + Request.ForgeVersion,
                    Request.MinecraftName, Request.ForgeEntry, TempMcFolder, ClientLoader,
                    Request.TargetInstanceFolder))
            {
                Show = false, ProgressWeight = 25d,
                Block = Request.FabricVersion is null && Request.LiteLoaderEntry is null &&
                        Request.NeoForgeEntry is null
            });
        // NeoForge
        if (Request.NeoForgeVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "NeoForge", Request.NeoForgeVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge, Request.NeoForgeVersion, "neoforge-" + Request.NeoForgeVersion,
                    Request.MinecraftName, Request.NeoForgeEntry, TempMcFolder, ClientLoader,
                    Request.TargetInstanceFolder))
            {
                Show = false, ProgressWeight = 25d,
                Block = Request.ForgeEntry is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null
            });
        // Cleanroom
        if (Request.CleanroomVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Cleanroom", Request.CleanroomVersion),
                McDownloadForgelikeLoader(ModDownload.DlForgelikeEntry.ForgelikeType.Cleanroom, Request.CleanroomVersion,
                    "cleanroom-" + Request.CleanroomVersion, Request.MinecraftName, Request.CleanroomEntry,
                    TempMcFolder, ClientLoader, Request.TargetInstanceFolder))
            {
                Show = false, ProgressWeight = 25d,
                Block = Request.ForgeEntry is null && Request.FabricVersion is null && Request.LiteLoaderEntry is null
            });
        // LiteLoader
        if (Request.LiteLoaderEntry is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "LiteLoader", Request.MinecraftName),
                McDownloadLiteLoaderLoader(Request.LiteLoaderEntry, TempMcFolder, ClientLoader, false))
            {
                Show = false,
                ProgressWeight = 1d,
                Block = Request.FabricVersion is null
            });
        // Fabric
        if (Request.FabricVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Fabric", Request.FabricVersion),
                McDownloadFabricLoader(Request.FabricVersion, Request.MinecraftName, TempMcFolder, false))
            {
                Show = false,
                ProgressWeight = 2d,
                Block = true
            });
        // LegacyFabric
        if (Request.LegacyFabricVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Legacy Fabric", Request.LegacyFabricVersion),
                McDownloadLegacyFabricLoader(Request.LegacyFabricVersion, Request.MinecraftName, TempMcFolder, false))
            {
                Show = false,
                ProgressWeight = 2d,
                Block = true
            });
        // Quilt
        if (Request.QuiltVersion is not null)
            LoaderList.Add(new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.LoaderDownloadCombo", "Quilt", Request.QuiltVersion),
                    McDownloadQuiltLoader(Request.QuiltVersion, Request.MinecraftName, TempMcFolder, false))
                { Show = false, ProgressWeight = 2d, Block = true });

        LabyModSkip: ;

        // 合并安装
        LoaderList.Add(new ModLoader.LoaderTask<string, string>(Lang.Text("Minecraft.Download.Stage.InstallGame"),
            Task =>
        {
            // 合并 JSON
            MergeJson(InstanceFolder, InstanceFolder, OptiFineFolder, OptiFineAsMod, ForgeFolder, Request.ForgeVersion,
                NeoForgeFolder, Request.NeoForgeVersion, CleanroomFolder, Request.CleanroomVersion, FabricFolder,
                QuiltFolder, LabyModFolder, Request.LabyModChannel, LiteLoaderFolder, Request.MMCPackInfo,
                LegacyFabricFolder);
            Task.Progress = 0.2d;
            // 迁移文件
            if (Directory.Exists(Path.Combine(TempMcFolder, "libraries")))
                ModBase.CopyDirectory(Path.Combine(TempMcFolder, "libraries"), Path.Combine(ModMinecraft.McFolderSelected, "libraries"));
            Task.Progress = 0.8d;
            // 创建 Mod 和资源包文件夹
            var ModsFolder = Path.Combine(new ModMinecraft.McInstance(InstanceFolder).PathIndie, "mods"); // 版本隔离信息在此时被决定
            if (Directory.Exists(ModsTempFolder))
            {
                ModBase.CopyDirectory(ModsTempFolder, ModsFolder);
            }
            else if (Modable)
            {
                Directory.CreateDirectory(ModsFolder);
                ModBase.Log("[Download] 自动创建 Mod 文件夹：" + ModsFolder);
            }

            var ResourcepacksFolder = Path.Combine(new ModMinecraft.McInstance(InstanceFolder).PathIndie, "resourcepacks");
            Directory.CreateDirectory(ResourcepacksFolder);
            ModBase.Log("[Download] 自动创建资源包文件夹：" + ResourcepacksFolder);
        })
        {
            ProgressWeight = 2d,
            Block = true
        });
        // 补全文件
        if (!DontFixLibraries && (Request.OptiFineEntry is not null ||
                                  (Request.ForgeVersion is not null &&
                                   Convert.ToDouble(Request.ForgeVersion.BeforeFirst(".")) >= 20d) ||
                                  Request.NeoForgeVersion is not null || Request.FabricVersion is not null ||
                                  Request.QuiltVersion is not null || Request.CleanroomVersion is not null ||
                                  Request.LiteLoaderEntry is not null || Request.LabyModCommitRef is not null))
        {
            var LoadersLib = new List<ModLoader.LoaderBase>();
            if (Request.LabyModCommitRef is not null)
            {
                var LabyModClientLoader = new ModLoader.LoaderCombo<string>(
                    Lang.Text(
                        "Minecraft.Download.Stage.LoaderDownloadCombo",
                        Lang.Text("Minecraft.Version.Vanilla"), Request.MinecraftName
                    ),
                    McDownloadLabyModClientLoader(
                        Request.MinecraftName, Request.LabyModChannel,
                        Request.LabyModCommitRef, Request.TargetInstanceName
                    )
                )
                {
                    Show = false, ProgressWeight = 39d, Block = false
                };
                LoaderList.Add(LabyModClientLoader);
            }
            else
            {
                LoadersLib.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                        Lang.Text("Minecraft.Download.Stage.AnalyzeGameLibrariesSide"),
                        Task => Task.Output =
                            ModMinecraft.McLibNetFilesFromInstance(new ModMinecraft.McInstance(InstanceFolder)))
                    { ProgressWeight = 1d, Show = false });
                LoadersLib.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadGameLibrariesSide"),
                        new List<DownloadFile>())
                    { ProgressWeight = 7d, Show = false });
                LoaderList.Add(
                    new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadGameLibraries"),
                        LoadersLib) { ProgressWeight = 8d });
            }
        }

        // 删除忽略标识
        LoaderList.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
                _ => File.Delete(Path.Combine(InstanceFolder, ".pclignore")))
            { Show = false });
        // 总加载器
        return LoaderList;
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

        var HasOptiFine = OptiFineFolder is not null && !OptiFineAsMod;
        var HasForge = ForgeFolder is not null;
        var HasLegacyFabric = LegacyFabricFolder is not null;
        var HasNeoForge = NeoForgeFolder is not null;
        var HasCleanroom = CleanroomFolder is not null;
        var HasLiteLoader = LiteLoaderFolder is not null;
        var HasFabric = FabricFolder is not null;
        var HasQuilt = QuiltFolder is not null;
        var HasLabyMod = LabyModFolder is not null;
        string OutputName;
        string MinecraftName;
        string OptiFineName;
        string ForgeName;
        string NeoForgeName;
        string CleanroomName;
        string LiteLoaderName;
        string FabricName;
        string LegacyFabricName;
        string QuiltName;
        string LabyModName;
        string OutputJsonPath;
        string MinecraftJsonPath;
        string OptiFineJsonPath = null;
        string ForgeJsonPath = null;
        string NeoForgeJsonPath = null;
        string CleanroomJsonPath = null;
        string LiteLoaderJsonPath = null;
        string FabricJsonPath = null;
        string QuiltJsonPath = null;
        string LabyModJsonPath = null;
        string LegacyFabricJsonPath = null;
        string OutputJar;
        string MinecraftJar;

        #region 初始化路径信息

        if (!OutputFolder.EndsWithF(@"\"))
            OutputFolder += @"\";
        OutputName = ModBase.GetFolderNameFromPath(OutputFolder);
        OutputJsonPath = Path.Combine(OutputFolder, OutputName + ".json");
        OutputJar = Path.Combine(OutputFolder, OutputName + ".jar");

        if (!MinecraftFolder.EndsWithF(@"\"))
            MinecraftFolder += @"\";
        MinecraftName = ModBase.GetFolderNameFromPath(MinecraftFolder);
        MinecraftJsonPath = Path.Combine(MinecraftFolder, MinecraftName + ".json");
        MinecraftJar = Path.Combine(MinecraftFolder, MinecraftName + ".jar");

        if (HasOptiFine)
        {
            if (!OptiFineFolder.EndsWithF(@"\"))
                OptiFineFolder += @"\";
            OptiFineName = ModBase.GetFolderNameFromPath(OptiFineFolder);
            OptiFineJsonPath = Path.Combine(OptiFineFolder, OptiFineName + ".json");
        }

        if (HasForge)
        {
            if (!ForgeFolder.EndsWithF(@"\"))
                ForgeFolder += @"\";
            ForgeName = ModBase.GetFolderNameFromPath(ForgeFolder);
            ForgeJsonPath = Path.Combine(ForgeFolder, ForgeName + ".json");
        }

        if (HasNeoForge)
        {
            if (!NeoForgeFolder.EndsWithF(@"\"))
                NeoForgeFolder += @"\";
            NeoForgeName = ModBase.GetFolderNameFromPath(NeoForgeFolder);
            NeoForgeJsonPath = Path.Combine(NeoForgeFolder, NeoForgeName + ".json");
        }

        if (HasCleanroom)
        {
            if (!CleanroomFolder.EndsWithF(@"\"))
                CleanroomFolder += @"\";
            CleanroomName = ModBase.GetFolderNameFromPath(CleanroomFolder);
            CleanroomJsonPath = Path.Combine(CleanroomFolder, CleanroomName + ".json");
        }

        if (HasLiteLoader)
        {
            if (!LiteLoaderFolder.EndsWithF(@"\"))
                LiteLoaderFolder += @"\";
            LiteLoaderName = ModBase.GetFolderNameFromPath(LiteLoaderFolder);
            LiteLoaderJsonPath = Path.Combine(LiteLoaderFolder, LiteLoaderName + ".json");
        }

        if (HasFabric)
        {
            if (!FabricFolder.EndsWithF(@"\"))
                FabricFolder += @"\";
            FabricName = ModBase.GetFolderNameFromPath(FabricFolder);
            FabricJsonPath = Path.Combine(FabricFolder, FabricName + ".json");
        }

        if (HasLegacyFabric)
        {
            if (!LegacyFabricFolder.EndsWithF(@"\"))
                LegacyFabricFolder += @"\";
            LegacyFabricName = ModBase.GetFolderNameFromPath(LegacyFabricFolder);
            LegacyFabricJsonPath = Path.Combine(LegacyFabricFolder, LegacyFabricName + ".json");
        }

        if (HasQuilt)
        {
            if (!QuiltFolder.EndsWithF(@"\"))
                QuiltFolder += @"\";
            QuiltName = ModBase.GetFolderNameFromPath(QuiltFolder);
            QuiltJsonPath = Path.Combine(QuiltFolder, QuiltName + ".json");
        }

        if (HasLabyMod)
        {
            if (!LabyModFolder.EndsWithF(@"\"))
                LabyModFolder += @"\";
            LabyModName = ModBase.GetFolderNameFromPath(LabyModFolder);
            LabyModJsonPath = Path.Combine(LabyModFolder, LabyModName + ".json");
        }

        #endregion

        JsonObject OutputJson;
        JsonObject MinecraftJson = null;
        JsonObject OptiFineJson = null;
        JsonObject ForgeJson = null;
        JsonObject NeoForgeJson = null;
        JsonObject LegacyFabricJson = null;
        JsonObject CleanroomJson = null;
        JsonObject LiteLoaderJson = null;
        JsonObject FabricJson = null;
        JsonObject QuiltJson = null;
        JsonObject LabyModJson = null;

        #region 读取文件并检查文件是否合规

        var MinecraftJsonText = ModBase.ReadFile(MinecraftJsonPath);
        if (!HasLabyMod)
        {
            if (!MinecraftJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Minecraft", MinecraftJsonPath,
                    MinecraftJsonText.Substring(0, Math.Min(MinecraftJsonText.Length, 1000))));
            MinecraftJson = (JsonObject)ModBase.GetJson(MinecraftJsonText);
        }

        if (HasOptiFine)
        {
            var OptiFineJsonText = ModBase.ReadFile(OptiFineJsonPath);
            if (!OptiFineJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "OptiFine", OptiFineJsonPath,
                    OptiFineJsonText.Substring(0, Math.Min(OptiFineJsonText.Length, 1000))));
            OptiFineJson = (JsonObject)ModBase.GetJson(OptiFineJsonText);
        }

        if (HasForge)
        {
            var ForgeJsonText = ModBase.ReadFile(ForgeJsonPath);
            if (!ForgeJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Forge", ForgeJsonPath,
                    ForgeJsonText.Substring(0, Math.Min(ForgeJsonText.Length, 1000))));
            ForgeJson = (JsonObject)ModBase.GetJson(ForgeJsonText);
        }

        if (HasNeoForge)
        {
            var NeoForgeJsonText = ModBase.ReadFile(NeoForgeJsonPath);
            if (!NeoForgeJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "NeoForge", NeoForgeJsonPath,
                    NeoForgeJsonText.Substring(0, Math.Min(NeoForgeJsonText.Length, 1000))));
            NeoForgeJson = (JsonObject)ModBase.GetJson(NeoForgeJsonText);
        }

        if (HasCleanroom)
        {
            var CleanroomJsonText = ModBase.ReadFile(CleanroomJsonPath);
            if (!CleanroomJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Cleanroom", CleanroomJsonPath,
                    CleanroomJsonText.Substring(0, Math.Min(CleanroomJsonText.Length, 1000))));
            CleanroomJson = (JsonObject)ModBase.GetJson(CleanroomJsonText);
        }

        if (HasLiteLoader)
        {
            var LiteLoaderJsonText = ModBase.ReadFile(LiteLoaderJsonPath);
            if (!LiteLoaderJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "LiteLoader", LiteLoaderJsonPath,
                    LiteLoaderJsonText.Substring(0, Math.Min(LiteLoaderJsonText.Length, 1000))));
            LiteLoaderJson = (JsonObject)ModBase.GetJson(LiteLoaderJsonText);
        }

        if (HasFabric)
        {
            var FabricJsonText = ModBase.ReadFile(FabricJsonPath);
            if (!FabricJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Fabric", FabricJsonPath,
                    FabricJsonText.Substring(0, Math.Min(FabricJsonText.Length, 1000))));
            FabricJson = (JsonObject)ModBase.GetJson(FabricJsonText);
        }

        if (HasLegacyFabric)
        {
            var LegacyFabricJsonText = ModBase.ReadFile(LegacyFabricJsonPath);
            if (!LegacyFabricJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Legacy Fabric", FabricJsonPath,
                    LegacyFabricJsonText.Substring(0, Math.Min(LegacyFabricJsonText.Length, 1000))));
            LegacyFabricJson = (JsonObject)ModBase.GetJson(LegacyFabricJsonText);
        }

        if (HasQuilt)
        {
            var QuiltJsonText = ModBase.ReadFile(QuiltJsonPath);
            if (!QuiltJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "Quilt", QuiltJsonPath,
                    QuiltJsonText.Substring(0, Math.Min(QuiltJsonText.Length, 1000))));
            QuiltJson = (JsonObject)ModBase.GetJson(QuiltJsonText);
        }

        if (HasLabyMod)
        {
            var LabyModJsonText = ModBase.ReadFile(LabyModJsonPath);
            if (!LabyModJsonText.StartsWithF("{"))
                throw new Exception(Lang.Text("Minecraft.Download.Error.JsonInvalid", "LabyMod", LabyModJsonPath,
                    LabyModJsonText.Substring(0, Math.Min(LabyModJsonText.Length, 1000))));
            LabyModJson = (JsonObject)ModBase.GetJson(LabyModJsonText);
        }

        #endregion

        #region 处理 JSON 文件

        // 获取 minecraftArguments
        var AllArguments = (MinecraftJson is not null ? (MinecraftJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (LabyModJson is not null ? (LabyModJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (OptiFineJson is not null ? (OptiFineJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " + (ForgeJson is not null ? (ForgeJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " +
                           (NeoForgeJson is not null ? (NeoForgeJson["minecraftArguments"] ?? " ").ToString() : " ") +
                           " " + (LiteLoaderJson is not null
                               ? (LiteLoaderJson["minecraftArguments"] ?? " ").ToString()
                               : " ") + " " + (CleanroomJson is not null
                               ? (CleanroomJson["minecraftArguments"] ?? " ").ToString()
                               : " ");
        // 分割参数字符串
        var RawArguments = AllArguments.Split(" ").Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Trim()).ToList();
        var SplitArguments = new List<string>();
        for (int i = 0, loopTo = RawArguments.Count - 1; i <= loopTo; i++)
            if (RawArguments[i].StartsWithF("-"))
                SplitArguments.Add(RawArguments[i]);
            else if (SplitArguments.Any() && SplitArguments.Last().StartsWithF("-") &&
                     !SplitArguments.Last().Contains(" "))
                SplitArguments[SplitArguments.Count - 1] = SplitArguments.Last() + " " + RawArguments[i];
            else
                SplitArguments.Add(RawArguments[i]);

        var RealArguments = SplitArguments.Distinct().ToList().Join(" ");
        // 合并
        // 相关讨论见 #2801
        if (MMCPackInfo is not null)
        {
            if (MMCPackInfo.IsMinecraftOverrided)
            {
                ModBase.Log("[Download] 当前实例的 MC 核心已被修改，使用对应的 MMC 整合包参数");
                OutputJson = MMCPackInfo.OverridedJson;
            }
            else
            {
                ModBase.Log("[Download] 存在无修改 MC 核心文件的 MMC 整合包信息，应用相关参数");
                OutputJson = MinecraftJson;
                // 合并来自 MultiMC 的 JSON
                OutputJson.Merge(MMCPackInfo.OverridedJson);
            }
        }
        else
        {
            OutputJson = MinecraftJson;
        }

        if (HasOptiFine)
        {
            // 合并 OptiFine
            OptiFineJson.Remove("releaseTime");
            OptiFineJson.Remove("time");
            OutputJson.Merge(OptiFineJson);
        }

        if (HasForge)
            if (MMCPackInfo is null || !MMCPackInfo.IsForgeOverrided)
            {
                // 合并 Forge
                ForgeJson.Remove("releaseTime");
                ForgeJson.Remove("time");
                OutputJson.Merge(ForgeJson);
            }

        if (HasNeoForge)
            if (MMCPackInfo is null || !MMCPackInfo.IsNeoForgeOverrided)
            {
                // 合并 NeoForge
                NeoForgeJson.Remove("releaseTime");
                NeoForgeJson.Remove("time");
                OutputJson.Merge(NeoForgeJson);
            }

        if (HasCleanroom)
            if (MMCPackInfo is null || !MMCPackInfo.IsCleanroomOverrided)
            {
                // 合并 Cleanroom
                CleanroomJson.Remove("releaseTime");
                CleanroomJson.Remove("time");
                OutputJson.Merge(CleanroomJson);
            }

        if (HasLiteLoader)
        {
            // 合并 LiteLoader
            LiteLoaderJson.Remove("releaseTime");
            LiteLoaderJson.Remove("time");
            OutputJson.Merge(LiteLoaderJson);
        }

        if (HasFabric)
            if (MMCPackInfo is null || !MMCPackInfo.IsFabricOverrided)
            {
                // 合并 Fabric
                FabricJson.Remove("releaseTime");
                FabricJson.Remove("time");
                OutputJson.Merge(FabricJson);
            }

        if (HasLegacyFabric)
            if (MMCPackInfo is null || !MMCPackInfo.IsFabricOverrided)
            {
                // 合并 Fabric
                LegacyFabricJson.Remove("releaseTime");
                LegacyFabricJson.Remove("time");
                OutputJson.Merge(LegacyFabricJson);
            }

        if (HasQuilt)
            if (MMCPackInfo is null || !MMCPackInfo.IsQuiltOverrided)
            {
                // 合并 Quilt
                QuiltJson.Remove("releaseTime");
                QuiltJson.Remove("time");
                OutputJson.Merge(QuiltJson);
            }

        if (HasLabyMod)
        {
            // 合并 LabyMod
            LabyModJson.Remove("releaseTime");
            LabyModJson.Remove("time");
            if (OutputJson is null)
                OutputJson = new JsonObject();
            OutputJson.Merge(LabyModJson);

            var LabyModLib =
                (JsonObject)Requester.FetchJson(
                    $"https://releases.r2.labymod.net/api/v1/libraries/{LabyModChannel}.json", RequestParam.WithRetry);
            var LabyModCore = (JsonObject)Requester.FetchJson(
                $"https://releases.r2.labymod.net/api/v1/manifest/{LabyModChannel}/latest.json", RequestParam.WithRetry);
            var OutputLibraries = new JsonArray();
            var IsolatedLibraries = new Dictionary<string, bool>();
            var MinecraftVersion = LabyModJson["_minecraftVersion"];

            foreach (var Library in LabyModLib["isolated_libraries"].AsArray())
                if (((JsonArray)Library["versions"]).Contains(MinecraftVersion))
                    IsolatedLibraries.Add(Library["name"].ToString(), true);

            foreach (var Library in LabyModJson["libraries"].AsArray())
            {
                var RegexMatchResult = Library["name"].ToString().RegexSeek(RegexPatterns.CatchLwjglInLib);
                if (RegexMatchResult is null ||
                    !IsolatedLibraries.Contains(new KeyValuePair<string, bool>(RegexMatchResult, true)))
                    OutputLibraries.Add(Library);
            }

            foreach (var Library in LabyModLib["libraries"].AsArray())
            {
                var libraryUrl = Library?["url"]?.ToString() ?? "";
                OutputLibraries.Add(new JsonObject
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

            var labyModCommitReference = LabyModCore["commitReference"]?.ToString() ?? "";
            OutputLibraries.Add(new JsonObject
            {
                ["name"] = "net.labymod:LabyMod:4",
                ["downloads"] = new JsonObject
                {
                    ["artifact"] = new JsonObject
                    {
                        ["path"] = "net/labymod/LabyMod/4/LabyMod-4.jar",
                        ["sha1"] = LabyModCore["sha1"]?.ToString(),
                        ["size"] = LabyModCore["size"]?.DeepClone(),
                        ["url"] = $"https://releases.r2.labymod.net/api/v1/download/labymod4/{LabyModChannel}/{labyModCommitReference}.jar"
                    }
                }
            });
            OutputJson["libraries"] = OutputLibraries;
            OutputJson.Add("labymod_data", new JsonObject
            {
                ["channelType"] = LabyModChannel,
                ["commitReference"] = labyModCommitReference,
                ["version"] = LabyModCore["labyModVersion"]?.ToString(),
                ["versionType"] = "release"
            });
        }

        // 修改
        if (RealArguments is not null && !string.IsNullOrEmpty(RealArguments.Replace(" ", "")))
            OutputJson["minecraftArguments"] = RealArguments;
        if (MMCPackInfo is not null && MMCPackInfo.IsMcArgsEdited)
            OutputJson.Remove("minecraftArguments");
        OutputJson.Remove("_comment_");
        OutputJson.Remove("inheritsFrom");
        OutputJson.Remove("jar");
        OutputJson["id"] = OutputName;

        #endregion

        #region 保存

        ModBase.WriteFile(OutputJsonPath, OutputJson.ToString());
        if ((MinecraftJar ?? "") != (OutputJar ?? "")) // 可能是同一个文件
        {
            if (File.Exists(OutputJar))
                File.Delete(OutputJar);
            ModBase.CopyFile(MinecraftJar, OutputJar);
        }

        ModBase.Log("[Download] 实例合并 " + OutputName + " 完成");

        #endregion
    }

    #endregion
}
