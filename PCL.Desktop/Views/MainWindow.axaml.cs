// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Instances;
using PCL.Application.Launching;
using PCL.Application.Minecraft.Launch.Arguments;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Diagnostics;
using PCL.Desktop.Features.Community;
using PCL.Desktop.Hosting;
using PCL.Desktop.Localization;
using PCL.Desktop.Theme;
using PCL.Desktop.Platform;
using PCL.Desktop.Features.Downloads.Views;
using PCL.Desktop.Features.Instances.Views;
using PCL.Desktop.Features.Launching;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Settings.Views;
using PCL.Desktop.Features.Shared;
using PCL.Desktop.Features.Tasks.Views;
using PCL.Platform.Paths;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Views;

public partial class MainWindow : Window, IDisposable
{
    private static readonly IWebProxy SystemDefaultProxy = HttpClient.DefaultProxy;
    private Control? _showAnimationRoot;
    private RotateTransform? _showAnimationRotate;
    private TranslateTransform? _showAnimationTranslate;
    private bool _showAnimationStarted;
    private bool _isNavExpanded;
    private DispatcherTimer? _navAnimTimer;
    private double _navExpandedWidth = 200d;
    private double _navAnimStart;
    private double _navAnimTarget;
    private int _navAnimElapsed;
    private NavigationRouteId? _currentNavRoute;
    private NavigationRouteId? _pendingNavRoute;
    private bool _isMainWindowOpened;
    private PageLaunchLeft? _launchLeft;
    private PageLaunchRight? _launchRight;
    private PageLoginProfile? _loginProfilePage;
    private PageLoginProfileSkin? _loginProfileSkinPage;
    private PageLoginMs? _loginMsPage;
    private PageLoginAuth? _loginAuthPage;
    private PageLoginOffline? _loginOfflinePage;
    private PageDownloadLeft? _downloadLeft;
    private PageDownloadInstall? _downloadInstallPage;
    private PageCommunityLeft? _communityLeft;
    private PageCommunityRight? _communityRight;
    private PageCommunityDetail? _communityDetail;
    private PageCommunityFavoritesRight? _communityFavoritesRight;
    private readonly CommunityFavoritesStore _communityFavorites = new();
    private PageSpeedLeft? _speedLeft;
    private PageSpeedRight? _speedRight;
    private PageInstanceLeft? _instanceLeft;
    private PageInstanceSelectLeft? _instanceSelectLeft;
    private PageInstanceSelectRight? _instanceSelectPage;
    private PageInstanceManageRight? _instanceManagePage;
    private PageInstanceSetupRight? _instanceSetupPage;
    private PageInstanceExportRight? _instanceExportPage;
    private PageInstanceInstallRight? _instanceInstallPage;
    private PageInstanceSavesRight? _instanceSavesPage;
    private PageInstanceSavesInfoRight? _instanceSavesInfoPage;
    private PageInstanceScreenshotRight? _instanceScreenshotPage;
    private PageInstanceToolsRight? _instanceToolsPage;
    private PageInstanceModDisabledRight? _instanceModDisabledPage;
    private PageInstanceResourceRight? _instanceResourcePage;
    private PageInstanceResourceRight? _instanceDatapackPage;
    private PageInstanceServerRight? _instanceServerPage;
    private LaunchInstanceInfo? _managedInstance;
    private bool _isTitleSubPageVisible;
    private Action? _titleInnerBackAction;
    private MyScrollViewer? _backButtonScrollViewer;
    private CancellationTokenSource? _launchCancellation;
    private CancellationTokenSource? _microsoftLoginCancellation;
    private readonly MinecraftVanillaInstallService _minecraftInstallService = new();
    private readonly MinecraftLaunchCoordinator _launchCoordinator;
    private readonly ThirdPartyAuthService _thirdPartyAuthService = new();
    private readonly IMicrosoftMinecraftAuthService _microsoftAuthService;
    private PageSetupLeft? _setupLeft;
    private MyPageRight? _setupRight;
    private readonly List<LoginProfileInfo> _loginProfiles = [];
    private readonly List<MinecraftFolderInfo> _minecraftFolders = [];
    private NavigationPageDescriptor[] _navigationPages;
    private readonly Dictionary<string, TaskManagerEntrySnapshot> _taskSnapshots = [];
    private readonly Dictionary<string, CancellationTokenSource> _taskCancellations = [];
    private readonly DesktopPageAdapter _pageAdapter = new();
    private readonly DesktopPageContext _desktopPageContext;
    private int _registeredPageRequestId;
    private int _taskSequence;
    private bool _isTaskManagerVisible;
    private NavigationRouteId? _taskManagerBackRoute;
    private Action? _taskManagerBackAction;
    private string? _selectedMinecraftRoot;
    private bool _minecraftFoldersLoaded;
    private bool _isGameRunning;
    private Process? _runningGameProcess;
    private RunningGameContext? _runningGameContext;
    private double _targetWindowOpacity = 1d;
    private string? _backgroundStamp;
    private string? _backgroundFile;
    private string? _backgroundRefreshToken;
    private Bitmap? _backgroundBitmap;
    private string? _titleLogoFile;
    private Bitmap? _titleLogoBitmap;
    private string? _homepageSignature;
    private CancellationTokenSource? _homepageLoadCancellation;
    private readonly IDisposable _windowStateSubscription;
    private string? _registeredPluginPageSurfaceId;

    private const double NavCollapsedWidth = 50d;
    private const int NavAnimDuration = 200;

    private static readonly NavigationRouteId LaunchRoute = DesktopNavigationRegistry.LaunchRoute;
    private static readonly NavigationRouteId DownloadRoute = DesktopNavigationRegistry.DownloadRoute;
    private static readonly NavigationRouteId CommunityRoute = DesktopNavigationRegistry.CommunityRoute;
    private static readonly NavigationRouteId SettingsRoute = DesktopNavigationRegistry.SettingsRoute;

    public MainWindow()
        : this(new MicrosoftMinecraftAuthService())
    {
    }

    public MainWindow(IMicrosoftMinecraftAuthService microsoftAuthService)
    {
        _microsoftAuthService = microsoftAuthService ?? throw new ArgumentNullException(nameof(microsoftAuthService));
        _launchCoordinator = new MinecraftLaunchCoordinator(_minecraftInstallService);
        AvaloniaXamlLoader.Load(this);
        DesktopHostUiComposition.Instance.RegisterTarget("pcl.window.main", this);
        if (this.FindControl<Panel>("PanTitleSelect") is { } navigationPanel)
        {
            DesktopHostUiComposition.Instance.RegisterTarget("pcl.navigation.main", navigationPanel);
            DesktopHostUiComposition.Instance.RegisterSlot(
                "pcl.navigation.main",
                "items.after-download",
                navigationPanel);
        }
        _windowStateSubscription = this.GetObservable(WindowStateProperty).Subscribe(_ =>
        {
            UpdateBackgroundVideoPlayback();
            UpdateWindowChrome();
        });
        if (this.FindControl<MediaElement>("VideoBack") is { } video)
            video.MediaFailed += VideoFailed;
        _navigationPages = CreateNavigationPageMap(DesktopHost.Current.Navigation);
        BuildMainNavigationItems();
        _desktopPageContext = new DesktopPageContext(
            CreateLaunchMainPage,
            CreateDownloadMainPage,
            CreateCommunityMainPage,
            CreateSettingsMainPage,
            CreatePlaceholderMainPage);
        // Headless tests skip the load animation so the window is not left at Opacity 0.
        Opacity = ShouldSuppressStartupDialogs() ? 1d : 0d;
        CanResize = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        SetWindowIcon();
        LauncherSettingsPageBinder.SettingsChanged += LauncherSettingsChanged;
        AvaloniaLocalizationManager.LanguageChanged += LocalizationChanged;
        ApplyRuntimeSettings(LauncherSettingsPageBinder.LoadSettings());
        RefreshTitleButtonsBeforeFirstFrame();
        RefreshNavigationText();
        CaptureShowAnimationTransforms();
        Opened += OnMainWindowOpened;
        DesktopHostNotifications.Instance.Attach(OnPluginHostNotification);
        DesktopHost.Current.Navigation.Changed += NavigationRegistryChanged;
        DesktopHostNavigation.Instance.Attach(NavigateToPluginRoute);
        SyncTitleOverlayWidth();
        _ = LoadProfilesAsync();
        SelectNavRoute(LaunchRoute, animate: false);
    }

    private void OnPluginHostNotification(string message, bool critical) =>
        ShowHint(message, critical);

