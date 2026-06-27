// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Stopwatch _showAnimationClock = new();
    private Control? _showAnimationRoot;
    private RotateTransform? _showAnimationRotate;
    private TranslateTransform? _showAnimationTranslate;
    private DispatcherTimer? _showAnimationTimer;
    private bool _showAnimationStarted;
    private bool _isNavExpanded;
    private DispatcherTimer? _navAnimTimer;
    private readonly Stopwatch _pageChangeClock = new();
    private DispatcherTimer? _pageChangeTimer;
    private double _navExpandedWidth = 200d;
    private double _navAnimStart;
    private double _navAnimTarget;
    private int _navAnimElapsed;
    private int _currentNavPage;
    private int _pendingNavPage;
    private bool _isPageContentSwapped;
    private bool _isMainWindowOpened;

    private const double NavCollapsedWidth = 50d;
    private const int NavAnimDuration = 200;
    private const double PageFadeOutDuration = 110d;
    private const double PageFadeInDuration = 170d;

    private static readonly Dictionary<int, string> NavPageTitles = new()
    {
        [0] = "启动",
        [1] = "下载",
        [2] = "社区",
        [3] = "设置",
        [4] = "在线"
    };

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
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
        SelectNavPage(0, animate: false);
    }

    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
    }

    private void FormMain_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            e.GetPosition(this).Y <= 48)
        {
            BeginMoveDrag(e);
        }
    }

    private void FormMain_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncMainSize();
        SyncTitleOverlayWidth();
    }

    private void FormMain_Closing(object? sender, WindowClosingEventArgs e)
    {
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
    }

    private void BtnNavItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem item || !TryGetNavPage(item, out int page))
            return;

        SelectNavPage(page, animate: _isMainWindowOpened);
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
    }

    private void BtnExtraDownload_Click(object? sender, EventArgs e)
    {
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

    public void ActivateExistingInstance()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        ForceWindowsForeground();
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

    private void ForceWindowsForeground()
    {
        if (!OperatingSystem.IsWindows())
            return;

        nint handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
            return;

        WindowsForegroundApi.ShowWindow(handle, 9);
        WindowsForegroundApi.SetForegroundWindow(handle);
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

    private void SelectNavPage(int page, bool animate)
    {
        if (!NavPageTitles.ContainsKey(page))
            page = 0;

        MyListItem? selected = null;
        foreach (MyListItem item in GetNavItems())
        {
            if (TryGetNavPage(item, out int itemPage) && itemPage == page)
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

        if (!animate || page == _currentNavPage)
        {
            ApplyPagePlaceholder(page);
            return;
        }

        BeginPageChangeAnimation(page);
    }

    private void ApplyPagePlaceholder(int page)
    {
        _currentNavPage = page;
        if (this.FindControl<MyLoading>("LoadMain") is { } loading)
            loading.Text = $"正在加载{NavPageTitles[page]}页面";
        if (this.FindControl<Control>("PanMainRight") is { } right)
            right.Opacity = 1d;
    }

    private void BeginPageChangeAnimation(int page)
    {
        _pendingNavPage = page;
        _isPageContentSwapped = false;
        _pageChangeClock.Restart();
        _pageChangeTimer?.Stop();
        _pageChangeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _pageChangeTimer.Tick += PageChangeTimer_Tick;
        _pageChangeTimer.Start();
        PageChangeTimer_Tick(this, EventArgs.Empty);
    }

    private void PageChangeTimer_Tick(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanMainRight") is not { } right)
        {
            _pageChangeTimer?.Stop();
            _pageChangeTimer = null;
            ApplyPagePlaceholder(_pendingNavPage);
            return;
        }

        double elapsed = _pageChangeClock.Elapsed.TotalMilliseconds;
        if (elapsed <= PageFadeOutDuration)
        {
            right.Opacity = 1d - EaseOutCubic(Normalize(elapsed, PageFadeOutDuration));
            return;
        }

        if (!_isPageContentSwapped)
        {
            _isPageContentSwapped = true;
            ApplyPagePlaceholder(_pendingNavPage);
            right.Opacity = 0d;
        }

        double fadeInElapsed = elapsed - PageFadeOutDuration;
        right.Opacity = EaseOutCubic(Normalize(fadeInElapsed, PageFadeInDuration));
        if (fadeInElapsed < PageFadeInDuration)
            return;

        _pageChangeTimer?.Stop();
        _pageChangeTimer = null;
        right.Opacity = 1d;
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

    private static bool TryGetNavPage(MyListItem item, out int page)
    {
        page = 0;
        return item.Tag switch
        {
            int value => SetPage(value, out page),
            string text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out page),
            _ => false
        };
    }

    private static bool SetPage(int value, out int page)
    {
        page = value;
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
        _showAnimationClock.Restart();
        _showAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _showAnimationTimer.Tick += ShowAnimationTimer_Tick;
        _showAnimationTimer.Start();
        ShowAnimationTimer_Tick(this, EventArgs.Empty);
    }

    private void ShowAnimationTimer_Tick(object? sender, EventArgs e)
    {
        double elapsed = _showAnimationClock.Elapsed.TotalMilliseconds;
        double delayed = Math.Max(0d, elapsed - 100d);

        Opacity = EaseOutCubic(Normalize(delayed, 250d));

        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 60d * (1d - EaseOutBack(Normalize(delayed, 600d)));
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = -4d * (1d - EaseOutBack(Normalize(delayed, 500d)));

        if (elapsed < 720d)
            return;

        _showAnimationTimer?.Stop();
        _showAnimationTimer = null;
        Opacity = 1d;
        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 0d;
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = 0d;
        if (_showAnimationRoot is not null)
            _showAnimationRoot.RenderTransform = null;
    }

    private static double Normalize(double elapsedMilliseconds, double durationMilliseconds)
    {
        return Math.Clamp(elapsedMilliseconds / durationMilliseconds, 0d, 1d);
    }

    private static double EaseOutCubic(double progress)
    {
        double inverse = 1d - progress;
        return 1d - inverse * inverse * inverse;
    }

    private static double EaseOutBack(double progress)
    {
        const double overshoot = 1.15d;
        double shifted = progress - 1d;
        return 1d + shifted * shifted * ((overshoot + 1d) * shifted + overshoot);
    }

    private static class WindowsForegroundApi
    {
        private static readonly Lazy<Api?> ApiInstance = new(LoadApi);

        public static void ShowWindow(nint hWnd, int nCmdShow)
        {
            _ = ApiInstance.Value?.ShowWindow(hWnd, nCmdShow);
        }

        public static void SetForegroundWindow(nint hWnd)
        {
            _ = ApiInstance.Value?.SetForegroundWindow(hWnd);
        }

        private static Api? LoadApi()
        {
            if (!NativeLibrary.TryLoad("user32.dll", out nint library))
                return null;

            if (!NativeLibrary.TryGetExport(library, "ShowWindow", out nint showWindow) ||
                !NativeLibrary.TryGetExport(library, "SetForegroundWindow", out nint setForegroundWindow))
            {
                NativeLibrary.Free(library);
                return null;
            }

            return new Api(
                library,
                Marshal.GetDelegateForFunctionPointer<ShowWindowDelegate>(showWindow),
                Marshal.GetDelegateForFunctionPointer<SetForegroundWindowDelegate>(setForegroundWindow));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool ShowWindowDelegate(nint hWnd, int nCmdShow);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool SetForegroundWindowDelegate(nint hWnd);

        private sealed class Api(nint library, ShowWindowDelegate showWindow, SetForegroundWindowDelegate setForegroundWindow)
        {
            private readonly nint _library = library;

            public bool ShowWindow(nint hWnd, int nCmdShow) => showWindow(hWnd, nCmdShow);

            public bool SetForegroundWindow(nint hWnd) => setForegroundWindow(hWnd);

            ~Api()
            {
                if (_library != 0)
                    NativeLibrary.Free(_library);
            }
        }
    }
}
