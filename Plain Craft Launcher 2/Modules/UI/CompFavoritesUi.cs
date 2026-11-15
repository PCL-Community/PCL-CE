using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>收藏菜单的 WPF 实现。</summary>
public static class CompFavoritesUi
{
    public static void ShowMenu(
        Comp.CompProject project,
        UIElement pos,
        Action? closedCallBack = null)
    {
        var body = new ContextMenu();
        foreach (var i in Comp.CompFavorites.FavoritesList ?? [])
        {
            var item = new MyMenuItem
            {
                MaxWidth = 240d
            };
            var hasFavs = i.Favs.Contains(project.Id);
            if (hasFavs)
            {
                LocalizedText.TryBind(
                    item,
                    "Header",
                    () => Lang.Text(
                        "Download.Comp.Detail.Favorites.UnfavoriteContextMenu",
                        i.Name));
                item.SvgIcon = "lucide/heart-filled";
            }
            else
            {
                LocalizedText.TryBind(
                    item,
                    "Header",
                    () => Lang.Text(
                        "Download.Comp.Detail.Favorites.FavoriteContextMenu",
                        i.Name));
                item.SvgIcon = "lucide/heart";
            }

            item.Click += (_, _) =>
            {
                try
                {
                    if (hasFavs)
                    {
                        i.Favs.Remove(project.Id);
                        LauncherUi.Interaction?.ShowHint(
                            Lang.Text(
                                "Download.Comp.Detail.Favorites.Remove",
                                project.TranslatedName,
                                i.Name),
                            NotificationType.Success);
                    }
                    else
                    {
                        i.Favs.Add(project.Id);
                        LauncherUi.Interaction?.ShowHint(
                            Lang.Text(
                                "Download.Comp.Detail.Favorites.Add",
                                project.TranslatedName,
                                i.Name),
                            NotificationType.Success);
                    }

                    Comp.CompFavorites.Save();
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "[CompFavorites] 改变收藏项出错");
                }
            };
            body.Items.Add(item);
        }

        body.Closed += (_, _) => closedCallBack?.Invoke();
        body.Placement = PlacementMode.Bottom;
        body.PlacementTarget = pos;
        body.IsOpen = true;
    }

    public static void ShowMenu(
        List<Comp.CompProject> project,
        UIElement pos,
        Action? closedCallBack = null)
    {
        var body = new ContextMenu();
        foreach (var i in Comp.CompFavorites.FavoritesList ?? [])
        {
            var item = new MyMenuItem
            {
                MaxWidth = 240d,
                SvgIcon = "lucide/heart"
            };
            LocalizedText.TryBind(
                item,
                "Header",
                () => Lang.Text(
                    "Download.Comp.Detail.Favorites.FavoriteContextMenu",
                    i.Name));
            item.Click += (_, _) =>
            {
                try
                {
                    var count = i.Favs.Count;
                    project
                        .Select(p => p.Id)
                        .ToList()
                        .ForEach(x => i.Favs.Add(x));
                    Comp.CompFavorites.Save();
                    var successCount = i.Favs.Count - count;
                    var failedCount = project.Count - successCount;
                    LauncherUi.Interaction?.ShowHint(
                        Lang.Text(
                            failedCount > 0
                                ? "Download.Comp.Detail.Favorites.BulkAddWithFailures"
                                : "Download.Comp.Detail.Favorites.BulkAdd",
                            successCount,
                            i.Name,
                            failedCount),
                        NotificationType.Success);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "[CompFavorites] 改变收藏项出错");
                }
            };
            body.Items.Add(item);
        }

        body.Closed += (_, _) => closedCallBack?.Invoke();
        body.Placement = PlacementMode.Bottom;
        body.PlacementTarget = pos;
        body.IsOpen = true;
    }
}