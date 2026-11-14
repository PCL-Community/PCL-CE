using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Navigation;
using PCL.UI;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Shell;

namespace PCL.Views;

/// <summary>
///     主窗口：承载标题栏、左右页面宿主、背景层、提示与弹窗层，并接管窗口级交互与退出流程。
///     页面由 <see cref="PageRouter" /> 按 <see cref="IPageHost" /> 宿主挂载，窗口本身不感知具体页面。
/// </summary>
public partial class MainWindow : Window
{
    private const string LeftWidthAnimationKey = "MainWindow LeftChange";

    private readonly BackgroundLayer _background;
    private readonly ExtraButtonsPanel _extraButtons;
    private readonly TitleBar _titleBar;
    private bool _leftWidthUpdateQueued;

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        ApplyWindowIcon();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        _ = new WindowStateStore(this);
        ApplyDebugWindowSize();

        _titleBar = TitleBar;
        _extraButtons = PanExtraButtons;
        _background = new BackgroundLayer(PanForm, ImgBackImage);
        BackgroundHost.Attach(_background);
        _ = new FileDragHandler(this);
        _ = new KeyboardHandler(this, PageLeftHost);
        _ = new ExitFlow(this);

        PageRouter.Attach(PageLeftHost, PageRightHost);
        PageRouter.Navigated += OnNavigated;
        PageRouter.PageHostSwapped += OnPageHostSwapped;
        PageRouter.PageChanged += _ => _extraButtons.RefreshAllStates();
        PageLeftHost.SizeChanged += (_, _) => ScheduleLeftWidthUpdate();
        LocalizationService.LanguageChanged += OnLanguageChangedForLeftWidth;

        ApplyRoundedWindowClip();
        AddHandler(
            PointerPressedEvent,
            OnResizePointerPressed,
            RoutingStrategies.Tunnel,
            true);
        PointerMoved += OnResizePointerMoved;

        Topmost = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(
            () =>
            {
                Topmost = false;
                Activate();
                Focus();
            },
            DispatcherPriority.Loaded);
        Opened += (_, _) => TaskbarProgress.SetThumbnailClip(new Thickness(10d));
        SizeChanged += (_, _) => TaskbarProgress.SetThumbnailClip(new Thickness(10d));

        _titleBar.TabSelected += PageRouter.Navigate;
        _titleBar.BackRequested += () => PageRouter.Back();
        _titleBar.HelpRequested += OpenHelp;
        _titleBar.MinimizeRequested += () => WindowState = WindowState.Minimized;
        _titleBar.CloseRequested += Close;

        if (this.FindControl<Panel>("PanHint") is { } hintLayer)
            HintPresenter.Instance.Attach(this, hintLayer);
        if (this.FindControl<Panel>("PanDialogLayer") is { } dialogLayer)
            DialogPresenter.Instance.Attach(this, dialogLayer);

        Closed += (_, _) => OnClosed();

        PageRouter.ShowStandalone(new PageLoading());
        Lifecycle.When(
            LifecycleState.Running,
            () => { PageRouter.Navigate(MainTab.Launch); });

        var effects = new StartupEffects(this, _titleBar, _background, _extraButtons);
        Lifecycle.When(LifecycleState.WindowCreated, effects.Run);

        try
        {
            GameFolders.McFolderListLoader.Start(0);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "启动文件夹列表加载失败");
        }

#if DEBUG
        DebugPageSmoke.TryStart();
