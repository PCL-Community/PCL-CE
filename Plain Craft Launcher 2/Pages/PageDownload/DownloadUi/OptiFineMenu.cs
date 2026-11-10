using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem OptiFineDownloadListItem(
        GameDownload.DlOptiFineListEntry entry,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        var newItem = new MyListItem
        {
            Title = entry.DisplayName ?? "",
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Logo = $"{Common.PathImage}Blocks/GrassPath.png"
        };
        LocalizedText.TryBind(newItem, "Info", () =>
        {
            var infoParts = new List<string>
            {
                entry.IsPreview
                    ? Lang.Text("Download.Version.Type.Preview")
                    : Lang.Text("Download.Version.Type.Release")
            };

            if (entry.ReleaseTime is not null)
                infoParts.Add(Lang.Text("Download.Version.ReleaseDate",
                    Lang.Date(entry.ReleaseTime.Value, "d")));

            if (entry.RequiredForgeVersion is null)
                infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
            else if (!string.IsNullOrEmpty(entry.RequiredForgeVersion))
                infoParts.Add(Lang.Text("Download.Version.Optifine.CompatibleForge",
                    entry.RequiredForgeVersion));

            return string.Join("  |  ", infoParts);
        });

        newItem.Click += onClick;
        newItem.ContentHandler = isSaveOnly ? OptiFineSaveContMenuBuild : OptiFineContMenuBuild;
        return newItem;
    }

    private static void OptiFineSaveContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = CreateChangelogButton();
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[]
        {
            btnInfo
        };
    }

    private static void OptiFineContMenuBuild(object sender, EventArgs e)
    {
        var btnSave = new MyIconButton { SvgIcon = "lucide/save" };
        McDownloadContMenuLocalize(btnSave, "Download.Version.SaveAs");
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        var btnInfo = CreateChangelogButton();
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => OptiFineLog_Click(sender, (RoutedEventArgs)e);
        ((dynamic)sender).Buttons = new[]
        {
            btnSave,
            btnInfo
        };
    }

    private static void OptiFineLog_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlOptiFineListEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        Common.OpenWebsite($"https://optifine.net/changelog?f={version.NameFile}");
    }

    public static void OptiFineSave_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlOptiFineListEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Tag;
        else
            version = (GameDownload.DlOptiFineListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadOptiFineSave(version);
    }
}