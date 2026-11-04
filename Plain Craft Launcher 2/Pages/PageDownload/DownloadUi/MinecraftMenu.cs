using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem McDownloadListItem(
        JsonObject entry,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly)
    {
        var row = GameVersionItemViewModel.FromEntry(entry);
        var newItem = new MyListItem
        {
            Logo = row.Logo,
            SnapsToDevicePixels = true,
            Title = row.Title,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = row.Entry
        };
        LocalizedText.TryBind(newItem, "Info", () => row.Info.Value);
        if (row.Tags is not null)
            newItem.Tags = row.Tags;
        newItem.Click += onClick;
        McDownloadListItemAttachMenu(newItem, isSaveOnly);
        return newItem;
    }

    /// <summary>
    ///     为版本列表项挂上懒加载的右键菜单。<br />
    ///     <see cref="MyListItem.ContentHandler" /> 不是依赖属性，XAML 数据模板无法直接设置，需在 code-behind 里调用。
    /// </summary>
    public static void McDownloadListItemAttachMenu(MyListItem item, bool isSaveOnly = true)
    {
        if (isSaveOnly)
            item.ContentHandler = McDownloadSaveMenuBuild;
        else
            item.ContentHandler = McDownloadMenuBuild;
    }

    private static void McDownloadSaveMenuBuild(object sender, EventArgs _)
    {
        var btnInfo = CreateChangelogButton();
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (ss, ee) => McDownloadMenuLog(ss, (dynamic)ee);
        var btnServer = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/server" };
        McDownloadContMenuLocalize(btnServer, "Download.Version.DownloadServer");
        ToolTipService.SetPlacement(btnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnServer, 30d);
        ToolTipService.SetHorizontalOffset(btnServer, 2d);
        btnServer.Click += (ss, ee) => McDownloadMenuSaveServer(ss, (dynamic)ee);
        ((dynamic)sender).Buttons = new[]
        {
            btnServer,
            btnInfo
        };
    }

    private static void McDownloadMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { SvgIcon = "lucide/save" };
        McDownloadContMenuLocalize(btnSave, "Download.Version.SaveAs");
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (a, b) => McDownloadMenuSave(a, (dynamic)b);
        var btnInfo = CreateChangelogButton();
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => McDownloadMenuLog(a, (dynamic)b);
        var btnServer = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/server" };
        McDownloadContMenuLocalize(btnServer, "Download.Version.DownloadServer");
        ToolTipService.SetPlacement(btnServer, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnServer, 30d);
        ToolTipService.SetHorizontalOffset(btnServer, 2d);
        btnServer.Click += (a, b) => McDownloadMenuSaveServer(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[]
        {
            btnSave,
            btnInfo,
            btnServer
        };
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

    private static MyListItem _ResolveVersionListItem(object sender)
    {
        if (sender is MyListItem item)
            return item;
        if (((dynamic)sender).Parent is MyListItem parent)
            return parent;
        return (MyListItem)((dynamic)sender).Parent.Parent;
    }

    public static void McDownloadMenuSave(object sender, RoutedEventArgs e)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-client-save"))
            return;
        var element = (FrameworkElement)sender;
        MyListItem version;
        if (element is MyListItem s1)
            version = s1;
        else if (element.Parent is MyListItem s2)
            version = s2;
        else
            version = (MyListItem)((FrameworkElement)element.Parent).Parent;
        try
        {
            var id = version.Title;
            var jsonUrl = ((JsonObject)version.Tag)["url"]!.ToString();
            var versionFolder = SystemDialogs.SelectFolder();
            if (!versionFolder.Contains('\\'))
                return;
            versionFolder = Path.Combine(versionFolder, id);

            foreach (var ongoingLoader in Loading.loaderTaskbar.ToList())
            {
                if (ongoingLoader.Identifier != McDownloadStageId(id))
                    continue;
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                    [
                        new DownloadFile(
                            GameDownload.DlSourceLauncherOrMetaGet(jsonUrl),
                            Path.Combine(versionFolder, $"{id}.json"),
                            new FileChecker(canUseExistsFile: false, isJson: true)
                        )
                    ]
                )
                {
                    ProgressWeight = 2d
                },
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    task => task.output =
                        GameDownload.DlClientJarGet(new McInstance(versionFolder), false) is { } jar
                            ? [jar]
                            : []
                )
                {
                    ProgressWeight = 0.5d,
                    show = false
                },
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), [])
                {
                    ProgressWeight = 5d
                }
            };

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id),
                loaders)
            {
                OnStateChanged = LoaderStateChangedHintOnly,
                Identifier = McDownloadStageId(id)
            };
            loader.Start(id);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 Minecraft 下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    /// <summary>
    ///     显示某 Minecraft 版本的更新日志。
    /// </summary>
    /// <param name="versionJson">在 version_manifest.json 中的对应项。</param>
    public static void McUpdateLogShow(JsonNode versionJson)
    {
        var wikiName = McFormatter.GetWikiUrlSuffix(versionJson["id"]?.ToString() ?? "");
        var wikiUrl = $"{McFormatter.GetWikiBaseUrl()}/{wikiName.TrimStart('/')}";
        Common.OpenWebsite(wikiUrl);
    }
}