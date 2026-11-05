using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
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
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        ToolTip.SetTip(PanVerSearchBox, Lang.Text("Select.Instance.Search.ToolTip"));
        PanVerSearchBox.TextChanged += (_, _) => OnSearchTextChanged();
        BtnEmptyDownload.Click += (_, _) =>
            PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
        DetachedFromVisualTree += (_, _) => StopReloadTimer();
    }

    public static PageSelectRight? Current { get; private set; }

    public bool ShowHidden { get; set; }

    public void Refresh()
    {
        RefreshInstances(true);
    }

    public bool ToggleHiddenInstances()
    {
        ShowHidden = !ShowHidden;
        RefreshInstances(true);
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
            autoRun: false);
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
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected ?? "",
            Loading.LoaderFolderRunType.RunOnUpdated,
            1,
            $"versions{Path.DirectorySeparatorChar}");
        PanBack.ScrollToHome();
    }

    public void RefreshInstances(bool force)
    {
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected ?? "",
            force ? Loading.LoaderFolderRunType.ForceRun : Loading.LoaderFolderRunType.RunOnUpdated,
            1,
            $"versions{Path.DirectorySeparatorChar}");
    }

    #region 搜索

    private void OnSearchTextChanged()
    {
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
        if (_reloadTimer is null)
            return;
        _reloadTimer.Stop();
        _reloadTimer = null;
        _refreshed = false;
    }

    #endregion

    #region 列表构建

    private void McInstanceListUi(Loading.LoaderTask<string, int> loader)
    {
        try
        {
            PanMain.Children.Clear();

            var hasAnyResults = false;
            var searchText = PanVerSearchBox.Text?.Trim().ToLower() ?? string.Empty;
            var originalHasInstances = InstanceList
                .mcInstanceList
                .ToArray()
                .Any(card => card.Value.Count > 0);

            foreach (var card in InstanceList.mcInstanceList.ToArray())
            {
                if (card.Key == McInstanceCardType.Hidden != ShowHidden)
                    continue;
                var filtered = card
                    .Value
                    .Where(instance => MatchesSearch(instance, searchText))
                    .ToList();
                if (filtered.Count == 0)
                    continue;

                hasAnyResults = true;
                var cardNameKey = GetCardNameKey(card.Key, card.Value);
                var cardType = card.Key;
                var cardCount = filtered.Count;

                var newCard = new MyCard
                {
                    Margin = new Thickness(0d, 0d, 0d, 15d),
                    Tag = (Key: cardNameKey, Type: cardType)
                };
                LocalizedText.TryBind(
                    newCard,
                    "Title",
                    () => $"{Lang.Text(cardNameKey)}"
                        + (cardType == McInstanceCardType.Star
                            ? ""
                            : $" ({Lang.Number(cardCount, "N0")})"));

                var stack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    Tag = filtered
                };
                newCard.Children.Add(stack);
                newCard.SwapControl = stack;

                void PutMethod(StackPanel panel)
                {
                    foreach (var instance in (List<McInstance>)panel.Tag!)
                        panel.Children.Add(CreateInstanceItem(instance));
                }

                if (card.Key is McInstanceCardType.Rubbish
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
            }

            if (PanMain.Children.Count == 1
                && PanMain.Children[0] is MyCard { IsSwapped: true } onlyCard)
                onlyCard.IsSwapped = false;

            if (hasAnyResults)
            {
                PanAllBack.IsVisible = true;
                PanVerSearchBox.IsVisible = true;
                PanEmpty.IsVisible = false;
                PanEmptySearch.IsVisible = false;
                return;
            }

            // 完全没有实例：显示空态；隐藏实例模式下提示无隐藏实例。
            if (!originalHasInstances)
            {
                PanAllBack.IsVisible = false;
                PanVerSearchBox.IsVisible = false;
                PanEmptySearch.IsVisible = false;
                PanEmpty.IsVisible = true;
                if (ShowHidden)
                {
                    LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                    LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                    BtnEmptyDownload.IsVisible = false;
                }
                else
                {
                    LabEmptyTitle.Text = Lang.Text("Select.Instance.Empty.Title");
                    LabEmptyContent.Text = Lang.Text("Select.Instance.Empty.Message");
                    BtnEmptyDownload.IsVisible =
                        HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
                }

                return;
            }

            if (ShowHidden)
            {
                var hasHiddenInstances = InstanceList
                    .mcInstanceList
                    .ToArray()
                    .Any(card => card.Key == McInstanceCardType.Hidden && card.Value.Count > 0);
                if (hasHiddenInstances)
                {
                    PanAllBack.IsVisible = true;
                    PanVerSearchBox.IsVisible = true;
                    PanEmpty.IsVisible = false;
                    PanEmptySearch.IsVisible = true;
                    LabEmptySearchTitle.Text = Lang.Text("Select.Instance.Hidden.EmptySearchTitle");
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoHiddenResult", searchText);
                }
                else
                {
                    PanAllBack.IsVisible = false;
                    PanVerSearchBox.IsVisible = false;
                    PanEmpty.IsVisible = true;
                    PanEmptySearch.IsVisible = false;
                    LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                    LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                    BtnEmptyDownload.IsVisible = false;
                }

                return;
            }

            PanAllBack.IsVisible = true;
            PanVerSearchBox.IsVisible = true;
            PanEmpty.IsVisible = false;
            PanEmptySearch.IsVisible = true;
            LabEmptySearchTitle.Text = Lang.Text("Select.Instance.EmptySearch.Title");
            LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                ? Lang.Text("Select.Instance.Search.EmptyInput")
                : Lang.Text("Select.Instance.Search.NoResult", searchText);
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

    private static string GetCardNameKey(McInstanceCardType type, List<McInstance> instances)
    {
        switch (type)
        {
            case McInstanceCardType.OriginalLike:
                return "Select.Instance.Card.Regular";
            case McInstanceCardType.API:
            {
                var flags = new HashSet<string>(StringComparer.Ordinal);
                foreach (var instance in instances)
                {
                    if (!instance.IsLoaded)
                        instance.Load();
                    if (instance.Info.HasFabric) flags.Add("Fabric");
                    if (instance.Info.HasQuilt) flags.Add("Quilt");
                    if (instance.Info.HasLiteLoader) flags.Add("LiteLoader");
                    if (instance.Info.HasForge) flags.Add("Forge");
                    if (instance.Info.HasNeoForge) flags.Add("NeoForge");
                    if (instance.Info.HasCleanroom) flags.Add("Cleanroom");
                    if (instance.Info.HasLabyMod) flags.Add("LabyMod");
                }

                if (flags.Count > 1) return "Select.Instance.Card.Modable";
                if (flags.Contains("Forge")) return "Select.Instance.Card.Forge";
                if (flags.Contains("NeoForge")) return "Select.Instance.Card.NeoForge";
                if (flags.Contains("Cleanroom")) return "Select.Instance.Card.Cleanroom";
                if (flags.Contains("LabyMod")) return "Select.Instance.Card.LabyMod";
                if (flags.Contains("LiteLoader")) return "Select.Instance.Card.LiteLoader";
                if (flags.Contains("Quilt")) return "Select.Instance.Card.Quilt";
                return "Select.Instance.Card.Fabric";
            }
            case McInstanceCardType.Error:
                return "Select.Instance.Card.Error";
            case McInstanceCardType.Hidden:
                return "Select.Instance.Card.Hidden";
            case McInstanceCardType.Rubbish:
                return "Select.Instance.Card.LessUsed";
            case McInstanceCardType.Star:
                return "Select.Instance.Card.Favorites";
            case McInstanceCardType.Fool:
                return "Select.Instance.Card.AprilFools";
            default:
                throw new ArgumentException($"未知的卡片种类（{(int)type}）");
        }
    }

    private static bool MatchesSearch(McInstance instance, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return true;
        return (instance.Name ?? "").ToLower().Contains(searchText)
            || (instance.Desc ?? "").ToLower().Contains(searchText)
            || instance
                .GetDefaultDescription()
                .Replace(",", "")
                .ToLower()
                .Trim()
                .Contains(searchText);
    }

    private static MyListItem CreateInstanceItem(McInstance instance)
    {
        var newItem = new MyListItem
        {
            Title = instance.Name,
            Height = 42d,
            tag = instance,
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

        newItem.ContentHandler = InstanceItemContent;
        return newItem;
    }

    private static void InstanceItemContent(MyListItem sender, EventArgs e)
    {
        if (sender.tag is not McInstance version)
            return;

        sender.Click += (_, _) => ItemClick(version);

        var shiftPressed = false;
        sender.PointerReleased += (_, args) =>
            shiftPressed = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
        sender.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(sender).Properties.IsRightButtonPressed)
                OpenInstanceSettings(version);
        };

        var btnStar = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = version.IsStar ? "lucide/heart-filled" : "lucide/heart"
        };
        ToolTip.SetTip(
            btnStar,
            Lang.Text(version.IsStar ? "Select.Instance.Unfavorite" : "Select.Instance.Favorite"));
        btnStar.Click += (_, _) =>
        {
            States.Instance.Starred[version.PathInstance] = !version.IsStar;
            InstanceList.mcInstanceListForceRefresh = true;
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                GameFolders.mcFolderSelected ?? "",
                Loading.LoaderFolderRunType.ForceRun,
                1,
                $"versions{Path.DirectorySeparatorChar}");
        };

        var btnOpenFolder = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/folder-open"
        };
        ToolTip.SetTip(btnOpenFolder, Lang.Text("Select.Instance.OpenFolder"));
        btnOpenFolder.Click += (_, _) => OpenVersionFolder(version);

        var btnDelete = new MyIconButton
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/trash-2"
        };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) => _ = DeleteVersionAsync(version, shiftPressed);

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
            btnSettings.Click += (_, _) => OpenVersionFolder(version);
        else
            btnSettings.Click += (_, _) => OpenInstanceSettings(version);

        sender.Buttons = [btnStar, btnOpenFolder, btnDelete, btnSettings];
    }

    public static void OpenVersionFolder(McInstance version)
    {
        Basics.OpenPath(version.PathInstance);
    }

    public static void OpenInstanceSettings(McInstance version)
    {
        PageRouter.Navigate(
            new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, version));
    }

    private static void ItemClick(McInstance instance)
    {
        if (new McInstance(instance.PathInstance).Check())
        {
            InstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance.Name;
            PageRouter.Back();
        }
        else
        {
            OpenVersionFolder(instance);
        }
    }

    private static async Task DeleteVersionAsync(McInstance instance, bool permanent)
    {
        try
        {
            var isHintIndie = instance.state != McInstanceState.Error
                && (instance.PathIndie ?? "") != (GameFolders.mcFolderSelected ?? "");
            // Shift 直接永久删除；否则优先走系统回收站。平台或所在卷没有回收站时，
            // 确认文案必须改写成“永久删除、不可恢复”，不能沿用“已移至回收站”的说法。
            var canRecycle = !permanent && RecycleBin.IsSupported(instance.PathInstance);
            var confirmMsg = !permanent && !canRecycle
                ? Lang.Text("Select.Instance.Delete.RecycleBinUnavailable", instance.Name)
                : Lang.Text(
                    permanent
                        ? "Select.Instance.Delete.ConfirmPermanentMessage"
                        : "Select.Instance.Delete.ConfirmMessage",
                    instance.Name);
            var confirmFullMsg = confirmMsg
                + (isHintIndie
                    ? "\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning")
                    : "");
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                    Content = confirmFullMsg,
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            try
            {
                if (ConfigService.GetProvider(ConfigSource.GameInstance) is
                    DynamicCacheConfigStorage storage)
                    storage.InvalidateCache(instance.PathInstance);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "清除实例配置缓存失败");
            }

            LaunchLogCache(instance);
            LauncherCommon.IniClearCache(Path.Combine(instance.PathIndie ?? "", "options.txt"));
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        if (!permanent && TryRecycleInstance(instance)) return;
                        LauncherCommon.DeleteDirectory(instance.PathInstance);
                        HintService.Show(
                            Hints.Success(
                                Lang.Text(
                                    "Select.Instance.Delete.PermanentSuccess",
                                    instance.Name)));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(
                            ex,
                            Lang.Text("Select.Instance.Error.Delete", instance.Name),
                            LauncherLogLevel.Feedback,
                            userSummary: Lang.Text("Select.Instance.Error.Delete", instance.Name));
                    }
                    finally
                    {
                        Loading.LoaderFolderRun(
                            InstanceList.mcInstanceListLoader,
                            GameFolders.mcFolderSelected ?? "",
                            Loading.LoaderFolderRunType.ForceRun,
                            1,
                            $"versions{Path.DirectorySeparatorChar}");
                    }
                },
                "Instance Delete " + LauncherCommon.GetUuid(),
                ThreadPriority.BelowNormal);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.Delete", instance.Name),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.Delete", instance.Name));
        }
    }

    /// <summary>
    ///     尝试把实例目录移入系统回收站；成功时给出“已删除到回收站”提示并返回 <c>true</c>。
    ///     平台或所在卷不支持回收站时返回 <c>false</c>，交由调用方按永久删除处理（文案已在确认阶段告知）。
    /// </summary>
    private static bool TryRecycleInstance(McInstance instance)
    {
        var outcome = RecycleBin.Move(instance.PathInstance, out var error);
        switch (outcome)
        {
            case RecycleBin.Outcome.Moved:
                HintService.Show(
                    Hints.Success(
                        Lang.Text("Select.Instance.Delete.RecycleBinSuccess", instance.Name)));
                return true;
            case RecycleBin.Outcome.Failed:
                throw new IOException(
                    error ?? Lang.Text("Select.Instance.Error.Delete", instance.Name));
            case RecycleBin.Outcome.Unsupported:
            default:
                return false;
        }
    }

    private static void LaunchLogCache(McInstance instance)
    {
        LauncherCommon.Log($"[Minecraft] 删除实例 {instance.Name}");
    }

    #endregion
}