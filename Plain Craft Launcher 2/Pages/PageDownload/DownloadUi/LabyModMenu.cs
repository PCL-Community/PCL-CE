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
        var isSnapshot = entry["channel"]?.ToString().Contains("snapshot") == true;
        var newItem = new MyListItem
        {
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Logo = $"{Common.PathImage}Blocks/LabyMod.png"
        };
        newItem.SetLocalizedValue(
            MyListItem.InfoProperty,
            isSnapshot
                ? "Download.Version.Type.Snapshot"
                : "Download.Version.Type.Stable");
        LocalizedText.TryBind(
            newItem,
            "Title",
            () => entry["version"] + " " + (isSnapshot
                ? Lang.Text("Download.Version.Type.Snapshot")
                : Lang.Text("Download.Version.Type.Stable")));
        newItem.Click += onClick;
        newItem.ContentHandler = LabyModContMenuBuild;
        return newItem;
    }

    private static void LabyModContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { SvgIcon = "lucide/save" };
        McDownloadContMenuLocalize(btnSave, "Download.Version.SaveAs");
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (a, b) => LabyModSave_Click(a, (dynamic)b);
        var btnInfo = CreateChangelogButton();
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