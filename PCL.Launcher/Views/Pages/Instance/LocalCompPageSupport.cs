using Avalonia.Controls;
using Avalonia.Input.Platform;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;

namespace PCL.Views.Pages.Instance;

/// <summary>本地资源页与数据包页共用的菜单、分享等页面操作。</summary>
internal static class LocalCompPageSupport
{
    public static void SetSwipeSelected<TEntry>(
        IReadOnlyList<TEntry> entries,
        int index,
        bool selected,
        bool animate,
        Action<TEntry, bool> setSelected,
        IList<Control> children)
    {
        if (index < 0 || index >= entries.Count)
            return;

        var entry = entries[index];
        setSelected(entry, selected);
        if (index < children.Count && children[index] is MyLocalModItem item)
            item.SetChecked(selected, animate);
    }

    public static void ShowSortMenu(
        Control anchor,
        IReadOnlyList<string> names,
        Action<int> applySort)
    {
        var menu = new ContextMenu();
        for (var i = 0; i < names.Count; i++)
        {
            var index = i;
            var item = new MyMenuItem
            {
                Header = names[i]
            };
            item.Click += (_, _) => applySort(index);
            menu.Items.Add(item);
        }

        menu.Open(anchor);
    }

    public static void ShowFavoritesMenu(
        IReadOnlyList<Comp.CompProject> projects,
        Control anchor)
    {
        if (projects.Count > 0)
            CompFavoritesMenu.Show(projects, anchor);
    }

    public static void ShareSelected(
        Control owner,
        Func<IReadOnlyList<string>> getShareIds,
        Action clearSelection,
        Action rebuild)
    {
        try
        {
            var ids = getShareIds();
            if (ids.Count == 0)
                return;

            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard is null)
                return;

            _ = clipboard.SetTextAsync(Comp.CompFavorites.GetShareCode(ids.ToHashSet()));
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
            clearSelection();
            rebuild();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }
}