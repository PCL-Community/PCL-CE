using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例设置页左栏：按实例能力与隐藏开关列出设置子页，点击通过路由切换右栏；
///     设置、安装、导出、存档、截图、资源与服务器项带有行内初始化/刷新按钮。
/// </summary>
public partial class PageInstanceLeft : MyPageLeft, IRefreshable
{
    private readonly List<(PageSubType SubType, MyListItem Item)> _items = [];
    private bool _isBuilding;
    private bool _resetting;

    public PageInstanceLeft()
    {
        InitializeComponent();
        ViewModel.Attach();
        Current = this;
        AnimatedControl = PanItem;
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => ViewModel.InvalidateRefresh();
    }

    public static McInstance? CurrentInstance => PageInstanceLeftViewModel.CurrentInstance;

    public static PageInstanceLeft? Current { get; private set; }

    public PageInstanceLeftViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        ViewModel.RequestRefresh();
        RefreshOnNavigate();
    }

    public static void SetCurrentInstance(McInstance? instance)
    {
        PageInstanceLeftViewModel.SetCurrentInstance(instance);
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (ViewModel.NavigateTo(parameter))
            BuildItems();
        RefreshChecked();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        ViewModel.RequestRefresh();
        RefreshOnNavigate();
    }

    /// <summary>实例分类或 Mod 可用性变化后重建左栏。</summary>
    public void RefreshForInstanceChange()
    {
        ViewModel.RequestRefresh();
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (!ViewModel.TryBeginRefresh())
            return;
        BuildItems();
        RefreshChecked();
    }

    private void BuildItems()
    {
        if (_isBuilding)
            return;
        _isBuilding = true;
        try
        {
            PanItem.Children.Clear();
            _items.Clear();

            foreach (var entry in ViewModel.BuildEntries())
            {
                if (entry.IsSection)
                {
                    PanItem.Children.Add(CreateSectionTitle(entry.SectionTitle!));
                    continue;
                }

                if (entry.Item is { } item)
                    AddItem(item);
            }
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private MyListItem AddItem(PageInstanceLeftItem definition)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            IsScaleAnimationEnabled = false,
            MinPaddingRight = 35,
            Height = 36,
            VerticalAlignment = VerticalAlignment.Top,
            Title = Lang.Text(definition.TitleKey),
            SvgIcon = definition.SvgIcon,
            LogoScale = definition.LogoScale,
            tag = definition.SubType,
            IsVisible = definition.IsVisible
        };
        item.Check += (sender, _) =>
        {
            if (!_isBuilding && sender is MyListItem { tag: PageSubType target })
                PageInstanceLeftViewModel.ChangePage(target);
        };

        var buttons = new List<MyIconButton>();
        if (definition.IsResetButton)
        {
            var reset = new MyIconButton
            {
                SvgIcon = "lucide/rotate-ccw",
                LogoScale = 0.9d
            };
            ToolTip.SetTip(reset, Lang.Text("Common.Action.Initialize"));
            reset.Click += (_, _) => _ = ResetInstanceSettingsAsync();
            buttons.Add(reset);
        }

        if (definition.RefreshType is { } refresh)
        {
            var refreshButton = new MyIconButton
            {
                SvgIcon = "lucide/refresh-cw",
                LogoScale = 0.85d
            };
            ToolTip.SetTip(refreshButton, Lang.Text("Common.Action.Refresh"));
            refreshButton.Click += (_, _) => PageInstanceLeftViewModel.RefreshSubPage(refresh);
            buttons.Add(refreshButton);
        }

        if (buttons.Count > 0)
            item.Buttons = [.. buttons];

        _items.Add((definition.SubType, item));
        PanItem.Children.Add(item);
        return item;
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        // WPF 实例左栏使用 13,6,5,4；共享 SectionTitle 的 18 上边距用于其它页面，这里局部覆盖。
        var title = new TextBlock
        {
            Text = text,
            Margin = new Thickness(13d, 6d, 5d, 4d)
        };
        title.Classes.Add("SectionTitle");
        return title;
    }

    public void RefreshChecked()
    {
        _isBuilding = true;
        try
        {
            var current = PageInstanceLeftViewModel.CurrentSubType;
            foreach (var (subType, item) in _items)
                if (item.IsVisible)
                    item.Checked = subType == current;
        }
        finally
        {
            _isBuilding = false;
        }
    }

    public static McInstance? ResolveInstance()
    {
        return PageInstanceLeftViewModel.ResolveInstance();
    }

    private async Task ResetInstanceSettingsAsync()
    {
        if (_resetting)
            return;
        _resetting = true;
        try
        {
            if (await ViewModel.ResetInstanceSettingsAsync())
            {
                BuildItems();
                RefreshChecked();
            }
        }
        finally
        {
            _resetting = false;
        }
    }
}