#endif
    }

    public static MainWindow? Current { get; private set; }

    private void OnNavigated(PageNavigationState state)
    {
        _titleBar.UpdateState(state);
        _extraButtons.RefreshAllStates();
    }

    private void OnPageHostSwapped()
    {
        ScheduleLeftWidthUpdate();
    }

    private void OnLanguageChangedForLeftWidth()
    {
        Dispatcher.UIThread.Post(ScheduleLeftWidthUpdate, DispatcherPriority.Loaded);
    }

    /// <summary>合并同一轮布局中的多次宽度变化，避免背景宽度来回抖动。</summary>
    private void ScheduleLeftWidthUpdate()
    {
        if (_leftWidthUpdateQueued)
            return;
        _leftWidthUpdateQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _leftWidthUpdateQueued = false;
                var width = PageLeftHost.IsVisible
                    ? PageLeftHost.Bounds.Width > 0d
                        ? PageLeftHost.Bounds.Width
                        : PageLeftHost.DesiredSize.Width
                    : 0d;
                AnimateLeftWidth(width);
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     仅用于截图/布局证据：PCL_DEBUG_WINDOW_SIZE=810x470 或 workarea。
    ///     不实现真正的最大化；UI 设计中没有最大化场景。
    /// </summary>
    private void ApplyDebugWindowSize()
    {
        var value = Environment.GetEnvironmentVariable("PCL_DEBUG_WINDOW_SIZE");
        if (string.IsNullOrWhiteSpace(value)) return;

        if (value.Equals("workarea", StringComparison.OrdinalIgnoreCase))
        {
            var screen = Screens.Primary;
            if (screen is null) return;
            var scaling = screen.Scaling <= 0d ? 1d : screen.Scaling;
            Width = Math.Max(MinWidth, screen.WorkingArea.Width / scaling);
            Height = Math.Max(MinHeight, screen.WorkingArea.Height / scaling);
            return;
        }

        var parts = value.Split('x', 'X');
        if (parts.Length != 2
            || !double.TryParse(parts[0], out var width)
            || !double.TryParse(parts[1], out var height))
            return;

        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
    }

    private void ApplyRoundedWindowClip()
    {
        PanBack.SizeChanged += (_, e) =>
        {
            PanBack.Clip = new RectangleGeometry
            {
                Rect = new Rect(e.NewSize),
                RadiusX = 6d,
                RadiusY = 6d
            };
        };
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize
            || WindowState == WindowState.Maximized
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (GetResizeEdge(e.GetPosition(this)) is not { } edge) return;
        BeginResizeDrag(edge, e);
        e.Handled = true;
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        Cursor = !CanResize || WindowState == WindowState.Maximized
            ? null
            : GetResizeEdge(e.GetPosition(this)) switch
            {
                WindowEdge.NorthWest => new Cursor(StandardCursorType.TopLeftCorner),
                WindowEdge.North => new Cursor(StandardCursorType.TopSide),
                WindowEdge.NorthEast => new Cursor(StandardCursorType.TopRightCorner),
                WindowEdge.West => new Cursor(StandardCursorType.LeftSide),
                WindowEdge.East => new Cursor(StandardCursorType.RightSide),
                WindowEdge.SouthWest => new Cursor(StandardCursorType.BottomLeftCorner),
                WindowEdge.South => new Cursor(StandardCursorType.BottomSide),
                WindowEdge.SouthEast => new Cursor(StandardCursorType.BottomRightCorner),
                _ => null
            };
    }

    private WindowEdge? GetResizeEdge(Point point)
    {
        const double band = 8d;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var left = point.X <= band;
        var right = point.X >= width - band;
        var top = point.Y <= band;
        var bottom = point.Y >= height - band;

        if (top && left) return WindowEdge.NorthWest;
        if (top && right) return WindowEdge.NorthEast;
        if (bottom && left) return WindowEdge.SouthWest;
        if (bottom && right) return WindowEdge.SouthEast;
        if (top) return WindowEdge.North;
        if (bottom) return WindowEdge.South;
        if (left) return WindowEdge.West;
        if (right) return WindowEdge.East;
        return null;
    }

    private void AnimateLeftWidth(double target)
    {
        var current = double.IsNaN(RectLeftBackground.Width)
            ? RectLeftBackground.Bounds.Width
            : RectLeftBackground.Width;

        if (Math.Abs(target - current) < 0.1d
            || !IsVisible
            || !AnimationService.IsEnabled
            || !RectLeftBackground.IsLoaded)
        {
            AnimationService.CancelAnimationByName(LeftWidthAnimationKey);
            RectLeftBackground.Width = target;
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new WidthAnimation(
                    RectLeftBackground,
                    target - current,
                    180,
                    ease: new FluentEaseOut(EasePower.ExtraStrong))
            },
            LeftWidthAnimationKey,
            true);
    }

    private void OnClosed()
    {
        PageRouter.Navigated -= OnNavigated;
        PageRouter.PageHostSwapped -= OnPageHostSwapped;
        LocalizationService.LanguageChanged -= OnLanguageChangedForLeftWidth;
        BackgroundHost.Detach(_background);
        _background.Remove();
        if (ReferenceEquals(Current, this)) Current = null;
    }

    private void ApplyWindowIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(
                new Uri("avares://PCL.Launcher/Assets/Images/icon.ico"));
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"加载窗口图标失败：{ex.Message}");
        }
    }

    private static void OpenHelp()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo("https://www.bilibili.com/video/BV1uT4y1P7CX")
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 打开帮助失败：{ex.Message}");
        }
    }
}