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
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        newItem.SetLocalizedValue(
            MyListItem.InfoProperty,
            entry["stable"].ToObject<bool>()
                ? "Download.Version.Type.Stable"
                : "Download.Version.Type.Preview");
        newItem.Click += onClick;
        newItem.ContentHandler = FabricContMenuBuild;
        return newItem;
    }

    private static void FabricContMenuBuild(object sender, EventArgs e)
    {
        var btnInfo = CreateChangelogButton();
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

    public static MyListItem FabricApiDownloadListItem(
        Comp.CompFile entry,
        MyListItem.ClickEventHandler onClick)
    {
        var newItem = new MyListItem
        {
            Title = entry.DisplayName.Split("]")[1]
                .Replace("Fabric API ", "")
                .Replace(" build ", ".")
                .Trim(),
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        LocalizedText.TryBind(
            newItem,
            "Info",
            () => entry.StatusDescription + Lang.Text(
                "Download.Version.ReleaseDate",
                Lang.Date(entry.ReleaseDate, "g")));
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
            Logo = $"{Common.PathImage}Blocks/OptiFabric.png"
        };
        LocalizedText.TryBind(
            newItem,
            "Info",
            () => entry.StatusDescription + Lang.Text(
                "Download.Version.ReleaseDate",
                Lang.Date(entry.ReleaseDate, "g")));
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
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        // 发布状态按当前语言实时求值，切换语言后自动刷新
        LocalizedText.TryBind(
            newItem,
            "Info",
            () => entry["stable"]?.ToObject<bool>() == true
                ? Lang.Text("Download.Version.Type.Stable")
                : Lang.Text("Download.Version.Type.Preview"));
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
            Logo = $"{Common.PathImage}Blocks/Fabric.png"
        };
        LocalizedText.TryBind(
            newItem,
            "Info",
            () => entry.StatusDescription + Lang.Text(
                "Download.Version.ReleaseDate",
                Lang.Date(entry.ReleaseDate, "g")));
        newItem.Click += onClick;
        return newItem;
    }
}