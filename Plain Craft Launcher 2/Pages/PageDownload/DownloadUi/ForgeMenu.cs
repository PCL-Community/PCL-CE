using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class DownloadUi
{
    private static bool _isForgeRecommendedRefreshed;

    public static void ForgeDownloadListItemPreload(
        StackPanel stack,
        List<GameDownload.DlForgeVersionEntry> entries,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly)
    {
        if (entries.Count == 1)
            return;

        GameDownload.DlForgeVersionEntry? freshVersion = null;
        if (entries.Count != 0)
            freshVersion = entries[0];
        else
            Common.Log("[System] 未找到可用的 Forge 版本", Common.LogLevel.Debug);

        GameDownload.DlForgeVersionEntry? recommendedVersion = null;
        foreach (var version in entries.Where(entry => entry.IsRecommended))
            recommendedVersion = version;

        if (freshVersion is not null && ReferenceEquals(freshVersion, recommendedVersion))
            freshVersion = null;

        if (recommendedVersion is not null)
        {
            var recommended = ForgeDownloadListItem(recommendedVersion, onClick, isSaveOnly);
            recommended.Info = Lang.Text("Download.Version.Type.Recommended")
                + (string.IsNullOrEmpty(recommended.Info) ? "" : $"  |  {recommended.Info}");
            stack.Children.Add(recommended);
        }

        if (freshVersion is not null)
        {
            var fresh = ForgeDownloadListItem(freshVersion, onClick, isSaveOnly);
            fresh.Info = Lang.Text("Download.Version.Latest.Title")
                + (string.IsNullOrEmpty(fresh.Info) ? "" : $"  |  {fresh.Info}");
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

    public static MyListItem ForgeDownloadListItem(
        GameDownload.DlForgeVersionEntry entry,
        MyListItem.ClickEventHandler onClick,
        bool isSaveOnly)
    {
        var infoParts = new List<string>();

        if (!string.IsNullOrEmpty(entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", entry.ReleaseTime));

        if (Common.modeDebug)
            infoParts.Add(Lang.Text("Download.Version.Forge.Type", entry.Category));

        var newItem = new MyListItem
        {
            Title = entry.VersionName,
            SnapsToDevicePixels = true,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = entry,
            Info = string.Join("  |  ", infoParts),
            Logo = $"{Common.PathImage}Blocks/Anvil.png"
        };

        newItem.Click += onClick;
        if (isSaveOnly)
            newItem.ContentHandler = ForgeSaveContMenuBuild;
        else
            newItem.ContentHandler = ForgeContMenuBuild;
        return newItem;
    }

    private static void ForgeContMenuBuild(MyListItem sender, EventArgs e)
    {
        var btnSave = new MyIconButton
        {
            SvgIcon = "lucide/save",
            ToolTip = Lang.Text("Download.Version.SaveAs")
        };
        ToolTipService.SetPlacement(btnSave, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnSave, 30d);
        ToolTipService.SetHorizontalOffset(btnSave, 2d);
        btnSave.Click += (ss, ee) => ForgeSave_Click(ss, (dynamic)ee);
        var btnInfo = new MyIconButton
        {
            LogoScale = 1.05d,
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Download.Version.Changelog")
        };
        ToolTipService.SetPlacement(btnInfo, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnInfo, 30d);
        ToolTipService.SetHorizontalOffset(btnInfo, 2d);
        btnInfo.Click += (ss, ee) => ForgeLog_Click(ss, (dynamic)ee);
        sender.Buttons = [btnSave, btnInfo];
    }

    private static void ForgeSaveContMenuBuild(MyListItem sender, EventArgs e)
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
        btnInfo.Click += (ss, _) => ForgeLog_Click(ss, (dynamic)e);
        sender.Buttons = [btnInfo];
    }

    private static void ForgeLog_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlForgeVersionEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        Common.OpenWebsite(
            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{version.Inherit}-{version.VersionName}/forge-{version.Inherit}-{version.VersionName}-changelog.txt"
        );
    }

    public static void ForgeSave_Click(object sender, RoutedEventArgs e)
    {
        GameDownload.DlForgeVersionEntry version;
        if (((dynamic)sender).Tag is not null)
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Tag;
        else if (((dynamic)sender).Parent.Tag is not null)
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Tag;
        else
            version = (GameDownload.DlForgeVersionEntry)((dynamic)sender).Parent.Parent.Tag;
        McDownloadForgelikeSave(version);
    }

    /// <summary>
    ///     尝试刷新 Forge 推荐版本缓存。
    /// </summary>
    public static void McDownloadForgeRecommendedRefresh()
    {
        if (_isForgeRecommendedRefreshed)
            return;

        _isForgeRecommendedRefreshed = true;
        Common.RunInNewThread(
            () =>
            {
                try
                {
                    Common.Log("[Download] 刷新 Forge 推荐版本缓存开始");
                    var result = NetFacade.NetGetCodeByLoader(
                        "https://bmclapi2.bangbang93.com/forge/promos");
                    if (result.Length < 1000)
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.ForgePromosResultTooShort",
                                result));

                    var resultJson = Common.GetJson(result);
                    var recommendedList = new List<string>();
                    foreach (var versionNode in resultJson.AsArray())
                    {
                        if (versionNode is not JsonObject version
                            || version["name"] is null
                            || version["build"] is null)
                            continue;
                        var name = version["name"]?.ToString() ?? "";
                        if (!name.EndsWithF("-recommended"))
                            continue;
                        var buildVersion = version["build"]?["version"]?.ToString() ?? "";
                        recommendedList.Add(
                            $"\"{name.Replace("-recommended", $"\":\"{buildVersion}\"")}");
                    }

                    if (recommendedList.Count < 5)
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.ForgeRecommendedTooFew", result));

                    var cacheJson = $"{{{recommendedList.Join(",")}}}";
                    Common.WriteFile(
                        Path.Combine(Common.pathTemp, "Cache", "ForgeRecommendedList.json"),
                        cacheJson);
                    Common.Log("[Download] 刷新 Forge 推荐版本缓存成功");
                }
                catch (Exception ex)
                {
                    Common.Log(ex, "刷新 Forge 推荐版本缓存失败");
                }
            },
            "ForgeRecommendedRefresh"
        );
    }

    /// <summary>
    ///     尝试获取某个 MC 版本对应的 Forge 推荐版本。如果不可用会返回 Nothing。
    /// </summary>
    public static string? McDownloadForgeRecommendedGet(string? mcInstance)
    {
        try
        {
            if (mcInstance is null)
                return null;
            var list = Common.ReadFile(
                Path.Combine(Common.pathTemp, "Cache", "ForgeRecommendedList.json"));
            if (list is null || string.IsNullOrEmpty(list))
            {
                Common.Log("[Download] 没有 Forge 推荐版本缓存文件");
                return null;
            }

            var json = (JsonObject)Common.GetJson(list);
            if (json is null
                || !mcInstance.Contains(".")
                || !json.ContainsKey(mcInstance))
                return null;
            return (json[mcInstance] ?? "").ToString();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"获取 Forge 推荐版本失败（{mcInstance ?? "null"}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
            return null;
        }
    }
}