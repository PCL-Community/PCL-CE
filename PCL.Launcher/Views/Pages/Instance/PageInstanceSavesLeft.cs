using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档管理左栏：在「存档信息」与「数据包」两个子页之间切换，并提供打开存档文件夹入口。
///     数据包子页仅在存档数据版本支持数据包（17w47a 及以上）时显示；当前存档路径由
///     <see cref="currentSave" /> 记录，右栏详情页与数据包页从路由参数或该字段读取。
/// </summary>
public class PageInstanceSavesLeft : MyPageLeft, IRefreshable
{
    public static string currentSave = string.Empty;

    private static bool _showDatapackTab = true;

    public static bool ShowDatapackTab
    {
        get => _showDatapackTab;
        set
        {
            _showDatapackTab = value;
            Current?.ApplyDatapackVisibility();
        }
    }

    public static PageInstanceSavesLeft? Current { get; private set; }

    private readonly StackPanel _panel;
    private readonly List<(PageSubType SubType, MyListItem Item)> _items = [];
    private MyListItem? _datapackItem;
    private bool _building;

    public PageInstanceSavesLeft()
    {
        var panel = new StackPanel { Margin = new Thickness(0d, 12d, 0d, 0d) };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panel = panel;
        Children.Add(scroll);
        AnimatedControl = panel;
        BuildItems();
        Current = this;
    }

    private void BuildItems()
    {
        _panel.Children.Clear();
        _items.Clear();

        AddItem(PageSubType.VersionSavesInfo, "Instance.Saves.Left.Info", "lucide/info");
        _datapackItem = AddItem(PageSubType.VersionSavesDatapack, "Instance.Saves.Left.Datapack", "lucide/file-archive");
        var datapackRefresh = new MyIconButton { SvgIcon = "lucide/refresh-cw", LogoScale = 0.85d };
        ToolTip.SetTip(datapackRefresh, Lang.Text("Common.Action.Refresh"));
        datapackRefresh.Click += (_, _) => Refresh();
        _datapackItem.Buttons = [datapackRefresh];

        _panel.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Instance.Saves.Left.QuickActions"),
                Margin = new Thickness(13d, 6d, 5d, 4d),
                Opacity = 0.6d,
                FontSize = 12d
            });

        var openFolder = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Title = Lang.Text("Common.Action.OpenFolder"),
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.8d,
            IsScaleAnimationEnabled = false,
            Height = 36d,
            MinPaddingRight = 35
        };
        openFolder.Click += (_, _) => InstancePageHelper.OpenFolder(currentSave);
        _panel.Children.Add(openFolder);

        ApplyDatapackVisibility();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        Current = this;
        if (parameter is string path && !string.IsNullOrEmpty(path))
            currentSave = path;
        ApplyDatapackVisibility();
        RefreshChecked();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        BuildItems();
        RefreshChecked();
    }

    public void Refresh()
    {
        var target = PageRouter.Current.Page == PageType.VersionSaves
            ? PageRouter.Current.SubType
            : PageSubType.VersionSavesInfo;
        PageRouter.Navigate(new PageStackData(PageType.VersionSaves, target, currentSave));
        if (PageRouter.CurrentPage is IRefreshable refreshable)
            refreshable.Refresh();
    }

    private MyListItem AddItem(PageSubType subType, string titleKey, string svgIcon)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = Lang.Text(titleKey),
            SvgIcon = svgIcon,
            IsScaleAnimationEnabled = false,
            Height = 36d,
            MinPaddingRight = 35,
            tag = subType
        };
        item.Check += (_, _) =>
        {
            if (!_building && item.tag is PageSubType target)
                Navigate(target);
        };
        _items.Add((subType, item));
        _panel.Children.Add(item);
        return item;
    }

    private void ApplyDatapackVisibility()
    {
        if (_datapackItem is null)
            return;
        _datapackItem.IsVisible = _showDatapackTab;
        if (!_showDatapackTab && PageRouter.Current.Page == PageType.VersionSaves
            && PageRouter.Current.SubType == PageSubType.VersionSavesDatapack)
            PageRouter.Navigate(new PageStackData(PageType.VersionSaves, PageSubType.VersionSavesInfo, currentSave));
    }

    private void Navigate(PageSubType subType)
    {
        PageRouter.Navigate(new PageStackData(PageType.VersionSaves, subType, currentSave));
    }

    public void RefreshChecked()
    {
        _building = true;
        try
        {
            var current = PageRouter.Current.Page == PageType.VersionSaves
                ? PageRouter.Current.SubType
                : PageSubType.VersionSavesInfo;
            foreach (var (subType, item) in _items)
                item.Checked = subType == current;
        }
        finally
        {
            _building = false;
        }
    }
}
