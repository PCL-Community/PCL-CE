using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem LiteLoaderDownloadListItem(
        GameDownload.DlLiteLoaderListEntry entry,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        var newItem = new MyListItem
        {
            Title = entry.Inherit ?? "",
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Logo = $"{Common.PathImage}Blocks/Egg.png"
        };
        LocalizedText.TryBind(newItem, "Info", () =>
        {
            var infoParts = new List<string>
            {
                entry.IsPreview
                    ? Lang.Text("Download.Version.Type.Preview")
                    : Lang.Text("Download.Version.Type.Stable")
            };

            if (entry.ReleaseTime is not null)
                infoParts.Add(Lang.Text("Download.Version.ReleaseDate",
                    Lang.Date(entry.ReleaseTime.Value, "g")));

            return string.Join("  |  ", infoParts);
        });

        newItem.Click += onClick;
        newItem.ContentHandler = isSaveOnly ? LiteLoaderSaveContMenuBuild : LiteLoaderContMenuBuild;
        return newItem;
    }

    private static void LiteLoaderSaveContMenuBuild(MyListItem sender, EventArgs e)
    {
        if ((bool)((dynamic)sender.Tag).IsLegacy)
        {
            sender.Buttons = [];
        }
        else
        {
            var btnList = new MyIconButton { SvgIcon = "lucide/list", Tag = sender };
            McDownloadContMenuLocalize(btnList, "Download.Version.ViewAllVersions");
            ToolTipService.SetPlacement(btnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnList, 30d);
            ToolTipService.SetHorizontalOffset(btnList, 2d);
            btnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = [btnList];
        }
    }

    private static void LiteLoaderContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton { SvgIcon = "lucide/save", Tag = sender };
        McDownloadContMenuLocalize(btnSave, "Download.Version.SaveInstaller");
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
            var btnList = new MyIconButton { SvgIcon = "lucide/list", Tag = sender };
            McDownloadContMenuLocalize(btnList, "Download.Version.ViewAllVersions");
            ToolTipService.SetPlacement(btnList, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnList, 30d);
            ToolTipService.SetHorizontalOffset(btnList, 2d);
            btnList.Click += (sender, e) => LiteLoaderAll_Click(sender, (RoutedEventArgs)e);
            sender.Buttons = [btnSave, btnList];
        }
    }

    private static void LiteLoaderAll_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlLiteLoaderListEntry version;
        if (((dynamic)sender).Tag is GameDownload.DlLiteLoaderListEntry)
            version = (GameDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            version = (GameDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        Common.OpenWebsite($"https://jenkins.liteloader.com/view/{version.Inherit}");
    }

    public static void LiteLoaderSave_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlLiteLoaderListEntry version;
        if (((dynamic)sender).Tag is GameDownload.DlLiteLoaderListEntry)
            version = (GameDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag;
        else
            version = (GameDownload.DlLiteLoaderListEntry)((dynamic)sender).Tag.Tag;
        McDownloadLiteLoaderSave(version);
    }
}