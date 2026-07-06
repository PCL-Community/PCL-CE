// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
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
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;
using PCL.Desktop.Platform;
using PCL.Desktop.Features.Downloads.Views;
using PCL.Desktop.Features.Instances.Views;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Settings.Views;
using PCL.Desktop.Features.Tasks.Views;
using PCL.Platform.Paths;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Views;

public partial class MainWindow : Window, IDisposable
{
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
    private PageSpeedLeft? _speedLeft;
    private PageSpeedRight? _speedRight;
    private PageInstanceLeft? _instanceLeft;
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
    private readonly MinecraftVanillaInstallService _minecraftInstallService = new();
    private readonly ThirdPartyAuthService _thirdPartyAuthService = new();
    private PageSetupLeft? _setupLeft;
    private MyPageRight? _setupRight;
    private readonly List<LoginProfileInfo> _loginProfiles = [];
    private readonly NavigationPageDescriptor[] _navigationPages;
    private readonly Dictionary<string, TaskManagerEntrySnapshot> _taskSnapshots = [];
    private readonly Dictionary<string, CancellationTokenSource> _taskCancellations = [];
    private readonly DesktopPageAdapter _pageAdapter = new();
    private readonly DesktopPageContext _desktopPageContext;
    private int _registeredPageRequestId;
    private int _taskSequence;
    private bool _isTaskManagerVisible;
    private NavigationRouteId? _taskManagerBackRoute;

    private const double NavCollapsedWidth = 50d;
    private const int NavAnimDuration = 200;

