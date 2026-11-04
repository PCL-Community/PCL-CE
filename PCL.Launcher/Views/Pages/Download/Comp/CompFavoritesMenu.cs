using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public static class CompFavoritesMenu
{
    public static void Show(Comp.CompProject project, Control anchor, Action? closedCallback = null)
    {
        var items = new List<MyMenuItem>();
        foreach (var group in Comp.CompFavorites.FavoritesList ?? [])
        {
            var groupName = group.Name;
            var hasFavorite = group.Favs.Contains(project.Id);
            var item = new MyMenuItem
            {
                Header = Lang.Text(
                    hasFavorite
                        ? "Download.Comp.Detail.Favorites.UnfavoriteContextMenu"
                        : "Download.Comp.Detail.Favorites.FavoriteContextMenu",
                    groupName),
                SvgIcon = hasFavorite ? "lucide/heart-filled" : "lucide/heart",
                MaxWidth = 240d
            };
            item.Click += (_, _) => Toggle(project, group, hasFavorite);
            items.Add(item);
        }

        if (items.Count == 0)
            return;

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Closed += (_, _) => closedCallback?.Invoke();
        menu.Open(anchor);
    }

    private static void Toggle(
        Comp.CompProject project,
        Comp.CompFavorites.FavData group,
        bool hasFavorite)
    {
        try
        {
            if (hasFavorite)
            {
                group.Favs.Remove(project.Id);
                LauncherUi.Interaction?.ShowHint(
                    Lang.Text(
                        "Download.Comp.Detail.Favorites.Remove",
                        project.TranslatedName,
                        group.Name),
                    NotificationType.Success);
            }
            else
            {
                group.Favs.Add(project.Id);
                LauncherUi.Interaction?.ShowHint(
                    Lang.Text(
                        "Download.Comp.Detail.Favorites.Add",
                        project.TranslatedName,
                        group.Name),
                    NotificationType.Success);
            }

            Comp.CompFavorites.Save();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[CompFavorites] 改变收藏项出错");
        }
    }
}