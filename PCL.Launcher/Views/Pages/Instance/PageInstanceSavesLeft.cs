using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档管理左栏：在「存档信息」与「数据包」两个子页之间切换，并提供打开存档文件夹入口。
///     数据包子页仅在存档数据版本支持数据包（17w47a 及以上）时显示；当前存档路径由
///     <see cref="CurrentSave" /> 记录，右栏详情页与数据包页从路由参数或该字段读取。
/// </summary>
public class PageInstanceSavesLeft : MyPageLeft, IRefreshable
{
    private readonly List<(PageSubType SubType, MyListItem Item)> _items = [];

    private readonly StackPanel _panel;
    private bool _building;
    private MyListItem? _datapackItem;

    public PageInstanceSavesLeft()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0d, 12d, 0d, 0d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panel = panel;
        Children.Add(scroll);
        AnimatedControl = panel;
        DataContext = ViewModel;
        BuildItems();
        Current = this;
    }

    public static string CurrentSave
    {
        get => PageInstanceSavesLeftViewModel.CurrentSave;
        set => PageInstanceSavesLeftViewModel.CurrentSave = value;
    }

    public static bool ShowDatapackTab
    {
        get => PageInstanceSavesLeftViewModel.ShowDatapackTab;
        set
        {
            PageInstanceSavesLeftViewModel.ShowDatapackTab = value;
            Current?.ApplyDatapackVisibility();
        }
    }

    public static PageInstanceSavesLeft? Current { get; private set; }

    public PageInstanceSavesLeftViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        PageInstanceSavesLeftViewModel.Refresh();
    }

    private void BuildItems()
    {
        _panel.Children.Clear();
        _items.Clear();
        _datapackItem = null;

        foreach (var definition in ViewModel.Items)
        {
            var item = AddItem(definition.SubType, definition.TitleKey, definition.SvgIcon);
            if (definition.SubType != PageSubType.VersionSavesDatapack)
                continue;

            _datapackItem = item;
            var datapackRefresh = new MyIconButton
            {
                SvgIcon = "lucide/refresh-cw",
                LogoScale = 0.85d
            };
            ToolTip.SetTip(datapackRefresh, PageInstanceSavesLeftViewModel.RefreshToolTip);
            datapackRefresh.Click += (_, _) => PageInstanceSavesLeftViewModel.Refresh();
            item.Buttons = [datapackRefresh];
        }

        _panel.Children.Add(
            new TextBlock
            {
                Text = PageInstanceSavesLeftViewModel.QuickActionsTitle,
                Margin = new Thickness(13d, 6d, 5d, 4d),
                Opacity = 0.6d,
                FontSize = 12d
            });

        var openFolder = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Title = PageInstanceSavesLeftViewModel.OpenFolderTitle,
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.8d,
            IsScaleAnimationEnabled = false,
            Height = 36d,
            MinPaddingRight = 35
        };
        openFolder.Click += (_, _) => PageInstanceSavesLeftViewModel.OpenFolder();
        _panel.Children.Add(openFolder);

        ApplyDatapackVisibility();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        Current = this;
        ViewModel.Attach();
        PageInstanceSavesLeftViewModel.UpdateNavigationParameter(parameter);
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
                PageInstanceSavesLeftViewModel.Navigate(target);
        };
        _items.Add((subType, item));
        _panel.Children.Add(item);
        return item;
    }

    private void ApplyDatapackVisibility()
    {
        _datapackItem?.IsVisible = PageInstanceSavesLeftViewModel.IsDatapackTabVisible;
        PageInstanceSavesLeftViewModel.ApplyDatapackVisibility();
    }

    public void RefreshChecked()
    {
        _building = true;
        try
        {
            var current = PageInstanceSavesLeftViewModel.ResolveCurrentSubPage();
            foreach (var (subType, item) in _items)
                item.Checked = subType == current;
        }
        finally
        {
            _building = false;
        }
    }
}