    private static readonly NavigationRouteId LaunchRoute = DesktopNavigationRegistry.LaunchRoute;
    private static readonly NavigationRouteId DownloadRoute = DesktopNavigationRegistry.DownloadRoute;
    private static readonly NavigationRouteId SettingsRoute = DesktopNavigationRegistry.SettingsRoute;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _navigationPages = CreateNavigationPageMap(DesktopHost.Current.Navigation);
        BuildMainNavigationItems();
        _desktopPageContext = new DesktopPageContext(
            CreateLaunchMainPage,
            CreateDownloadMainPage,
            CreateSettingsMainPage,
            CreatePlaceholderMainPage);
        Opacity = 0d;
        CanResize = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        SetWindowIcon();
        CaptureShowAnimationTransforms();
        Opened += (_, _) =>
        {
            _isMainWindowOpened = true;
            StartShowAnimation();
        };
        SyncTitleOverlayWidth();
        _ = LoadProfilesAsync();
        SelectNavRoute(LaunchRoute, animate: false);
    }

    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
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
        CancelAllTrackedTasks();
        _launchCancellation?.Cancel();
    }

    private void FormMain_Activated(object? sender, EventArgs e)
    {
    }

    private void FrmMain_Drop(object? sender, DragEventArgs e)
    {
    }

    private void FormMain_MouseMove(object? sender, PointerEventArgs e)
    {
    }

    private void VideoEnded(object? sender, EventArgs e)
    {
    }

    private void PanTitle_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncTitleOverlayWidth();
    }

    private void BtnTitleClose_Click(object? sender, EventArgs e) => Close();

    private void BtnTitleMin_Click(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

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
    }

    private void BtnExtraLog_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_RightClick(object? sender, PointerReleasedEventArgs e)
    {
    }

    private void FormDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
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
        back.Show = scroll is not null &&
            scroll.IsVisible &&
            scroll.Offset.Y > Height + (back.Show ? 0d : 700d);
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

        if (!animate || (_currentNavRoute is NavigationRouteId currentRoute && currentRoute.Equals(route.Value)))
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

        if (page.Title is { Length: > 0 } title)
            EnterTitleSubPage(title);
        else
            ExitTitleSubPage();

        RefreshBackToTopBinding();
        page.Activated?.Invoke();
        rightHost.Opacity = 1d;
    }

    private DesktopMainPage CreateLaunchMainPage()
    {
        _launchLeft ??= CreateLaunchLeftPage();
        _launchRight ??= new PageLaunchRight();
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

    private PageLaunchLeft CreateLaunchLeftPage()
    {
        PageLaunchLeft page = new();
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
            page.PageChangeToLogin();
            _launchRight?.AppendLog("已取消启动。");
        };
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
        page.LoginPageRequested += (_, type) => ApplyLaunchLoginPage(page, type);
        page.LaunchRequested += (_, instance) => _ = StartMinecraftAsync(page, instance);
        return page;
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
                if (rightPage is PageDownloadInstall installPage)
                    installPage.ClearInstallTargetOverride();
                _downloadLeft.TriggerShowAnimation();
                rightPage.PageOnEnter();
            });
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

        leftHost.Child = null;
        _instanceSelectPage ??= CreateInstanceSelectPage();
        _instanceSelectPage.SetInstances(_launchLeft?.Instances ?? [], _launchLeft?.SelectedInstance);
        rightHost.Child = _instanceSelectPage;
        EnterTitleSubPage("选择版本");
        RefreshBackToTopBinding();
        _instanceSelectPage.PageOnEnter();
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
            _launchRight?.AppendLog($"已选择游戏版本 {instance.Name}。");
            SelectNavRoute(LaunchRoute, animate: true);
        };
        page.InstanceManageRequested += (_, instance) => ApplyInstanceManagePage(instance);
        return page;
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
        leftHost.Child = _instanceLeft;
        _instanceLeft.TriggerShowAnimation();
        _instanceLeft.SelectPage(subPage);
        EnterTitleSubPage($"版本设置 - {instance.Name}");

        MyPageRight rightPage = GetInstanceRightPage(instance, subPage);
        MyPageRight? oldRight = rightHost.Child as MyPageRight;
        if (ReferenceEquals(oldRight, rightPage))
        {
            rightPage.PageOnEnter();
            return;
        }

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
        await installPage.FocusVersionAsync(
                versionId,
                instance.Name,
                preserveInstallNameOnLoaderSelect: true,
                minecraftRootDirectory: minecraftRoot,
                openLoaderKind: request.LoaderKind)
            .ConfigureAwait(true);
    }

    private PageDownloadInstall ActivateDownloadInstallPage(bool animate)
    {
        _downloadLeft ??= CreateDownloadLeftPage();
        PageDownloadInstall installPage = CreateDownloadInstallPage();
        _downloadLeft.PageChange(DownloadPageSubType.Install, force: true);
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
            _taskManagerBackRoute = GetCurrentNavigationRoute();

        _registeredPageRequestId++;
        _isTaskManagerVisible = true;
        _titleInnerBackAction = () => SelectNavRoute(_taskManagerBackRoute ?? LaunchRoute, animate: true);

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
        _taskSnapshots.Remove(taskId);
        _speedRight?.RemoveTask(taskId);
        UpdateTaskManagerViews();
        RefreshTaskManagerButton();

        if (_isTaskManagerVisible && _taskSnapshots.Count == 0)
            SelectNavRoute(_taskManagerBackRoute ?? LaunchRoute, animate: true);
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
        button.Progress = hasActiveTask ? CreateTaskManagerSummary().Progress : 0d;
        button.Show = hasActiveTask && !_isTaskManagerVisible;
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
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
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
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
        return page;
    }

    private PageInstanceSavesInfoRight CreateInstanceSavesInfoPage()
    {
        PageInstanceSavesInfoRight page = new();
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
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
        page.DownloadRequested += (_, _) => SelectNavRoute(DownloadRoute, animate: true);
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
        return page;
    }

    private PageInstanceResourceRight CreateInstanceDatapackPage()
    {
        PageInstanceResourceRight page = new();
        page.OpenFolderRequested += (_, path) => OpenFolder(path);
        page.DownloadRequested += (_, _) => SelectNavRoute(DownloadRoute, animate: true);
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
        return page;
    }

    private PageInstanceServerRight CreateInstanceServerPage()
    {
        PageInstanceServerRight page = new();
        page.RefreshRequested += (_, _) => page.Reload();
        page.AddServerRequested += (_, instance) => PromptAddServer(instance, page);
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
            await MinecraftServerListService.AddAsync(
                    GetMinecraftRootFromInstance(instance),
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
        page.RefreshSkinRequested += (_, _) => RefreshProfileSkin(page);
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
            OpenExternalUrl("https://www.minecraft.net/msaprofile/mygames/editskin");
            ShowTextDialog(action, "已打开 Minecraft 官方档案页面。请在网页中完成修改，之后回到启动器重新登录或刷新档案。", "知道了");
            return;
        }

        if (profile.Kind == LaunchLoginProfileKind.ThirdParty)
        {
            OpenAuthServerProfilePage(profile, action);
            return;
        }

        ShowTextDialog(action, "离线档案不会同步在线皮肤或披风。你可以在创建离线档案时选择一个已登录正版档案的皮肤作为外观来源。", "知道了");
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
            OpenExternalUrl("https://www.minecraft.net/msaprofile/mygames/editprofile");
            ShowTextDialog("修改玩家名", "已打开 Minecraft 官方档案页面。玩家名修改完成后，请回到启动器重新登录或刷新档案。", "知道了");
            return;
        }

        if (profile.Kind == LaunchLoginProfileKind.ThirdParty)
        {
            OpenAuthServerProfilePage(profile, "修改玩家名");
            return;
        }

        ShowTextDialog("修改玩家名", "离线档案的玩家名保存在本地。请新建一个离线档案来使用新的玩家名。", "知道了");
    }

    private void OpenAuthServerProfilePage(LoginProfileInfo profile, string action)
    {
        string? url = NormalizeAuthServerUrl(profile.AuthServer);
        if (url is not null)
        {
            OpenExternalUrl(url);
            ShowTextDialog(action, "已打开此第三方账户所属的认证服务器。请在服务器网页中完成账户资料修改。", "知道了");
            return;
        }

        ShowTextDialog(action, "第三方账户的资料由认证服务器管理，但当前档案没有记录可打开的服务器地址。请到对应认证服务器的网站中修改。", "知道了");
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

    private void RefreshProfileSkin(PageLoginProfileSkin page)
    {
        page.Reload();
        ShowTextDialog("已刷新档案显示", "启动器已重新载入当前档案信息。若你刚刚在网页中修改了皮肤或披风，请重新登录以获取最新资料。", "知道了");
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
                        Loader = request.Loader
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

    private async Task StartMinecraftAsync(PageLaunchLeft launchPage, LaunchInstanceInfo instance, string? worldName = null)
    {
        LoginProfileInfo? profile = _loginProfiles.FirstOrDefault();
        if (profile is null)
        {
            launchPage.PageChangeToLogin();
            ShowTextDialog("请选择账户档案", "启动游戏前需要先选择或创建一个账户档案。");
            return;
        }

        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        _launchCancellation = new CancellationTokenSource();

        try
        {
            launchPage.UpdateLaunchingStatus("正在读取版本文件", 0.18d, "准备启动参数");
            InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(
                    instance.InstanceDirectory,
                    _launchCancellation.Token)
                .ConfigureAwait(true);
            MinecraftProcessLaunchPlan plan = await CreateLaunchPlanAsync(
                    instance,
                    profile,
                    ResolvePreferredJavaExecutablePath(),
                    _launchCancellation.Token,
                    worldName,
                    metadata)
                .ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(metadata.PreLaunchCommand))
            {
                launchPage.UpdateLaunchingStatus("正在执行预启动命令", 0.58d, "运行启动前任务");
                await RunPreLaunchCommandAsync(metadata, plan.StartInfo.WorkingDirectory, _launchCancellation.Token)
                    .ConfigureAwait(true);
            }

            launchPage.UpdateLaunchingStatus("正在启动 Java 进程", 0.72d, "启动 Minecraft");
            Process? process = Process.Start(plan.StartInfo);
            if (process is null)
                throw new InvalidOperationException("Java 进程未能启动。");

            launchPage.LaunchingRefresh("游戏进程已启动", 1d, isLaunched: true, method: "PID " + process.Id.ToString(CultureInfo.InvariantCulture));
            await IncrementInstanceLaunchCountAsync(instance).ConfigureAwait(true);
            _launchRight?.AppendLog(string.IsNullOrWhiteSpace(worldName)
                ? $"{instance.Name} 已启动。"
                : $"{instance.Name} 已启动，正在进入存档 {worldName}。");
        }
        catch (OperationCanceledException)
        {
            launchPage.PageChangeToLogin();
        }
        catch (Exception ex)
        {
            launchPage.PageChangeToLogin();
            ShowTextDialog("启动失败", "未能启动游戏。\n\n详细信息：" + ex.Message);
            _launchRight?.AppendLog("启动失败：" + ex.Message);
        }
    }

    private async Task IncrementInstanceLaunchCountAsync(LaunchInstanceInfo instance)
    {
        try
        {
            InstanceMetadata metadata = await InstanceMetadataStore.UpdateAsync(
                    instance.InstanceDirectory,
                    current => current with { LaunchCount = Math.Max(0, current.LaunchCount) + 1 })
                .ConfigureAwait(true);

            if (_instanceManagePage is not null &&
                _managedInstance is not null &&
                string.Equals(_managedInstance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _instanceManagePage.SetInstance(instance);
            }

            _launchRight?.AppendLog($"这是 {instance.Name} 的第 {metadata.LaunchCount.ToString(CultureInfo.InvariantCulture)} 次启动。");
        }
        catch (Exception ex)
        {
            _launchRight?.AppendLog("记录启动次数失败：" + ex.Message);
        }
    }

    private static async Task<MinecraftProcessLaunchPlan> CreateLaunchPlanAsync(
        LaunchInstanceInfo instance,
        LoginProfileInfo profile,
        string javaExecutablePath,
        CancellationToken cancellationToken,
        string? worldName = null,
        InstanceMetadata? metadataOverride = null)
    {
        InstanceMetadata metadata = metadataOverride ??
            await InstanceMetadataStore.LoadAsync(instance.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
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
                CustomJvmArguments = FirstNonEmpty(metadata.JvmArguments, GetTextOption(settings, LauncherSettingKeys.LaunchAdvanceJvm)),
                CustomGameArguments = FirstNonEmpty(metadata.GameArguments, GetTextOption(settings, LauncherSettingKeys.LaunchAdvanceGame)),
                ClasspathHeadEntries = SplitClasspathHead(metadata.ClasspathHead),
                AuthlibInjectorPath = authlibPath,
                AuthlibServer = authlibServer,
                AuthlibPrefetchedMetadata = authlibMetadata,
                PreferredIpStack = GetPreferredIpStack(settings),
                Server = string.IsNullOrWhiteSpace(worldName) ? metadata.ServerToEnter : null,
                ReleaseTime = TryReadReleaseTime(instance),
                HasOptiFine = HasOptiFine(instance),
                WorldName = worldName
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
        InstanceMetadata metadata,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.PreLaunchCommand))
            return;

        using Process? process = Process.Start(CreateShellStartInfo(metadata.PreLaunchCommand, workingDirectory));
        if (process is null)
            throw new InvalidOperationException("预启动命令未能启动。");

        if (!metadata.WaitForPreLaunchCommand)
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

    private static string ResolvePreferredJavaExecutablePath()
    {
        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            if (settings.TryGetTextOption(LauncherSettingKeys.LaunchSelectedJava, out string? selectedJava) &&
                !string.IsNullOrWhiteSpace(selectedJava) &&
                File.Exists(selectedJava))
            {
                if (OperatingSystem.IsWindows() &&
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

        return OperatingSystem.IsWindows() ? "javaw" : "java";
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
        InstanceMetadata metadata = InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult();
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

            MinecraftProcessLaunchPlan plan = await CreateLaunchPlanAsync(instance, profile, "java", CancellationToken.None)
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

    private static string GetDefaultMinecraftRoot()
    {
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
        DisposeTrackedTasks();
        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        (_launchLeft as IDisposable)?.Dispose();
        _launchRight?.Dispose();
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
        page.LoginRequested += (_, _) => ShowOnlinePluginUnavailable(page);
        return page;
    }

    private void ShowOnlinePluginUnavailable(PageLoginMs page)
    {
        page.FinishLogin();
        _launchRight?.AppendLog("Microsoft 登录需要 Online 内置插件，目前尚未启用。");
        ShowTextDialog(
            "Microsoft 登录暂不可用",
            "在线账户功能将由后续 Online Host Module 提供。当前版本可以先使用离线档案或第三方登录。",
            "知道了");
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
            LoginProfileInfo profile = new(
                result.Username,
                $"Authlib-Injector · {result.AuthServerDisplayName}",
                LaunchLoginProfileKind.ThirdParty,
                result.Uuid,
                SvgIcon: "lucide/key-round",
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

    private void BeginPageChangeAnimation(NavigationRouteId route)
    {
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
                ModAnimation.AaOpacity(this, 1d, 250, 100),
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

}
