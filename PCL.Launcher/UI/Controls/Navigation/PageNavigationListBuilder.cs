using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Navigation;

namespace PCL.UI.Controls.Navigation;

public sealed record PageNavigationListResult(
    IReadOnlyList<(SubPageId Sub, MyListItem Item)> Items,
    IReadOnlyList<(TextBlock Title, string GroupKey)> Groups);

/// <summary>根据页面描述构建左栏分组与导航项。</summary>
public static class PageNavigationListBuilder
{
    /// <summary>
    /// 同步导航列表的单选状态。先选中目标项，让它在取消其他项之前仍能取到旧的选中项作为滑动起点；
    /// 目标项不存在时取消全部，避免保留过期选中。调用方负责在同步期间抑制选中回调。
    /// </summary>
    public static void Select(
        IReadOnlyList<(SubPageId Sub, MyListItem Item)> items,
        SubPageId selected,
        bool animate = true)
    {
        MyListItem? target = null;
        foreach (var (sub, item) in items)
        {
            if (sub != selected)
                continue;

            target = item;
            break;
        }

        if (target is not null && !target.Checked)
            target.SetChecked(true, false, animate);

        foreach (var (sub, item) in items)
        {
            if (sub == selected || !item.Checked)
                continue;

            item.SetChecked(false, false, false);
        }
    }

    public static PageNavigationListResult Build(
        PageDescriptor page,
        StackPanel panel,
        bool forceShow,
        Action<SubPageId> onSelected,
        Func<SubPageId, Control?>? pageProvider = null,
        Action<SubPageId>? onRefresh = null,
        Action<SubPageId>? onReset = null,
        Thickness? categoryMargin = null)
    {
        panel.Children.Clear();

        var items = new List<(SubPageId, MyListItem)>();
        var groups = new List<(TextBlock, string)>();
        var visible = page.VisibleSubPages(forceShow);
        var orderedGroups = page
            .Groups
            .OrderBy(group => group.Order)
            .Select(group => (group, subPages: visible
                .Where(sub => string.Equals(sub.GroupKey, group.Key, StringComparison.Ordinal))
                .OrderBy(sub => sub.Order)
                .ToArray()))
            .Where(pair => pair.subPages.Length > 0)
            .ToArray();

        if (orderedGroups.Length == 0)
            orderedGroups =
            [
                (
                    new PageGroupDescriptor(string.Empty, string.Empty, 0),
                    [.. visible]
                )
            ];

        foreach (var (group, subPages) in orderedGroups)
        {
            if (!string.IsNullOrEmpty(group.TitleKey))
            {
                var title = new TextBlock
                {
                    Opacity = 0.6d,
                    FontSize = 12d,
                    Margin = categoryMargin ?? new Thickness(13d, 5d, 5d, 3d)
                };
                title.SetLocalizedValue(TextBlock.TextProperty, group.TitleKey);
                title.Classes.Add("SectionTitle");
                panel.Children.Add(title);
                groups.Add((title, group.Key));
            }

            foreach (var sub in subPages)
            {
                var item = new MyListItem
                {
                    Type = MyListItem.CheckType.RadioBox,
                    IsScaleAnimationEnabled = false,
                    MinPaddingRight = 35,
                    Height = 36d,
                    VerticalAlignment = VerticalAlignment.Top,
                    SvgIcon = sub.IconKey,
                    LogoScale = sub.LogoScale,
                    Tag = sub.Id,
                    IsVisible = true
                };
                item.SetLocalizedValue(MyListItem.TitleProperty, sub.TitleKey);
                item.Check += (sender, _) =>
                {
                    if (sender is MyListItem { Checked: true, Tag: SubPageId target })
                        onSelected(target);
                };

                var buttons = new List<MyIconButton>();
                if (sub.ResetMessageKey is not null
                    && pageProvider is not null
                    && onReset is not null)
                {
                    var reset = new MyIconButton
                    {
                        SvgIcon = "lucide/rotate-ccw",
                        LogoScale = 0.9d
                    };
                    reset.SetLocalizedValue(ToolTip.TipProperty, "Common.Action.Initialize");
                    reset.Click += (_, _) => onReset(sub.Id);
                    buttons.Add(reset);
                }

                if (sub.CanRefresh && onRefresh is not null)
                {
                    var refresh = new MyIconButton
                    {
                        SvgIcon = "lucide/refresh-cw",
                        LogoScale = 0.85d
                    };
                    refresh.SetLocalizedValue(ToolTip.TipProperty, "Common.Action.Refresh");
                    refresh.Click += (_, _) => onRefresh(sub.Id);
                    buttons.Add(refresh);
                }

                if (buttons.Count > 0)
                    item.Buttons = buttons;
                panel.Children.Add(item);
                items.Add((sub.Id, item));
            }
        }

        return new PageNavigationListResult(items, groups);
    }
}