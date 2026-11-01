using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem McDownloadListItem(
        JsonObject entry,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly)
    {
        var logo = (entry["type"]?.ToString() ?? "") switch
        {
            "release" => $"{Common.PathImage}Blocks/Grass.png",
            "snapshot" or "pending" => $"{Common.PathImage}Blocks/CommandBlock.png",
            "special" => $"{Common.PathImage}Blocks/GoldBlock.png",
            _ => $"{Common.PathImage}Blocks/CobbleStone.png"
        };

        var versionId = entry["id"]?.ToString() ?? "";
        var formattedVersion = McFormatter.FormatVersion(versionId).Replace("_", " ");
        var newItem = new MyListItem
        {
            Logo = logo,
            SnapsToDevicePixels = true,
            Title = formattedVersion,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry
        };
        if (entry["lore"] is null)
        {
            var releaseTime = entry["releaseTime"].ToObject<DateTime>();
            LocalizedText.TryBind(
                newItem,
                "Info",
                () => formattedVersion != versionId
                    ? $"{Lang.Date(releaseTime, "g")} | {versionId}"
                    : Lang.Date(releaseTime, "g"));
        }
        else
        {
            var loreKey = entry["lore"].ToString();
            var loreRawArgs = (entry["loreArgs"] as JsonArray)
                ?.Select(object? (node) => node?.ToString())
                .ToArray() ?? [];
            var isDateArg = (string)entry["loreArgsType"] == "date";
            LocalizedText.TryBind(
                newItem,
                "Info",
                () =>
                {
                    var loreArgs = isDateArg &&
                                   loreRawArgs.Length == 1 &&
                                   DateTime.TryParse(
                                       loreRawArgs[0]?.ToString(),
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.RoundtripKind, out var loreDate)
                        ? [Lang.Date(loreDate, "g")]
                        : loreRawArgs;
                    return formattedVersion != versionId
                        ? $"{Lang.Text(loreKey, loreArgs)} | {versionId}"
                        : Lang.Text(loreKey, loreArgs);
                });
        }

        if ((entry["url"]?.ToString() ?? "").Contains("unlisted-versions-of-minecraft"))
            newItem.Tags = "Download.Source.Tag.Uvmc";
        newItem.Click += onClick;
        if (isSaveOnly)
            newItem.ContentHandler = McDownloadSaveMenuBuild;
        else
            newItem.ContentHandler = McDownloadMenuBuild;
        return newItem;
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
            if (!versionFolder.Contains(@"\"))
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
                            new Common.FileChecker(canUseExistsFile: false, isJson: true)
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