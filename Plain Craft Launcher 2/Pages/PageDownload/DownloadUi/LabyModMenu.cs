using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem LabyModDownloadListItem(
        JsonObject entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title =
                $"{entry["version"]} {(
                    (entry["channel"]?.ToString() ?? "").Contains("snapshot")
                        ? Lang.Text("Download.Version.Type.Snapshot")
                        : Lang.Text("Download.Version.Type.Stable")
                )}",
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = (entry["channel"]?.ToString() ?? "").Contains("snapshot")
                ? Lang.Text("Download.Version.Type.Snapshot")
                : Lang.Text("Download.Version.Type.Stable"),
            Logo = $"{Common.PathImage}Blocks/LabyMod.png"
        };
        newItem.Click += onClick;
        newItem.ContentHandler = LabyModContMenuBuild;
        return newItem;
    }

    private static void LabyModContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton
        {
            SvgIcon = "lucide/save",
            ToolTip = Lang.Text("Download.Version.SaveAs")
        };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (a, b) => LabyModSave_Click(a, (dynamic)b);
        var btnInfo = new MyIconButton
        {
            LogoScale = 1.05d,
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Download.Version.Changelog")
        };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => LabyModLog_Click(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[]
        {
            btnSave,
            btnInfo
        };
    }

    private static void LabyModLog_Click(object sender, RoutedEventArgs e)
    {
        Common.OpenWebsite("https://www.labymod.net/zh_Hans/download");
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
        if (version["channel"]?.ToString() == "snapshot")
            McDownloadLabyModSnapshotLoaderSave();
        else
            McDownloadLabyModProductionLoaderSave();
    }
}