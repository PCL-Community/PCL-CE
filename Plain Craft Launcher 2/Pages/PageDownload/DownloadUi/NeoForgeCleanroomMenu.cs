using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    public static void NeoForgeDownloadListItemPreload(
        StackPanel stack,
        List<GameDownload.DlNeoForgeListEntry> entries,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        if (entries.Count == 1)
            return;
        GameDownload.DlNeoForgeListEntry? freshStableVersion = null;
        GameDownload.DlNeoForgeListEntry? freshBetaVersion = null;
        if (entries.Count != 0)
            foreach (var entry in entries.ToList())
                if (entry.IsBeta)
                {
                    freshBetaVersion ??= entry;
                }
                else
                {
                    freshStableVersion = entry;
                    break;
                }
        else
            Common.Log("[System] 未找到可用的 NeoForge 版本", Common.LogLevel.Debug);

        if (freshStableVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshStableVersion, onClick, isSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Stable")
                : $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {fresh.Info}";
            stack.Children.Add(fresh);
        }

        if (freshBetaVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshBetaVersion, onClick, isSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Development")
                : $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {fresh.Info}";
            stack.Children.Add(fresh);
        }

        stack.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Version.AllVersions", entries.Count),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 13d, 0d, 4d)
            }
        );
    }

    public static MyListItem NeoForgeDownloadListItem(
        GameDownload.DlNeoForgeListEntry info,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        var newItem = new MyListItem
        {
            Title = info.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = info,
            Info = info.IsBeta
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable"),
            Logo = $"{Common.PathImage}Blocks/NeoForge.png"
        };
        newItem.Click += onClick;
        if (isSaveOnly)
            newItem.ContentHandler = NeoForgeSaveContMenuBuild;
        else
            newItem.ContentHandler = NeoForgeContMenuBuild;
        return newItem;
    }

    private static void NeoForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton
        {
            SvgIcon = "lucide/save",
            ToolTip = Lang.Text("Download.Version.SaveAs")
        };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (sender, e) => NeoForgeSave_Click(sender, (RoutedEventArgs)e);
        var btnInfo = new MyIconButton
        {
            LogoScale = 1.05d,
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Download.Version.Changelog")
        };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = [btnSave, btnInfo];
    }

    private static void NeoForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
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
        btnInfo.Click += (sender, e) => NeoForgeLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = [btnInfo];
    }

    private static void NeoForgeLog_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlNeoForgeListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        Common.OpenWebsite($"{info.UrlBase}-changelog.txt");
    }

    public static void NeoForgeSave_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlNeoForgeListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (GameDownload.DlNeoForgeListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(info);
    }

    public static void CleanroomDownloadListItemPreload(
        StackPanel stack,
        List<GameDownload.DlCleanroomListEntry> entries,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        GameDownload.DlCleanroomListEntry? freshBetaVersion = null;
        if (entries.Any())
            freshBetaVersion = entries[0];
        else
            Common.Log("[System] 未找到可用的 Cleanroom 版本", Common.LogLevel.Debug);
        if (freshBetaVersion is not null)
        {
            var fresh = CleanroomDownloadListItem(freshBetaVersion, onClick, isSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Development")
                : $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {fresh.Info}";
            stack.Children.Add(fresh);
        }

        stack.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Version.AllVersions", entries.Count),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 13d, 0d, 4d)
            }
        );
    }

    public static MyListItem CleanroomDownloadListItem(
        GameDownload.DlCleanroomListEntry info,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly
    )
    {
        var newItem = new MyListItem
        {
            Title = info.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = info,
            Info = info.IsBeta
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable"),
            Logo = $"{Common.PathImage}Blocks/Cleanroom.png"
        };
        newItem.Click += onClick;
        if (isSaveOnly)
            newItem.ContentHandler = CleanroomSaveContMenuBuild;
        else
            newItem.ContentHandler = CleanroomContMenuBuild;
        return newItem;
    }

    private static void CleanroomContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton
        {
            SvgIcon = "lucide/save",
            ToolTip = Lang.Text("Download.Version.SaveAs")
        };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (sender, e) => CleanroomSave_Click(sender, (RoutedEventArgs)e);
        var btnInfo = new MyIconButton
        {
            LogoScale = 1.05d,
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Download.Version.Changelog")
        };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (sender, e) => CleanroomLog_Click(sender, (RoutedEventArgs)e);
        sender.Buttons = [btnSave, btnInfo];
    }

    private static void CleanroomSaveContMenuBuild(MyListItem sender, EventArgs e)
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
        btnInfo.Click += (a, b) => CleanroomLog_Click(a, (dynamic)b);
        sender.Buttons = [btnInfo];
    }

    private static void CleanroomLog_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlCleanroomListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        Common.OpenWebsite($"{info.UrlBase}-changelog.txt");
    }

    public static void CleanroomSave_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlCleanroomListEntry info;
        if (((dynamic)sender).Tag is not null)
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Tag;
        else
            info = (GameDownload.DlCleanroomListEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(info);
    }
}