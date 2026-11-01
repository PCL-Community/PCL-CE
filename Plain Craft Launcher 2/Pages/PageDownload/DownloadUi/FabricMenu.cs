using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class DownloadUi
{
    public static MyListItem FabricDownloadListItem(
        JsonObject entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title = entry["version"]?.ToString().Replace("+build", "") ?? "",
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = entry["stable"].ToObject<bool>()
                ? Lang.Text("Download.Version.Type.Stable")
                : Lang.Text("Download.Version.Type.Preview"),
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        newItem.Click += onClick;
        newItem.ContentHandler = FabricContMenuBuild;
        return newItem;
    }

    private static void FabricContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = new MyIconButton
        {
            LogoScale = 1.05d,
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Download.Version.Changelog")
        };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (a, b) => FabricLog_Click(a, (dynamic)b);
        ((dynamic)sender).Buttons = new[]
        {
            btnInfo
        };
    }

    private static void FabricLog_Click(object sender, RoutedEventArgs e)
    {
        Common.OpenWebsite("https://fabricmc.net/blog");
    }

    public static FabricApiVersionRow FabricApiVersionRowBuild(Comp.CompFile entry)
    {
        return new FabricApiVersionRow
        {
            Title = entry.DisplayName.Split("]")[1]
                .Replace("Fabric API ", "")
                .Replace(" build ", ".")
                .Trim(),
            Info = entry.StatusDescription
                   + Lang.Text("Download.Version.ReleaseDate", Lang.Date(entry.ReleaseDate, "g")),
            Logo = $"{Common.PathImage}Blocks/Fabric.png",
            Entry = entry
        };
    }

    public static MyListItem FabricApiDownloadListItem(
        Comp.CompFile entry,
        MyListItem.ClickEventHandler onClick)
    {
        var row = FabricApiVersionRowBuild(entry);
        var newItem = new MyListItem
        {
            Title = row.Title,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = row.Entry,
            Info = row.Info,
            Logo = row.Logo
        };
        newItem.Click += onClick;
        return newItem;
    }

    public static MyListItem OptiFabricDownloadListItem(
        Comp.CompFile entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title = entry
                .DisplayName.ToLower()
                .Replace("optifabric-", "")
                .Replace(".jar", "")
                .Trim()
                .TrimStart('v'),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = entry.StatusDescription
                + Lang.Text("Download.Version.ReleaseDate", Lang.Date(entry.ReleaseDate, "g")),
            Logo = $"{Common.PathImage}Blocks/OptiFabric.png"
        };
        newItem.Click += onClick;
        return newItem;
    }

    public static MyListItem LegacyFabricDownloadListItem(
        JsonObject entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title = entry["version"]?.ToString() ?? "",
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = entry["stable"].ToObject<bool>()
                ? Lang.Text("Download.Version.Type.Stable")
                : Lang.Text("Download.Version.Type.Preview"),
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        newItem.Click += onClick;
        return newItem;
    }

    public static MyListItem LegacyFabricApiDownloadListItem(
        Comp.CompFile entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title = entry.DisplayName.Replace("Legacy Fabric API ", ""),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = entry.StatusDescription
                + Lang.Text("Download.Version.ReleaseDate", Lang.Date(entry.ReleaseDate, "g")),
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        newItem.Click += onClick;
        return newItem;
    }
}