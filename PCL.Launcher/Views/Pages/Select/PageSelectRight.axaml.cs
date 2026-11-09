using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Select;

/// <summary>
///     实例选择页右栏：按实例分类分组的实例卡片列表，支持搜索、收藏、删除与进入实例设置。
///     数据来自 <see cref="InstanceList.mcInstanceListLoader" />；加载环状态由
///     <see cref="MyPageRight.PageLoaderInit" /> 统一管理。
/// </summary>
public partial class PageSelectRight : MyPageRight, IRefreshable, IShellShortcutTarget
{
    private const int NormalDelay = 75;
    private const int QuickDelay = 50;

    private bool _isLoadInitialized;

    private bool _isRefreshing;
    private DateTime _lastInputTime = DateTime.MinValue;
    private bool _refreshed;
    private DispatcherTimer? _reloadTimer;

    public PageSelectRight()
    {
        InitializeComponent();
        Current = this;
        PanScroll = PanBack;
        DataContext = ViewModel;
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        ToolTip.SetTip(PanVerSearchBox, Lang.Text("Select.Instance.Search.ToolTip"));
        PanVerSearchBox.TextChanged += (_, _) => OnSearchTextChanged();
        DetachedFromVisualTree += (_, _) => StopReloadTimer();
        EnsureLoaderInitialized();
        PageEnter += OnPageEnter;
    }

    public static PageSelectRight? Current { get; private set; }

    public PageSelectRightViewModel ViewModel { get; } = new();

    public bool ShowHidden
    {
        get => ViewModel.ShowHidden;
        set => ViewModel.ShowHidden = value;
    }

    public void Refresh()
    {
        RefreshInstances(true);
    }

    public bool ToggleHiddenInstances()
    {
        ViewModel.ShowHidden = !ViewModel.ShowHidden;
        McInstanceListUi(InstanceList.mcInstanceListLoader);
        return true;
    }

    public bool LaunchGame()
    {
        return false;
    }

    private void EnsureLoaderInitialized()
    {
        if (_isLoadInitialized)
            return;
        _isLoadInitialized = true;
        PageLoaderInit(
            Load,
            PanLoad,
            PanAllBack,
            null,
            InstanceList.mcInstanceListLoader,
            _ => McInstanceListUi(InstanceList.mcInstanceListLoader),
            () => GameFolders.mcFolderSelected ?? "",
            false);
    }

    /// <summary>
    ///     PageOnEnter 会先触发 PageEnter，再根据加载器状态决定显示内容还是加载环。
    ///     因此必须在这里完成加载器初始化与刷新，不能等到 OnNavigatedTo。
    /// </summary>
    private void OnPageEnter()
    {
        EnsureLoaderInitialized();
        RefreshOnNavigate();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        EnsureLoaderInitialized();
        RefreshOnNavigate();
    }

    protected override void RefreshLocalization()
    {
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        ToolTip.SetTip(PanVerSearchBox, Lang.Text("Select.Instance.Search.ToolTip"));
        if (InstanceList.mcInstanceListLoader.State != LoadState.Finished)
            return;
        var savedOffset = PanBack.Offset;
        McInstanceListUi(InstanceList.mcInstanceListLoader);
        Dispatcher.UIThread.Post(() => PanBack.Offset = savedOffset);
    }

    public override void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;