    private void NavigationRegistryChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => NavigationRegistryChanged(sender, e));
            return;
        }

        NavigationRouteId selected = _currentNavRoute ?? LaunchRoute;
        _navigationPages = CreateNavigationPageMap(DesktopHost.Current.Navigation);
        BuildMainNavigationItems();
        RefreshNavigationText();
        if (FindNavigationPage(selected) is not null)
            SelectNavRoute(selected, animate: false);
    }

    private void NavigateToPluginRoute(string route)
    {
        if (!string.IsNullOrWhiteSpace(route))
            SelectNavRoute(NavigationRouteId.Parse(route), animate: true);
    }

    private void RefreshTitleButtonsBeforeFirstFrame()
    {
        foreach (string name in new[] { "BtnTitleClose", "BtnTitleMax", "BtnTitleMin", "BtnTitleHelp" })
            this.FindControl<MyIconButton>(name)?.RefreshAnim();
    }

    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
        if (this.FindControl<Panel>("PanMsg") is { Children.Count: > 0 })
            return;

        if (e.Key == Key.Escape && _isTitleSubPageVisible)
        {
            BtnTitleInner_Click(sender, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F11 &&
            this.FindControl<Border>("PanMainRight")?.Child is PageInstanceSelectRight instanceSelectPage)
        {
            instanceSelectPage.ShowHidden = !instanceSelectPage.ShowHidden;
            e.Handled = true;
        }
    }

    private void FormMain_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (IsTextInputEventSource(e.Source))
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            e.GetPosition(this).Y <= 48)
        {
            BeginMoveDrag(e);
        }
    }

    private static bool IsTextInputEventSource(object? source)
    {
        if (source is not Visual visual)
            return false;

        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox)
                return true;
            if (current is ComboBox { IsEditable: true })
                return true;
        }

        return false;
    }

    private void FormMain_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncMainSize();
        SyncTitleOverlayWidth();
    }

    private void FormMain_Closing(object? sender, WindowClosingEventArgs e)
    {
        LauncherSettingsPageBinder.SettingsChanged -= LauncherSettingsChanged;
        AvaloniaLocalizationManager.LanguageChanged -= LocalizationChanged;
        CancelAllTrackedTasks();
        _launchCancellation?.Cancel();
        this.FindControl<MediaElement>("VideoBack")?.Stop();
    }

    private void FormMain_Activated(object? sender, EventArgs e)
    {
        UpdateBackgroundVideoPlayback();
    }

    private void FrmMain_Drop(object? sender, DragEventArgs e)
    {
    }

    private void FormMain_MouseMove(object? sender, PointerEventArgs e)
    {
    }

    private void VideoEnded(object? sender, EventArgs e)
    {
        if (sender is not MediaElement video || video.Source is null)
            return;

        video.Stop();
        UpdateBackgroundVideoPlayback();
    }

    private void VideoFailed(object? sender, MediaFailedEventArgs e)
    {
        if (sender is MediaElement video)
            video.IsVisible = false;
        Debug.WriteLine($"[UI] 背景视频播放失败：{e.Exception.Message}");
    }

    private void PanTitle_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncTitleOverlayWidth();
    }

    private void BtnTitleClose_Click(object? sender, EventArgs e) => Close();

    private void BtnTitleMin_Click(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnTitleMax_Click(object? sender, EventArgs e) => ToggleMaximized();

    private void BtnTitleHelp_Click(object? sender, EventArgs e)
    {
    }

    private void BtnTitleInner_Click(object? sender, EventArgs e)
    {
        if (_titleInnerBackAction is { } backAction)
        {
            _titleInnerBackAction = null;
            backAction();
            return;
        }

        SelectNavRoute(LaunchRoute, animate: true);
    }

    private void BtnNavItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem item || !TryGetNavRoute(item, out NavigationRouteId route))
            return;

        SelectNavPage(route, animate: _isMainWindowOpened);
        e.Handled = true;
    }

    private void BtnNavToggle_Click(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanNavLayer") is not { } navLayer)
            return;

        _isNavExpanded = !_isNavExpanded;
        if (_isNavExpanded)
            _navExpandedWidth = MeasureNavExpandedWidth(navLayer);

        _navAnimStart = GetCurrentNavWidth(navLayer);
        _navAnimTarget = _isNavExpanded ? _navExpandedWidth : NavCollapsedWidth;
        _navAnimElapsed = 0;
        _navAnimTimer?.Stop();
        _navAnimTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _navAnimTimer.Tick += NavAnimTimer_Tick;
        _navAnimTimer.Start();
    }

    private void PanMainLeft_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // WPF FormMain.PanMainLeft_Resize tracked RectLeftBackground width.
        // Frosted fill is applied on the PanMainLeft host; keep names for compatibility.
        if (this.FindControl<AnimatedBackgroundGrid>("RectLeftBackground") is { } rectBg)
            rectBg.Width = Math.Max(0d, e.NewSize.Width);
    }

    private void BtnExtraUpdateRestart_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraBack_Click(object? sender, EventArgs e)
    {
        if (GetCurrentRightScroll() is { } scroll)
            scroll.PerformVerticalOffsetDelta(-scroll.Offset.Y);
    }

    private void BtnExtraDownload_Click(object? sender, EventArgs e)
    {
        ApplyTaskManagerPage(animate: true);
    }

    private void BtnExtraApril_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraShutdown_Click(object? sender, EventArgs e)
    {
        // WPF: kill the watched Minecraft process and hide the shutdown extra button.
        Process? process = _runningGameProcess;
        if (process is null || process.HasExited)
        {
            SetGameRunningExtras(null);
            return;
        }

        ShowConfirmDialog(
            "关闭游戏",
            "确定要强制结束正在运行的 Minecraft 进程吗？未保存的进度可能会丢失。",
            confirmed =>
            {
                if (!confirmed)
                    return;
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    ShowHint("结束游戏失败：" + TruncateHint(ex.Message), critical: true);
                }
                finally
                {
                    SetGameRunningExtras(null);
                }
            },
            "结束游戏",
            "取消");
    }

    private void BtnExtraLog_Click(object? sender, EventArgs e)
    {
        // WPF BtnExtraLog: open the running instance's logs folder / latest.log.
        try
        {
            LaunchInstanceInfo? instance = _launchLeft?.SelectedInstance ?? _managedInstance;
            if (instance is null)
            {
                ShowHint("当前没有可查看的游戏日志");
                return;
            }

            string logsDir = Path.Combine(instance.InstanceDirectory, "logs");
            string latestLog = Path.Combine(logsDir, "latest.log");
            string openTarget = File.Exists(latestLog)
                ? latestLog
                : Directory.Exists(logsDir)
                    ? logsDir
                    : instance.InstanceDirectory;

            Process.Start(new ProcessStartInfo
            {
                FileName = openTarget,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowHint("打开游戏日志失败：" + TruncateHint(ex.Message), critical: true);
        }
    }

    private void BtnExtraMusic_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_RightClick(object? sender, PointerReleasedEventArgs e)
    {
    }

    private void FormDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2 && CanResize)
        {
            ToggleMaximized();
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
    }

    private void ToggleMaximized()
    {
        if (!CanResize)
            return;

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize || WindowState != WindowState.Normal ||
            sender is not Control { Tag: string edgeName } ||
            !Enum.TryParse(edgeName, ignoreCase: false, out WindowEdge edge) ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginResizeDrag(edge, e);
        e.Handled = true;
    }

    private void UpdateWindowChrome()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (this.FindControl<Border>("PanBack") is { } background)
        {
            background.Margin = maximized ? new Thickness(0d) : new Thickness(10d);
            background.CornerRadius = maximized ? new CornerRadius(0d) : new CornerRadius(8d);
        }

        if (this.FindControl<MyIconButton>("BtnTitleMax") is { } maximizeButton)
            maximizeButton.SvgIcon = maximized ? "lucide/copy" : "lucide/square";

        // SizeChanged re-measures PanTitle after the native state transition.
        // Keep the normal title layer visible during the immediate state update.
        SyncTitleOverlayWidth();
        if (!_isTitleSubPageVisible && this.FindControl<Control>("PanTitleMain") is { } titleMain)
        {
            titleMain.IsVisible = true;
            titleMain.Opacity = 1d;
        }
    }

    private void SyncTitleOverlayWidth()
    {
        Control? panTitle = this.FindControl<Control>("PanTitle");
        Control? panTitleMain = this.FindControl<Control>("PanTitleMain");
        Control? panTitleInner = this.FindControl<Control>("PanTitleInner");
        if (panTitle is null)
            return;

        double width = panTitle.Bounds.Width;
        if (width <= 0)
            width = Width;
        if (panTitleMain is not null)
            panTitleMain.Width = width;
        if (panTitleInner is not null)
            panTitleInner.Width = width;
    }

    private void EnterTitleSubPage(string title)
    {
        Control? panTitleMain = this.FindControl<Control>("PanTitleMain");
        Control? panTitleInner = this.FindControl<Control>("PanTitleInner");
        TextBlock? labTitleInner = this.FindControl<TextBlock>("LabTitleInner");
        if (panTitleMain is null || panTitleInner is null || labTitleInner is null)
            return;

        if (_isTitleSubPageVisible)
        {
            if (labTitleInner.Text == title)
                return;

            if (_isMainWindowOpened)
            {
                ModAnimation.AniStart(
                    new List<ModAnimation.AniData>
                    {
                        ModAnimation.AaOpacity(labTitleInner, -labTitleInner.Opacity, 130),
                        ModAnimation.AaCode(() => labTitleInner.Text = title, after: true),
                        ModAnimation.AaOpacity(labTitleInner, 1d, 150, 30)
                    },
                    "FrmMain Titlebar SubLayer");
            }
            else
            {
                labTitleInner.Text = title;
                labTitleInner.Opacity = 1d;
            }
            return;
        }

        _isTitleSubPageVisible = true;
        panTitleInner.IsVisible = true;
        panTitleInner.IsHitTestVisible = true;
        panTitleMain.IsHitTestVisible = false;
        labTitleInner.Text = title;

        if (!_isMainWindowOpened)
        {
            panTitleMain.IsVisible = false;
            panTitleMain.Opacity = 0d;
            panTitleInner.Opacity = 1d;
            panTitleInner.Margin = new Thickness(-16d, 0d, 0d, 0d);
            return;
        }

        panTitleMain.IsVisible = true;
        panTitleInner.Opacity = 0d;
        panTitleInner.Margin = new Thickness(-16d, 0d, 0d, 0d);
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(panTitleMain, -panTitleMain.Opacity, 150),
                ModAnimation.AaX(panTitleMain, 12d - panTitleMain.Margin.Left, 150,
                    ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaOpacity(panTitleInner, 1d - panTitleInner.Opacity, 150, 200),
                ModAnimation.AaX(panTitleInner, -panTitleInner.Margin.Left, 350, 200,
                    new ModAnimation.AniEaseOutBack()),
                ModAnimation.AaCode(() => panTitleMain.IsVisible = false, after: true)
            },
            "FrmMain Titlebar FirstLayer");
    }

    private void ExitTitleSubPage()
    {
        if (!_isTitleSubPageVisible)
            return;

        Control? panTitleMain = this.FindControl<Control>("PanTitleMain");
        Control? panTitleInner = this.FindControl<Control>("PanTitleInner");
        if (panTitleMain is null || panTitleInner is null)
            return;

        _isTitleSubPageVisible = false;
        panTitleMain.IsVisible = true;
        panTitleMain.IsHitTestVisible = true;
        panTitleInner.IsHitTestVisible = false;

        if (!_isMainWindowOpened)
        {
            panTitleMain.Opacity = 1d;
            panTitleMain.Margin = new Thickness(0d);
            panTitleInner.Opacity = 0d;
            panTitleInner.Margin = new Thickness(-16d, 0d, 0d, 0d);
            panTitleInner.IsVisible = false;
            return;
        }

        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(panTitleInner, -panTitleInner.Opacity, 150),
                ModAnimation.AaX(panTitleInner, -18d - panTitleInner.Margin.Left, 150,
                    ease: new ModAnimation.AniEaseInFluent()),
                ModAnimation.AaOpacity(panTitleMain, 1d - panTitleMain.Opacity, 150, 200),
                ModAnimation.AaX(panTitleMain, -panTitleMain.Margin.Left, 350, 200,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => panTitleInner.IsVisible = false, after: true)
            },
            "FrmMain Titlebar FirstLayer");
    }

    private void RefreshBackToTopBinding()
    {
        if (_backButtonScrollViewer is not null)
            _backButtonScrollViewer.ScrollChanged -= BackButtonScrollViewer_ScrollChanged;

        _backButtonScrollViewer = GetCurrentRightScroll();
        if (_backButtonScrollViewer is not null)
            _backButtonScrollViewer.ScrollChanged += BackButtonScrollViewer_ScrollChanged;

        UpdateBackToTopButton();
    }

    private void BackButtonScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateBackToTopButton();

    private void UpdateBackToTopButton()
    {
        if (this.FindControl<MyExtraButton>("BtnExtraBack") is not { } back)
            return;

        MyScrollViewer? scroll = _backButtonScrollViewer ?? GetCurrentRightScroll();
        // WPF: show once scrolled roughly one viewport (not window height + 700px).
        double threshold = scroll is null
            ? double.MaxValue
            : Math.Max(240d, scroll.Viewport.Height * 0.55d);
        bool show = scroll is { IsVisible: true } && scroll.Offset.Y > threshold;
        back.Show = show;
        // Keep IsVisible in sync so the button is actually clickable (Show alone is not enough
        // before the extra-button host has applied scale animations on first frame).
        if (!back.IsVisible && show)
            back.IsVisible = true;
    }

    private MyScrollViewer? GetCurrentRightScroll() =>
        this.FindControl<Border>("PanMainRight")?.Child is MyPageRight page ? page.PanScroll : null;

    public void ActivateExistingInstance()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        WindowActivationApi.BringToForeground(this);
    }

    private void SetWindowIcon()
    {
        try
        {
            using Stream iconStream = Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://PCL.Desktop/Assets/icon.ico", UriKind.Absolute));
            Icon = new WindowIcon(iconStream);
        }
        catch (IOException)
        {
        }
    }

    private void SyncMainSize(double? navWidth = null)
    {
        Control? panBack = this.FindControl<Control>("PanBack");
        Control? panForm = this.FindControl<Control>("PanForm");
        Control? panTitle = this.FindControl<Control>("PanTitle");
        Control? panMain = this.FindControl<Control>("PanMain");
        Control? navLayer = this.FindControl<Control>("PanNavLayer");
        Control? videoBack = this.FindControl<Control>("VideoBack");
        if (panBack is null)
            return;

        double formWidth = panBack.Bounds.Width;
        double formHeight = panBack.Bounds.Height;
        if (formWidth <= 0d)
            formWidth = Math.Max(0d, Width - 20d);
        if (formHeight <= 0d)
            formHeight = Math.Max(0d, Height - 20d);

        if (panForm is not null)
        {
            panForm.Width = formWidth;
            panForm.Height = formHeight;
        }

        if (panMain is not null)
        {
            double currentNavWidth = navWidth ?? GetCurrentNavWidth(navLayer);
            panMain.Width = Math.Max(0d, formWidth - currentNavWidth);
            panMain.Height = Math.Max(0d, formHeight - (panTitle?.Bounds.Height ?? 0d));
        }

        if (videoBack is not null)
        {
            videoBack.Width = formWidth;
            videoBack.Height = formHeight;
        }
    }

    private void SetNavWidth(Control navLayer, double width)
    {
        navLayer.Width = width;
        SyncMainSize(width);
    }

    private double MeasureNavExpandedWidth(Control navLayer)
    {
        double originalWidth = navLayer.Width;
        navLayer.Width = double.NaN;
        navLayer.InvalidateMeasure();
        navLayer.Measure(new Size(double.PositiveInfinity, Math.Max(0d, Bounds.Height)));

        double measuredWidth = navLayer.DesiredSize.Width;
        foreach (MyListItem item in GetNavItems())
        {
            item.Measure(new Size(double.PositiveInfinity, item.Bounds.Height > 0d ? item.Bounds.Height : 42d));
            measuredWidth = Math.Max(measuredWidth, item.DesiredSize.Width + 2d);
        }

        navLayer.Width = originalWidth;
        navLayer.InvalidateMeasure();

        if (double.IsNaN(measuredWidth) || double.IsInfinity(measuredWidth) || measuredWidth <= 0d)
            measuredWidth = _navExpandedWidth;
        return Math.Max(measuredWidth, NavCollapsedWidth + 1d) + 10d;
    }

    private static double GetCurrentNavWidth(Control? navLayer)
    {
        if (navLayer is null)
            return NavCollapsedWidth;
        if (!double.IsNaN(navLayer.Width) && navLayer.Width > 0d)
            return navLayer.Width;
        return navLayer.Bounds.Width > 0d ? navLayer.Bounds.Width : NavCollapsedWidth;
    }

    private void NavAnimTimer_Tick(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanNavLayer") is not { } navLayer)
        {
            _navAnimTimer?.Stop();
            _navAnimTimer = null;
            return;
        }

        _navAnimElapsed += 16;
        double progress = Math.Min(1d, (double)_navAnimElapsed / NavAnimDuration);
        double current = _navAnimStart + (_navAnimTarget - _navAnimStart) * EaseOutCubic(progress);
        SetNavWidth(navLayer, current);
        if (progress < 1d)
            return;

        _navAnimTimer?.Stop();
        _navAnimTimer = null;
        SetNavWidth(navLayer, _navAnimTarget);
    }

    private void SelectNavPage(NavigationRouteId route, bool animate)
    {
        _titleInnerBackAction = null;
        NavigationPageDescriptor? descriptor = FindNavigationPage(route);
        if (descriptor is null)
            descriptor = _navigationPages.Length > 0 ? _navigationPages[0] : null;
        if (descriptor is null)
            return;
        route = descriptor.Route;

        if (_pendingNavRoute is NavigationRouteId pendingRoute && pendingRoute.Equals(route.Value))
        {
            return;
        }

        if (_currentNavRoute is NavigationRouteId currentRoute && currentRoute.Equals(route.Value))
        {
            if (_isTitleSubPageVisible || _isTaskManagerVisible)
                ApplyPagePlaceholder(route);
            return;
        }

        MyListItem? selected = null;
        foreach (MyListItem item in GetNavItems())
        {
            if (TryGetNavRoute(item, out NavigationRouteId itemRoute) && itemRoute.Equals(route.Value))
            {
                selected = item;
                break;
            }
        }

        if (selected is null)
            return;

        selected.Checked = true;
        foreach (MyListItem item in GetNavItems())
        {
            if (!ReferenceEquals(item, selected))
                item.Checked = false;
        }

        if (!animate)
        {
            ApplyPagePlaceholder(route);
            return;
        }

        BeginPageChangeAnimation(route);
    }

    private void SelectNavRoute(NavigationRouteId route, bool animate) =>
        SelectNavPage(route, animate);

    private NavigationPageDescriptor? FindNavigationPage(NavigationRouteId route)
    {
        foreach (NavigationPageDescriptor page in _navigationPages)
        {
            if (page.Route.Equals(route.Value))
                return page;
        }

        return null;
    }

    private void ApplyPagePlaceholder(NavigationRouteId route)
    {
        NavigationPageDescriptor? descriptor = FindNavigationPage(route);
        if (descriptor is null)
            return;

        _currentNavRoute = descriptor.Route;
        _pendingNavRoute = null;
        int requestId = ++_registeredPageRequestId;
        PageCreateContext context = new(descriptor.Route.Value, DesktopHost.Current.Services, _desktopPageContext);
        ValueTask<DesktopMainPage> createTask;
        try
        {
            createTask = _pageAdapter.CreateMainPageAsync(descriptor.Provider, context, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ApplyPageCreationError(descriptor.Title, ex);
            return;
        }

        if (createTask.IsCompletedSuccessfully)
        {
            ApplyRegisteredMainPage(createTask.Result);
            return;
        }

        ApplyRegisteredMainPage(CreateLoadingMainPage(descriptor.Title));
        _ = CompleteRegisteredPageAsync(createTask.AsTask(), requestId, descriptor.Title);
    }

    private async Task CompleteRegisteredPageAsync(
        Task<DesktopMainPage> createTask,
        int requestId,
        string pageTitle)
    {
        try
        {
            DesktopMainPage page = await createTask.ConfigureAwait(true);
            if (requestId != _registeredPageRequestId)
                return;

            ApplyRegisteredMainPage(page);
        }
        catch (Exception ex)
        {
            if (requestId == _registeredPageRequestId)
                ApplyPageCreationError(pageTitle, ex);
        }
    }

    private void ApplyPageCreationError(string pageTitle, Exception exception)
    {
        ApplyRegisteredMainPage(new DesktopMainPage(
            null,
            CreateTextPlaceholder(pageTitle, "页面暂时无法打开。\n\n详细信息：" + exception.Message)));
    }

    private void ApplyRegisteredMainPage(DesktopMainPage page)
    {
        _titleInnerBackAction = null;
        _isTaskManagerVisible = false;
        RefreshTaskManagerButton();
        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        if (!ReferenceEquals(leftHost.Child, page.Left))
        {
            if (leftHost.Child is MyPageLeft oldLeft)
                oldLeft.TriggerHideAnimation();
            leftHost.Child = page.Left;
        }

        if (!ReferenceEquals(rightHost.Child, page.Right))
        {
            if (rightHost.Child is MyPageRight oldRight)
                oldRight.PageOnExit();
            rightHost.Child = page.Right;
        }

        RegisterCurrentPluginPageSurface(page.Right);

        if (page.Title is { Length: > 0 } title)
            EnterTitleSubPage(title);
        else
            ExitTitleSubPage();

        RefreshBackToTopBinding();
        page.Activated?.Invoke();
        rightHost.Opacity = 1d;
    }

    private void RegisterCurrentPluginPageSurface(Control page)
    {
        string? surfaceId = _currentNavRoute?.Value switch
        {
            DesktopNavigationRegistry.LaunchRouteValue => "pcl.page.launch",
            DesktopNavigationRegistry.DownloadRouteValue => "pcl.page.download",
            DesktopNavigationRegistry.CommunityRouteValue => "pcl.page.community",
            DesktopNavigationRegistry.SettingsRouteValue => "pcl.page.settings",
            { Length: > 0 } route => route.StartsWith("pcl.", StringComparison.OrdinalIgnoreCase)
                ? "pcl.page." + route[4..]
                : route,
            _ => null
        };
        if (_registeredPluginPageSurfaceId is { } previous &&
            !string.Equals(previous, surfaceId, StringComparison.OrdinalIgnoreCase))
        {
            DesktopHostUiComposition.Instance.UnregisterTarget(previous);
        }
        if (surfaceId is null)
            return;
        _registeredPluginPageSurfaceId = surfaceId;
        DesktopHostUiComposition.Instance.RegisterTarget(surfaceId, page);
    }

    private DesktopMainPage CreateLaunchMainPage()
    {
        _launchLeft ??= CreateLaunchLeftPage();
        if (_launchRight is null)
        {
            _launchRight = new PageLaunchRight();
            _launchRight.CommunityHintHideRequested += (_, _) => PromptHideCommunityHint();
        }

        LauncherSettings launchSettings = LauncherSettingsPageBinder.LoadSettings();
        _launchRight.SetMaximumLogLines(ResolveMaximumLogLines(launchSettings));
        ApplyLaunchPageSettings(launchSettings);
        ApplyHomepageSettings(launchSettings);
        return new DesktopMainPage(
            _launchLeft,
            _launchRight,
            Activated: () =>
            {
                _ = _launchLeft.EnsureInstancesLoadedAsync();
                _launchLeft.TriggerShowAnimation();
                _launchRight.PageOnEnter();
            });
    }

    private void PromptHideCommunityHint()
    {
        ShowInputDialog(
            AvaloniaLocalizationManager.GetText(
                "Launch.Right.CommunityHint.InputTitle",
                "输入 PCL N 开发者名称"),
            AvaloniaLocalizationManager.GetText(
                "Launch.Right.CommunityHint.HidePrompt",
                "若要永久隐藏此提示，请输入正确的 PCL N 开发者名称。"),
            string.Empty,
            "开发者名称",
            answer =>
            {
                if (answer is null)
                    return;

                // WPF: must match developer name (MUXUE1230, case-insensitive).
                if (string.Equals(answer.Trim(), "MUXUE1230", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(answer.Trim(), "muxue1230-owo", StringComparison.OrdinalIgnoreCase))
                {
                    PageLaunchRight.SetCommunityHintPermanentlyHidden(true);
                    if (_launchRight?.FindControl<MyCard>("PanHint") is { } card)
                        card.IsVisible = false;
                    ShowHint("已永久隐藏 N 版提示");
                }
                else
                {
                    ShowTextDialog(
                        AvaloniaLocalizationManager.GetText(
                            "Launch.Right.CommunityHint.Title",
                            "社区提示"),
                        AvaloniaLocalizationManager.GetText(
                            "Launch.Right.CommunityHint.WrongInput",
                            "不太对哦……"));
                }
            });
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        _isMainWindowOpened = true;

        // Headless/automation: skip show animation + first-run dialogs so Window.Show() can finish.
        if (ShouldSuppressStartupDialogs())
        {
            Opacity = _targetWindowOpacity;
            if (_showAnimationRoot is not null)
                _showAnimationRoot.RenderTransform = null;
            return;
        }

        StartShowAnimation();
        // First-run chain: community welcome → special build notice (no EULA gate).
        Dispatcher.UIThread.Post(MaybeShowFirstRunDialogs, DispatcherPriority.Background);
    }

    private void MaybeShowFirstRunDialogs()
    {
        // Headless unit tests / automation never click dialogs; the chain would hang the UI dispatcher.
        if (ShouldSuppressStartupDialogs())
            return;

        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            MaybeShowCommunityWelcome(settings);
        }
        catch (Exception ex)
        {
            DesktopFileLog.Write("[FirstRun] " + ex.Message);
            MaybeShowSpecialVersionNotice();
        }
    }

    /// <summary>
    /// Skip community / special-build modal chains under automated hosts.
    /// <c>PCL_DISABLE_FIRST_RUN</c> or <c>PCL_DISABLE_DEBUG_HINT</c> (any non-empty value).
    /// </summary>
    private static bool ShouldSuppressStartupDialogs() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PCL_DISABLE_FIRST_RUN")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT"));

    private void MaybeShowCommunityWelcome(LauncherSettings settings)
    {
        string currentVersion = PclBuildInfo.DisplayVersion;
        string seen = settings.GetTextOption("UiCommunityNoticeVersion", string.Empty);
        if (string.Equals(seen, currentVersion, StringComparison.Ordinal))
        {
            MaybeShowSpecialVersionNotice();
            return;
        }

        string title = AvaloniaLocalizationManager.GetText("Update.CommunityNotice.Title", "PCL N Edition");
        string body = AvaloniaLocalizationManager.GetText(
            "Update.CommunityNotice.Body",
            "欢迎使用 Plain Craft Launcher N Edition！\n\n" +
            "PCL N 是由 MUXUE1230 维护的个人分支，与官方 / 社区版无隶属关系。\n" +
            "请将问题反馈到本项目的 GitHub Issues。\n\n" +
            "此提示在每次版本更新后显示一次。");
        string confirm = AvaloniaLocalizationManager.GetText("Update.CommunityNotice.Confirm", "开始使用");

        ShowTextDialog(title, body, confirm);
        settings.SetTextOption("UiCommunityNoticeVersion", currentVersion);
        LauncherSettingsPageBinder.SaveSettings(settings);
        MaybeShowSpecialVersionNotice();
    }

    private void MaybeShowSpecialVersionNotice()
    {
        // WPF FormMain special build notice (Debug / CI).
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT")))
            return;

        bool isDebug =
#if DEBUG
            true;
#else
            false;
#endif
        bool isCi =
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) ||
            PclBuildInfo.InformationalVersion.Contains("ci", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetAssemblyConfiguration(), "CI", StringComparison.OrdinalIgnoreCase);

        if (!isDebug && !isCi)
            return;

        string title = AvaloniaLocalizationManager.GetText("Main.SpecialVersion.Title", "特殊版本提示");
        string body = isDebug
            ? AvaloniaLocalizationManager.GetText(
                "Main.SpecialVersion.DebugHint",
                "当前 PCL N Edition 为 Debug 构建。\n该构建仅用于开发调试。")
            : AvaloniaLocalizationManager.GetText(
                "Main.SpecialVersion.CiHint",
                "当前 PCL N Edition 为自动化 CI 构建。\n稳定性较低，不适合日常使用。");
        string hideNotice = AvaloniaLocalizationManager.GetText(
            "Main.SpecialVersion.HideHintNotice",
            "可设置环境变量 PCL_DISABLE_DEBUG_HINT 为任意值以隐藏此提示。");

        ShowConfirmDialog(
            title,
            body + "\n\n" + hideNotice,
            confirmed =>
            {
                if (confirmed)
                    return;

                // Secondary: open download page and exit.
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/MuXue1230-owo/PCL-N/releases/latest",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // ignore
                }

                Close();
            },
            AvaloniaLocalizationManager.GetText("Main.SpecialVersion.IUnderstand", "我知道我在做什么"),
            AvaloniaLocalizationManager.GetText("Main.SpecialVersion.OpenDownloadPageAndExit", "打开最新下载页并退出"),
            isWarn: true);
    }

    private static string GetAssemblyConfiguration()
    {
        try
        {
            return Assembly.GetEntryAssembly()
                       ?.GetCustomAttribute<AssemblyConfigurationAttribute>()
                       ?.Configuration
                   ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private PageLaunchLeft CreateLaunchLeftPage()
    {
        EnsureMinecraftFoldersLoaded();
        PageLaunchLeft page = new();
        page.SetPreferredInstanceDirectory(LoadPreferredInstanceDirectory());
        page.SetMinecraftRootDirectory(_selectedMinecraftRoot);
        page.DownloadRequested += (_, _) => SelectNavRoute(DownloadRoute, animate: true);
        page.InstanceSelectRequested += (_, _) => ApplyInstanceSelectPage();
        page.InstanceSettingsRequested += (_, _) =>
        {
            if (page.SelectedInstance is not null)
                ApplyInstanceManagePage(page.SelectedInstance);
        };
        page.CancelLaunchRequested += (_, _) =>
        {
            _launchCancellation?.Cancel();
            HandleStatusMessage("已取消启动。");
        };
        page.StatusMessage += (_, message) => HandleStatusMessage(message);
        page.LoginPageRequested += (_, type) => ApplyLaunchLoginPage(page, type);
        page.LaunchRequested += (_, instance) => _ = StartMinecraftAsync(page, instance);
        return page;
    }

    private void HandleStatusMessage(string message)
    {
        // WPF: most status strings only go to the launch log; bottom Hint is reserved for short toasts.
        _launchRight?.AppendLog(message);
    }

    /// <summary>
    /// WPF FormMain.Hint — single-line bottom toast (PanHint). No multi-line wrap.
    /// </summary>
    private void ShowHint(string message, bool critical = false)
    {
        if (this.FindControl<StackPanel>("PanHint") is not { } host)
            return;

        string text = TruncateHint(message);
        if (text.Length == 0)
            return;

        Border? duplicate = host.Children.OfType<Border>()
            .FirstOrDefault(border => string.Equals(border.Tag as string, text, StringComparison.Ordinal));
        if (duplicate is not null)
        {
            host.Children.Remove(duplicate);
            host.Children.Add(duplicate);
            return;
        }

        // WPF keeps a very short stack (typically ≤3).
        while (host.Children.Count >= 3)
            host.Children.RemoveAt(0);

        bool alignRight = host.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Right;
        double enterOffset = 28d;
        Thickness startMargin = alignRight
            ? new Thickness(0d, 0d, -enterOffset, 6d)
            : new Thickness(-enterOffset, 0d, 0d, 6d);
        Thickness restMargin = new(0d, 0d, 0d, 6d);

        Border bar = new()
        {
            Tag = text,
            Height = 34d,
            MaxHeight = 34d,
            MaxWidth = 420d,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = startMargin,
            Padding = new Thickness(14d, 0d, 14d, 0d),
            CornerRadius = new CornerRadius(3d),
            Opacity = 0d,
            ClipToBounds = true,
            Background = new SolidColorBrush(
                critical ? Color.Parse("#E0CE2111") : Color.Parse("#E0259BFC")),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 13d,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                MaxLines = 1
            }
        };
        host.Children.Add(bar);

        string aniKey = "FrmMain Hint " + text.GetHashCode(StringComparison.Ordinal);
        List<ModAnimation.AniData> enter =
        [
            ModAnimation.AaOpacity(bar, 1d - bar.Opacity, 150, ease: new ModAnimation.AniEaseOutFluent())
        ];
        if (!alignRight)
            enter.Add(ModAnimation.AaX(bar, enterOffset, 200, ease: new ModAnimation.AniEaseOutFluent()));
        else
            enter.Add(ModAnimation.AaCode(() => bar.Margin = restMargin, 0));
        ModAnimation.AniStart(enter, aniKey + " In");

        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3.8d) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            List<ModAnimation.AniData> exit =
            [
                ModAnimation.AaOpacity(bar, -bar.Opacity, 160, ease: new ModAnimation.AniEaseInFluent())
            ];
            if (!alignRight)
                exit.Add(ModAnimation.AaX(bar, -enterOffset, 160, ease: new ModAnimation.AniEaseInFluent()));
            exit.Add(ModAnimation.AaCode(() => host.Children.Remove(bar), after: true));
            ModAnimation.AniStart(exit, aniKey + " Out");
        };
        timer.Start();
    }

    private static string TruncateHint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        string text = message
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        // WPF Hint is single-line; keep captions short so they don't overflow the bar.
        const int maxChars = 48;
        return text.Length <= maxChars ? text : text[..(maxChars - 1)] + "…";
    }

    private DesktopMainPage CreateDownloadMainPage()
    {
        _downloadLeft ??= CreateDownloadLeftPage();
        MyPageRight rightPage = _downloadLeft.GetOrCreateCurrentPage();
        return new DesktopMainPage(
            _downloadLeft,
            rightPage,
            Activated: () =>
            {
                _downloadLeft.TriggerShowAnimation();
                if (rightPage is PageDownloadInstall installPage)
                {
                    if (!installPage.HasPendingFocusedNavigation)
                        installPage.ClearInstallTargetOverride();
                    installPage.PageOnEnter();
                }
                else
                {
                    rightPage.PageOnEnter();
                }
            });
    }

    private DesktopMainPage CreateCommunityMainPage()
    {
        _communityRight ??= CreateCommunityRightPage();
        _communityLeft ??= CreateCommunityLeftPage(_communityRight);
        return new DesktopMainPage(
            _communityLeft,
            _communityRight,
            Activated: () =>
            {
                _communityLeft.TriggerShowAnimation();
                _communityRight.PageOnEnter();
            });
    }

    private PageCommunityLeft CreateCommunityLeftPage(PageCommunityRight rightPage)
    {
        PageCommunityLeft page = new();
        page.CategoryChanged += (_, category) =>
        {
            ApplyCommunityRightPage(rightPage);
            _ = rightPage.SetCategoryAsync(category);
        };
        page.RefreshRequested += (_, category) =>
        {
            if (rightPage.Category == category)
                _ = rightPage.RefreshAsync();
            else
                _ = rightPage.SetCategoryAsync(category);
        };
        page.FavoritesRequested += (_, _) =>
        {
            _communityFavoritesRight ??= CreateCommunityFavoritesRightPage();
            _communityFavoritesRight.Refresh();
            ApplyCommunityRightPage(_communityFavoritesRight);
        };
        return page;
    }

    private PageCommunityRight CreateCommunityRightPage()
    {
        PageCommunityRight page = new(new CompositeCommunityResourceCatalog(), ownsCatalog: true, _communityFavorites);
        page.OpenProjectRequested += (_, entry) => _ = OpenCommunityDetailAsync(entry, page.Category, page.CurrentSearchOptions);
        page.DownloadRequested += (_, request) => _ = DownloadCommunityResourceAsync(request);
        return page;
    }

    private PageCommunityDetail CreateCommunityDetailPage()
    {
        PageCommunityDetail page = new(new CompositeCommunityResourceCatalog(), ownsCatalog: true, _communityFavorites);
        page.BackRequested += (_, _) => CloseCommunityDetail();
        page.OpenWebRequested += (_, entry) => OpenExternalUrl(entry.WebsiteUrl);
        page.DownloadRequested += (_, request) => _ = DownloadCommunityResourceAsync(request);
        return page;
    }

    private PageCommunityFavoritesRight CreateCommunityFavoritesRightPage()
    {
        PageCommunityFavoritesRight page = new(_communityFavorites);
        page.OpenProjectRequested += (_, favorite) =>
            _ = OpenCommunityDetailAsync(
                favorite.Entry,
                favorite.Category,
                new CommunitySearchOptions(Source: favorite.Entry.Source));
        page.DownloadRequested += (_, request) => _ = DownloadCommunityResourceAsync(request);
        return page;
    }

    private void ApplyCommunityRightPage(MyPageRight target)
    {
        if (this.FindControl<Border>("PanMainRight") is not { } rightHost)
            return;

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (ReferenceEquals(oldRight, target))
            return;

        oldRight?.PageOnExit();
        rightHost.Child = target;
        RefreshBackToTopBinding();
        target.PageOnEnter();
    }

    /// <summary>
    /// Open resource detail as a fully independent page (WPF FormMain PageDownloadCompDetail):
    /// replaces both left + right content, not only the right pane.
    /// </summary>
    private async Task OpenCommunityDetailAsync(
        CommunityResourceEntry entry,
        CommunityResourceCategory category,
        CommunitySearchOptions options)
    {
        _communityDetail ??= CreateCommunityDetailPage();
        _communityRight ??= CreateCommunityRightPage();
        _communityLeft ??= CreateCommunityLeftPage(_communityRight);

        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        // Full-frame detail: clear left rail, host detail in right (full content width).
        if (leftHost.Child is MyPageLeft oldLeft)
            oldLeft.TriggerHideAnimation();
        leftHost.Child = null;

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (!ReferenceEquals(oldRight, _communityDetail))
        {
            oldRight?.PageOnExit();
            rightHost.Child = _communityDetail;
            RefreshBackToTopBinding();
            _communityDetail.PageOnEnter();
        }

        EnterTitleSubPage(entry.Title);
        _titleInnerBackAction = CloseCommunityDetail;
        await _communityDetail.ShowAsync(entry, category, options).ConfigureAwait(true);
    }

    private void CloseCommunityDetail()
    {
        _titleInnerBackAction = null;
        _communityRight ??= CreateCommunityRightPage();
        _communityLeft ??= CreateCommunityLeftPage(_communityRight);

        if (this.FindControl<Border>("PanMainLeft") is { } leftHost)
        {
            if (!ReferenceEquals(leftHost.Child, _communityLeft))
            {
                leftHost.Child = _communityLeft;
                _communityLeft.TriggerShowAnimation();
            }
        }

        if (this.FindControl<Border>("PanMainRight") is { } rightHost)
        {
            MyPageRight? oldRight = rightHost.Child as MyPageRight;
            MyPageRight target = _communityLeft.IsFavoritesSelected
                ? _communityFavoritesRight ??= CreateCommunityFavoritesRightPage()
                : _communityRight;
            if (!ReferenceEquals(oldRight, target))
            {
                oldRight?.PageOnExit();
                rightHost.Child = target;
                RefreshBackToTopBinding();
                target.PageOnEnter();
            }
        }

        ExitTitleSubPage();
    }

    private async Task DownloadCommunityResourceAsync(CommunityResourceDownloadRequest request)
    {
        LaunchInstanceInfo? instance = _launchLeft?.SelectedInstance ?? _managedInstance;
        string taskId = CreateTaskId("community", request.Entry.ProjectId);
        using CancellationTokenSource cancellation = RegisterTrackedTask(taskId);
        string taskTitle = "下载 " + request.Entry.Title;

        // WPF: stay on community list (or return from detail) — do not jump to task manager.
        if (this.FindControl<Border>("PanMainRight")?.Child is PageCommunityDetail)
            CloseCommunityDetail();

        TrackTaskBegin(taskId, taskTitle, "解析下载地址");
        ShowHint("已开始下载：" + request.Entry.Title);

        try
        {
            CommunitySearchOptions downloadOptions = instance is null
                ? request.Options
                : CommunityInstanceCompatibility.Apply(request.Options, request.Category, instance);
            using CompositeCommunityResourceCatalog catalog = new();
            CommunityResourceVersion? selectedVersion = request.PreferredVersion;
            CommunityResourceDownloadFile? file = request.PreferredFile;
            if (selectedVersion is null)
            {
                IReadOnlyList<CommunityResourceVersion> versions = await catalog.GetVersionsAsync(
                        request.Entry,
                        downloadOptions,
                        cancellation.Token)
                    .ConfigureAwait(true);
                selectedVersion = file is null
                    ? versions.OrderByDescending(static version => version.PublishedAt ?? DateTimeOffset.MinValue)
                        .FirstOrDefault()
                    : versions.FirstOrDefault(version =>
                        string.Equals(version.VersionId, file.VersionId, StringComparison.OrdinalIgnoreCase));
            }

            file ??= selectedVersion is { Files.Count: > 0 } ? selectedVersion.Files[0] : null;

            if (file is null)
            {
                TrackTaskFailed(taskId, taskTitle, "未找到匹配当前筛选条件的版本文件。", canceled: false);
                ShowHint("下载失败：未找到可下载的文件", critical: true);
                return;
            }

            selectedVersion ??= new CommunityResourceVersion(
                file.VersionId,
                file.VersionName,
                file.VersionName,
                null,
                null,
                [],
                [],
                [file]);

            string baseDirectory = instance is null
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "PCL-N Downloads")
                : await InstanceGameDirectory.ResolveAsync(instance, cancellation.Token).ConfigureAwait(true);

            IReadOnlyList<CommunityResourceDownloadPlanItem> plan;
            if (request.Category == CommunityResourceCategory.Mod)
            {
                TrackTaskBegin(taskId, taskTitle, "正在解析必需前置");
                plan = await CommunityResourceDependencyResolver.ResolveRequiredDownloadsAsync(
                        catalog,
                        request.Entry,
                        selectedVersion,
                        file,
                        downloadOptions,
                        cancellation.Token)
                    .ConfigureAwait(true);
            }
            else
            {
                plan = [new CommunityResourceDownloadPlanItem(request.Entry, selectedVersion, file, false)];
            }

            using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
            string completedPath = string.Empty;
            int dependencyCount = plan.Count(static item => item.IsDependency);
            if (dependencyCount > 0)
            {
                _launchRight?.AppendLog(
                    $"社区资源：{request.Entry.Title} 需要 {dependencyCount} 个必需前置，将自动下载。");
            }

            foreach (CommunityResourceDownloadPlanItem item in plan)
            {
                CommunityResourceCategory itemCategory = item.IsDependency
                    ? CommunityResourceCategory.Mod
                    : request.Category;
                string path = await DownloadCommunityPlanItemAsync(
                        client,
                        item,
                        itemCategory,
                        baseDirectory,
                        taskId,
                        taskTitle,
                        cancellation.Token)
                    .ConfigureAwait(true);
                if (item.IsDependency)
                    _launchRight?.AppendLog($"已安装前置：{item.Entry.Title} → {path}");
                else
                    completedPath = path;
            }

            TrackTaskFinished(taskId, taskTitle, "已保存到 " + completedPath);
            _launchRight?.AppendLog($"社区资源已下载：{request.Entry.Title} → {completedPath}");
            ShowHint(request.Category == CommunityResourceCategory.World
                ? "世界安装完成：" + Path.GetFileName(completedPath)
                : "下载完成：" + Path.GetFileName(completedPath));
        }
        catch (OperationCanceledException)
        {
            TrackTaskFailed(taskId, taskTitle, "下载已取消。", canceled: true);
            ShowHint("下载已取消");
        }
        catch (Exception ex)
        {
            TrackTaskFailed(taskId, taskTitle, ex.Message, canceled: false);
            ShowHint("下载失败：" + TruncateHint(ex.Message), critical: true);
        }
        finally
        {
            UnregisterTrackedTask(taskId, cancellation);
        }
    }

    private async Task<string> DownloadCommunityPlanItemAsync(
        HttpClient client,
        CommunityResourceDownloadPlanItem item,
        CommunityResourceCategory category,
        string baseDirectory,
        string taskId,
        string taskTitle,
        CancellationToken cancellationToken)
    {
        string targetDirectory = ResolveCommunityDownloadDirectory(category, baseDirectory);
        Directory.CreateDirectory(targetDirectory);
        string targetPath = Path.Combine(targetDirectory, SanitizeFileName(item.File.FileName));
        string temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".PCLDownloading";
        string phase = item.IsDependency
            ? "正在下载前置 " + item.Entry.Title
            : "正在下载 " + item.File.FileName;
        TrackTaskBegin(taskId, taskTitle, phase);

        try
        {
            using HttpResponseMessage response = await client.GetAsync(
                    item.File.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(true);
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            await using Stream network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(true);
            await using (FileStream output = new(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             useAsync: true))
            {
                byte[] buffer = new byte[64 * 1024];
                long written = 0;
                int read;
                while ((read = await network.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                           .ConfigureAwait(true)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(true);
                    written += read;
                    double progress = total is > 0 ? written / (double)total.Value : 0d;
                    TrackTaskProgress(
                        taskId,
                        taskTitle,
                        Math.Clamp(progress, 0d, 1d),
                        $"{written.ToString(CultureInfo.InvariantCulture)} / {(total?.ToString(CultureInfo.InvariantCulture) ?? "?")} 字节");
                }
            }

            if (category == CommunityResourceCategory.Mod)
            {
                targetPath = MinecraftModArchiveInstaller.Install(
                    temporaryPath,
                    targetDirectory,
                    Path.GetFileName(targetPath));
            }
            else
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
            }

            if (category != CommunityResourceCategory.World)
                return targetPath;

            TrackTaskBegin(taskId, taskTitle, "正在安装世界");
            string installed = await MinecraftWorldArchiveInstaller
                .InstallAsync(targetPath, targetDirectory, cancellationToken)
                .ConfigureAwait(true);
            File.Delete(targetPath);
            return installed;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A failed cleanup must not mask the original download result.
                }
            }
        }
    }

    private static string ResolveCommunityDownloadDirectory(
        CommunityResourceCategory category,
        string baseDirectory)
    {
        return category switch
        {
            CommunityResourceCategory.Mod => Path.Combine(baseDirectory, "mods"),
            CommunityResourceCategory.ResourcePack => Path.Combine(baseDirectory, "resourcepacks"),
            CommunityResourceCategory.Shader => Path.Combine(baseDirectory, "shaderpacks"),
            CommunityResourceCategory.DataPack => Path.Combine(baseDirectory, "datapacks"),
            CommunityResourceCategory.Modpack => Path.Combine(baseDirectory, "modpacks"),
            CommunityResourceCategory.World => Path.Combine(baseDirectory, "saves"),
            _ => baseDirectory
        };
    }

    private PageDownloadLeft CreateDownloadLeftPage()
    {
        PageDownloadLeft page = new(CreateDownloadInstallPage);
        page.PageChanged += (_, args) => ApplyDownloadRightPage(args.Page);
        return page;
    }

    private PageDownloadInstall CreateDownloadInstallPage()
    {
        if (_downloadInstallPage is not null)
            return _downloadInstallPage;

        PageDownloadInstall page = new(_minecraftInstallService);
        page.InstallRequested += (_, request) => _ = StartInstallAsync(request);
        _downloadInstallPage = page;
        return _downloadInstallPage;
    }

    private PageSpeedRight CreateTaskManagerRightPage()
    {
        if (_speedRight is not null)
            return _speedRight;

        PageSpeedRight page = new();
        page.CancelRequested += (_, args) => CancelTrackedTask(args.TaskId);
        page.DismissRequested += (_, args) => RemoveTask(args.TaskId);
        _speedRight = page;
        return _speedRight;
    }

    private void ApplyDownloadRightPage(MyPageRight target)
    {
        if (this.FindControl<Border>("PanMainRight") is not { } rightHost)
            return;

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (ReferenceEquals(oldRight, target))
            return;

        oldRight?.PageOnExit();
        rightHost.Child = target;
        RefreshBackToTopBinding();
        target.PageOnEnter();
    }

    private void ApplyInstanceSelectPage()
    {
        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        EnsureMinecraftFoldersLoaded();
        _instanceSelectLeft ??= CreateInstanceSelectLeftPage();
        _instanceSelectLeft.SetFolders(_minecraftFolders, _selectedMinecraftRoot);
        leftHost.Child = _instanceSelectLeft;
        _instanceSelectLeft.TriggerShowAnimation();
        _instanceSelectPage ??= CreateInstanceSelectPage();
        _instanceSelectPage.SetInstances(_launchLeft?.Instances ?? [], _launchLeft?.SelectedInstance);
        rightHost.Child = _instanceSelectPage;
        EnterTitleSubPage("选择版本");
        RefreshBackToTopBinding();
        _instanceSelectPage.PageOnEnter();
    }

    private PageInstanceSelectLeft CreateInstanceSelectLeftPage()
    {
        PageInstanceSelectLeft page = new();
        page.FolderSelected += (_, folder) => _ = SelectMinecraftFolderAsync(folder);
        page.FolderRefreshRequested += (_, folder) => _ = SelectMinecraftFolderAsync(folder, forceRefresh: true);
        page.FolderOpenRequested += (_, folder) => OpenFolder(folder.RootDirectory);
        page.FolderRenameRequested += (_, folder) => ShowInputDialog(
            "重命名游戏文件夹",
            "请输入新的显示名称。",
            folder.Name,
            "游戏文件夹名称",
            result => RenameMinecraftFolder(folder, result));
        page.FolderRemoveRequested += (_, folder) => RemoveMinecraftFolder(folder);
        page.CreateFolderRequested += (_, _) => _ = CreateDefaultMinecraftFolderAsync();
        page.AddFolderRequested += (_, _) => _ = AddMinecraftFolderAsync();
        page.ImportModpackRequested += (_, _) => _ = PickModpackForImportAsync();
        return page;
    }

    private PageInstanceSelectRight CreateInstanceSelectPage()
    {
        PageInstanceSelectRight page = new();
        page.RefreshRequested += async (_, _) =>
        {
            if (_launchLeft is null)
                return;
            await _launchLeft.RefreshInstancesAsync().ConfigureAwait(true);
            page.SetInstances(_launchLeft.Instances, _launchLeft.SelectedInstance);
        };
        page.DownloadRequested += (_, _) => SelectNavRoute(DownloadRoute, animate: true);
        page.InstanceOpenFolderRequested += (_, instance) => OpenFolder(instance.InstanceDirectory);
        page.InstanceDeleteRequested += (_, instance) => PromptDeleteInstance(instance);
        page.InstanceSelected += (_, instance) =>
        {
            _launchLeft?.SetInstances(_launchLeft.Instances, instance);
            PersistPreferredInstanceDirectory(_launchLeft?.PreferredInstanceDirectory ?? instance.InstanceDirectory);
            _launchRight?.AppendLog($"已选择游戏版本 {instance.Name}。");
            SelectNavRoute(LaunchRoute, animate: true);
        };
        page.InstanceManageRequested += (_, instance) => ApplyInstanceManagePage(instance);
        return page;
    }

    private static string? LoadPreferredInstanceDirectory()
    {
        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            string directory = settings.GetTextOption(LauncherSettingKeys.LaunchSelectedInstanceDirectory);
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private void PersistPreferredInstanceDirectory(string? instanceDirectory)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory))
            return;

        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            settings.SetTextOption(LauncherSettingKeys.LaunchSelectedInstanceDirectory, instanceDirectory);
            LauncherSettingsPageBinder.SaveSettings(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _launchRight?.AppendLog("未能保存所选游戏版本：" + ex.Message);
        }
    }

    private void EnsureMinecraftFoldersLoaded()
    {
        if (_minecraftFoldersLoaded)
            return;

        _minecraftFoldersLoaded = true;
        _minecraftFolders.Clear();
        foreach (string root in LaunchInstanceDiscovery.GetCandidateRoots())
        {
            string? normalized = NormalizeDirectoryPath(root);
            if (normalized is null || _minecraftFolders.Any(folder =>
                    string.Equals(folder.RootDirectory, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _minecraftFolders.Add(new MinecraftFolderInfo(GetAutomaticMinecraftFolderName(normalized), normalized));
        }

        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            string serializedFolders = settings.GetTextOption(LauncherSettingKeys.LaunchMinecraftFolders);
            if (!string.IsNullOrWhiteSpace(serializedFolders))
            {
                MinecraftFolderSetting[] customFolders = ParseMinecraftFolderSettings(serializedFolders);
                foreach (MinecraftFolderSetting custom in customFolders)
                {
                    string? normalized = NormalizeDirectoryPath(custom.RootDirectory);
                    if (normalized is null || _minecraftFolders.Any(folder =>
                            string.Equals(folder.RootDirectory, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string name = string.IsNullOrWhiteSpace(custom.Name)
                        ? GetAutomaticMinecraftFolderName(normalized)
                        : custom.Name.Trim();
                    _minecraftFolders.Add(new MinecraftFolderInfo(name, normalized, IsCustom: true));
                }
            }

            _selectedMinecraftRoot = NormalizeDirectoryPath(
                settings.GetTextOption(LauncherSettingKeys.LaunchSelectedMinecraftRoot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            _selectedMinecraftRoot = null;
        }

        if (_minecraftFolders.Count == 0)
        {
            string fallback = Path.Combine(AppContext.BaseDirectory, ".minecraft");
            _minecraftFolders.Add(new MinecraftFolderInfo("当前文件夹", NormalizeDirectoryPath(fallback) ?? fallback));
        }

        if (_selectedMinecraftRoot is null || !_minecraftFolders.Any(folder =>
                string.Equals(folder.RootDirectory, _selectedMinecraftRoot, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedMinecraftRoot = TryGetMinecraftRootFromInstanceDirectory(LoadPreferredInstanceDirectory())
                                     ?? _minecraftFolders[0].RootDirectory;
            if (!_minecraftFolders.Any(folder =>
                    string.Equals(folder.RootDirectory, _selectedMinecraftRoot, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedMinecraftRoot = _minecraftFolders[0].RootDirectory;
            }
        }
    }

    private async Task SelectMinecraftFolderAsync(MinecraftFolderInfo folder, bool forceRefresh = false)
    {
        string? normalized = NormalizeDirectoryPath(folder.RootDirectory);
        if (normalized is null || _launchLeft is null)
            return;

        bool changed = !string.Equals(_selectedMinecraftRoot, normalized, StringComparison.OrdinalIgnoreCase);
        _selectedMinecraftRoot = normalized;
        PersistMinecraftFolders();
        _instanceSelectLeft?.SetFolders(_minecraftFolders, _selectedMinecraftRoot);
        _launchLeft.SetMinecraftRootDirectory(normalized);
        if (changed || forceRefresh)
            await _launchLeft.RefreshInstancesAsync().ConfigureAwait(true);

        _instanceSelectPage?.SetInstances(_launchLeft.Instances, _launchLeft.SelectedInstance);
    }

    private async Task AddMinecraftFolderAsync()
    {
        string? selected = await PickOpenFolderPathAsync("选择 Minecraft 文件夹").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        string root = NormalizeSelectedMinecraftRoot(selected);
        MinecraftFolderInfo folder = AddOrGetMinecraftFolder(root, GetAutomaticMinecraftFolderName(root));
        await SelectMinecraftFolderAsync(folder, forceRefresh: true).ConfigureAwait(true);
    }

    private async Task CreateDefaultMinecraftFolderAsync()
    {
        string root = Path.Combine(AppContext.BaseDirectory, ".minecraft");
        Directory.CreateDirectory(Path.Combine(root, "versions"));
        MinecraftFolderInfo folder = AddOrGetMinecraftFolder(root, "当前文件夹");
        await SelectMinecraftFolderAsync(folder, forceRefresh: true).ConfigureAwait(true);
    }

    private async Task PickModpackForImportAsync()
    {
        string? sourcePath = await PickOpenFilePathAsync(
                "选择整合包",
                new FilePickerFileType("整合包压缩文件") { Patterns = ["*.zip", "*.mrpack"] })
            .ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(sourcePath))
            _launchRight?.AppendLog("已选择整合包：" + sourcePath);
    }

    private MinecraftFolderInfo AddOrGetMinecraftFolder(string rootDirectory, string name)
    {
        string normalized = NormalizeDirectoryPath(rootDirectory)
                            ?? throw new ArgumentException("Minecraft 文件夹路径无效。", nameof(rootDirectory));
        MinecraftFolderInfo? existing = _minecraftFolders.FirstOrDefault(folder =>
            string.Equals(folder.RootDirectory, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        MinecraftFolderInfo added = new(name, normalized, IsCustom: true);
        _minecraftFolders.Add(added);
        PersistMinecraftFolders();
        _instanceSelectLeft?.SetFolders(_minecraftFolders, _selectedMinecraftRoot);
        return added;
    }

    private void RenameMinecraftFolder(MinecraftFolderInfo folder, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        int index = _minecraftFolders.IndexOf(folder);
        if (index < 0)
            return;

        _minecraftFolders[index] = folder with { Name = name.Trim(), IsCustom = true };
        PersistMinecraftFolders();
        _instanceSelectLeft?.SetFolders(_minecraftFolders, _selectedMinecraftRoot);
    }

    private void RemoveMinecraftFolder(MinecraftFolderInfo folder)
    {
        if (!folder.IsCustom || !_minecraftFolders.Remove(folder))
            return;

        if (_minecraftFolders.Count == 0)
        {
            string fallback = NormalizeDirectoryPath(Path.Combine(AppContext.BaseDirectory, ".minecraft"))
                              ?? Path.Combine(AppContext.BaseDirectory, ".minecraft");
            _minecraftFolders.Add(new MinecraftFolderInfo("当前文件夹", fallback));
        }

        if (string.Equals(_selectedMinecraftRoot, folder.RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _selectedMinecraftRoot = _minecraftFolders[0].RootDirectory;
            _ = SelectMinecraftFolderAsync(_minecraftFolders[0], forceRefresh: true);
        }
        else
        {
            PersistMinecraftFolders();
            _instanceSelectLeft?.SetFolders(_minecraftFolders, _selectedMinecraftRoot);
        }
    }

    private void PersistMinecraftFolders()
    {
        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            MinecraftFolderSetting[] customFolders = _minecraftFolders
                .Where(static folder => folder.IsCustom)
                .Select(static folder => new MinecraftFolderSetting(folder.Name, folder.RootDirectory))
                .ToArray();
            settings.SetTextOption(
                LauncherSettingKeys.LaunchMinecraftFolders,
                SerializeMinecraftFolderSettings(customFolders));
            settings.SetTextOption(
                LauncherSettingKeys.LaunchSelectedMinecraftRoot,
                _selectedMinecraftRoot ?? string.Empty);
            LauncherSettingsPageBinder.SaveSettings(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _launchRight?.AppendLog("未能保存游戏文件夹列表：" + ex.Message);
        }
    }

    private static MinecraftFolderSetting[] ParseMinecraftFolderSettings(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        List<MinecraftFolderSetting> result = [];
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            string? name = TryReadJsonString(element, "name") ?? TryReadJsonString(element, "Name");
            string? rootDirectory = TryReadJsonString(element, "rootDirectory") ??
                                    TryReadJsonString(element, "RootDirectory");
            if (!string.IsNullOrWhiteSpace(rootDirectory))
                result.Add(new MinecraftFolderSetting(name ?? string.Empty, rootDirectory));
        }

        return result.ToArray();
    }

    private static string SerializeMinecraftFolderSettings(IEnumerable<MinecraftFolderSetting> folders)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartArray();
            foreach (MinecraftFolderSetting folder in folders)
            {
                writer.WriteStartObject();
                writer.WriteString("name", folder.Name);
                writer.WriteString("rootDirectory", folder.RootDirectory);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizeSelectedMinecraftRoot(string selectedDirectory)
    {
        string root = NormalizeDirectoryPath(selectedDirectory) ?? selectedDirectory;
        string nestedMinecraft = Path.Combine(root, ".minecraft");
        return Directory.Exists(Path.Combine(nestedMinecraft, "versions")) &&
               !Directory.Exists(Path.Combine(root, "versions"))
            ? nestedMinecraft
            : root;
    }

    private static string GetAutomaticMinecraftFolderName(string rootDirectory)
    {
        string normalizedBase = NormalizeDirectoryPath(Path.Combine(AppContext.BaseDirectory, ".minecraft")) ?? string.Empty;
        if (string.Equals(rootDirectory, normalizedBase, StringComparison.OrdinalIgnoreCase))
            return "当前文件夹";

        string trimmed = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(trimmed);
        if (string.Equals(leaf, ".minecraft", StringComparison.OrdinalIgnoreCase))
        {
            string? parent = Path.GetDirectoryName(trimmed);
            string parentName = string.IsNullOrWhiteSpace(parent) ? string.Empty : Path.GetFileName(parent);
            return string.IsNullOrWhiteSpace(parentName) ? "Minecraft" : parentName;
        }

        return string.IsNullOrWhiteSpace(leaf) ? rootDirectory : leaf;
    }

    private static string? TryGetMinecraftRootFromInstanceDirectory(string? instanceDirectory)
    {
        string? normalized = NormalizeDirectoryPath(instanceDirectory);
        if (normalized is null)
            return null;

        DirectoryInfo? versions = Directory.GetParent(normalized);
        return versions?.Parent?.FullName is { } root ? NormalizeDirectoryPath(root) : null;
    }

    private static string? NormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private void ApplyInstanceManagePage(LaunchInstanceInfo instance, InstancePageSubType subPage = InstancePageSubType.Overall)
    {
        _titleInnerBackAction = null;
        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        _managedInstance = instance;
        _instanceLeft ??= CreateInstanceLeftPage();
        _instanceLeft.SetInstance(instance);
        subPage = _instanceLeft.NormalizePage(subPage);
        if (!ReferenceEquals(leftHost.Child, _instanceLeft))
        {
            if (leftHost.Child is MyPageLeft oldLeft)
                oldLeft.TriggerHideAnimation();
            leftHost.Child = _instanceLeft;
            _instanceLeft.TriggerShowAnimation();
        }
        _instanceLeft.SelectPage(subPage);
        EnterTitleSubPage($"版本设置 - {instance.Name}");

        MyPageRight rightPage = GetInstanceRightPage(instance, subPage);
        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (ReferenceEquals(oldRight, rightPage))
            return;

        oldRight?.PageOnExit();
        rightHost.Child = rightPage;
        RefreshBackToTopBinding();
        rightPage.PageOnEnter();
    }

    private PageInstanceLeft CreateInstanceLeftPage()
    {
        PageInstanceLeft page = new();
        page.PageChanged += (_, subPage) =>
        {
            if (_managedInstance is not null)
                ApplyInstanceManagePage(_managedInstance, subPage);
        };
        page.RefreshRequested += (_, subPage) =>
        {
            if (subPage == InstancePageSubType.Overall)
                _ = RefreshInstancesAfterManagementAsync(_managedInstance?.InstanceDirectory);
            else if (subPage == InstancePageSubType.Servers)
                _instanceServerPage?.Reload();
            else if (subPage == InstancePageSubType.Export)
                _instanceExportPage?.RefreshAll();
            else if (subPage == InstancePageSubType.Install)
                _instanceInstallPage?.RefreshAll();
            else if (subPage == InstancePageSubType.Saves)
                _instanceSavesPage?.Reload();
            else if (subPage == InstancePageSubType.Screenshots)
                _ = _instanceScreenshotPage?.Reload();
            else if (subPage is InstancePageSubType.Mods or InstancePageSubType.ResourcePacks or InstancePageSubType.Shaders or InstancePageSubType.Schematics)
                _instanceResourcePage?.Reload();
            else
                _instanceToolsPage?.Reload();
        };
        page.ResetRequested += (_, _) =>
        {
            if (_managedInstance is not null)
                PromptResetInstanceSettings(_managedInstance);
        };
        return page;
    }

    private MyPageRight GetInstanceRightPage(LaunchInstanceInfo instance, InstancePageSubType subPage)
    {
        if (subPage == InstancePageSubType.Overall)
        {
            _instanceManagePage ??= CreateInstanceManagePage();
            _instanceManagePage.SetInstance(instance);
            return _instanceManagePage;
        }

        if (subPage == InstancePageSubType.Servers)
        {
            _instanceServerPage ??= CreateInstanceServerPage();
            _instanceServerPage.SetInstance(instance);
            return _instanceServerPage;
        }

        if (subPage == InstancePageSubType.Setup)
        {
            _instanceSetupPage ??= CreateInstanceSetupPage();
            _instanceSetupPage.SetInstance(instance);
            return _instanceSetupPage;
        }

        if (subPage == InstancePageSubType.Export)
        {
            _instanceExportPage ??= CreateInstanceExportPage();
            _instanceExportPage.SetInstance(instance);
            return _instanceExportPage;
        }

        if (subPage == InstancePageSubType.Install)
        {
            _instanceInstallPage ??= CreateInstanceInstallPage();
            _instanceInstallPage.SetInstance(instance);
            return _instanceInstallPage;
        }

        if (subPage == InstancePageSubType.Screenshots)
        {
            _instanceScreenshotPage ??= CreateInstanceScreenshotPage();
            _instanceScreenshotPage.SetInstance(instance);
            return _instanceScreenshotPage;
        }

        if (subPage == InstancePageSubType.Saves)
        {
            _instanceSavesPage ??= CreateInstanceSavesPage();
            _instanceSavesPage.SetInstance(instance);
            return _instanceSavesPage;
        }

        if (subPage == InstancePageSubType.ModsDisabled)
        {
            _instanceModDisabledPage ??= CreateInstanceModDisabledPage();
            return _instanceModDisabledPage;
        }

        if (subPage is InstancePageSubType.Mods or InstancePageSubType.ResourcePacks or InstancePageSubType.Shaders or InstancePageSubType.Schematics)
        {
            _instanceResourcePage ??= CreateInstanceResourcePage();
            _instanceResourcePage.SetContext(instance, subPage);
            return _instanceResourcePage;
        }

        _instanceToolsPage ??= CreateInstanceToolsPage();
        _instanceToolsPage.SetContext(instance, subPage);
        return _instanceToolsPage;
    }

    private PageInstanceManageRight CreateInstanceManagePage()
    {
        PageInstanceManageRight page = new();
        page.OpenFolderRequested += (_, instance) => OpenFolder(instance.InstanceDirectory);
        page.OpenPathRequested += (_, path) => OpenFolder(path);
        page.RenameRequested += (_, instance) => PromptRenameInstance(instance);
        page.DeleteRequested += (_, instance) => PromptDeleteInstance(instance);
        page.EditDescriptionRequested += (_, instance) => PromptEditInstanceDescription(instance);
        page.ToggleStarRequested += (_, instance) => _ = ToggleInstanceStarAsync(instance);
        page.ExportLaunchScriptRequested += (_, instance) => _ = ExportLaunchScriptAsync(instance);
        page.TestLaunchRequested += (_, instance) => _ = TestLaunchFromInstancePageAsync(instance);
        page.RepairFilesRequested += (_, instance) => _ = RepairInstanceFilesAsync(instance);
        page.ResetSettingsRequested += (_, instance) => PromptResetInstanceSettings(instance);
        page.PatchCoreRequested += (_, instance) => _ = PatchInstanceCoreAsync(instance);
        return page;
    }

    private PageInstanceSetupRight CreateInstanceSetupPage()
    {
        PageInstanceSetupRight page = new();
        page.OpenGlobalSettingsRequested += (_, _) => SelectNavRoute(SettingsRoute, animate: true);
        page.MessageRequested += (_, args) => ShowTextDialog(args.Title, args.Message, args.PrimaryButton);
        page.ConfirmRequested += (_, args) => ShowConfirmDialog(
            args.Title,
            args.Message,
            args.Complete,
            args.PrimaryButton,
            args.SecondaryButton,
            args.IsWarn);
        page.CreateAuthProfileRequested += (_, authServer) =>
        {
            SelectNavRoute(LaunchRoute, animate: true);
            _launchLeft ??= CreateLaunchLeftPage();
            ApplyLaunchLoginPage(_launchLeft, PageLaunchLeft.LaunchLoginPageType.Auth);
            _loginAuthPage?.SetServer(authServer);
        };
        return page;
    }

    private PageInstanceExportRight CreateInstanceExportPage()
    {
        PageInstanceExportRight page = new();
        page.ExportRequested += (_, request) => _ = ExportInstanceZipAsync(request);
        page.ImportConfigRequested += (_, _) => _ = ImportInstanceRulesConfigAsync(page);
        page.ExportConfigRequested += (_, rules) => _ = ExportInstanceRulesConfigAsync(rules);
        return page;
    }

    private PageInstanceInstallRight CreateInstanceInstallPage()
    {
        PageInstanceInstallRight page = new();
        page.ModifyRequested += (_, request) => _ = OpenDownloadInstallForInstanceAsync(request);
        return page;
    }

    private async Task OpenDownloadInstallForInstanceAsync(InstanceInstallModifyRequest request)
    {
        LaunchInstanceInfo instance = request.Instance;
        string versionId = string.IsNullOrWhiteSpace(request.MinecraftVersionId)
            ? ReadMinecraftVersionId(instance)
            : request.MinecraftVersionId;
        string minecraftRoot = GetMinecraftRootFromInstance(instance);
        PageDownloadInstall installPage = ActivateDownloadInstallPage(animate: true);
        if (request.AddonKind is { } addonKind &&
            request.CurrentLoaderKind is { } currentLoaderKind &&
            !string.IsNullOrWhiteSpace(request.CurrentLoaderVersion))
        {
            await installPage.FocusExistingInstallAddonAsync(
                    versionId,
                    instance.Name,
                    minecraftRoot,
                    currentLoaderKind,
                    request.CurrentLoaderVersion,
                    addonKind,
                    request.CurrentOptiFineVersion)
                .ConfigureAwait(true);
            return;
        }

        await installPage.FocusVersionAsync(
                versionId,
                instance.Name,
                preserveInstallNameOnLoaderSelect: true,
                minecraftRootDirectory: minecraftRoot,
                openLoaderKind: request.LoaderKind,
                replaceExistingVersion: true)
            .ConfigureAwait(true);
    }

    private PageDownloadInstall ActivateDownloadInstallPage(bool animate)
    {
        _downloadLeft ??= CreateDownloadLeftPage();
        PageDownloadInstall installPage = CreateDownloadInstallPage();
        _downloadLeft.PageChange(DownloadPageSubType.Install);
        SelectNavRoute(DownloadRoute, animate);
        return installPage;
    }

    private PageSpeedRight ActivateTaskManagerPage(bool animate)
    {
        PageSpeedRight rightPage = CreateTaskManagerRightPage();
        ApplyTaskManagerPage(animate);
        return rightPage;
    }

    private void ApplyTaskManagerPage(bool animate)
    {
        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        if (!_isTaskManagerVisible)
        {
            _taskManagerBackRoute = GetCurrentNavigationRoute();
            _taskManagerBackAction = CaptureTaskManagerBackAction();
        }

        _registeredPageRequestId++;
        _isTaskManagerVisible = true;
        _titleInnerBackAction = ReturnFromTaskManager;

        _speedLeft ??= new PageSpeedLeft();
        PageSpeedRight rightPage = CreateTaskManagerRightPage();
        UpdateTaskManagerViews();

        if (!ReferenceEquals(leftHost.Child, _speedLeft))
        {
            if (leftHost.Child is MyPageLeft oldLeft)
                oldLeft.TriggerHideAnimation();
            leftHost.Child = _speedLeft;
        }

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (!ReferenceEquals(oldRight, rightPage))
        {
            oldRight?.PageOnExit();
            if (animate && _isMainWindowOpened)
            {
                ModAnimation.AniStart(
                    new List<ModAnimation.AniData>
                    {
                        ModAnimation.AaOpacity(rightHost, -rightHost.Opacity, 110),
                        ModAnimation.AaCode(() =>
                        {
                            rightHost.Child = rightPage;
                            rightHost.Opacity = 0d;
                            RefreshBackToTopBinding();
                        }, after: true),
                        ModAnimation.AaOpacity(rightHost, 1d, 170),
                        ModAnimation.AaCode(rightPage.PageOnEnter, after: true)
                    },
                    "FrmMain PageChangeRight");
            }
            else
            {
                rightHost.Child = rightPage;
                rightHost.Opacity = 1d;
                RefreshBackToTopBinding();
                rightPage.PageOnEnter();
            }
        }
        else
        {
            rightHost.Opacity = 1d;
            RefreshBackToTopBinding();
            rightPage.PageOnEnter();
        }

        EnterTitleSubPage(GetResourceText("Main.Title.TaskManager", "任务管理"));
        _speedLeft.TriggerShowAnimation();
        RefreshTaskManagerButton();
    }

    private NavigationRouteId GetCurrentNavigationRoute() =>
        _currentNavRoute is NavigationRouteId route && FindNavigationPage(route) is not null
            ? route
            : _navigationPages.Length > 0
                ? _navigationPages[0].Route
                : LaunchRoute;

    private Action CaptureTaskManagerBackAction()
    {
        if (this.FindControl<Border>("PanMainRight")?.Child is PageInstanceSelectRight)
            return ApplyInstanceSelectPage;

        if (_managedInstance is not null &&
            this.FindControl<Border>("PanMainLeft")?.Child is PageInstanceLeft &&
            _instanceLeft is not null)
        {
            LaunchInstanceInfo instance = _managedInstance;
            InstancePageSubType subPage = _instanceLeft.PageId;
            return () => ApplyInstanceManagePage(instance, subPage);
        }

        NavigationRouteId route = _taskManagerBackRoute ?? LaunchRoute;
        return () => SelectNavRoute(route, animate: true);
    }

    private void ReturnFromTaskManager()
    {
        Action backAction = _taskManagerBackAction ??
                            (() => SelectNavRoute(_taskManagerBackRoute ?? LaunchRoute, animate: true));
        _taskManagerBackAction = null;
        _isTaskManagerVisible = false;
        backAction();
    }

    private string GetResourceText(string key, string fallback)
    {
        if (TryGetResource(key, null, out object? resource) && resource is string text)
            return text;

        return Avalonia.Application.Current?.TryGetResource(key, null, out resource) == true && resource is string appText
            ? appText
            : fallback;
    }

    private void TrackTaskBegin(string taskId, string title, string stage)
    {
        _taskSnapshots[taskId] = new TaskManagerEntrySnapshot(
            taskId,
            title,
            stage,
            string.Empty,
            0d,
            0,
            0,
            0,
            TaskManagerTaskState.Waiting);
        UpdateTaskManagerViews();
        NotifyTaskManagerButton(ribble: true);
    }

    private void TrackTaskProgress(string taskId, string title, double progress, string detail)
    {
        TaskManagerEntrySnapshot previous = GetTaskSnapshotOrDefault(taskId, title);
        _taskSnapshots[taskId] = previous with
        {
            Title = title,
            Stage = previous.Stage,
            Detail = detail,
            Progress = Math.Clamp(progress, 0d, 1d),
            State = TaskManagerTaskState.Running,
            ErrorMessage = null
        };
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();
    }

    private void TrackInstallProgress(string taskId, string title, MinecraftInstallProgress progress)
    {
        string stage = string.IsNullOrWhiteSpace(progress.Stage) ? "正在处理下载任务" : progress.Stage;
        _taskSnapshots[taskId] = new TaskManagerEntrySnapshot(
            taskId,
            title,
            stage,
            progress.Detail,
            progress.Progress,
            progress.CompletedFiles,
            progress.TotalFiles,
            progress.SpeedBytesPerSecond,
            TaskManagerTaskState.Running,
            ActiveThreads: progress.ActiveThreads,
            ThreadLimit: progress.ThreadLimit,
            Steps: CreateInstallTaskSteps(progress));
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();
    }

    private static TaskManagerSubTaskSnapshot[] CreateInstallTaskSteps(
        MinecraftInstallProgress progress)
    {
        if (progress.Steps.Count == 0)
        {
            return
            [
                new TaskManagerSubTaskSnapshot(
                    string.IsNullOrWhiteSpace(progress.Stage) ? "正在处理下载任务" : progress.Stage,
                    progress.Detail,
                    progress.Progress,
                    TaskManagerTaskState.Running)
            ];
        }

        return progress.Steps
            .Select(static step => new TaskManagerSubTaskSnapshot(
                step.Name,
                step.Detail,
                step.Progress,
                MapInstallStepState(step.State)))
            .ToArray();
    }

    private static TaskManagerTaskState MapInstallStepState(MinecraftInstallStepState state) =>
        state switch
        {
            MinecraftInstallStepState.Waiting => TaskManagerTaskState.Waiting,
            MinecraftInstallStepState.Running => TaskManagerTaskState.Running,
            MinecraftInstallStepState.Finished => TaskManagerTaskState.Finished,
            MinecraftInstallStepState.Failed => TaskManagerTaskState.Failed,
            _ => TaskManagerTaskState.Running
        };

    private static TaskManagerSubTaskSnapshot[]? UpdateTaskStepStates(
        IReadOnlyList<TaskManagerSubTaskSnapshot>? steps,
        TaskManagerTaskState state,
        double progress) =>
        steps is null ? null : steps.Select(step => step with { State = state, Progress = progress }).ToArray();

    private void TrackTaskFinished(string taskId, string title, string stage)
    {
        TaskManagerEntrySnapshot previous = GetTaskSnapshotOrDefault(taskId, title);
        _taskSnapshots[taskId] = previous with
        {
            Title = title,
            Stage = stage,
            Detail = "任务已完成",
            Progress = 1d,
            State = TaskManagerTaskState.Finished,
            ErrorMessage = null,
            Steps = UpdateTaskStepStates(previous.Steps, TaskManagerTaskState.Finished, 1d)
        };
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();
        _ = RemoveTaskAfterDelayAsync(taskId, TimeSpan.FromMilliseconds(900));
    }

    private void TrackTaskFailed(string taskId, string title, string message, bool canceled)
    {
        TaskManagerEntrySnapshot previous = GetTaskSnapshotOrDefault(taskId, title);
        _taskSnapshots[taskId] = previous with
        {
            Title = title,
            Stage = canceled ? "任务已取消" : "任务失败",
            Detail = canceled ? "已停止下载任务" : "请查看错误信息并稍后重试",
            State = canceled ? TaskManagerTaskState.Canceled : TaskManagerTaskState.Failed,
            ErrorMessage = message,
            Steps = UpdateTaskStepStates(
                previous.Steps,
                canceled ? TaskManagerTaskState.Canceled : TaskManagerTaskState.Failed,
                previous.Progress)
        };
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();
        if (canceled)
            _ = RemoveTaskAfterDelayAsync(taskId, TimeSpan.FromMilliseconds(700));
    }

    private TaskManagerEntrySnapshot GetTaskSnapshotOrDefault(string taskId, string title) =>
        _taskSnapshots.TryGetValue(taskId, out TaskManagerEntrySnapshot? snapshot)
            ? snapshot
            : new TaskManagerEntrySnapshot(
                taskId,
                title,
                "正在准备任务",
                string.Empty,
                0d,
                0,
                0,
                0,
                TaskManagerTaskState.Waiting);

    private async Task RemoveTaskAfterDelayAsync(string taskId, TimeSpan delay)
    {
        await Task.Delay(delay).ConfigureAwait(true);
        RemoveTask(taskId);
    }

    private void RemoveTask(string taskId)
    {
        _taskSnapshots.Remove(taskId);
        _speedRight?.RemoveTask(taskId);
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();

        if (_isTaskManagerVisible && _taskSnapshots.Count == 0)
            ReturnFromTaskManager();
    }

    private void UpdateTaskManagerViews()
    {
        if (_taskSnapshots.Count == 0)
        {
            _speedLeft?.SetIdle();
            return;
        }

        foreach (TaskManagerEntrySnapshot snapshot in _taskSnapshots.Values)
            _speedRight?.UpsertTask(snapshot);

        _speedLeft?.UpdateSummary(CreateTaskManagerSummary());
    }

    private TaskManagerSummary CreateTaskManagerSummary()
    {
        TaskManagerEntrySnapshot[] activeTasks = _taskSnapshots.Values
            .Where(static snapshot => snapshot.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running)
            .ToArray();
        TaskManagerEntrySnapshot[] sourceTasks = activeTasks.Length == 0 ? _taskSnapshots.Values.ToArray() : activeTasks;

        double progress = sourceTasks.Length == 0
            ? 1d
            : sourceTasks.Average(static snapshot => Math.Clamp(snapshot.Progress, 0d, 1d));
        long speed = activeTasks.Sum(static snapshot => snapshot.SpeedBytesPerSecond);
        int remainingFiles = activeTasks.Sum(static snapshot =>
            snapshot.TotalFiles > 0 ? Math.Max(0, snapshot.TotalFiles - snapshot.CompletedFiles) : 0);
        int threadLimit = activeTasks.Sum(static snapshot => Math.Max(1, snapshot.ThreadLimit));
        if (threadLimit <= 0)
            threadLimit = Math.Max(1, Environment.ProcessorCount);

        return new TaskManagerSummary(
            progress,
            speed,
            remainingFiles,
            activeTasks.Sum(static snapshot => Math.Max(0, snapshot.ActiveThreads)),
            threadLimit);
    }

    private void NotifyTaskManagerButton(bool ribble)
    {
        RefreshTaskManagerButton();
        if (!ribble ||
            this.FindControl<MyExtraButton>("BtnExtraDownload") is not { } button ||
            !button.Show)
        {
            return;
        }

        button.Ribble();
    }

    private void RefreshTaskManagerButton()
    {
        if (this.FindControl<MyExtraButton>("BtnExtraDownload") is not { } button)
            return;

        bool hasActiveTask = _taskSnapshots.Values.Any(static snapshot =>
            snapshot.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running);
        bool hasVisibleTask = _taskSnapshots.Values.Any(static snapshot =>
            snapshot.State is TaskManagerTaskState.Waiting or TaskManagerTaskState.Running or
                TaskManagerTaskState.Failed or TaskManagerTaskState.Canceled);
        button.Progress = hasActiveTask ? CreateTaskManagerSummary().Progress : hasVisibleTask ? 1d : 0d;
        button.Show = hasVisibleTask && !_isTaskManagerVisible;
    }

    private string CreateTaskId(string kind, string identity)
    {
        int sequence = Interlocked.Increment(ref _taskSequence);
        string safeIdentity = identity
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');
        return string.Concat(kind, ":", sequence.ToString(CultureInfo.InvariantCulture), ":", safeIdentity);
    }

    private CancellationTokenSource RegisterTrackedTask(string taskId)
    {
        CancellationTokenSource cancellation = new();
        if (_taskCancellations.Remove(taskId, out CancellationTokenSource? previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _taskCancellations.Add(taskId, cancellation);
        return cancellation;
    }

    private void CancelTrackedTask(string taskId)
    {
        if (_taskCancellations.TryGetValue(taskId, out CancellationTokenSource? cancellation))
            cancellation.Cancel();
    }

    private void UnregisterTrackedTask(string taskId, CancellationTokenSource cancellation)
    {
        if (_taskCancellations.TryGetValue(taskId, out CancellationTokenSource? registered) &&
            ReferenceEquals(registered, cancellation))
        {
            _taskCancellations.Remove(taskId);
        }
    }

    private void CancelAllTrackedTasks()
    {
        foreach (CancellationTokenSource cancellation in _taskCancellations.Values)
            cancellation.Cancel();
    }

    private void DisposeTrackedTasks()
    {
        foreach (CancellationTokenSource cancellation in _taskCancellations.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        _taskCancellations.Clear();
    }

    private PageInstanceScreenshotRight CreateInstanceScreenshotPage()
    {
        PageInstanceScreenshotRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        page.OpenFileRequested += (_, path) => OpenExistingPath(path);
        page.StatusMessage += (_, message) => HandleStatusMessage(message);
        return page;
    }

    private PageInstanceSavesRight CreateInstanceSavesPage()
    {
        PageInstanceSavesRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        page.SaveDetailsRequested += (_, path) => _ = ShowInstanceSaveDetailsAsync(path);
        page.QuickPlayRequested += (_, worldName) =>
        {
            if (_managedInstance is not null && _launchLeft is not null)
                _ = StartMinecraftAsync(_launchLeft, _managedInstance, worldName);
        };
        page.StatusMessage += (_, message) => HandleStatusMessage(message);
        return page;
    }

    private PageInstanceSavesInfoRight CreateInstanceSavesInfoPage()
    {
        PageInstanceSavesInfoRight page = new();
        page.StatusMessage += (_, message) => HandleStatusMessage(message);
        page.DatapackManageRequested += (_, saveFolder) => ShowInstanceDatapacks(saveFolder);
        return page;
    }

    private async Task ShowInstanceSaveDetailsAsync(string saveFolder)
    {
        if (_managedInstance is null ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        _instanceSavesInfoPage ??= CreateInstanceSavesInfoPage();
        PageInstanceSavesInfoRight page = _instanceSavesInfoPage;
        _titleInnerBackAction = () =>
        {
            if (_managedInstance is not null)
                ApplyInstanceManagePage(_managedInstance, InstancePageSubType.Saves);
        };
        EnterTitleSubPage("存档详情 - " + Path.GetFileName(saveFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (!ReferenceEquals(oldRight, page))
        {
            oldRight?.PageOnExit();
            rightHost.Child = page;
        }

        RefreshBackToTopBinding();
        page.PageOnEnter();
        await page.SetSaveFolderAsync(saveFolder).ConfigureAwait(true);
    }

    private void ShowInstanceDatapacks(string saveFolder)
    {
        if (this.FindControl<Border>("PanMainRight") is not { } rightHost)
            return;

        _instanceDatapackPage ??= CreateInstanceDatapackPage();
        PageInstanceResourceRight page = _instanceDatapackPage;
        _titleInnerBackAction = () => _ = ShowInstanceSaveDetailsAsync(saveFolder);
        EnterTitleSubPage("数据包 - " + Path.GetFileName(saveFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (!ReferenceEquals(oldRight, page))
        {
            oldRight?.PageOnExit();
            rightHost.Child = page;
        }

        page.SetDataPackFolder(saveFolder);
        RefreshBackToTopBinding();
        page.PageOnEnter();
    }

    private PageInstanceToolsRight CreateInstanceToolsPage()
    {
        PageInstanceToolsRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        return page;
    }

    private PageInstanceModDisabledRight CreateInstanceModDisabledPage()
    {
        PageInstanceModDisabledRight page = new();
        page.DownloadRequested += (_, _) => SelectNavRoute(DownloadRoute, animate: true);
        page.InstanceSelectRequested += (_, _) =>
        {
            SelectNavRoute(LaunchRoute, animate: true);
            ApplyInstanceSelectPage();
        };
        return page;
    }

    private PageInstanceResourceRight CreateInstanceResourcePage()
    {
        PageInstanceResourceRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        page.DownloadRequested += (_, subPage) => OpenCommunityForResourcePage(subPage);
        page.StatusMessage += (_, message) =>
        {
            HandleStatusMessage(message);
            ShowHint(message);
        };
        return page;
    }

    private PageInstanceResourceRight CreateInstanceDatapackPage()
    {
        PageInstanceResourceRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        page.DownloadRequested += (_, _) =>
        {
            SelectNavRoute(CommunityRoute, animate: true);
            _ = _communityLeft?.TrySelectCategory(CommunityResourceCategory.DataPack) == true
                ? _communityRight?.SetCategoryAsync(CommunityResourceCategory.DataPack)
                : Task.CompletedTask;
        };
        page.StatusMessage += (_, message) =>
        {
            HandleStatusMessage(message);
            ShowHint(message);
        };
        return page;
    }

    private void OpenCommunityForResourcePage(InstancePageSubType subPage)
    {
        CommunityResourceCategory category = subPage switch
        {
            InstancePageSubType.Mods => CommunityResourceCategory.Mod,
            InstancePageSubType.ResourcePacks => CommunityResourceCategory.ResourcePack,
            InstancePageSubType.Shaders => CommunityResourceCategory.Shader,
            InstancePageSubType.Schematics => CommunityResourceCategory.World,
            _ => CommunityResourceCategory.Mod
        };

        SelectNavRoute(CommunityRoute, animate: true);
        if (_communityLeft is not null && _communityLeft.TrySelectCategory(category))
            _ = _communityRight?.SetCategoryAsync(category);
    }

    private PageInstanceServerRight CreateInstanceServerPage()
    {
        PageInstanceServerRight page = new();
        page.RefreshRequested += (_, _) => page.Reload();
        page.AddServerRequested += (_, instance) => PromptAddServer(instance, page);
        page.ConnectServerRequested += (_, server) =>
        {
            if (_managedInstance is { } instance && _launchLeft is { } launchPage)
                _ = StartMinecraftAsync(launchPage, instance, serverAddress: server.Address);
        };
        page.EditServerRequested += (_, server) =>
        {
            if (_managedInstance is { } instance)
                PromptEditServer(instance, page, server);
        };
        page.RemoveServerRequested += (_, server) =>
        {
            if (_managedInstance is { } instance)
                PromptRemoveServer(instance, page, server);
        };
        return page;
    }

    private void PromptAddServer(LaunchInstanceInfo instance, PageInstanceServerRight page)
    {
        ShowInputDialog(
            "添加服务器",
            "请输入服务器地址。可以填写域名、IP，或带端口的地址。",
            string.Empty,
            "例如 play.example.net",
            address =>
            {
                if (string.IsNullOrWhiteSpace(address))
                    return;

                string trimmedAddress = address.Trim();
                ShowInputDialog(
                    "服务器名称",
                    "给这个服务器起一个容易识别的名称。",
                    trimmedAddress,
                    "服务器名称",
                    name =>
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return;

                        _ = AddServerAsync(instance, page, name.Trim(), trimmedAddress);
                    });
            });
    }

    private async Task AddServerAsync(
        LaunchInstanceInfo instance,
        PageInstanceServerRight page,
        string name,
        string address)
    {
        try
        {
            string gameDir = await InstanceGameDirectory.ResolveAsync(instance).ConfigureAwait(true);
            await MinecraftServerListService.AddAsync(
                    gameDir,
                    new MinecraftServerEntry(name, address, null))
                .ConfigureAwait(true);
            page.Reload();
            _launchRight?.AppendLog($"已添加服务器 {name}。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("添加失败", "未能添加服务器。\n\n详细信息：" + ex.Message);
        }
    }

    private void PromptEditServer(
        LaunchInstanceInfo instance,
        PageInstanceServerRight page,
        MinecraftServerEntry server)
    {
        ShowInputDialog(
            "编辑服务器名称",
            "修改服务器在列表中显示的名称。",
            server.Name,
            "服务器名称",
            name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                ShowInputDialog(
                    "编辑服务器地址",
                    "请输入服务器域名、IP，或带端口的地址。",
                    server.Address,
                    "例如 play.example.net",
                    address =>
                    {
                        if (!string.IsNullOrWhiteSpace(address))
                        {
                            _ = UpdateServerAsync(
                                instance,
                                page,
                                server,
                                server with { Name = name.Trim(), Address = address.Trim() });
                        }
                    });
            });
    }

    private async Task UpdateServerAsync(
        LaunchInstanceInfo instance,
        PageInstanceServerRight page,
        MinecraftServerEntry original,
        MinecraftServerEntry updated)
    {
        try
        {
            string gameDir = await InstanceGameDirectory.ResolveAsync(instance).ConfigureAwait(true);
            bool changed = await MinecraftServerListService.UpdateAsync(
                    gameDir,
                    original,
                    updated)
                .ConfigureAwait(true);
            if (!changed)
            {
                ShowTextDialog("编辑失败", "服务器条目已不存在，请刷新列表后重试。");
                return;
            }

            page.Reload();
            _launchRight?.AppendLog($"已更新服务器 {updated.Name}。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("编辑失败", "未能更新服务器。\n\n详细信息：" + ex.Message);
        }
    }

    private void PromptRemoveServer(
        LaunchInstanceInfo instance,
        PageInstanceServerRight page,
        MinecraftServerEntry server)
    {
        ShowConfirmDialog(
            "删除服务器",
            $"确定要从列表中删除“{server.Name}”吗？\n\n{server.Address}",
            confirmed =>
            {
                if (confirmed)
                    _ = RemoveServerAsync(instance, page, server);
            },
            "删除",
            "取消",
            isWarn: true);
    }

    private async Task RemoveServerAsync(
        LaunchInstanceInfo instance,
        PageInstanceServerRight page,
        MinecraftServerEntry server)
    {
        try
        {
            string gameDir = await InstanceGameDirectory.ResolveAsync(instance).ConfigureAwait(true);
            bool removed = await MinecraftServerListService.RemoveAsync(
                    gameDir,
                    server)
                .ConfigureAwait(true);
            if (!removed)
            {
                ShowTextDialog("删除失败", "服务器条目已不存在，请刷新列表后重试。");
                return;
            }

            page.Reload();
            _launchRight?.AppendLog($"已删除服务器 {server.Name}。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("删除失败", "未能删除服务器。\n\n详细信息：" + ex.Message);
        }
    }

    private DesktopMainPage CreateSettingsMainPage()
    {
        _setupLeft ??= CreateSetupLeftPage();
        MyPageRight rightPage = _setupLeft.GetOrCreateCurrentPage();
        _setupRight = rightPage;
        return new DesktopMainPage(
            _setupLeft,
            rightPage,
            Activated: () =>
            {
                _setupLeft.TriggerShowAnimation();
                rightPage.PageOnEnter();
            });
    }

    private PageSetupLeft CreateSetupLeftPage()
    {
        PageSetupLeft page = new();
        page.PageCreated += (_, created) => WireSetupPage(created);
        page.PageChanged += (_, args) => ApplySetupRightPage(args.Page);
        page.ResetRequested += (_, args) =>
            ShowConfirmDialog(
                args.Title,
                args.Message,
                args.Complete,
                args.PrimaryButton,
                args.SecondaryButton,
                args.IsWarn);
        return page;
    }

    private void WireSetupPage(MyPageRight page)
    {
        if (page is PageSetupLaunch launchSettingsPage)
        {
            launchSettingsPage.SwitchToInstanceSetupRequested += (_, _) => _ = SwitchToSelectedInstanceSetupAsync();
        }

        if (page is not ISettingsPageInteractionSource source)
            return;

        source.OpenPathRequested += (_, args) => OpenFolder(args.Path);
        source.OpenUrlRequested += (_, args) => OpenExternalUrl(args.Url);
        source.MessageRequested += (_, args) => ShowTextDialog(args.Title, args.Message, args.PrimaryButton);
        source.ConfirmRequested += (_, args) =>
            ShowConfirmDialog(
                args.Title,
                args.Message,
                args.Complete,
                args.PrimaryButton,
                args.SecondaryButton,
                args.IsWarn);
    }

    private async Task SwitchToSelectedInstanceSetupAsync()
    {
        _launchLeft ??= CreateLaunchLeftPage();
        await _launchLeft.EnsureInstancesLoadedAsync().ConfigureAwait(true);

        LaunchInstanceInfo? selectedInstance = _launchLeft.SelectedInstance;
        if (selectedInstance is null)
        {
            ShowTextDialog(
                "还没有可设置的版本",
                "当前没有找到可用的 Minecraft 版本。请先下载一个版本，或把已有游戏目录添加到启动器中。",
                "知道了");
            return;
        }

        SelectNavRoute(LaunchRoute, animate: true);
        ApplyInstanceManagePage(selectedInstance, InstancePageSubType.Setup);
    }

    private void ApplySetupRightPage(MyPageRight target)
    {
        if (this.FindControl<Border>("PanMainRight") is not { } rightHost)
            return;

        if (ReferenceEquals(_setupRight, target) && ReferenceEquals(rightHost.Child, target))
            return;

        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        _setupRight = target;
        ModAnimation.AniStop("FrmMain PageChangeRight");
        oldRight?.PageOnExit();
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaCode(() =>
                {
                    oldRight?.PageOnForceExit();
                    rightHost.Child = target;
                    target.Opacity = 0d;
                }, 130),
                ModAnimation.AaCode(() =>
                {
                    target.Opacity = 1d;
                    RefreshBackToTopBinding();
                    target.PageOnEnter();
                }, 30, after: true)
            },
            "PageSetupLeft PageChange");
    }

    private void ApplyLaunchLoginPage(PageLaunchLeft launchPage, PageLaunchLeft.LaunchLoginPageType type)
    {
        switch (type)
        {
            case PageLaunchLeft.LaunchLoginPageType.ProfileSkin:
                if (_loginProfiles.Count == 0)
                {
                    launchPage.SetSelectedProfilePresent(false);
                    ApplyLaunchLoginPage(launchPage, PageLaunchLeft.LaunchLoginPageType.Profile);
                    return;
                }

                LoginProfileInfo selectedProfile = _loginProfiles[0];
                _loginProfileSkinPage ??= CreateProfileSkinPage(launchPage);
                _loginProfileSkinPage.SetProfile(selectedProfile);
                launchPage.SetLoginPage(_loginProfileSkinPage, animate: true, PageLaunchLeft.LaunchLoginPageType.ProfileSkin);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Profile:
                _loginProfilePage ??= CreateProfilePage(launchPage);
                _loginProfilePage.SetProfiles(_loginProfiles);
                launchPage.SetLoginPage(_loginProfilePage, animate: true, PageLaunchLeft.LaunchLoginPageType.Profile);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Ms:
                _loginMsPage ??= CreateMicrosoftLoginPage(launchPage);
                launchPage.SetLoginPage(_loginMsPage, animate: true, PageLaunchLeft.LaunchLoginPageType.Ms);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Auth:
                _loginAuthPage ??= CreateAuthLoginPage(launchPage);
                launchPage.SetLoginPage(_loginAuthPage, animate: true, PageLaunchLeft.LaunchLoginPageType.Auth);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Offline:
                _loginOfflinePage ??= CreateOfflineLoginPage(launchPage);
                _loginOfflinePage.SetSkinSources(_loginProfiles);
                launchPage.SetLoginPage(_loginOfflinePage, animate: true, PageLaunchLeft.LaunchLoginPageType.Offline);
                break;
            default:
                _loginProfilePage ??= CreateProfilePage(launchPage);
                _loginProfilePage.SetProfiles(_loginProfiles);
                launchPage.SetLoginPage(_loginProfilePage, animate: true, PageLaunchLeft.LaunchLoginPageType.Profile);
                break;
        }
    }

    private PageLoginProfile CreateProfilePage(PageLaunchLeft launchPage)
    {
        PageLoginProfile page = new();
        page.ProfileSelected += (_, profile) =>
        {
            _loginProfiles.Remove(profile);
            _loginProfiles.Insert(0, profile);
            launchPage.SetSelectedProfilePresent(true);
            launchPage.RefreshPage(anim: true);
            SaveProfilesInBackground("保存账户档案选择");
            _launchRight?.AppendLog($"已选择账户档案 {profile.Username}。");
        };
        page.CreateProfileRequested += (_, _) =>
        {
            ShowProfileTypeSelector(launchPage);
        };
        page.ImportExportRequested += (_, _) => ShowProfileImportExportSelector(page, launchPage);
        return page;
    }

    private PageLoginProfileSkin CreateProfileSkinPage(PageLaunchLeft launchPage)
    {
        PageLoginProfileSkin page = new();
        page.ChangeProfileRequested += (_, _) =>
        {
            launchPage.SetSelectedProfilePresent(false);
            launchPage.RefreshPage(anim: true);
        };
        page.ChangeSkinRequested += (_, _) => OpenProfileAppearancePage(page.Profile, "更换皮肤");
        page.SaveSkinRequested += (_, _) => _ = SaveProfileSkinAsync(page.Profile);
        page.RefreshSkinRequested += (_, _) => _ = RefreshProfileSkinAsync(page);
        page.ChangeCapeRequested += (_, _) => OpenProfileAppearancePage(page.Profile, "更换披风");
        page.EditPasswordRequested += (_, _) => OpenProfileSecurityPage(page.Profile);
        page.EditNameRequested += (_, _) => OpenProfileNamePage(page.Profile);
        return page;
    }

    private void OpenProfileAppearancePage(LoginProfileInfo? profile, string action)
    {
        if (profile is null)
            return;

        if (profile.Kind == LaunchLoginProfileKind.Microsoft)
        {
            // WPF: ModProfile.ChangeSkinMs — pick local PNG and upload to Minecraft services.
            _ = ChangeMicrosoftSkinAsync(profile, action);
            return;
        }

        if (profile.Kind == LaunchLoginProfileKind.ThirdParty)
        {
            OpenAuthServerProfilePage(profile, action);
            return;
        }

        // WPF offline: borrow MS profile skin or pick local file.
        ShowOfflineSkinOptions(profile, action);
    }

    private void ShowOfflineSkinOptions(LoginProfileInfo profile, string action)
    {
        List<LoginProfileInfo> msProfiles = _loginProfiles
            .Where(static p => p.Kind == LaunchLoginProfileKind.Microsoft)
            .ToList();
        if (msProfiles.Count == 0)
        {
            _ = PickOfflineSkinFileAsync(profile, action);
            return;
        }

        List<MyListItem> items =
        [
            CreateProfileTypeItem("使用本地 PNG 文件", "从磁盘选择皮肤文件作为离线外观。", "lucide/image")
        ];
        foreach (LoginProfileInfo ms in msProfiles)
        {
            items.Add(CreateProfileTypeItem(
                "借用 " + ms.Username + " 的正版皮肤",
                "使用该正版档案当前皮肤作为离线外观来源。",
                "lucide/user"));
        }

        MyMsgSelect dialog = new();
        dialog.Configure(action, items);
        ShowSelectionDialog(dialog, selectedIndex =>
        {
            if (selectedIndex is not int index)
                return;
            if (index == 0)
            {
                _ = PickOfflineSkinFileAsync(profile, action);
                return;
            }

            LoginProfileInfo source = msProfiles[index - 1];
            string skin = MySkin.ResolveSkinAddress(
                source.SkinAddress,
                source.Uuid,
                source.Kind == LaunchLoginProfileKind.ThirdParty ? source.AuthServer : null);
            LoginProfileInfo updated = profile with { SkinAddress = skin };
            ReplaceLoginProfile(profile, updated);
            _loginProfilePage?.SetProfiles(_loginProfiles, updated);
            _loginProfileSkinPage?.SetProfile(updated);
            SaveProfilesInBackground("借用正版皮肤");
            HandleStatusMessage($"已为 {updated.Username} 借用 {source.Username} 的皮肤。");
        });
    }

    private async Task PickOfflineSkinFileAsync(LoginProfileInfo profile, string action)
    {
        string? path = await PickOpenFilePathAsync(
                action,
                new FilePickerFileType("皮肤 PNG") { Patterns = ["*.png"] })
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
            return;

        LoginProfileInfo updated = profile with { SkinAddress = path };
        ReplaceLoginProfile(profile, updated);
        _loginProfilePage?.SetProfiles(_loginProfiles, updated);
        _loginProfileSkinPage?.SetProfile(updated);
        SaveProfilesInBackground("更新离线皮肤");
        ShowTextDialog(action, "已使用本地皮肤文件：\n" + path, "知道了");
    }

    private async Task ChangeMicrosoftSkinAsync(LoginProfileInfo profile, string action)
    {
        string? path = await PickOpenFilePathAsync(
                action,
                new FilePickerFileType("皮肤 PNG") { Patterns = ["*.png"] })
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (string.IsNullOrWhiteSpace(profile.AccessToken))
        {
            ShowTextDialog(action, "当前正版档案缺少访问令牌，请先重新登录后再更换皮肤。", "知道了");
            return;
        }

        try
        {
            HandleStatusMessage("正在上传皮肤…");
            byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(true);
            using MultipartFormDataContent content = new();
            content.Add(new StringContent("classic"), "variant");
            ByteArrayContent fileContent = new(bytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", Path.GetFileName(path));

            using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                "https://api.minecraftservices.com/minecraft/profile/skins")
            {
                Content = content
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                ShowTextDialog(action, "皮肤上传失败。\n\n" + body, "知道了");
                return;
            }

            string? skinUrl = null;
            using (JsonDocument document = JsonDocument.Parse(body))
            {
                if (document.RootElement.TryGetProperty("skins", out JsonElement skins) &&
                    skins.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement skin in skins.EnumerateArray())
                    {
                        if (skin.ValueKind != JsonValueKind.Object)
                            continue;
                        string state = skin.TryGetProperty("state", out JsonElement stateEl)
                            ? stateEl.GetString() ?? string.Empty
                            : string.Empty;
                        string? url = skin.TryGetProperty("url", out JsonElement urlEl)
                            ? urlEl.GetString()
                            : null;
                        if (string.Equals(state, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(url))
                        {
                            skinUrl = url;
                            break;
                        }

                        skinUrl ??= url;
                    }
                }
            }

            LoginProfileInfo updated = profile with
            {
                SkinAddress = skinUrl ?? path
            };
            ReplaceLoginProfile(profile, updated);
            _loginProfilePage?.SetProfiles(_loginProfiles, updated);
            _loginProfileSkinPage?.SetProfile(updated);
            SaveProfilesInBackground("更换 Microsoft 皮肤");
            ShowTextDialog(action, "皮肤已上传并更新。", "知道了");
        }
        catch (Exception ex)
        {
            ShowTextDialog(action, "皮肤上传失败。\n\n详细信息：" + ex.Message, "知道了");
        }
    }

    private void OpenProfileSecurityPage(LoginProfileInfo? profile)
    {
        if (profile is null)
            return;

        if (profile.Kind == LaunchLoginProfileKind.Microsoft)
        {
            OpenExternalUrl("https://account.microsoft.com/security");
            ShowTextDialog("修改密码", "已打开 Microsoft 账户安全页面。密码修改完成后，可能需要在启动器中重新登录。", "知道了");
            return;
        }

        if (profile.Kind == LaunchLoginProfileKind.ThirdParty)
        {
            OpenAuthServerProfilePage(profile, "修改密码");
            return;
        }

        ShowTextDialog("修改密码", "离线档案没有在线密码。若需要更换玩家名或 UUID，请新建一个离线档案。", "知道了");
    }

    private void OpenProfileNamePage(LoginProfileInfo? profile)
    {
        if (profile is null)
            return;

        if (profile.Kind == LaunchLoginProfileKind.Microsoft)
        {
            // WPF: ModProfile.EditProfileId — rename via Minecraft services API.
            ShowInputDialog(
                "修改玩家名",
                "正版玩家名 30 天内通常只能修改一次。请输入 3–16 位字母/数字/下划线。",
                profile.Username,
                "新的玩家名",
                newName =>
                {
                    if (string.IsNullOrWhiteSpace(newName))
                        return;
                    _ = RenameMicrosoftProfileAsync(profile, newName.Trim());
                });
            return;
        }

        if (profile.Kind == LaunchLoginProfileKind.ThirdParty)
        {
            OpenAuthServerProfilePage(profile, "修改玩家名");
            return;
        }

        // WPF offline: rename + regenerate offline UUID from the new name.
        ShowInputDialog(
            "修改档案",
            "请输入新的离线玩家名（3–16 位字母、数字或下划线）。",
            profile.Username,
            "玩家名",
            newName =>
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                string trimmed = newName.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9_]{3,16}$"))
                {
                    ShowTextDialog("修改档案", "玩家名不合法。请使用 3–16 位字母、数字或下划线。", "知道了");
                    return;
                }

                if (string.Equals(trimmed, profile.Username, StringComparison.Ordinal))
                    return;

                string uuid = CreateOfflineUuid(trimmed, legacy: false);
                LoginProfileInfo updated = profile with
                {
                    Username = trimmed,
                    Uuid = uuid,
                    Info = string.IsNullOrWhiteSpace(profile.Info) || profile.Info.Contains("离线", StringComparison.Ordinal)
                        ? "离线"
                        : profile.Info
                };
                ReplaceLoginProfile(profile, updated);
                _loginProfilePage?.SetProfiles(_loginProfiles, updated);
                _loginProfileSkinPage?.SetProfile(updated);
                SaveProfilesInBackground("修改离线档案");
                HandleStatusMessage("已将离线档案重命名为 " + trimmed);
            });
    }

    private async Task RenameMicrosoftProfileAsync(LoginProfileInfo profile, string newUsername)
    {
        if (string.IsNullOrWhiteSpace(profile.AccessToken))
        {
            ShowTextDialog("修改玩家名", "当前正版档案缺少访问令牌，请先重新登录。", "知道了");
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(newUsername, @"^[A-Za-z0-9_]{3,16}$"))
        {
            ShowTextDialog("修改玩家名", "玩家名不合法。请使用 3–16 位字母、数字或下划线。", "知道了");
            return;
        }

        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
            using (HttpRequestMessage check = new(
                       HttpMethod.Get,
                       "https://api.minecraftservices.com/minecraft/profile/name/" +
                       Uri.EscapeDataString(newUsername) + "/available"))
            {
                check.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
                using HttpResponseMessage checkResponse = await client.SendAsync(check).ConfigureAwait(true);
                string checkBody = await checkResponse.Content.ReadAsStringAsync().ConfigureAwait(true);
                if (checkResponse.IsSuccessStatusCode)
                {
                    using JsonDocument checkDoc = JsonDocument.Parse(checkBody);
                    string status = checkDoc.RootElement.TryGetProperty("status", out JsonElement statusEl)
                        ? statusEl.GetString() ?? string.Empty
                        : string.Empty;
                    if (string.Equals(status, "DUPLICATE", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowTextDialog("修改玩家名", "该玩家名已被占用。", "知道了");
                        return;
                    }

                    if (string.Equals(status, "NOT_ALLOWED", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowTextDialog("修改玩家名", "该玩家名不被允许。", "知道了");
                        return;
                    }
                }
            }

            using HttpRequestMessage put = new(
                HttpMethod.Put,
                "https://api.minecraftservices.com/minecraft/profile/name/" + Uri.EscapeDataString(newUsername));
            put.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.AccessToken);
            put.Content = new StringContent(string.Empty);
            using HttpResponseMessage putResponse = await client.SendAsync(put).ConfigureAwait(true);
            string putBody = await putResponse.Content.ReadAsStringAsync().ConfigureAwait(true);
            if (!putResponse.IsSuccessStatusCode)
            {
                string message = putResponse.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "修改被拒绝（可能处于冷却期或权限不足）。"
                    : putBody;
                ShowTextDialog("修改玩家名", "修改失败。\n\n" + message, "知道了");
                return;
            }

            string finalName = newUsername;
            try
            {
                using JsonDocument result = JsonDocument.Parse(putBody);
                if (result.RootElement.TryGetProperty("name", out JsonElement nameEl) &&
                    nameEl.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(nameEl.GetString()))
                {
                    finalName = nameEl.GetString()!;
                }
            }
            catch (JsonException)
            {
            }

            LoginProfileInfo updated = profile with { Username = finalName };
            ReplaceLoginProfile(profile, updated);
            _loginProfilePage?.SetProfiles(_loginProfiles, updated);
            _loginProfileSkinPage?.SetProfile(updated);
            SaveProfilesInBackground("修改 Microsoft 玩家名");
            ShowTextDialog("修改玩家名", "玩家名已更新为：" + finalName, "知道了");
        }
        catch (Exception ex)
        {
            ShowTextDialog("修改玩家名", "修改失败。\n\n详细信息：" + ex.Message, "知道了");
        }
    }

    private static string CreateOfflineUuid(string username, bool legacy)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(legacy ? username : "OfflinePlayer:" + username);
#pragma warning disable CA5351
        byte[] hash = System.Security.Cryptography.MD5.HashData(bytes);
#pragma warning restore CA5351
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash).ToString("N");
    }

    private void OpenAuthServerProfilePage(LoginProfileInfo profile, string action)
    {
        // WPF: strip /api/yggdrasil/authserver and open /user/profile (or server root).
        string? server = profile.AuthServer?.Trim();
        if (string.IsNullOrWhiteSpace(server))
        {
            ShowTextDialog(action, "第三方账户的资料由认证服务器管理，但当前档案没有记录可打开的服务器地址。请到对应认证服务器的网站中修改。", "知道了");
            return;
        }

        string url = server;
        int authIdx = url.IndexOf("api/yggdrasil/authserver", StringComparison.OrdinalIgnoreCase);
        if (authIdx >= 0)
            url = url[..authIdx].TrimEnd('/') + "/user/profile";
        else
            url = NormalizeAuthServerUrl(server) ?? server;

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url.TrimStart('/');

        OpenExternalUrl(url);
        ShowTextDialog(action, "已打开此第三方账户所属的认证服务器页面。请在服务器网页中完成账户资料修改。", "知道了");
    }

    private async Task SaveProfileSkinAsync(LoginProfileInfo? profile)
    {
        if (profile is null)
            return;

        if (string.IsNullOrWhiteSpace(profile.SkinAddress))
        {
            ShowTextDialog("保存皮肤", "当前档案没有可保存的皮肤资源。请先登录带有皮肤的在线档案，或在离线档案中选择一个皮肤来源。", "知道了");
            return;
        }

        string suggestedFileName = SanitizeFileName(profile.Username) + "-skin.png";
        string targetPath = await PickSaveFilePathAsync(
                "保存皮肤",
                suggestedFileName,
                new FilePickerFileType("PNG 图片") { Patterns = ["*.png"] })
            .ConfigureAwait(true)
            ?? Path.Combine(GetDesktopOrBaseDirectory(), suggestedFileName);

        try
        {
            if (TryCreateHttpUri(profile.SkinAddress, out Uri? uri))
            {
                using HttpClient client = new();
                byte[] bytes = await client.GetByteArrayAsync(uri).ConfigureAwait(true);
                await File.WriteAllBytesAsync(targetPath, bytes).ConfigureAwait(true);
            }
            else if (File.Exists(profile.SkinAddress))
            {
                File.Copy(profile.SkinAddress, targetPath, overwrite: true);
            }
            else
            {
                ShowTextDialog("保存皮肤", "当前皮肤资源不存在，可能已经被移动或需要重新登录后刷新。", "知道了");
                return;
            }

            ShowTextDialog("保存完成", "皮肤已保存到：\n" + targetPath);
        }
        catch (Exception ex)
        {
            ShowTextDialog("保存失败", "未能保存皮肤。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task RefreshProfileSkinAsync(PageLoginProfileSkin page)
    {
        LoginProfileInfo? profile = page.Profile;
        if (profile is null)
            return;

        try
        {
            if (profile.Kind == LaunchLoginProfileKind.Microsoft &&
                !string.IsNullOrWhiteSpace(profile.RefreshToken))
            {
                LoginProfileInfo refreshed = await RefreshLaunchProfileAsync(profile, CancellationToken.None)
                    .ConfigureAwait(true);
                AddOrUpdateLoginProfile(refreshed);
                _loginProfilePage?.SetProfiles(_loginProfiles, refreshed);
                page.SetProfile(refreshed);
                SaveProfilesInBackground("刷新 Microsoft 皮肤");
                ShowTextDialog("皮肤已刷新", "已从 Microsoft 重新获取档案与皮肤信息。", "知道了");
                return;
            }

            page.Reload();
            ShowTextDialog(
                "已刷新档案显示",
                profile.Kind == LaunchLoginProfileKind.Offline
                    ? "已重新加载离线档案皮肤。若使用本地 PNG，请确认文件仍然存在。"
                    : "已重新载入档案信息。第三方皮肤请在认证站修改后重新登录。",
                "知道了");
        }
        catch (Exception ex)
        {
            page.Reload();
            ShowTextDialog("刷新失败", "未能刷新皮肤。\n\n详细信息：" + ex.Message, "知道了");
        }
    }

    private void ShowProfileTypeSelector(PageLaunchLeft launchPage)
    {
        MyMsgSelect dialog = new();
        dialog.Configure(
            "选择账户类型",
            [
                CreateProfileTypeItem(
                    "Microsoft 登录",
                    "使用正版 Microsoft 账户登录，适合已购买 Minecraft 的玩家。",
                    "lucide/shield-check"),
                CreateProfileTypeItem(
                    "第三方登录",
                    "使用 Authlib-Injector 兼容认证服务器登录。",
                    "lucide/network"),
                CreateProfileTypeItem(
                    "离线登录",
                    "创建本地离线档案。联机服务器可能不会接受此档案。",
                    "lucide/link-2-off")
            ]);
        ShowSelectionDialog(dialog, selectedIndex =>
        {
            if (selectedIndex is not int index)
                return;

            PageLaunchLeft.LaunchLoginPageType? target = index switch
            {
                0 => PageLaunchLeft.LaunchLoginPageType.Ms,
                1 => PageLaunchLeft.LaunchLoginPageType.Auth,
                2 => PageLaunchLeft.LaunchLoginPageType.Offline,
                _ => null
            };
            if (target is null)
                return;

            launchPage.RefreshPage(anim: true, target.Value);
            _launchRight?.AppendLog($"正在创建{dialog.Items[index].Title}档案。");
        });
    }

    private static MyListItem CreateProfileTypeItem(string title, string info, string icon) =>
        new()
        {
            Title = title,
            Info = info,
            SvgIcon = icon,
            LogoScale = 0.82d,
            MinHeight = 42d,
            Margin = new Thickness(0d, 2d)
        };

    private void ShowProfileImportExportSelector(PageLoginProfile page, PageLaunchLeft launchPage)
    {
        MyMsgSelect dialog = new();
        dialog.Configure(
            "导入或导出账户档案",
            [
                CreateProfileTypeItem(
                    "导入账户档案",
                    "从本地 JSON 文件导入账户档案，并与当前列表合并。",
                    "lucide/file-input"),
                CreateProfileTypeItem(
                    "导出账户档案",
                    "将当前账户档案保存为 JSON 文件，方便备份或转移到其他设备。",
                    "lucide/file-output")
            ]);

        ShowSelectionDialog(dialog, selectedIndex =>
        {
            if (selectedIndex == 0)
                _ = ImportProfilesAsync(page, launchPage);
            else if (selectedIndex == 1)
                _ = ExportProfilesAsync();
        });
    }

    private void ShowSelectionDialog(MyMsgSelect dialog, Action<int?> closed)
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            closed(null);
            return;
        }

        host.Children.Clear();
        background.IsVisible = true;
        AnimateMsgBackground(background, 90);
        dialog.Closed += (_, args) =>
        {
            host.Children.Remove(dialog);
            if (host.Children.Count == 0)
            {
                AnimateMsgBackground(background, 0, () =>
                {
                    background.Background = Brushes.Transparent;
                    background.IsVisible = false;
                });
            }
            closed(args.SelectedIndex);
        };
        host.Children.Add(dialog);
        dialog.BeginShowAnimation();
    }

    private void ShowTextDialog(string title, string caption, string primaryButton = "确定")
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            _launchRight?.AppendLog($"{title}：{caption}");
            return;
        }

        MyMsgText dialog = new();
        dialog.Configure(title, caption, primaryButton);
        host.Children.Clear();
        background.IsVisible = true;
        AnimateMsgBackground(background, 90);
        dialog.Closed += (_, _) =>
        {
            host.Children.Remove(dialog);
            if (host.Children.Count == 0)
            {
                AnimateMsgBackground(background, 0, () =>
                {
                    background.Background = Brushes.Transparent;
                    background.IsVisible = false;
                });
            }
        };
        host.Children.Add(dialog);
        dialog.BeginShowAnimation();
    }

    private void ShowConfirmDialog(
        string title,
        string caption,
        Action<bool> closed,
        string primaryButton = "确定",
        string secondaryButton = "取消",
        bool isWarn = false)
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            closed(false);
            return;
        }

        MyMsgText dialog = new();
        dialog.Configure(title, caption, primaryButton, secondaryButton, isWarn: isWarn);
        host.Children.Clear();
        background.IsVisible = true;
        AnimateMsgBackground(background, 90);
        dialog.Closed += (_, args) =>
        {
            host.Children.Remove(dialog);
            if (host.Children.Count == 0)
            {
                AnimateMsgBackground(background, 0, () =>
                {
                    background.Background = Brushes.Transparent;
                    background.IsVisible = false;
                });
            }
            closed(args.IsPrimary);
        };
        host.Children.Add(dialog);
        dialog.BeginShowAnimation();
    }

    private void ShowInputDialog(
        string title,
        string caption,
        string content,
        string hintText,
        Action<string?> closed,
        bool isWarn = false)
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            closed(null);
            return;
        }

        MyMsgInput dialog = new();
        dialog.Configure(title, caption, content, hintText, isWarn: isWarn);
        host.Children.Clear();
        background.IsVisible = true;
        AnimateMsgBackground(background, 90);
        dialog.Closed += (_, args) =>
        {
            host.Children.Remove(dialog);
            if (host.Children.Count == 0)
            {
                AnimateMsgBackground(background, 0, () =>
                {
                    background.Background = Brushes.Transparent;
                    background.IsVisible = false;
                });
            }
            closed(args.Result);
        };
        host.Children.Add(dialog);
        dialog.BeginShowAnimation();
    }

    private void ShowLoginDialog(MyMsgLogin dialog, Action closed)
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            _launchRight?.AppendLog($"{dialog.Title}：{dialog.Caption}");
            closed();
            return;
        }

        host.Children.Clear();
        background.IsVisible = true;
        AnimateMsgBackground(background, 90);
        dialog.ReopenWebpageRequested += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(dialog.Website))
                OpenExternalUrl(dialog.Website);
        };
        dialog.CopyCodeRequested += async (_, _) =>
        {
            try
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(dialog.UserCode).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _launchRight?.AppendLog("复制登录代码失败：" + ex.Message);
            }
        };
        dialog.CancelRequested += (_, _) => closed();
        dialog.DragRequested += (_, e) => BeginMoveDrag(e);
        dialog.Closed += (_, _) =>
        {
            if (host.Children.Count == 0)
            {
                AnimateMsgBackground(background, 0, () =>
                {
                    background.Background = Brushes.Transparent;
                    background.IsVisible = false;
                });
            }
        };
        host.Children.Add(dialog);
    }

    private async Task StartInstallAsync(DownloadInstallRequest request)
    {
        string taskId = CreateTaskId("install", request.VersionId);
        using CancellationTokenSource cancellation = RegisterTrackedTask(taskId);
        string taskTitle = "安装 " + request.VersionId;
        ActivateTaskManagerPage(animate: true);
        TrackTaskBegin(taskId, taskTitle, "准备安装文件");

        string minecraftRoot = string.IsNullOrWhiteSpace(request.MinecraftRootDirectory)
            ? GetDefaultMinecraftRoot()
            : request.MinecraftRootDirectory;
        Directory.CreateDirectory(minecraftRoot);
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        int downloadThreadLimit = Math.Clamp(GetIntegerOption(settings, LauncherSettingKeys.ToolDownloadThread, 63) + 1, 1, 256);
        Progress<MinecraftInstallProgress> progress = new(update => TrackInstallProgress(taskId, taskTitle, update));
        try
        {
            MinecraftInstallResult result = await _minecraftInstallService.InstallAsync(
                    new MinecraftInstallRequest
                    {
                        VersionId = request.VersionId,
                        BaseVersionId = request.BaseVersionId,
                        VersionJsonUrl = request.VersionJsonUrl,
                        MinecraftRootDirectory = minecraftRoot,
                        PreferOfficialSource = true,
                        DownloadThreadLimit = downloadThreadLimit,
                        Loader = request.Loader,
                        Addons = request.Addons ?? [],
                        ReplaceExistingVersion = request.ReplaceExistingVersion,
                        JavaExecutablePath = ResolvePreferredJavaExecutablePath(forceConsole: true)
                    },
                    progress,
                    cancellation.Token)
                .ConfigureAwait(true);
            TrackTaskFinished(taskId, taskTitle, "安装完成");
            _launchRight?.AppendLog($"{request.VersionId} 安装完成。");

            if (_launchLeft is not null)
            {
                await _launchLeft.RefreshInstancesAsync().ConfigureAwait(true);
                LaunchInstanceInfo? installed = _launchLeft.Instances.FirstOrDefault(instance =>
                    string.Equals(instance.InstanceDirectory, result.InstanceDirectory, StringComparison.OrdinalIgnoreCase));
                if (installed is not null)
                    _launchLeft.SetInstances(_launchLeft.Instances, installed);
            }
        }
        catch (OperationCanceledException)
        {
            TrackTaskFailed(taskId, taskTitle, "安装已取消。", canceled: true);
        }
        catch (Exception ex)
        {
            TrackTaskFailed(taskId, taskTitle, ex.Message, canceled: false);
            ShowTextDialog("安装失败", "未能完成 Minecraft 安装。\n\n详细信息：" + ex.Message);
        }
        finally
        {
            UnregisterTrackedTask(taskId, cancellation);
        }
    }

    private async Task StartMinecraftAsync(
        PageLaunchLeft launchPage,
        LaunchInstanceInfo instance,
        string? worldName = null,
        string? serverAddress = null)
    {
        // Prefer the profile currently shown on the login UI (not always the first saved entry).
        LoginProfileInfo? profile =
            _loginProfileSkinPage?.Profile ??
            _loginProfilePage?.SelectedProfile ??
            _loginProfiles.FirstOrDefault();
        if (profile is null)
        {
            if (launchPage.IsLaunchInProgress)
                launchPage.PageChangeToLogin();
            ShowTextDialog("请选择账户档案", "启动游戏前需要先选择或创建一个账户档案。");
            return;
        }

        // Paint launching UI immediately (PageLaunchLeft already calls ShowLaunching on click;
        // keep a fallback for other entry points). Then hop off the UI thread completely.
        if (!launchPage.IsLaunchInProgress)
            launchPage.ShowLaunching(instance);

        // Yield until after layout + animation frames so the launching pane is visible
        // before any disk/network work (WPF ModLaunch similarly paints first).
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Yield();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(32).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        _launchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _launchCancellation.Token;
        LauncherSettings? runtimeSettingsForRepair = null;

        try
        {
            // Entire prep + coordinate pipeline off UI thread (no UI-thread JSON/IO).
            string instanceDirectory = instance.InstanceDirectory;
            InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(
                    instanceDirectory,
                    cancellationToken)
                .ConfigureAwait(false);
            LauncherSettings runtimeSettings = await Task.Run(
                    LauncherSettingsPageBinder.LoadSettings,
                    cancellationToken)
                .ConfigureAwait(false);
            runtimeSettingsForRepair = runtimeSettings;
            string method = MinecraftLaunchCoordinator.FormatLoginMethod(profile);
            Dispatcher.UIThread.Post(() => launchPage.LaunchingRefresh(
                AvaloniaLocalizationManager.GetText("Common.Action.Initialize", "初始化"),
                0d,
                method: method), DispatcherPriority.Background);

            MinecraftLaunchCoordinatorResult result = await _launchCoordinator.RunAsync(
                    new MinecraftLaunchCoordinatorRequest
                    {
                        Instance = instance,
                        Profile = profile,
                        Metadata = metadata,
                        Settings = runtimeSettings,
                        MinecraftRootDirectory = GetMinecraftRootFromInstance(instance),
                        PreferOfficialSource = runtimeSettings.DownloadSource !=
                                               DownloadSourcePreference.MirrorOnly,
                        WorldName = worldName,
                        ServerAddress = serverAddress,
                        Report = report =>
                        {
                            // Always Post — never call LaunchingRefresh synchronously on a hot path.
                            Dispatcher.UIThread.Post(
                                () => launchPage.LaunchingRefresh(
                                    report.StageName,
                                    report.Progress,
                                    report.IsLaunched,
                                    report.Method,
                                    report.DownloadSpeed),
                                DispatcherPriority.Background);
                        },
                        Log = message =>
                        {
                            Dispatcher.UIThread.Post(
                                () => _launchRight?.AppendLog(message),
                                DispatcherPriority.Background);
                        },
                        RefreshProfileAsync = RefreshLaunchProfileAsync,
                        CreatePlanAsync = CreateLaunchPlanAsync,
                        RunPreLaunchCommandAsync = RunPreLaunchCommandAsync,
                        ApplyProcessPriority = ApplyProcessPriority,
                        ConfirmJavaDownloadAsync = ConfirmJavaDownloadAsync
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            // Launch pipeline succeeded — never fold post-success UI side effects into "启动失败".
            // (e.g. Close()/Hide launcher visibility can throw ObjectDisposedException.)
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(result.Profile, profile) &&
                        result.Profile.Kind == LaunchLoginProfileKind.Microsoft)
                    {
                        AddOrUpdateLoginProfile(result.Profile);
                        _loginProfilePage?.SetProfiles(_loginProfiles, result.Profile);
                        _loginProfileSkinPage?.SetProfile(result.Profile);
                        SaveProfilesInBackground("刷新 Microsoft 正版档案");
                    }

                    Process process = result.Process;
                    SetGameRunningExtras(process, new RunningGameContext(instance, launchPage, runtimeSettings));
                    UpdateBackgroundVideoPlayback(runtimeSettings);
                    _launchRight?.AppendLog(!string.IsNullOrWhiteSpace(worldName)
                        ? $"{instance.Name} 已启动，正在进入存档 {worldName}。"
                        : !string.IsNullOrWhiteSpace(serverAddress)
                            ? $"{instance.Name} 已启动，正在连接服务器 {serverAddress}。"
                            : $"{instance.Name} 已启动。");

                    if (runtimeSettings.GetIntegerOption(
                            "LaunchArgumentVisible",
                            LauncherSettingDefaults.GetInteger("LaunchArgumentVisible")) != 0)
                    {
                        launchPage.PageChangeToLogin();
                    }

                    // Visibility last — Close/Hide must not reverse a successful launch UX.
                    ApplyLauncherVisibility(process, runtimeSettings);
                });
            }
            catch (Exception postEx)
            {
                _launchRight?.AppendLog("启动后界面处理异常（游戏已启动）：" + postEx.Message);
            }

            try
            {
                await IncrementInstanceLaunchCountAsync(instance).ConfigureAwait(false);
            }
            catch (Exception countEx)
            {
                _launchRight?.AppendLog("记录启动次数失败：" + countEx.Message);
            }
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (launchPage.IsLaunchInProgress)
                    launchPage.PageChangeToLogin();
            });
        }
        catch (Exception ex)
        {
            if (runtimeSettingsForRepair is { AutomaticallyRepairGameIssues: true } repairSettings &&
                ex.Message.Contains("游戏进程在启动后立即退出", StringComparison.Ordinal))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _launchRight?.AppendLog("启动失败，正在检查缺失前置：" + ex.Message));
                await TryRepairMissingDependenciesAsync(
                        new RunningGameContext(instance, launchPage, repairSettings))
                    .ConfigureAwait(false);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (launchPage.IsLaunchInProgress)
                        launchPage.PageChangeToLogin();
                    ShowTextDialog("启动失败", "未能启动游戏。\n\n详细信息：" + ex.Message);
                    _launchRight?.AppendLog("启动失败：" + ex.Message);
                });
            }
        }
    }

    private void SetGameRunningExtras(Process? process, RunningGameContext? context = null)
    {
        if (!ReferenceEquals(_runningGameProcess, process))
        {
            if (_runningGameProcess is { } previous)
            {
                try
                {
                    previous.EnableRaisingEvents = false;
                    previous.Exited -= RunningGameProcess_Exited;
                }
                catch
                {
                    // process may already be disposed
                }
            }

            _runningGameProcess = process is { HasExited: false } ? process : null;
            _runningGameContext = _runningGameProcess is null ? null : context;
            if (_runningGameProcess is { } current)
            {
                try
                {
                    current.EnableRaisingEvents = true;
                    current.Exited += RunningGameProcess_Exited;
                }
                catch
                {
                    // EnableRaisingEvents can throw if process already exited
                    _runningGameProcess = null;
                    _runningGameContext = null;
                }
            }
        }
        else if (process is null || process.HasExited)
        {
            _runningGameProcess = null;
            _runningGameContext = null;
        }

        _isGameRunning = _runningGameProcess is not null;

        if (this.FindControl<MyExtraButton>("BtnExtraShutdown") is { } shutdown)
        {
            // WPF: BtnExtraShutdown.Show = game running (bottom-right extra power button)
            shutdown.Show = _isGameRunning;
        }

        if (this.FindControl<MyExtraButton>("BtnExtraLog") is { } logBtn)
            logBtn.Show = _isGameRunning;
    }

    private void RunningGameProcess_Exited(object? sender, EventArgs e)
    {
        RunningGameContext? context = _runningGameContext;
        int exitCode = 0;
        if (sender is Process process)
        {
            try
            {
                exitCode = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                exitCode = -1;
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_runningGameProcess, sender) ||
                _runningGameProcess is null ||
                _runningGameProcess.HasExited)
            {
                SetGameRunningExtras(null);
            }
        }, DispatcherPriority.Background);

        if (exitCode != 0 && context is { Settings.AutomaticallyRepairGameIssues: true })
            _ = TryRepairMissingDependenciesAsync(context);
    }

    private async Task TryRepairMissingDependenciesAsync(RunningGameContext context)
    {
        try
        {
            string gameDirectory = await InstanceGameDirectory.ResolveAsync(context.Instance).ConfigureAwait(false);
            IReadOnlyList<string> crashLines = await ReadRecentCrashLinesAsync(gameDirectory).ConfigureAwait(false);
            IReadOnlyList<MinecraftMissingDependency> dependencies = MinecraftMissingDependencyParser.Parse(crashLines);
            if (dependencies.Count == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _launchRight?.AppendLog("自动修复：崩溃日志中未识别到缺失前置模组。");
                    ShowHint("自动修复：未识别到缺失前置模组");
                    if (context.LaunchPage.IsLaunchInProgress)
                        context.LaunchPage.PageChangeToLogin();
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                context.LaunchPage.ShowRepairing();
                _launchRight?.AppendLog($"自动修复：发现 {dependencies.Count} 个缺失前置模组。");
            });

            string modsDirectory = Path.Combine(gameDirectory, "mods");
            Directory.CreateDirectory(modsDirectory);
            string gameVersion = MinecraftVersionJsonInspector.Read(context.Instance).MinecraftVersionId;
            int repaired = 0;
            using CompositeCommunityResourceCatalog catalog = new();
            using HttpClient downloader = new() { Timeout = TimeSpan.FromMinutes(5) };
            downloader.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
            for (int index = 0; index < dependencies.Count; index++)
            {
                MinecraftMissingDependency dependency = dependencies[index];
                await Dispatcher.UIThread.InvokeAsync(() => context.LaunchPage.UpdateRepairStep(index + 1, dependencies.Count));
                if (await DownloadMissingDependencyAsync(
                        catalog,
                        downloader,
                        dependency,
                        gameVersion,
                        modsDirectory)
                    .ConfigureAwait(false))
                {
                    repaired++;
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                context.LaunchPage.HideRepairing();
                if (context.LaunchPage.IsLaunchInProgress)
                    context.LaunchPage.PageChangeToLogin();
                string result = repaired == dependencies.Count
                    ? $"自动修复完成：已安装 {repaired} 个前置模组，请重新启动游戏。"
                    : $"自动修复完成：已安装 {repaired}/{dependencies.Count} 个前置模组。";
                _launchRight?.AppendLog(result);
                ShowHint(result, critical: repaired != dependencies.Count);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                context.LaunchPage.HideRepairing();
                if (context.LaunchPage.IsLaunchInProgress)
                    context.LaunchPage.PageChangeToLogin();
                _launchRight?.AppendLog("自动修复失败：" + ex.Message);
                ShowHint("自动修复失败：" + TruncateHint(ex.Message), critical: true);
            });
        }
    }

    private static async Task<bool> DownloadMissingDependencyAsync(
        CompositeCommunityResourceCatalog catalog,
        HttpClient downloader,
        MinecraftMissingDependency dependency,
        string gameVersion,
        string modsDirectory)
    {
        CommunitySearchOptions options = new(
            CommunityResourceSort.Relevance,
            GameVersion: gameVersion,
            Loader: "fabric",
            Source: CommunityResourceSource.All);
        IReadOnlyList<CommunityResourceEntry> entries = await catalog.SearchAsync(
                CommunityResourceCategory.Mod,
                dependency.ModId,
                options)
            .ConfigureAwait(false);
        CommunityResourceEntry? entry = entries
            .OrderBy(candidate => GetDependencyMatchScore(candidate, dependency))
            .FirstOrDefault();
        if (entry is null && !string.Equals(dependency.Name, dependency.ModId, StringComparison.OrdinalIgnoreCase))
        {
            entries = await catalog.SearchAsync(
                    CommunityResourceCategory.Mod,
                    dependency.Name,
                    options)
                .ConfigureAwait(false);
            entry = entries.OrderBy(candidate => GetDependencyMatchScore(candidate, dependency)).FirstOrDefault();
        }
        if (entry is null)
            return false;

        CommunityResourceDownloadFile? file = await catalog.ResolveDownloadAsync(entry, options).ConfigureAwait(false);
        if (file is null)
            return false;
        string targetPath = Path.Combine(modsDirectory, SanitizeFileName(file.FileName));
        if (File.Exists(targetPath))
        {
            MinecraftModArchiveInstaller.DisableConflicts(targetPath, modsDirectory);
            return true;
        }

        string temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".PCLDownloading";
        try
        {
            using HttpResponseMessage response = await downloader.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using (FileStream target = new(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             useAsync: true))
            {
                await source.CopyToAsync(target).ConfigureAwait(false);
            }

            MinecraftModArchiveInstaller.Install(temporaryPath, modsDirectory, Path.GetFileName(targetPath));
            return true;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static int GetDependencyMatchScore(
        CommunityResourceEntry entry,
        MinecraftMissingDependency dependency)
    {
        if (string.Equals(entry.Slug, dependency.ModId, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (string.Equals(entry.Title, dependency.Name, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static async Task<IReadOnlyList<string>> ReadRecentCrashLinesAsync(string gameDirectory)
    {
        List<string> paths = [];
        string latestLog = Path.Combine(gameDirectory, "logs", "latest.log");
        if (File.Exists(latestLog))
            paths.Add(latestLog);
        string crashDirectory = Path.Combine(gameDirectory, "crash-reports");
        if (Directory.Exists(crashDirectory))
        {
            string? latestCrash = Directory.EnumerateFiles(crashDirectory, "*.txt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (latestCrash is not null)
                paths.Add(latestCrash);
        }

        List<string> lines = [];
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FileInfo file = new(path);
            if (file.Length > 8L * 1024L * 1024L)
                continue;
            lines.AddRange(await File.ReadAllLinesAsync(path).ConfigureAwait(false));
        }
        return lines;
    }

    private sealed record RunningGameContext(
        LaunchInstanceInfo Instance,
        PageLaunchLeft LaunchPage,
        LauncherSettings Settings);

    private Task<bool> ConfirmJavaDownloadAsync(string versionLabel, CancellationToken cancellationToken)
    {
        // Always Post so we never show+await a modal on the same UI stack frame (deadlock/freeze).
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
            tcs);

        Dispatcher.UIThread.Post(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                registration.Dispose();
                return;
            }

            ShowConfirmDialog(
                "需要下载 Java",
                "启动该版本需要 Java " + versionLabel + "，但本机未找到兼容的 Java。\n\n是否由 PCL N 自动下载并安装官方运行时？",
                confirmed =>
                {
                    registration.Dispose();
                    tcs.TrySetResult(confirmed);
                },
                "下载并安装",
                "取消");
        });

        return tcs.Task;
    }

    private async Task<LoginProfileInfo> RefreshLaunchProfileAsync(
        LoginProfileInfo profile,
        CancellationToken cancellationToken)
    {
        if (profile.Kind != LaunchLoginProfileKind.Microsoft ||
            string.IsNullOrWhiteSpace(profile.RefreshToken))
        {
            return profile;
        }

        string clientId = MicrosoftMinecraftAuthService.ResolveClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            // Local/debug builds may intentionally omit the OAuth client id. A
            // previously authenticated Minecraft access token remains usable
            // until its own expiry; refreshing is only mandatory afterwards.
            if (IsAccessTokenUsable(profile.AccessToken))
            {
                Dispatcher.UIThread.Post(
                    () => _launchRight?.AppendLog("未配置 Microsoft Client ID，使用档案中仍有效的访问令牌启动。"),
                    DispatcherPriority.Background);
                return profile;
            }

            throw new InvalidOperationException(
                "缺少 Microsoft 登录配置，无法刷新正版登录状态。请提供 PCL_MS_CLIENT_ID 后重试。");
        }

        MicrosoftMinecraftLoginResult refreshed = await _microsoftAuthService
            .RefreshAsync(clientId, profile.RefreshToken, cancellationToken)
            .ConfigureAwait(false);
        return profile with
        {
            Username = refreshed.Username,
            Uuid = refreshed.Uuid,
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            SkinAddress = refreshed.SkinAddress ?? profile.SkinAddress,
            Info = refreshed.OwnsMinecraft ? "Microsoft 正版" : profile.Info
        };
    }

    private static bool IsAccessTokenUsable(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        string[] parts = accessToken.Split('.');
        if (parts.Length < 2)
            return true;
        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using JsonDocument document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (!document.RootElement.TryGetProperty("exp", out JsonElement expiration) ||
                !expiration.TryGetInt64(out long seconds))
            {
                return true;
            }

            return DateTimeOffset.FromUnixTimeSeconds(seconds) > DateTimeOffset.UtcNow.AddMinutes(2d);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private async Task IncrementInstanceLaunchCountAsync(LaunchInstanceInfo instance)
    {
        try
        {
            InstanceMetadata metadata = await InstanceMetadataStore.UpdateAsync(
                    instance.InstanceDirectory,
                    current => current with { LaunchCount = Math.Max(0, current.LaunchCount) + 1 })
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_instanceManagePage is not null &&
                    _managedInstance is not null &&
                    string.Equals(_managedInstance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _instanceManagePage.SetInstance(instance);
                }

                _launchRight?.AppendLog($"这是 {instance.Name} 的第 {metadata.LaunchCount.ToString(CultureInfo.InvariantCulture)} 次启动。");
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () => _launchRight?.AppendLog("记录启动次数失败：" + ex.Message),
                DispatcherPriority.Background);
        }
    }

    private static async Task<MinecraftProcessLaunchPlan> CreateLaunchPlanAsync(
        LaunchInstanceInfo instance,
        LoginProfileInfo profile,
        string javaExecutablePath,
        CancellationToken cancellationToken,
        string? worldName = null,
        InstanceMetadata? metadataOverride = null,
        string? serverAddress = null)
    {
        InstanceMetadata metadata = metadataOverride ??
            await InstanceMetadataStore.LoadAsync(instance.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        // Never hit settings store synchronously on the launch path (disk IO hitch).
        LauncherSettings settings = await Task.Run(
                LauncherSettingsPageBinder.LoadSettings,
                cancellationToken)
            .ConfigureAwait(false);
        int windowType = GetIntegerOption(settings, LauncherSettingKeys.LaunchArgumentWindowType, 1);
        (int width, int height) = GetWindowSize(settings);
        (string? authlibPath, string? authlibServer, string? authlibMetadata) =
            await ResolveAuthlibLaunchOptionsAsync(profile, cancellationToken).ConfigureAwait(false);

        return await MinecraftProcessLaunchService.CreatePlanAsync(
            new MinecraftProcessLaunchRequest
            {
                VersionId = instance.Name,
                VersionJsonPath = instance.VersionJsonPath,
                InstanceDirectory = instance.InstanceDirectory,
                MinecraftRootDirectory = GetMinecraftRootFromInstance(instance),
                PlayerName = profile.Username,
                PlayerUuid = string.IsNullOrWhiteSpace(profile.Uuid) ? Guid.NewGuid().ToString("N") : profile.Uuid,
                AccessToken = string.IsNullOrWhiteSpace(profile.AccessToken) ? "0" : profile.AccessToken,
                JavaExecutablePath = javaExecutablePath,
                MemoryMegabytes = ResolveLaunchMemoryMegabytes(instance, metadata, settings),
                Width = width,
                Height = height,
                Fullscreen = windowType == 0,
                IsolatedGameDirectory = metadata.InstanceIsolation,
                CustomJvmArguments = BuildInstanceJvmArguments(metadata, settings),
                CustomGameArguments = FirstNonEmpty(metadata.GameArguments, GetTextOption(settings, LauncherSettingKeys.LaunchAdvanceGame)),
                ClasspathHeadEntries = SplitClasspathHead(metadata.ClasspathHead),
                AuthlibInjectorPath = authlibPath,
                AuthlibServer = authlibServer,
                AuthlibPrefetchedMetadata = authlibMetadata,
                PreferredIpStack = GetPreferredIpStack(settings),
                Server = string.IsNullOrWhiteSpace(worldName)
                    ? FirstNonEmpty(serverAddress, metadata.ServerToEnter)
                    : null,
                ReleaseTime = TryReadReleaseTime(instance),
                HasOptiFine = HasOptiFine(instance),
                WorldName = worldName,
                LauncherName = "PCL-N",
                VersionType = FirstNonEmpty(
                    metadata.CustomInfo,
                    settings.GetTextOption("LaunchArgumentInfo", LauncherSettingDefaults.GetText("LaunchArgumentInfo"))) ?? "PCL-N"
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(string? Path, string? Server, string? Metadata)> ResolveAuthlibLaunchOptionsAsync(
        LoginProfileInfo profile,
        CancellationToken cancellationToken)
    {
        if (profile.Kind != LaunchLoginProfileKind.ThirdParty || string.IsNullOrWhiteSpace(profile.AuthServer))
            return (null, null, null);

        AuthlibInjectorService service = new();
        string authServer = AuthlibInjectorService.NormalizeAuthServer(profile.AuthServer);
        string authlibPath = await service.EnsureAsync(GetAuthlibInjectorCachePath(), cancellationToken)
            .ConfigureAwait(false);
        string metadata = await service.GetServerMetadataAsync(authServer, cancellationToken)
            .ConfigureAwait(false);
        return (authlibPath, authServer, metadata);
    }

    private static string GetAuthlibInjectorCachePath()
    {
        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "authlib-injector.jar");
    }

    private static async Task RunPreLaunchCommandAsync(
        string command,
        bool waitForExit,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        using Process? process = Process.Start(CreateShellStartInfo(command, workingDirectory));
        if (process is null)
            throw new InvalidOperationException("预启动命令未能启动。");

        if (!waitForExit)
            return;

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException("预启动命令执行失败，退出码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
            startInfo.ArgumentList.Add("/C");
        else
            startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }

    private static void ApplyProcessPriority(Process process, LauncherSettings settings)
    {
        try
        {
            process.PriorityClass = settings.GetIntegerOption(
                "LaunchArgumentPriority",
                LauncherSettingDefaults.GetInteger("LaunchArgumentPriority")) switch
            {
                0 => ProcessPriorityClass.AboveNormal,
                2 => ProcessPriorityClass.BelowNormal,
                3 => ProcessPriorityClass.High,
                4 => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
    }

    private void ApplyLauncherVisibility(Process process, LauncherSettings settings)
    {
        int visibility = settings.GetIntegerOption(
            "LaunchArgumentVisible",
            LauncherSettingDefaults.GetInteger("LaunchArgumentVisible"));
        switch (visibility)
        {
            case 0:
                Close();
                break;
            case 2:
                Hide();
                break;
            case 3:
                Hide();
                break;
            case 4:
                WindowState = WindowState.Minimized;
                break;
        }

        _ = RestoreAfterGameExitAsync(process, visibility);
    }

    private async Task RestoreAfterGameExitAsync(Process process, int visibility)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetGameRunningExtras(null);
                UpdateBackgroundVideoPlayback();
                if (visibility == 2)
                {
                    Close();
                    return;
                }

                if (visibility == 3 && !IsVisible)
                    Show();
                if (visibility == 4 && WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                if (visibility is 3 or 4)
                    Activate();
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetGameRunningExtras(null));
        }
        finally
        {
            process.Dispose();
        }
    }

    private static int ResolveLaunchMemoryMegabytes(
        LaunchInstanceInfo instance,
        InstanceMetadata metadata,
        LauncherSettings settings)
    {
        int memorySolution = metadata.MemorySolution;
        int customMemorySize = metadata.CustomMemorySize;
        if (memorySolution == 2)
        {
            memorySolution = GetIntegerOption(settings, LauncherSettingKeys.LaunchRamType, 0);
            customMemorySize = GetIntegerOption(settings, LauncherSettingKeys.LaunchRamCustom, 15);
        }

        return LaunchMemoryCalculator.ResolveMemoryMegabytes(
            new LaunchMemoryRequest
            {
                MemorySolution = memorySolution,
                CustomMemorySize = customMemorySize,
                MemoryInfo = new PCL.Platform.System.DefaultSystemInfoProvider().GetMemoryInfo(),
                Profile = GetMemoryProfile(instance, metadata),
                ModCount = CountModFiles(instance, metadata)
            });
    }

    private static LaunchMemoryProfile GetMemoryProfile(LaunchInstanceInfo instance, InstanceMetadata metadata)
    {
        if (CountModFiles(instance, metadata) > 0 || VersionJsonContains(instance, "fabric-loader", "forge", "neoforge", "quilt"))
            return LaunchMemoryProfile.Modded;
        return HasOptiFine(instance) ? LaunchMemoryProfile.OptiFine : LaunchMemoryProfile.Vanilla;
    }

    private static int CountModFiles(LaunchInstanceInfo instance, InstanceMetadata metadata)
    {
        HashSet<string> modPaths = new(StringComparer.OrdinalIgnoreCase);
        AddModFiles(modPaths, Path.Combine(instance.InstanceDirectory, "mods"));
        if (!metadata.InstanceIsolation)
            AddModFiles(modPaths, Path.Combine(GetMinecraftRootFromInstance(instance), "mods"));
        return modPaths.Count;
    }

    private static void AddModFiles(HashSet<string> modPaths, string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
            return;

        foreach (string file in Directory.EnumerateFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly))
            modPaths.Add(file);
    }

    private static (int Width, int Height) GetWindowSize(LauncherSettings settings)
    {
        int width = GetTextOptionAsInt(settings, LauncherSettingKeys.LaunchArgumentWindowWidth, 854);
        int height = GetTextOptionAsInt(settings, LauncherSettingKeys.LaunchArgumentWindowHeight, 480);
        return (Math.Clamp(width, 1, 9999), Math.Clamp(height, 1, 9999));
    }

    private static int GetIntegerOption(LauncherSettings settings, SettingKey key, int fallback) =>
        settings.GetIntegerOption(key, fallback);

    private static string GetTextOption(LauncherSettings settings, SettingKey key) =>
        settings.GetTextOption(key);

    private static int GetTextOptionAsInt(LauncherSettings settings, SettingKey key, int fallback) =>
        int.TryParse(GetTextOption(settings, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;

    private static MinecraftJvmIpPreference GetPreferredIpStack(LauncherSettings settings) =>
        GetIntegerOption(settings, LauncherSettingKeys.LaunchPreferredIpStack, 1) switch
        {
            0 => MinecraftJvmIpPreference.PreferV4,
            2 => MinecraftJvmIpPreference.PreferV6,
            _ => MinecraftJvmIpPreference.SystemDefault
        };

    private static string[] SplitClasspathHead(string classpathHead)
    {
        if (string.IsNullOrWhiteSpace(classpathHead))
            return [];

        return classpathHead.Split(
                ["\r\n", "\n", Path.PathSeparator.ToString()],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .ToArray();
    }

    private static string BuildInstanceJvmArguments(InstanceMetadata metadata, LauncherSettings settings)
    {
        string arguments = FirstNonEmpty(
            metadata.JvmArguments,
            GetTextOption(settings, LauncherSettingKeys.LaunchAdvanceJvm)) ?? string.Empty;
        if (!metadata.UseProxy ||
            settings.GetIntegerOption("SystemHttpProxyType", LauncherSettingDefaults.GetInteger("SystemHttpProxyType")) != 2 ||
            !Uri.TryCreate(
                settings.GetTextOption("SystemHttpProxy", LauncherSettingDefaults.GetText("SystemHttpProxy")),
                UriKind.Absolute,
                out Uri? proxy))
        {
            return arguments;
        }

        string proxyArguments = $"-Dhttp.proxyHost={proxy.Host} -Dhttp.proxyPort={proxy.Port} " +
                                $"-Dhttps.proxyHost={proxy.Host} -Dhttps.proxyPort={proxy.Port}";
        return string.IsNullOrWhiteSpace(arguments) ? proxyArguments : arguments.Trim() + " " + proxyArguments;
    }

    private static string ResolvePreferredJavaExecutablePath(bool forceConsole = false)
    {
        bool forceConsoleJava = forceConsole || LauncherSettingDefaults.GetBoolean("LaunchAdvanceNoJavaw");
        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            forceConsoleJava = forceConsole || settings.GetBooleanOption(
                "LaunchAdvanceNoJavaw",
                LauncherSettingDefaults.GetBoolean("LaunchAdvanceNoJavaw"));
            if (settings.TryGetTextOption(LauncherSettingKeys.LaunchSelectedJava, out string? selectedJava) &&
                !string.IsNullOrWhiteSpace(selectedJava) &&
                File.Exists(selectedJava))
            {
                if (OperatingSystem.IsWindows() && forceConsoleJava &&
                    string.Equals(Path.GetFileName(selectedJava), "javaw.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string java = Path.Combine(Path.GetDirectoryName(selectedJava) ?? string.Empty, "java.exe");
                    if (File.Exists(java))
                        return java;
                }

                if (!forceConsoleJava && OperatingSystem.IsWindows() &&
                    string.Equals(Path.GetFileName(selectedJava), "java.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string javaw = Path.Combine(Path.GetDirectoryName(selectedJava) ?? string.Empty, "javaw.exe");
                    if (File.Exists(javaw))
                        return javaw;
                }

                return selectedJava;
            }
        }
        catch (Exception)
        {
            // 启动路径读取失败时退回系统 PATH，避免设置文件损坏阻断启动。
        }

        return OperatingSystem.IsWindows() && !forceConsoleJava
            ? "javaw"
            : "java";
    }

    private void PromptRenameInstance(LaunchInstanceInfo instance)
    {
        ShowInputDialog(
            "重命名版本",
            "请输入新的版本名称。名称会同步到版本文件夹与 version.json。",
            instance.Name,
            "新的版本名称",
            result =>
            {
                if (string.IsNullOrWhiteSpace(result) || string.Equals(result, instance.Name, StringComparison.Ordinal))
                    return;
                RenameInstance(instance, result.Trim());
            });
    }

    private void PromptDeleteInstance(LaunchInstanceInfo instance)
    {
        ShowConfirmDialog(
            "删除版本",
            $"确定要删除“{instance.Name}”吗？\n\n该操作会删除版本文件夹：\n{instance.InstanceDirectory}",
            confirmed =>
            {
                if (confirmed)
                    DeleteInstance(instance);
            },
            "删除",
            "取消",
            isWarn: true);
    }

    private void PromptEditInstanceDescription(LaunchInstanceInfo instance)
    {
        _ = PromptEditInstanceDescriptionAsync(instance);
    }

    private async Task PromptEditInstanceDescriptionAsync(LaunchInstanceInfo instance)
    {
        InstanceMetadata metadata;
        try
        {
            metadata = await InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).ConfigureAwait(true);
        }
        catch
        {
            metadata = new InstanceMetadata();
        }

        ShowInputDialog(
            "编辑版本描述",
            "这段描述会显示在版本卡片上，用来区分不同配置或整合包。",
            metadata.Description,
            "默认描述",
            result =>
            {
                if (result is null)
                    return;

                _ = SaveInstanceDescriptionAsync(instance, result);
            });
    }

    private async Task SaveInstanceDescriptionAsync(LaunchInstanceInfo instance, string description)
    {
        try
        {
            await InstanceMetadataStore.UpdateAsync(
                    instance.InstanceDirectory,
                    metadata => metadata with { Description = description.Trim() })
                .ConfigureAwait(true);
            _instanceManagePage?.SetInstance(instance);
            _launchRight?.AppendLog($"已更新 {instance.Name} 的版本描述。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("保存失败", "未能保存版本描述。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task ToggleInstanceStarAsync(LaunchInstanceInfo instance)
    {
        try
        {
            InstanceMetadata metadata = await InstanceMetadataStore.UpdateAsync(
                    instance.InstanceDirectory,
                    current => current with { IsStarred = !current.IsStarred })
                .ConfigureAwait(true);
            _instanceManagePage?.SetInstance(instance);
            _launchRight?.AppendLog(metadata.IsStarred
                ? $"已收藏版本 {instance.Name}。"
                : $"已取消收藏版本 {instance.Name}。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("收藏失败", "未能更新收藏状态。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task ExportLaunchScriptAsync(LaunchInstanceInfo instance)
    {
        LoginProfileInfo? profile = _loginProfiles.FirstOrDefault();
        if (profile is null)
        {
            SelectNavRoute(LaunchRoute, animate: true);
            _launchLeft?.PageChangeToLogin();
            ShowTextDialog("请选择账户档案", "导出启动脚本前需要先选择或创建一个账户档案。");
            return;
        }

        try
        {
            string defaultExtension = OperatingSystem.IsWindows() ? ".bat" : ".sh";
            string suggestedFileName = "启动 " + SanitizeFileName(instance.Name) + defaultExtension;
            string targetPath = await PickSaveFilePathAsync(
                    "导出启动脚本",
                    suggestedFileName,
                    OperatingSystem.IsWindows()
                        ? new FilePickerFileType("Windows 批处理") { Patterns = ["*.bat", "*.cmd"] }
                        : new FilePickerFileType("Shell 脚本") { Patterns = ["*.sh"] })
                .ConfigureAwait(true)
                ?? Path.Combine(GetDesktopOrBaseDirectory(), suggestedFileName);

            InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(instance.InstanceDirectory)
                .ConfigureAwait(true);
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            string javaPath = await MinecraftLaunchCoordinator.ResolveJavaExecutableAsync(
                    new MinecraftLaunchCoordinatorRequest
                    {
                        Instance = instance,
                        Profile = profile,
                        Metadata = metadata,
                        Settings = settings,
                        MinecraftRootDirectory = GetMinecraftRootFromInstance(instance),
                        Report = static _ => { },
                        RefreshProfileAsync = static (current, _) => Task.FromResult(current),
                        CreatePlanAsync = CreateLaunchPlanAsync,
                        RunPreLaunchCommandAsync = RunPreLaunchCommandAsync,
                        ApplyProcessPriority = ApplyProcessPriority
                    },
                    CancellationToken.None)
                .ConfigureAwait(true);
            MinecraftProcessLaunchPlan plan = await CreateLaunchPlanAsync(
                    instance,
                    profile,
                    javaPath,
                    CancellationToken.None,
                    metadataOverride: metadata)
                .ConfigureAwait(true);
            await MinecraftLaunchScriptService.SaveAsync(
                    new MinecraftLaunchScriptRequest
                    {
                        LaunchPlan = plan,
                        TargetPath = targetPath
                    })
                .ConfigureAwait(true);
            ShowTextDialog("导出完成", "启动脚本已保存到：\n" + targetPath);
        }
        catch (Exception ex)
        {
            ShowTextDialog("导出失败", "未能导出启动脚本。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task TestLaunchFromInstancePageAsync(LaunchInstanceInfo instance)
    {
        SelectNavRoute(LaunchRoute, animate: true);
        if (_launchLeft is null)
            return;

        if (_launchLeft.Instances.Count > 0)
            _launchLeft.SetInstances(_launchLeft.Instances, instance);
        await StartMinecraftAsync(_launchLeft, instance).ConfigureAwait(true);
    }

    private async Task RepairInstanceFilesAsync(LaunchInstanceInfo instance)
    {
        string taskId = CreateTaskId("repair", instance.InstanceDirectory);
        using CancellationTokenSource cancellation = RegisterTrackedTask(taskId);
        string taskTitle = "修复 " + instance.Name;
        ActivateTaskManagerPage(animate: true);
        TrackTaskBegin(taskId, taskTitle, "准备检查版本文件");

        Progress<MinecraftInstallProgress> progress = new(update => TrackInstallProgress(taskId, taskTitle, update));
        try
        {
            await _minecraftInstallService.RepairAsync(
                    new MinecraftRepairRequest
                    {
                        VersionId = instance.Name,
                        VersionJsonPath = instance.VersionJsonPath,
                        MinecraftRootDirectory = GetMinecraftRootFromInstance(instance),
                        InstanceDirectory = instance.InstanceDirectory,
                        PreferOfficialSource = true
                    },
                    progress,
                    cancellation.Token)
                .ConfigureAwait(true);
            TrackTaskFinished(taskId, taskTitle, "文件检查完成");
            _launchRight?.AppendLog($"{instance.Name} 文件检查完成。");
        }
        catch (OperationCanceledException)
        {
            TrackTaskFailed(taskId, taskTitle, "修复已取消。", canceled: true);
        }
        catch (Exception ex)
        {
            TrackTaskFailed(taskId, taskTitle, ex.Message, canceled: false);
            ShowTextDialog("修复失败", "未能修复版本文件。\n\n详细信息：" + ex.Message);
        }
        finally
        {
            UnregisterTrackedTask(taskId, cancellation);
        }
    }

    private void PromptResetInstanceSettings(LaunchInstanceInfo instance)
    {
        ShowConfirmDialog(
            "初始化版本设置",
            $"确定要初始化“{instance.Name}”的本地设置吗？\n\n该操作不会删除游戏文件，只会清除 PCL N 保存的版本描述、收藏、分类和文件校验偏好。",
            confirmed =>
            {
                if (confirmed)
                    _ = ResetInstanceSettingsAsync(instance);
            },
            "初始化",
            "取消",
            isWarn: true);
    }

    private async Task ResetInstanceSettingsAsync(LaunchInstanceInfo instance)
    {
        try
        {
            await InstanceMetadataStore.SaveAsync(instance.InstanceDirectory, new InstanceMetadata())
                .ConfigureAwait(true);
            _instanceManagePage?.SetInstance(instance);
            _launchRight?.AppendLog($"已初始化 {instance.Name} 的版本设置。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("初始化失败", "未能初始化版本设置。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task PatchInstanceCoreAsync(LaunchInstanceInfo instance)
    {
        try
        {
            string? patchPath = await PickOpenFilePathAsync(
                    "选择要补全到核心的文件",
                    new FilePickerFileType("Java 压缩包") { Patterns = ["*.jar", "*.zip"] })
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(patchPath))
                return;

            string targetJar = Path.Combine(instance.InstanceDirectory, instance.Name + ".jar");
            int count = await MinecraftJarPatchService.ApplyAsync(
                    new MinecraftJarPatchRequest
                    {
                        TargetJarPath = targetJar,
                        PatchArchivePath = patchPath
                    })
                .ConfigureAwait(true);
            await InstanceMetadataStore.UpdateAsync(
                    instance.InstanceDirectory,
                    metadata => metadata with { DisableAssetVerification = true })
                .ConfigureAwait(true);
            _instanceManagePage?.SetInstance(instance);
            ShowTextDialog("补全完成", $"已向核心文件写入 {count} 个文件。\n\n为避免补丁被校验覆盖，已自动关闭该版本的资源校验偏好。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("补全失败", "未能补全核心文件。\n\n详细信息：" + ex.Message);
        }
    }

    private void RenameInstance(LaunchInstanceInfo instance, string newName)
    {
        try
        {
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowTextDialog("名称不可用", "版本名称不能包含系统保留字符。");
                return;
            }

            string parent = Directory.GetParent(instance.InstanceDirectory)?.FullName
                            ?? throw new InvalidOperationException("无法确定版本目录。");
            string newDirectory = Path.Combine(parent, newName);
            if (Directory.Exists(newDirectory))
            {
                ShowTextDialog("名称已存在", "已经存在同名版本，请换一个名称。");
                return;
            }

            Directory.Move(instance.InstanceDirectory, newDirectory);
            RenameFileIfExists(Path.Combine(newDirectory, instance.Name + ".json"), Path.Combine(newDirectory, newName + ".json"));
            RenameFileIfExists(Path.Combine(newDirectory, instance.Name + ".jar"), Path.Combine(newDirectory, newName + ".jar"));
            UpdateVersionJsonId(Path.Combine(newDirectory, newName + ".json"), newName);
            _launchRight?.AppendLog($"已将版本 {instance.Name} 重命名为 {newName}。");
            _ = RefreshInstancesAfterManagementAsync(newDirectory);
        }
        catch (Exception ex)
        {
            ShowTextDialog("重命名失败", "未能重命名版本。\n\n详细信息：" + ex.Message);
        }
    }

    private void DeleteInstance(LaunchInstanceInfo instance)
    {
        try
        {
            Directory.Delete(instance.InstanceDirectory, recursive: true);
            _launchRight?.AppendLog($"已删除版本 {instance.Name}。");
            _ = RefreshInstancesAfterManagementAsync(null);
            SelectNavRoute(LaunchRoute, animate: true);
        }
        catch (Exception ex)
        {
            ShowTextDialog("删除失败", "未能删除版本。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task RefreshInstancesAfterManagementAsync(string? selectedDirectory)
    {
        if (_launchLeft is null)
            return;

        await _launchLeft.RefreshInstancesAsync().ConfigureAwait(true);
        LaunchInstanceInfo? selected = string.IsNullOrWhiteSpace(selectedDirectory)
            ? _launchLeft.SelectedInstance
            : _launchLeft.Instances.FirstOrDefault(instance =>
                string.Equals(instance.InstanceDirectory, selectedDirectory, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
            _launchLeft.SetInstances(_launchLeft.Instances, selected);
        _instanceSelectPage?.SetInstances(_launchLeft.Instances, selected);
        if (selected is not null)
            _instanceManagePage?.SetInstance(selected);
    }

    private static void RenameFileIfExists(string oldPath, string newPath)
    {
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath, overwrite: true);
    }

    private static void UpdateVersionJsonId(string jsonPath, string newName)
    {
        if (!File.Exists(jsonPath))
            return;

        using FileStream stream = new(
            jsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 16 * 1024,
            useAsync: false);
        JsonNode? node = JsonNode.Parse(stream);
        if (node is not JsonObject json)
            return;

        json["id"] = newName;
        string tempPath = jsonPath + ".tmp";
        using (FileStream output = new(
                   tempPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.Read,
                   bufferSize: 16 * 1024,
                   useAsync: false))
        {
            using Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true });
            json.WriteTo(writer);
            writer.Flush();
        }

        File.Move(tempPath, jsonPath, overwrite: true);
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowTextDialog("无法打开文件夹", ex.Message);
        }
    }

    private void OpenExistingPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                ShowTextDialog("无法打开", "目标文件不存在，可能已经被移动或删除。");
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowTextDialog("无法打开", ex.Message);
        }
    }

    private async Task ExportInstanceZipAsync(LaunchInstanceInfo instance)
    {
        await ExportInstanceZipAsync(
                new InstanceExportPageRequest(
                    instance,
                    instance.Name,
                    "1.0.0",
                    [],
                    IncludeLauncherFiles: false,
                    IncludeLauncherCustom: false,
                    IncludeBundleFiles: false,
                    ModrinthUploadMode: false))
            .ConfigureAwait(true);
    }

    private async Task ExportInstanceZipAsync(InstanceExportPageRequest request)
    {
        try
        {
            LaunchInstanceInfo instance = request.Instance;
            string fileName = $"PCLN-{SanitizeFileName(request.PackageName)}-{SanitizeFileName(request.PackageVersion)}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            string targetPath = Path.Combine(GetDesktopOrBaseDirectory(), fileName);
            _launchRight?.AppendLog($"正在导出版本 {instance.Name}。");
            await InstanceExportService.ExportAsync(
                    new InstanceExportRequest
                    {
                        InstanceDirectory = instance.InstanceDirectory,
                        GameDirectory = GetMinecraftRootFromInstance(instance),
                        TargetArchivePath = targetPath,
                        Rules = request.Rules
                    })
                .ConfigureAwait(true);
            ShowTextDialog("导出完成", "版本已导出到：\n" + targetPath);
            _launchRight?.AppendLog($"版本已导出到 {targetPath}。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("导出失败", "未能导出版本。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task ExportInstanceRulesConfigAsync(IReadOnlyList<string> rules)
    {
        try
        {
            string targetPath = await PickSaveFilePathAsync(
                    "导出整合包规则配置",
                    $"PCLN-ExportRules-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                    new FilePickerFileType("Text")
                    {
                        Patterns = ["*.txt"]
                    })
                .ConfigureAwait(true)
                ?? Path.Combine(GetDesktopOrBaseDirectory(), $"PCLN-ExportRules-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            await File.WriteAllLinesAsync(targetPath, rules).ConfigureAwait(true);
            ShowTextDialog("导出配置完成", "规则配置已导出到：\n" + targetPath);
        }
        catch (Exception ex)
        {
            ShowTextDialog("导出配置失败", "未能导出规则配置。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task ImportInstanceRulesConfigAsync(PageInstanceExportRight page)
    {
        try
        {
            string? sourcePath = await PickOpenFilePathAsync(
                    "导入整合包规则配置",
                    new FilePickerFileType("Text")
                    {
                        Patterns = ["*.txt", "*.cfg"]
                    })
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;

            string[] rules = await File.ReadAllLinesAsync(sourcePath).ConfigureAwait(true);
            page.ApplyRulesOverride(rules);
            ShowTextDialog("已导入配置", "导出内容将按配置文件中的规则生成。你可以点击“重置”恢复页面选项。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("导入配置失败", "未能导入规则配置。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task<string?> PickSaveFilePathAsync(
        string title,
        string suggestedFileName,
        FilePickerFileType fileType)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanSave != true)
            return null;

        IStorageFile? file = await storage.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = suggestedFileName,
                    FileTypeChoices = [fileType],
                    ShowOverwritePrompt = true
                })
            .ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFilePathAsync(
        string title,
        FilePickerFileType fileType)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanOpen != true)
            return null;

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = [fileType]
                })
            .ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static string ResolveInstanceJavaExecutablePath(InstanceMetadata metadata, bool forceConsole = false)
    {
        if (metadata.JavaSelectionMode == 2 &&
            !string.IsNullOrWhiteSpace(metadata.SelectedJavaPath) &&
            File.Exists(metadata.SelectedJavaPath))
        {
            string selectedJava = metadata.SelectedJavaPath;
            if (OperatingSystem.IsWindows())
            {
                string directory = Path.GetDirectoryName(selectedJava) ?? string.Empty;
                if (forceConsole && string.Equals(Path.GetFileName(selectedJava), "javaw.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string consoleJava = Path.Combine(directory, "java.exe");
                    if (File.Exists(consoleJava))
                        return consoleJava;
                }
                if (!forceConsole && string.Equals(Path.GetFileName(selectedJava), "java.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string windowJava = Path.Combine(directory, "javaw.exe");
                    if (File.Exists(windowJava))
                        return windowJava;
                }
            }
            return selectedJava;
        }

        if (metadata.JavaSelectionMode == 1)
            return OperatingSystem.IsWindows() && !forceConsole ? "javaw" : "java";
        return ResolvePreferredJavaExecutablePath(forceConsole);
    }

    private async Task<string?> PickOpenFolderPathAsync(string title)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanPickFolder != true)
            return null;

        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private static string GetDesktopOrBaseDirectory()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.IsNullOrWhiteSpace(desktop) ? AppContext.BaseDirectory : desktop;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Minecraft" : sanitized;
    }

    private string GetDefaultMinecraftRoot()
    {
        if (!string.IsNullOrWhiteSpace(_launchLeft?.MinecraftRootDirectory))
            return _launchLeft.MinecraftRootDirectory;

        IReadOnlyList<string> roots = LaunchInstanceDiscovery.GetCandidateRoots();
        foreach (string root in roots)
        {
            if (Directory.Exists(root))
                return root;
        }

        return roots.Count > 0 ? roots[0] : Path.Combine(AppContext.BaseDirectory, ".minecraft");
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo versionsDirectory = versionDirectory.Parent
            ?? throw new InvalidOperationException("无法确定 versions 目录。");
        return versionsDirectory.Parent?.FullName
               ?? throw new InvalidOperationException("无法确定 Minecraft 根目录。");
    }

    private static string ReadMinecraftVersionId(LaunchInstanceInfo instance)
    {
        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string? inheritsFrom = TryReadJsonString(root, "inheritsFrom");
            if (!string.IsNullOrWhiteSpace(inheritsFrom))
                return inheritsFrom;

            string? id = TryReadJsonString(root, "id");
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }
        catch (Exception)
        {
        }

        return instance.Name;
    }

    private static DateTimeOffset? TryReadReleaseTime(LaunchInstanceInfo instance)
    {
        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            string? releaseTime = TryReadJsonString(document.RootElement, "releaseTime");
            return DateTimeOffset.TryParse(
                releaseTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset value)
                ? value
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadJsonString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool HasOptiFine(LaunchInstanceInfo instance)
    {
        if (VersionJsonContains(instance, "optifine"))
            return true;

        try
        {
            return Directory.EnumerateFiles(instance.InstanceDirectory, "*", SearchOption.TopDirectoryOnly)
                .Any(static file => Path.GetFileName(file).Contains("optifine", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool VersionJsonContains(LaunchInstanceInfo instance, params string[] needles)
    {
        bool hasNeedle = false;
        int overlapLength = 0;
        foreach (string needle in needles)
        {
            if (string.IsNullOrWhiteSpace(needle))
                continue;

            hasNeedle = true;
            overlapLength = Math.Max(overlapLength, needle.Length - 1);
        }

        if (!hasNeedle)
            return false;

        try
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(8 * 1024 + overlapLength);
            try
            {
                using StreamReader reader = new(
                    new FileStream(
                        instance.VersionJsonPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 16 * 1024,
                        useAsync: false),
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 8 * 1024,
                    leaveOpen: false);

                int carryLength = 0;
                while (true)
                {
                    int read = reader.ReadBlock(buffer, carryLength, buffer.Length - carryLength);
                    if (read == 0)
                        return false;

                    ReadOnlySpan<char> current = buffer.AsSpan(0, carryLength + read);
                    foreach (string needle in needles)
                    {
                        if (!string.IsNullOrWhiteSpace(needle) &&
                            current.Contains(needle, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    carryLength = Math.Min(overlapLength, current.Length);
                    if (carryLength > 0)
                        current[^carryLength..].CopyTo(buffer);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_registeredPluginPageSurfaceId is { } pageSurface)
            DesktopHostUiComposition.Instance.UnregisterTarget(pageSurface);
        DesktopHostUiComposition.Instance.UnregisterSlot("pcl.navigation.main", "items.after-download");
        DesktopHostUiComposition.Instance.UnregisterTarget("pcl.navigation.main");
        DesktopHostUiComposition.Instance.UnregisterTarget("pcl.window.main");
        DesktopHostNotifications.Instance.Detach(OnPluginHostNotification);
        DesktopHost.Current.Navigation.Changed -= NavigationRegistryChanged;
        DesktopHostNavigation.Instance.Detach(NavigateToPluginRoute);
        LauncherSettingsPageBinder.SettingsChanged -= LauncherSettingsChanged;
        AvaloniaLocalizationManager.LanguageChanged -= LocalizationChanged;
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        _windowStateSubscription.Dispose();
        if (this.FindControl<MediaElement>("VideoBack") is { } video)
        {
            video.MediaFailed -= VideoFailed;
            video.Dispose();
        }
        _titleLogoBitmap?.Dispose();
        _titleLogoBitmap = null;
        _homepageLoadCancellation?.Cancel();
        _homepageLoadCancellation?.Dispose();
        _homepageLoadCancellation = null;
        DisposeTrackedTasks();
        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        _microsoftLoginCancellation?.Cancel();
        _microsoftLoginCancellation?.Dispose();
        (_launchLeft as IDisposable)?.Dispose();
        _launchRight?.Dispose();
        _communityRight?.Dispose();
        _communityDetail?.Dispose();
        _instanceSelectPage?.Dispose();
        _setupRight?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void AnimateMsgBackground(BlurBorder background, byte targetAlpha, Action? completed = null)
    {
        ModAnimation.AniStart(
        new List<ModAnimation.AniData>
        {
            ModAnimation.AaColor(
                background,
                Border.BackgroundProperty,
                Color.FromArgb(targetAlpha, 0, 0, 0),
                200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                background.Background = new SolidColorBrush(Color.FromArgb(targetAlpha, 0, 0, 0));
                completed?.Invoke();
            }, after: true)
        }, "MyMsg Background");
    }

    private PageLoginMs CreateMicrosoftLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginMs page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.PurchaseRequested += (_, _) => OpenExternalUrl(
            "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        page.WebsiteRequested += (_, _) => OpenExternalUrl("https://www.minecraft.net/zh-hans");
        page.LoginRequested += (_, _) => _ = StartMicrosoftLoginAsync(page, launchPage);
        return page;
    }

    private async Task StartMicrosoftLoginAsync(PageLoginMs page, PageLaunchLeft launchPage)
    {
        string clientId = MicrosoftMinecraftAuthService.ResolveClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            page.FinishLogin();
            _launchRight?.AppendLog("缺少 Microsoft 登录配置：PCL_MS_CLIENT_ID。");
            ShowTextDialog(
                "Microsoft 登录配置缺失",
                "缺少 Microsoft 登录配置。请为启动器提供 PCL_MS_CLIENT_ID（Microsoft OAuth 公共客户端 ID）后重试。",
                "知道了");
            return;
        }

        _microsoftLoginCancellation?.Cancel();
        _microsoftLoginCancellation?.Dispose();
        _microsoftLoginCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _microsoftLoginCancellation.Token;
        MyMsgLogin? dialog = null;
        try
        {
            _launchRight?.AppendLog("正在申请 Microsoft 设备登录代码。");
            page.UpdateProgress(0.04d);
            MicrosoftDeviceCodeInfo deviceCode = await _microsoftAuthService
                .RequestDeviceCodeAsync(clientId, cancellationToken)
                .ConfigureAwait(true);
            page.UpdateProgress(0.08d);
            dialog = new MyMsgLogin
            {
                Title = "Microsoft 正版档案登录",
                Caption = deviceCode.Message + $"\n\n授权码：{deviceCode.UserCode}",
                UserCode = deviceCode.UserCode,
                Website = FirstNonEmpty(deviceCode.VerificationUriComplete, deviceCode.VerificationUri)
            };
            ShowLoginDialog(dialog, () => _microsoftLoginCancellation?.Cancel());

            Progress<double> progress = new(value => page.UpdateProgress(value));
            MicrosoftMinecraftLoginResult result = await _microsoftAuthService
                .CompleteDeviceLoginAsync(clientId, deviceCode, progress, cancellationToken)
                .ConfigureAwait(true);
            if (dialog.Parent is not null)
                dialog.CloseLikeWpf();

            LoginProfileInfo profile = new(
                result.Username,
                result.OwnsMinecraft ? "Microsoft 正版" : "Microsoft 账户",
                LaunchLoginProfileKind.Microsoft,
                result.Uuid,
                SvgIcon: "lucide/badge-check",
                SkinAddress: result.SkinAddress,
                AccessToken: result.AccessToken,
                RefreshToken: result.RefreshToken);
            AddOrUpdateLoginProfile(profile);
            _loginProfilePage?.SetProfiles(_loginProfiles, profile);
            _loginProfileSkinPage?.SetProfile(profile);
            launchPage.SetSelectedProfilePresent(true);
            launchPage.RefreshPage(anim: true);
            SaveProfilesInBackground("保存 Microsoft 正版档案");
            _launchRight?.AppendLog($"Microsoft 登录成功，已选中档案 {profile.Username}。");
            ShowTextDialog("登录成功", $"已添加并选中正版档案 {profile.Username}。", "知道了");
        }
        catch (OperationCanceledException)
        {
            _launchRight?.AppendLog("Microsoft 登录已取消。");
        }
        catch (Exception ex)
        {
            if (dialog?.Parent is not null)
                dialog.CloseLikeWpf();
            _launchRight?.AppendLog("Microsoft 登录失败：" + ex.Message);
            ShowTextDialog("Microsoft 登录失败", ex.Message, "知道了");
        }
        finally
        {
            page.FinishLogin();
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private PageLoginAuth CreateAuthLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginAuth page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.ValidationFailed += (_, message) => _launchRight?.AppendLog(message);
        page.RegisterLinkRequested += (_, isRegisterMode) => OpenAuthAccountPage(page.CurrentServer, isRegisterMode);
        page.LoginRequested += (_, request) => _ = StartThirdPartyAuthLoginAsync(page, request);
        return page;
    }

    private async Task StartThirdPartyAuthLoginAsync(PageLoginAuth page, AuthLoginRequest request)
    {
        _launchRight?.AppendLog($"正在连接第三方认证服务器：{request.Server}");
        page.UpdateProgress(0.12d);
        try
        {
            ThirdPartyAuthLoginResult result = await _thirdPartyAuthService
                .AuthenticateAsync(
                    new ThirdPartyAuthLoginRequest(request.Server, request.Username, request.Password))
                .ConfigureAwait(true);
            page.UpdateProgress(0.8d);
            string skinAddress = MySkin.ResolveSkinAddress(
                skinAddress: null,
                uuid: result.Uuid,
                authServer: result.AuthServer);
            LoginProfileInfo profile = new(
                result.Username,
                $"Authlib-Injector · {result.AuthServerDisplayName}",
                LaunchLoginProfileKind.ThirdParty,
                result.Uuid,
                SvgIcon: "lucide/key-round",
                SkinAddress: string.IsNullOrWhiteSpace(skinAddress) ? null : skinAddress,
                AuthServer: result.AuthServer,
                AccessToken: result.AccessToken);
            AddOrUpdateLoginProfile(profile);
            _loginProfilePage?.SetProfiles(_loginProfiles, profile);
            _loginProfileSkinPage?.SetProfile(profile);
            _launchLeft?.SetSelectedProfilePresent(true);
            _launchLeft?.RefreshPage(anim: true);
            SaveProfilesInBackground("保存第三方认证档案");
            _launchRight?.AppendLog($"第三方认证登录成功，已选中档案 {profile.Username}。");
            ShowTextDialog("登录成功", $"已添加并选中 {profile.Username}。", "知道了");
        }
        catch (Exception ex)
        {
            ShowTextDialog("第三方登录失败", ex.Message, "知道了");
            _launchRight?.AppendLog("第三方认证登录失败：" + ex.Message);
        }
        finally
        {
            page.FinishLogin();
        }
    }

    private void OpenAuthAccountPage(string server, bool isRegisterMode)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            ShowTextDialog("请先填写认证服务器", "填写认证服务器地址后，启动器才能打开对应的注册或找回密码页面。", "知道了");
            return;
        }

        try
        {
            string authServer = ThirdPartyAuthService.NormalizeYggdrasilServer(server);
            string root = authServer;
            const string apiSuffix = "/api/yggdrasil";
            if (root.EndsWith(apiSuffix, StringComparison.OrdinalIgnoreCase))
                root = root[..^apiSuffix.Length];
            OpenExternalUrl(root.TrimEnd('/') + (isRegisterMode ? "/auth/register" : "/auth/forgot"));
        }
        catch (Exception ex)
        {
            ShowTextDialog("认证服务器地址无效", ex.Message, "知道了");
        }
    }

    private PageLoginOffline CreateOfflineLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginOffline page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.ValidationFailed += (_, message) => _launchRight?.AppendLog(message);
        page.ProfileCreateRequested += (_, request) =>
        {
            string info = string.IsNullOrWhiteSpace(request.SkinSourceUuid)
                ? "离线登录"
                : $"离线登录 · 借用 {request.SkinSourceName}";
            LoginProfileInfo profile = new(
                request.Username,
                info,
                LaunchLoginProfileKind.Offline,
                Uuid: request.Uuid,
                SvgIcon: "lucide/user");

            _loginProfiles.RemoveAll(existing =>
                existing.Kind == LaunchLoginProfileKind.Offline &&
                string.Equals(existing.Uuid, profile.Uuid, StringComparison.OrdinalIgnoreCase));
            _loginProfiles.Insert(0, profile);
            _loginProfilePage?.SetProfiles(_loginProfiles, profile);
            launchPage.SetSelectedProfilePresent(true);
            launchPage.RefreshPage(anim: true);
            SaveProfilesInBackground("保存离线账户档案");
            _launchRight?.AppendLog($"已创建并选中离线档案 {profile.Username}。");
        };
        return page;
    }

    private void AddOrUpdateLoginProfile(LoginProfileInfo profile)
    {
        int existingIndex = _loginProfiles.FindIndex(existing => IsSameProfile(existing, profile));
        if (existingIndex >= 0)
            _loginProfiles.RemoveAt(existingIndex);
        _loginProfiles.Insert(0, profile);
    }

    private void ReplaceLoginProfile(LoginProfileInfo original, LoginProfileInfo updated)
    {
        int existingIndex = _loginProfiles.FindIndex(existing => IsSameProfile(existing, original));
        if (existingIndex >= 0)
            _loginProfiles.RemoveAt(existingIndex);
        _loginProfiles.Insert(0, updated);
    }

    private async Task ImportProfilesAsync(PageLoginProfile page, PageLaunchLeft launchPage)
    {
        try
        {
            string? sourcePath = await PickOpenFilePathAsync(
                    "导入账户档案",
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"]
                    })
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;

            using LaunchProfileStore store = new(sourcePath);
            LaunchProfileLoadResult result = await store.LoadAsync().ConfigureAwait(true);
            List<LoginProfileInfo> imported = result.Profiles.Profiles
                .Select(ToLoginProfileInfo)
                .ToList();
            int added = 0;
            int updated = 0;
            foreach (LoginProfileInfo profile in imported)
            {
                int existingIndex = _loginProfiles.FindIndex(existing => IsSameProfile(existing, profile));
                if (existingIndex >= 0)
                {
                    _loginProfiles[existingIndex] = profile;
                    updated++;
                }
                else
                {
                    _loginProfiles.Add(profile);
                    added++;
                }
            }

            page.SetProfiles(_loginProfiles, _loginProfiles.FirstOrDefault());
            launchPage.SetSelectedProfilePresent(_loginProfiles.Count > 0);
            SaveProfilesInBackground("导入账户档案");
            ShowTextDialog("导入完成", $"已导入 {added} 个新档案，更新 {updated} 个已有档案。");
        }
        catch (Exception ex)
        {
            ShowTextDialog("导入失败", "未能导入账户档案。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task ExportProfilesAsync()
    {
        try
        {
            string? targetPath = await PickSaveFilePathAsync(
                    "导出账户档案",
                    $"PCLN-Profiles-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"]
                    })
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(targetPath))
                return;

            using LaunchProfileStore store = new(targetPath);
            await store.SaveAsync(
                    new LaunchProfileSet
                    {
                        Profiles = _loginProfiles.Select(ToLaunchProfile).ToArray()
                    })
                .ConfigureAwait(true);
            ShowTextDialog("导出完成", "账户档案已导出到：\n" + targetPath);
        }
        catch (Exception ex)
        {
            ShowTextDialog("导出失败", "未能导出账户档案。\n\n详细信息：" + ex.Message);
        }
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            using LaunchProfileStore store = CreateLaunchProfileStore();
            LaunchProfileLoadResult result = await store.LoadAsync().ConfigureAwait(false);
            List<LoginProfileInfo> profiles = result.Profiles.Profiles
                .Select(ToLoginProfileInfo)
                .ToList();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _loginProfiles.Clear();
                _loginProfiles.AddRange(profiles);
                _loginProfilePage?.SetProfiles(_loginProfiles);
                _launchLeft?.SetSelectedProfilePresent(_loginProfiles.Count > 0);
                if (result.WasRecovered)
                    _launchRight?.AppendLog($"账户档案配置已重置，损坏文件已备份到：{result.BackupPath}");
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _launchRight?.AppendLog("读取账户档案失败：" + ex.Message));
        }
    }

    private static bool IsSameProfile(LoginProfileInfo left, LoginProfileInfo right)
    {
        if (!string.IsNullOrWhiteSpace(left.Uuid) && !string.IsNullOrWhiteSpace(right.Uuid))
            return string.Equals(left.Uuid, right.Uuid, StringComparison.OrdinalIgnoreCase);

        return left.Kind == right.Kind &&
               string.Equals(left.Username, right.Username, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.AuthServer, right.AuthServer, StringComparison.OrdinalIgnoreCase);
    }

    private void SaveProfilesInBackground(string action)
    {
        LaunchProfileSet snapshot = new()
        {
            Profiles = _loginProfiles.Select(ToLaunchProfile).ToArray()
        };
        _ = Task.Run(async () =>
        {
            try
            {
                using LaunchProfileStore store = CreateLaunchProfileStore();
                await store.SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _launchRight?.AppendLog(action + "失败：" + ex.Message));
            }
        });
    }

    private static LaunchProfileStore CreateLaunchProfileStore() =>
        new(CreateLaunchProfilePath());

    private static string CreateLaunchProfilePath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("PCLN_LAUNCH_PROFILES_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "launch-profiles.json");
    }

    private static LoginProfileInfo ToLoginProfileInfo(LaunchProfile profile) =>
        new(
            profile.Username,
            profile.Info,
            profile.Kind switch
            {
                LaunchProfileKind.Microsoft => LaunchLoginProfileKind.Microsoft,
                LaunchProfileKind.ThirdParty => LaunchLoginProfileKind.ThirdParty,
                _ => LaunchLoginProfileKind.Offline
            },
            profile.Uuid,
            profile.Logo,
            profile.SvgIcon,
            profile.SkinAddress,
            profile.AuthServer,
            profile.AccessToken,
            profile.RefreshToken);

    private static LaunchProfile ToLaunchProfile(LoginProfileInfo profile) =>
        new()
        {
            Username = profile.Username,
            Info = profile.Info,
            Kind = profile.Kind switch
            {
                LaunchLoginProfileKind.Microsoft => LaunchProfileKind.Microsoft,
                LaunchLoginProfileKind.ThirdParty => LaunchProfileKind.ThirdParty,
                _ => LaunchProfileKind.Offline
            },
            Uuid = profile.Uuid,
            Logo = profile.Logo,
            SvgIcon = profile.SvgIcon,
            SkinAddress = profile.SkinAddress,
            AuthServer = profile.AuthServer,
            AccessToken = profile.AccessToken,
            RefreshToken = profile.RefreshToken
        };

    private static string? NormalizeAuthServerUrl(string authServer)
    {
        if (string.IsNullOrWhiteSpace(authServer))
            return null;

        string trimmed = authServer.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    private static bool TryCreateHttpUri(string value, out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        uri = null;
        return false;
    }

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _launchRight?.AppendLog("无法打开浏览器：" + ex.Message);
        }
    }

    private DesktopMainPage CreatePlaceholderMainPage(string pageTitle) =>
        new(null, CreateLoadingPlaceholder(pageTitle));

    private static DesktopMainPage CreateLoadingMainPage(string pageTitle) =>
        new(null, CreateLoadingPlaceholder(pageTitle));

    private static Grid CreateLoadingPlaceholder(string pageTitle) =>
        new()
        {
            Children =
            {
                new MyLoading
                {
                    Name = "LoadMain",
                    Width = 220d,
                    Height = 120d,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Text = $"正在加载{pageTitle}页面"
                }
            }
        };

    private static Grid CreateTextPlaceholder(string pageTitle, string message) =>
        new()
        {
            Children =
            {
                new StackPanel
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Spacing = 12d,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = pageTitle,
                            FontSize = 19d,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#343d4a"))
                        },
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 420d,
                            Foreground = new SolidColorBrush(Color.Parse("#1370f3"))
                        }
                    }
                }
            }
        };

    private static NavigationPageDescriptor[] CreateNavigationPageMap(
        INavigationRegistry navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return navigation.Pages
            .Where(static page => page.Region == PageRegion.Main)
            .ToArray();
    }

    private void BuildMainNavigationItems()
    {
        if (this.FindControl<Panel>("PanTitleSelect") is not { } panel)
            return;

        panel.Children.Clear();
        for (int pageIndex = 0; pageIndex < _navigationPages.Length; pageIndex++)
        {
            NavigationPageDescriptor descriptor = _navigationPages[pageIndex];
            MyListItem item = new()
            {
                Name = $"BtnTitleSelect{pageIndex.ToString(CultureInfo.InvariantCulture)}",
                Title = descriptor.Title,
                Tag = descriptor.Route,
                Margin = pageIndex == 0 ? new Thickness(1d, 10d, 1d, 0d) : new Thickness(1d, 0d, 1d, 0d),
                FontSize = 12d,
                Type = MyListItem.CheckType.RadioBox,
                LogoScale = 0.8d,
                SvgIcon = string.IsNullOrWhiteSpace(descriptor.Icon) ? "lucide/circle" : descriptor.Icon
            };
            item.Click += BtnNavItem_Click;
            panel.Children.Add(item);
        }
    }

    private void LauncherSettingsChanged(LauncherSettings settings)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyRuntimeSettings(settings));
            return;
        }

        ApplyRuntimeSettings(settings);
    }

    private void LocalizationChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshNavigationText);
            return;
        }

        RefreshNavigationText();
    }

    private void ApplyRuntimeSettings(LauncherSettings settings)
    {
        _targetWindowOpacity = Math.Clamp(
            0.4d + settings.GetIntegerOption(
                "UiLauncherTransparent",
                LauncherSettingDefaults.GetInteger("UiLauncherTransparent")) / 1000d,
            0.4d,
            1d);
        if (_isMainWindowOpened)
            Opacity = _targetWindowOpacity;

        CanResize = !settings.GetBooleanOption(
            "UiLockWindowSize",
            LauncherSettingDefaults.GetBoolean("UiLockWindowSize"));
        ModAnimation.Configure(
            settings.GetIntegerOption("UiAniFPS", LauncherSettingDefaults.GetInteger("UiAniFPS")) + 1,
            settings.GetIntegerOption("SystemDebugAnim", LauncherSettingDefaults.GetInteger("SystemDebugAnim")));
        // WPF only applies the optional advanced material to the modal overlay.
        // Applying native acrylic to this borderless window also tints its transparent
        // shadow margin, which leaves a visible rectangular frame around PanBack.
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        ApplyFormBackground(settings);
        ApplyTitleAppearance(settings);
        ApplyBackgroundAppearance(settings);
        ApplyNetworkProxy(settings);
        _launchRight?.SetMaximumLogLines(ResolveMaximumLogLines(settings));
        ApplyLaunchPageSettings(settings);
        ApplyHomepageSettings(settings);
    }

    private void ApplyFormBackground(LauncherSettings settings)
    {
        if (this.FindControl<Grid>("PanForm") is not { } form)
            return;

        bool colorful = settings.GetBooleanOption(
            "UiBackgroundColorful",
            LauncherSettingDefaults.GetBoolean("UiBackgroundColorful"));
        // Prefer live theme manager state so ColorMode.System tracks OS preference.
        bool isDarkMode = AvaloniaThemeManager.IsDarkMode;
        ColorTheme theme = isDarkMode ? settings.DarkColor : settings.LightColor;
        IReadOnlyDictionary<string, Color> palette = ThemeColorPalette.Create(isDarkMode, theme);
        if (!colorful)
        {
            form.Background = new SolidColorBrush(palette["ColorBrushBackground"]);
            return;
        }

        Color first = palette["ColorObject6"];
        Color middle = palette["ColorObject7"];
        Color last = palette["ColorObject8"];
        form.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.9d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.1d, 1d, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(first, 0d),
                new GradientStop(middle, 0.4d),
                new GradientStop(last, 1d)
            }
        };
    }

    private void ApplyLaunchPageSettings(LauncherSettings settings)
    {
        _launchLeft?.ConfigureLaunchingHint(settings.GetBooleanOption(
            "UiShowLaunchingHint",
            LauncherSettingDefaults.GetBoolean("UiShowLaunchingHint")));
        _launchRight?.ConfigureDebugLog(settings.GetBooleanOption(
            "SystemDebugMode",
            LauncherSettingDefaults.GetBoolean("SystemDebugMode")));
        if (this.FindControl<StackPanel>("PanHint") is { } hints)
        {
            bool alignRight = settings.GetBooleanOption(
                "UiHintAlignRight",
                LauncherSettingDefaults.GetBoolean("UiHintAlignRight"));
            // WPF: left = Margin 25,0,0,18; right = leave room for PanExtraButtons (~70)
            // Content column only — leave room for PanExtraButtons when right-aligned.
            hints.HorizontalAlignment = alignRight
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left;
            hints.Margin = alignRight
                ? new Thickness(0d, 0d, 70d, 18d)
                : new Thickness(12d, 0d, 0d, 18d);
        }
    }

    private void ApplyTitleAppearance(LauncherSettings settings)
    {
        Avalonia.Controls.Shapes.Path? defaultLogo = this.FindControl<Avalonia.Controls.Shapes.Path>("ShapeTitleLogo");
        MyImage? customLogo = this.FindControl<MyImage>("ImageTitleLogo");
        TextBlock? customText = this.FindControl<TextBlock>("LabTitleLogo");
        Grid? titleMain = this.FindControl<Grid>("PanTitleMain");
        if (defaultLogo is null || customLogo is null || customText is null)
            return;

        int titleType = settings.GetIntegerOption("UiLogoType", LauncherSettingDefaults.GetInteger("UiLogoType"));
        string logoPath = settings.GetTextOption(
            LauncherSettingKeys.UiCustomLogoPath,
            Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Logo.png"));
        bool hasCustomImage = titleType == 3 && File.Exists(logoPath);
        defaultLogo.IsVisible = titleType == 1 || titleType == 3 && !hasCustomImage;
        customText.IsVisible = titleType == 2;
        customLogo.IsVisible = hasCustomImage;
        customText.Text = settings.GetTextOption("UiLogoText", LauncherSettingDefaults.GetText("UiLogoText"));
        if (string.IsNullOrWhiteSpace(customText.Text))
            customText.Text = "PCL N";

        if (hasCustomImage)
        {
            try
            {
                string logoStamp = GetFileStamp(logoPath);
                if (!string.Equals(_titleLogoFile, logoStamp, StringComparison.Ordinal))
                {
                    _titleLogoBitmap?.Dispose();
                    _titleLogoBitmap = new Bitmap(logoPath);
                    _titleLogoFile = logoStamp;
                }
                customLogo.Source = _titleLogoBitmap;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                customLogo.IsVisible = false;
                defaultLogo.IsVisible = true;
            }
        }
        else
        {
            customLogo.Source = null;
            _titleLogoBitmap?.Dispose();
            _titleLogoBitmap = null;
            _titleLogoFile = null;
        }

        if (titleMain is not null)
        {
            titleMain.HorizontalAlignment = settings.GetBooleanOption(
                "UiLogoLeft",
                LauncherSettingDefaults.GetBoolean("UiLogoLeft"))
                ? Avalonia.Layout.HorizontalAlignment.Left
                : Avalonia.Layout.HorizontalAlignment.Center;
        }
    }

    private void ApplyBackgroundAppearance(LauncherSettings settings)
    {
        Image? image = this.FindControl<Image>("ImageBack");
        MediaElement? video = this.FindControl<MediaElement>("VideoBack");
        if (image is null || video is null)
            return;

        string directory = Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Backgrounds");
        string[] files = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedBackgroundFile)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        string refreshToken = settings.GetTextOption("UiBackgroundRefreshToken", string.Empty);
        bool refreshRequested = !string.Equals(_backgroundRefreshToken, refreshToken, StringComparison.Ordinal);
        _backgroundRefreshToken = refreshToken;
        string? file = !refreshRequested && _backgroundFile is not null && files.Contains(_backgroundFile, pathComparer)
            ? _backgroundFile
            : files.Length == 0 ? null : files[Random.Shared.Next(files.Length)];
        _backgroundFile = file;
        string? backgroundStamp = file is null ? null : GetFileStamp(file);
        bool isVideo = file is not null && IsVideoBackgroundFile(file);
        if (!string.Equals(_backgroundStamp, backgroundStamp, StringComparison.Ordinal))
        {
            _backgroundBitmap?.Dispose();
            _backgroundBitmap = null;
            _backgroundStamp = backgroundStamp;
            video.Close();
            if (file is not null && isVideo)
            {
                video.Source = new Uri(file, UriKind.Absolute);
            }
            else if (file is not null)
            {
                try
                {
                    _backgroundBitmap = new Bitmap(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _backgroundStamp = null;
                }
            }
            image.Source = _backgroundBitmap;
        }

        image.IsVisible = _backgroundBitmap is not null;
        video.IsVisible = isVideo && video.Source is not null;
        double opacity = Math.Clamp(
            settings.GetIntegerOption("UiBackgroundOpacity", LauncherSettingDefaults.GetInteger("UiBackgroundOpacity")) / 1000d,
            0d,
            1d);
        image.Opacity = opacity;
        video.Opacity = opacity;
        int blurRadius = settings.GetIntegerOption("UiBackgroundBlur", LauncherSettingDefaults.GetInteger("UiBackgroundBlur"));
        image.Effect = blurRadius > 0 ? new BlurEffect { Radius = blurRadius } : null;
        video.Effect = blurRadius > 0 ? new BlurEffect { Radius = blurRadius } : null;
        int backgroundSuit = settings.GetIntegerOption("UiBackgroundSuit", LauncherSettingDefaults.GetInteger("UiBackgroundSuit"));
        ApplyBackgroundSuit(image, backgroundSuit);
        ApplyBackgroundSuit(video, backgroundSuit);
        UpdateBackgroundVideoPlayback(settings);
    }

    private static bool IsSupportedBackgroundFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
        IsVideoBackgroundFile(path);

    private static bool IsVideoBackgroundFile(string path) =>
        path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mov", StringComparison.OrdinalIgnoreCase);

    private void UpdateBackgroundVideoPlayback(LauncherSettings? settings = null)
    {
        if (this.FindControl<MediaElement>("VideoBack") is not { Source: not null, IsVisible: true } video)
            return;

        settings ??= LauncherSettingsPageBinder.LoadSettings();
        bool pauseForGame = _isGameRunning && settings.GetBooleanOption(
            "UiAutoPauseVideo",
            LauncherSettingDefaults.GetBoolean("UiAutoPauseVideo"));
        if (WindowState == WindowState.Minimized || pauseForGame)
            video.Pause();
        else if (!video.IsPlaying)
            video.Play();
    }

    private static void ApplyBackgroundSuit(Image image, int mode)
    {
        image.Stretch = mode switch
        {
            2 => Stretch.Uniform,
            3 => Stretch.Fill,
            >= 1 => Stretch.None,
            _ => Stretch.UniformToFill
        };
        image.HorizontalAlignment = mode switch
        {
            5 or 7 => Avalonia.Layout.HorizontalAlignment.Left,
            6 or 8 => Avalonia.Layout.HorizontalAlignment.Right,
            _ => Avalonia.Layout.HorizontalAlignment.Center
        };
        image.VerticalAlignment = mode switch
        {
            5 or 6 => Avalonia.Layout.VerticalAlignment.Top,
            7 or 8 => Avalonia.Layout.VerticalAlignment.Bottom,
            _ => Avalonia.Layout.VerticalAlignment.Center
        };
    }

    private static int ResolveMaximumLogLines(LauncherSettings settings)
    {
        int value = settings.GetIntegerOption("SystemMaxLog", LauncherSettingDefaults.GetInteger("SystemMaxLog"));
        return value switch
        {
            <= 5 => value * 10 + 50,
            <= 13 => value * 50 - 150,
            <= 28 => value * 100 - 800,
            _ => int.MaxValue
        };
    }

    private static void ApplyNetworkProxy(LauncherSettings settings)
    {
        int proxyType = settings.GetIntegerOption(
            "SystemHttpProxyType",
            LauncherSettingDefaults.GetInteger("SystemHttpProxyType"));
        if (proxyType == 1)
        {
            HttpClient.DefaultProxy = SystemDefaultProxy;
            return;
        }

        if (proxyType != 2)
        {
            HttpClient.DefaultProxy = new WebProxy();
            return;
        }

        string address = settings.GetTextOption("SystemHttpProxy", LauncherSettingDefaults.GetText("SystemHttpProxy"));
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? proxyAddress))
            return;

        WebProxy proxy = new(proxyAddress);
        string username = settings.GetTextOption(
            "SystemHttpProxyCustomUsername",
            LauncherSettingDefaults.GetText("SystemHttpProxyCustomUsername"));
        if (!string.IsNullOrWhiteSpace(username))
        {
            proxy.Credentials = new NetworkCredential(
                username,
                settings.GetTextOption(
                    "SystemHttpProxyCustomPassword",
                    LauncherSettingDefaults.GetText("SystemHttpProxyCustomPassword")));
        }
        HttpClient.DefaultProxy = proxy;
    }

    private void ApplyHomepageSettings(LauncherSettings settings)
    {
        if (_launchRight is null)
            return;

        int mode = settings.GetIntegerOption("UiCustomType", LauncherSettingDefaults.GetInteger("UiCustomType"));
        string networkAddress = settings.GetTextOption("UiCustomNet", LauncherSettingDefaults.GetText("UiCustomNet"));
        int preset = settings.GetIntegerOption("UiCustomPreset", LauncherSettingDefaults.GetInteger("UiCustomPreset"));
        string signature = $"{mode.ToString(CultureInfo.InvariantCulture)}|{preset.ToString(CultureInfo.InvariantCulture)}|{networkAddress}";
        if (mode != 1 && string.Equals(_homepageSignature, signature, StringComparison.Ordinal))
            return;

        _homepageSignature = signature;
        _homepageLoadCancellation?.Cancel();
        _homepageLoadCancellation?.Dispose();
        _homepageLoadCancellation = null;

        switch (mode)
        {
            case 1:
                LoadLocalHomepage();
                break;
            case 2 when Uri.TryCreate(networkAddress, UriKind.Absolute, out Uri? address):
                _homepageLoadCancellation = new CancellationTokenSource();
                _ = LoadNetworkHomepageAsync(address, _homepageLoadCancellation.Token);
                break;
            case 3:
                _launchRight.ClearCustomContent();
                break;
            default:
                _launchRight.ClearCustomContent();
                break;
        }
    }

    private void LoadLocalHomepage()
    {
        string directory = Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "CustomHomepage");
        string? file = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(static path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                                      path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                                      path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;
        if (file is null)
        {
            _launchRight?.LoadTextContent("自定义主页目录中没有 .txt、.md 或 .xaml 文件。");
            return;
        }

        try
        {
            _launchRight?.LoadTextContent(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _launchRight?.LoadTextContent("读取自定义主页失败：" + ex.Message);
        }
    }

    private async Task LoadNetworkHomepageAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
            string content = await client.GetStringAsync(address, cancellationToken).ConfigureAwait(false);
            if (content.Length > 1024 * 1024)
                content = content[..(1024 * 1024)];
            await Dispatcher.UIThread.InvokeAsync(() => _launchRight?.LoadTextContent(content));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _launchRight?.LoadTextContent("下载自定义主页失败：" + ex.Message));
        }
    }

    private static string GetFileStamp(string path)
    {
        FileInfo file = new(path);
        return string.Concat(
            file.FullName,
            "|",
            file.Length.ToString(CultureInfo.InvariantCulture),
            "|",
            file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    private void RefreshNavigationText()
    {
        foreach (MyListItem item in GetNavItems())
        {
            if (!TryGetNavRoute(item, out NavigationRouteId route))
                continue;
            item.Title = route.Value switch
            {
                DesktopNavigationRegistry.LaunchRouteValue => AvaloniaLocalizationManager.GetText("Main.TopTitle.Launch", "启动"),
                DesktopNavigationRegistry.DownloadRouteValue => AvaloniaLocalizationManager.GetText("Main.TopTitle.Download", "下载"),
                DesktopNavigationRegistry.CommunityRouteValue => AvaloniaLocalizationManager.GetText("Main.TopTitle.Community", "社区"),
                DesktopNavigationRegistry.SettingsRouteValue => AvaloniaLocalizationManager.GetText("Main.TopTitle.Settings", "设置"),
                _ => item.Title
            };
        }
    }

    private void BeginPageChangeAnimation(NavigationRouteId route)
    {
        _pendingNavRoute = route;
        if (this.FindControl<Control>("PanMainRight") is not { } right)
        {
            ApplyPagePlaceholder(route);
            return;
        }

        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(right, -right.Opacity, 110),
                ModAnimation.AaCode(() =>
                {
                    ApplyPagePlaceholder(route);
                    right.Opacity = 0d;
                }, after: true),
                ModAnimation.AaOpacity(right, 1d, 170)
            },
            "FrmMain PageChangeRight");
    }

    private IEnumerable<MyListItem> GetNavItems()
    {
        if (this.FindControl<Panel>("PanTitleSelect") is not { } panel)
            yield break;

        foreach (Control child in panel.Children)
        {
            if (child is MyListItem item)
                yield return item;
        }
    }

    private static bool TryGetNavRoute(MyListItem item, out NavigationRouteId route)
    {
        route = default;
        return item.Tag switch
        {
            NavigationRouteId value => SetRoute(value, out route),
            string text when !string.IsNullOrWhiteSpace(text) => SetRoute(NavigationRouteId.Parse(text), out route),
            _ => false
        };
    }

    private static bool SetRoute(NavigationRouteId value, out NavigationRouteId route)
    {
        route = value;
        return true;
    }

    private void CaptureShowAnimationTransforms()
    {
        if (Content is not Control root)
            return;

        _showAnimationRoot = root;
        if (root.RenderTransform is not TransformGroup group)
            return;

        foreach (ITransform transform in group.Children)
        {
            _showAnimationRotate ??= transform as RotateTransform;
            _showAnimationTranslate ??= transform as TranslateTransform;
        }
    }

    private void StartShowAnimation()
    {
        if (_showAnimationStarted)
            return;

        _showAnimationStarted = true;
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(this, _targetWindowOpacity - Opacity, 250, 100),
                ModAnimation.AaDouble(
                    value =>
                    {
                        if (_showAnimationTranslate is not null)
                            _showAnimationTranslate.Y += value;
                    },
                    -(_showAnimationTranslate?.Y ?? 0d),
                    600,
                    100,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(
                    value =>
                    {
                        if (_showAnimationRotate is not null)
                            _showAnimationRotate.Angle += value;
                    },
                    -(_showAnimationRotate?.Angle ?? 0d),
                    500,
                    100,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() =>
                {
                    if (_showAnimationRoot is not null)
                        _showAnimationRoot.RenderTransform = null;
                }, after: true)
            },
            "FrmMain Load");
    }

    private static double EaseOutCubic(double progress)
    {
        double inverse = 1d - progress;
        return 1d - inverse * inverse * inverse;
    }

    private sealed record MinecraftFolderSetting(string Name, string RootDirectory);

}
