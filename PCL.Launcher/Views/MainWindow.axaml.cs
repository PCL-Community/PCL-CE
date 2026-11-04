using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.Logging;
using PCL.Navigation;
using PCL.UI;
using PCL.UI.Animations;
using PCL.UI.Effects;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Shell;
using static PCL.Animations;

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

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        ApplyWindowIcon();
        WindowBackdropController.Apply(this);
        _ = new WindowStateStore(this);

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

        ApplyRoundedWindowClip();
        AddHandler(
            PointerPressedEvent,
            OnResizePointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
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
        Lifecycle.When(LifecycleState.Running, () => PageRouter.Navigate(MainTab.Launch));

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
        Dispatcher.UIThread.Post(
            () => AnimateLeftWidth(PageLeftHost.Bounds.Width),
            DispatcherPriority.Loaded);
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
        if (WindowState == WindowState.Maximized
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (GetResizeEdge(e.GetPosition(this)) is not { } edge) return;
        BeginResizeDrag(edge, e);
        e.Handled = true;
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        Cursor = WindowState == WindowState.Maximized
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
            AniStop(LeftWidthAnimationKey);
            RectLeftBackground.Width = target;
            return;
        }

        AniStart(
            new List<AniData>
            {
                AaWidth(
                    RectLeftBackground,
                    target - current,
                    180,
                    ease: new AniEaseOutFluent(AniEasePower.ExtraStrong))
            },
            LeftWidthAnimationKey,
            true);
    }

    private void OnClosed()
    {
        PageRouter.Navigated -= OnNavigated;
        PageRouter.PageHostSwapped -= OnPageHostSwapped;
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