        var force =
            InstanceList.mcInstanceListLoader.State is LoadState.Failed or LoadState.Aborted;
        PageSelectRightViewModel.RefreshInstances(force);
        PanBack.ScrollToHome();
    }

    public static void RefreshInstances(bool force)
    {
        PageSelectRightViewModel.RefreshInstances(force);
    }

    #region 搜索

    private void OnSearchTextChanged()
    {
        ViewModel.SearchText = PanVerSearchBox.Text?.Trim() ?? "";
        _lastInputTime = DateTime.Now;
        _isRefreshing = false;

        _reloadTimer ??= CreateReloadTimer();
        var target = string.IsNullOrWhiteSpace(PanVerSearchBox.Text) ? QuickDelay : NormalDelay;
        if (Math.Abs(_reloadTimer.Interval.TotalMilliseconds - target) > 0.5d)
            _reloadTimer.Interval = TimeSpan.FromMilliseconds(target);
        if (!_reloadTimer.IsEnabled)
            _reloadTimer.Start();
    }

    private DispatcherTimer CreateReloadTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(NormalDelay)
        };
        timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.Now - _lastInputTime).TotalMilliseconds;
            if (elapsed < timer.Interval.TotalMilliseconds
                || InstanceList.mcInstanceListLoader.State != LoadState.Finished
                || _isRefreshing)
                return;

            _isRefreshing = true;
            Dispatcher.UIThread.Post(() =>
            {
                McInstanceListUi(InstanceList.mcInstanceListLoader);
                _isRefreshing = false;
            });
            timer.Stop();
        };
        return timer;
    }

    private void StopReloadTimer()
    {
        if (_reloadTimer is not null)
        {
            _reloadTimer.Stop();
            _reloadTimer = null;
        }

        _isRefreshing = false;
        _refreshed = false;
    }

    #endregion

    #region 列表构建

    private void McInstanceListUi(Loading.LoaderTask<string, int> loader)
    {
        try
        {
            ViewModel.RefreshGroups();
            PanMain.Children.Clear();

            foreach (var group in ViewModel.Groups)
            {
                var cardType = group.Type;
                var newCard = new MyCard
                {
                    Margin = new Thickness(0d, 0d, 0d, 15d),
                    Tag = (Key: group.NameKey, Type: cardType)
                };
                LocalizedText.TryBind(
                    newCard,
                    "Title",
                    () => PageSelectRightViewModel.GetCardTitle(group));

                var stack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    Tag = group.Instances.ToList()
                };
                newCard.Children.Add(stack);
                newCard.SwapControl = stack;

                if (cardType is McInstanceCardType.Rubbish
                    or McInstanceCardType.Error
                    or McInstanceCardType.Fool)
                {
                    newCard.IsSwapped = true;
                    newCard.InstallMethod = PutMethod;
                }
                else
                {
                    MyCard.StackInstall(ref stack, PutMethod);
                }

                PanMain.Children.Add(newCard);
                continue;

                void PutMethod(StackPanel panel)
                {
                    foreach (var instance in (List<McInstance>)panel.Tag!)
                        panel.Children.Add(CreateInstanceItem(instance));
                }
            }

            if (PanMain.Children is [MyCard { IsSwapped: true } onlyCard])
                onlyCard.IsSwapped = false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.UiUpdate"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.UiUpdate"));
        }
    }

    private MyListItem CreateInstanceItem(McInstance instance)
    {
        var newItem = new MyListItem
        {
            Title = instance.Name,
            Height = 42d,
            Tag = instance,
            Type = MyListItem.CheckType.Clickable
        };
        LocalizedText.TryBind(newItem, "Info", () => instance.Desc);

        var info = instance.Info;
        var tags = new List<string>
        {
            McInstanceInfo.GetVanillaNameDisplay(info.VanillaName)
        };
        if (info.HasForge)
            tags.Add("Forge" + McInstanceInfo.FormatVersion(info.Forge));
        else if (info.HasNeoForge)
            tags.Add("NeoForge" + McInstanceInfo.FormatVersion(info.NeoForge));
        else if (info.HasCleanroom)
            tags.Add("Cleanroom" + McInstanceInfo.FormatVersion(info.Cleanroom));
        else if (info.HasLabyMod)
            tags.Add("LabyMod" + McInstanceInfo.FormatVersion(info.LabyMod));
        else if (info.HasQuilt)
            tags.Add("Quilt" + McInstanceInfo.FormatVersion(info.Quilt));
        else if (info.HasFabric)
            tags.Add("Fabric" + McInstanceInfo.FormatVersion(info.Fabric));
        if (info.HasLiteLoader)
            tags.Add("LiteLoader");
        if (info.HasOptiFine)
            tags.Add("OptiFine" + McInstanceInfo.FormatVersion(info.OptiFine));
        newItem.PlainTags = tags;

        try
        {
            newItem.Logo = instance.Logo.EndsWith(Path.Combine("PCL", "Logo.png"))
                ? Path.Combine(instance.PathInstance, "PCL", "Logo.png")
                : instance.Logo;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.IconLoad"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Select.Instance.Error.IconLoad"));
            newItem.Logo = LauncherPaths.ImageRoot + "Blocks/RedstoneBlock.png";
        }

        InstanceItemContent(newItem, EventArgs.Empty);
        return newItem;
    }

    private void InstanceItemContent(MyListItem sender, EventArgs e)
    {
        if (sender.Tag is not McInstance version)
            return;

        sender.Click += (_, _) => ViewModel.SelectInstance(version);
        sender.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(sender).Properties.IsRightButtonPressed)
                PageSelectRightViewModel.OpenInstanceSettings(version);
        };

        var btnStar = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = version.IsStar ? "lucide/heart-filled" : "lucide/heart"
        };
        ToolTip.SetTip(
            btnStar,
            Lang.Text(version.IsStar ? "Select.Instance.Unfavorite" : "Select.Instance.Favorite"));
        btnStar.Click += (_, _) => PageSelectRightViewModel.ToggleFavorite(version);

        var btnOpenFolder = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/folder-open"
        };
        ToolTip.SetTip(btnOpenFolder, Lang.Text("Select.Instance.OpenFolder"));
        btnOpenFolder.Click += (_, _) => PageSelectRightViewModel.OpenVersionFolder(version);

        var btnDelete = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/trash-2"
        };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) => _ = ViewModel.DeleteVersionAsync(
            version,
            btnDelete.LastClickKeyModifiers.HasFlag(KeyModifiers.Shift));

        var btnSettings = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = version.state == McInstanceState.Error
                ? "lucide/folder-open"
                : "lucide/settings"
        };
        ToolTip.SetTip(
            btnSettings,
            Lang.Text(
                version.state == McInstanceState.Error
                    ? "Common.Action.OpenFolder"
                    : "Select.Instance.Settings"));
        if (version.state == McInstanceState.Error)
            btnSettings.Click += (_, _) => PageSelectRightViewModel.OpenVersionFolder(version);
        else
            btnSettings.Click += (_, _) => PageSelectRightViewModel.OpenInstanceSettings(version);

        sender.Buttons = [btnStar, btnOpenFolder, btnDelete, btnSettings];
    }

    public static void OpenVersionFolder(McInstance version)
    {
        PageSelectRightViewModel.OpenVersionFolder(version);
    }

    public static void OpenInstanceSettings(McInstance version)
    {
        PageSelectRightViewModel.OpenInstanceSettings(version);
    }

    #endregion
}