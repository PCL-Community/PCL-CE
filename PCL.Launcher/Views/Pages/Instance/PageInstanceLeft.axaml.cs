using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例设置页左栏：按实例能力与隐藏开关列出设置子页，点击通过路由切换右栏；
///     设置、安装、导出、存档、截图、资源与服务器项带有行内初始化/刷新按钮。
/// </summary>
public partial class PageInstanceLeft : MyPageLeft, IRefreshable
{
    public static McInstance? CurrentInstance { get; private set; }

    private readonly List<(PageSubType SubType, MyListItem Item)> _items = [];
    private TextBlock? _resourceTitle;
    private bool _isBuilding;
    private bool _refreshed;

    public PageInstanceLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => _refreshed = false;
    }

    public static void SetCurrentInstance(McInstance? instance)
    {
        CurrentInstance = instance;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is McInstance instance)
            CurrentInstance = instance;
        else if (CurrentInstance is null)
            CurrentInstance = InstanceList.McMcInstanceSelected
                              ?? InstanceList.mcInstanceList.SelectMany(card => card.Value).FirstOrDefault();
        RefreshOnNavigate();
        RefreshChecked();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        _refreshed = false;
        RefreshOnNavigate();
    }

    public void Refresh()
    {
        _refreshed = false;
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
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

            var instance = ResolveInstance();
            var modable = instance?.Modable ?? false;

            PanItem.Children.Add(CreateSectionTitle(Lang.Text("Instance.Left.Game")));
            AddItem(PageSubType.VersionOverall, "Instance.Left.Overview", "lucide/layout-dashboard", 0.95);
            AddItem(PageSubType.VersionSetup, "Instance.Left.Settings", "lucide/settings", 1.0, true);
            AddItem(
                PageSubType.VersionInstall,
                "Instance.Left.Install",
                "lucide/wrench",
                0.87,
                false,
                Config.Preference.Hide.InstanceEdit,
                PageSubType.VersionInstall);
            AddItem(
                PageSubType.VersionExport,
                "Instance.Left.Export",
                "lucide/package",
                1.0,
                false,
                Config.Preference.Hide.InstanceExport,
                PageSubType.VersionExport);

            _resourceTitle = CreateSectionTitle(Lang.Text("Instance.Left.Resources"));
            PanItem.Children.Add(_resourceTitle);
            AddItem(
                PageSubType.VersionWorld,
                "Instance.Left.Saves",
                "lucide/globe",
                0.95,
                false,
                Config.Preference.Hide.InstanceSave,
                PageSubType.VersionWorld);
            AddItem(
                PageSubType.VersionScreenshot,
                "Instance.Left.Screenshots",
                "lucide/image",
                0.95,
                false,
                Config.Preference.Hide.InstanceScreenshot,
                PageSubType.VersionScreenshot);
            if (modable)
            {
                AddItem(
                    PageSubType.VersionMod,
                    "Instance.Left.Mod",
                    "lucide/puzzle",
                    1.0,
                    false,
                    Config.Preference.Hide.InstanceMod,
                    PageSubType.VersionMod);
            }
            else
            {
                AddItem(PageSubType.VersionModDisabled, "Instance.Left.Mod", "lucide/puzzle", 1.0);
            }

            AddItem(
                PageSubType.VersionResourcePack,
                "Instance.Left.ResourcePacks",
                "lucide/layers",
                0.85,
                false,
                Config.Preference.Hide.InstanceResourcePack,
                PageSubType.VersionResourcePack);
            AddItem(
                PageSubType.VersionShader,
                "Instance.Left.Shaders",
                "lucide/sparkles",
                1.15,
                false,
                Config.Preference.Hide.InstanceShader,
                PageSubType.VersionShader);
            AddItem(
                PageSubType.VersionSchematic,
                "Instance.Left.Schematics",
                "lucide/file-box",
                0.85,
                false,
                Config.Preference.Hide.InstanceSchematic,
                PageSubType.VersionSchematic);
            AddItem(
                PageSubType.VersionServer,
                "Instance.Left.Servers",
                "lucide/server",
                0.85,
                false,
                Config.Preference.Hide.InstanceServer,
                PageSubType.VersionServer);

            var resourceTypes = new[]
            {
                PageSubType.VersionWorld, PageSubType.VersionScreenshot, PageSubType.VersionMod,
                PageSubType.VersionModDisabled, PageSubType.VersionResourcePack, PageSubType.VersionShader,
                PageSubType.VersionSchematic, PageSubType.VersionServer
            };
            _resourceTitle.IsVisible = _items.Any(pair => resourceTypes.Contains(pair.SubType) && pair.Item.IsVisible);
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private MyListItem AddItem(
        PageSubType subType,
        string titleKey,
        string svgIcon,
        double logoScale = 1d,
        bool isResetButton = false,
        bool hidden = false,
        PageSubType? refreshType = null)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            IsScaleAnimationEnabled = false,
            MinPaddingRight = 35,
            Height = 36,
            VerticalAlignment = VerticalAlignment.Top,
            Title = Lang.Text(titleKey),
            SvgIcon = svgIcon,
            LogoScale = logoScale,
            tag = subType,
            IsVisible = !hidden || HiddenFeatureState.ForceShow
        };
        item.Check += (sender, _) =>
        {
            if (!_isBuilding && sender is MyListItem { tag: PageSubType target })
                ChangePage(target);
        };

        var buttons = new List<MyIconButton>();
        if (isResetButton)
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

        if (refreshType is { } refresh)
        {
            var refreshButton = new MyIconButton
            {
                SvgIcon = "lucide/refresh-cw",
                LogoScale = 0.85d
            };
            ToolTip.SetTip(refreshButton, Lang.Text("Common.Action.Refresh"));
            refreshButton.Click += (_, _) => RefreshSubPage(refresh);
            buttons.Add(refreshButton);
        }

        if (buttons.Count > 0)
            item.Buttons = buttons.ToArray();

        _items.Add((subType, item));
        PanItem.Children.Add(item);
        return item;
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        var title = new TextBlock { Text = text };
        title.Classes.Add("SectionTitle");
        return title;
    }

    public void RefreshChecked()
    {
        _isBuilding = true;
        try
        {
            var current = PageRouter.Current.Page == PageType.InstanceSetup
                ? PageRouter.Current.SubType
                : PageSubType.VersionOverall;
            foreach (var (subType, item) in _items)
            {
                if (item.IsVisible)
                    item.Checked = subType == current;
            }
        }
        finally
        {
            _isBuilding = false;
        }
    }

    public static McInstance? ResolveInstance()
    {
        if (PageRouter.Current.Page == PageType.InstanceSetup
            && PageRouter.Current.Parameter is McInstance routed)
            return routed;
        if (CurrentInstance is not null)
            return CurrentInstance;
        return InstanceList.McMcInstanceSelected;
    }

    private void ChangePage(PageSubType subType)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        if (PageRouter.Current.Page == PageType.InstanceSetup
            && PageRouter.Current.SubType == subType
            && ReferenceEquals(PageRouter.Current.Parameter, instance))
            return;
        PageRouter.Navigate(new PageStackData(PageType.InstanceSetup, subType, instance));
    }

    private void RefreshSubPage(PageSubType subType)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        PageRouter.Navigate(new PageStackData(PageType.InstanceSetup, subType, instance));
        if (PageRouter.CurrentPage is IRefreshable refreshable)
            refreshable.Refresh();
    }

    private async Task ResetInstanceSettingsAsync()
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle"),
                    Content = Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            PageRouter.Navigate(new PageStackData(PageType.InstanceSetup, PageSubType.VersionSetup, ResolveInstance()));
            if (PageRouter.CurrentPage is PageInstanceSetup setup)
                setup.Reset();
            RefreshChecked();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "初始化实例设置失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
    }
}
