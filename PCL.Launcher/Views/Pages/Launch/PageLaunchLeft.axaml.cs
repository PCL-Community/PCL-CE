using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI.Theme;
using PCL.UI.Controls;
using PCL.Views.Shell;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using LaunchPageKind = PCL.PageLaunchLeftViewModel.LaunchPageKind;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页左栏：实例状态、账号区与启动按钮。
///     实例与账号状态、启动按钮状态机由 <see cref="PageLaunchLeftViewModel" /> 负责，
///     页面保留控件构建、登录子页切换动画、进度条与提示动画以及拖放交互。
/// </summary>
public partial class PageLaunchLeft : MyPageLeft, IShellShortcutTarget
{
    private readonly MyButton _btnLaunch;
    private readonly Grid _gridProgress;
    private readonly TextBlock _labVersion;
    private readonly MyLoading _loadLaunching;
    private readonly List<KeyValuePair<LaunchPageKind, Control>> _loginPages = [];
    private readonly Grid _panInput;
    private readonly Grid _panLaunch;
    private readonly Grid _panLaunching;
    private readonly Grid _panLaunchingHint;
    private readonly Grid _panLaunchingInfo;
    private readonly Grid _panLogin;
    private readonly Rectangle _progressFinished;
    private double _actualUsedHeight = double.NaN;
    private double _actualUsedWidth = double.NaN;

    private bool _isInfoHeightAnimating;
    private bool _isInfoWidthAnimating;
    private ScaleTransform? _launchButtonScale;
    private LaunchPageKind _loginPageCurrent = LaunchPageKind.None;

    public PageLaunchLeft()
    {
        InitializeComponent();
        Current = this;
        DataContext = ViewModel;

        _panInput = this.FindControl<Grid>("PanInput")!;
        _panLaunch = this.FindControl<Grid>("PanLaunch")!;
        _panLaunching = this.FindControl<Grid>("PanLaunching")!;
        _panLogin = this.FindControl<Grid>("PanLogin")!;
        _panLaunchingInfo = this.FindControl<Grid>("PanLaunchingInfo")!;
        _panLaunchingHint = this.FindControl<Grid>("PanLaunchingHint")!;
        _btnLaunch = this.FindControl<MyButton>("BtnLaunch")!;
        _labVersion = this.FindControl<TextBlock>("LabVersion")!;
        _gridProgress = this.FindControl<Grid>("GridLaunchProgress")!;
        _loadLaunching = this.FindControl<MyLoading>("LoadLaunching")!;
        _loadLaunching.Text = string.Empty;

        _progressFinished = this.FindControl<Rectangle>("ProgressFinished")!;
        RefreshProgressGradient();
        ActualThemeVariantChanged += (_, _) => RefreshProgressGradient();
        ThemeService.ColorModeChanged += (_, _) => RefreshProgressGradient();
        ThemeService.ColorThemeChanged += _ => RefreshProgressGradient();

        this.FindControl<Border>("HintBorder")!.Clip = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0d, 0d, 1000d, 1000d)),
            new RectangleGeometry(new Rect(94d, 0d, 72d, 10d)));

        _labVersion.RenderTransformOrigin = new RelativePoint(0.5d, -0.2d, RelativeUnit.Relative);
        _labVersion.RenderTransform = new ScaleTransform(1d, 1d);
        AttachVersionScale();

        _panLaunchingInfo.SizeChanged += OnLaunchingInfoSizeChanged;

        ViewModel.RefreshPageRequested += OnRefreshPageRequested;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        ExtraButtonsPanel.AprilGiveupResetAction = ResetAprilState;
        FileDragHandler.AuthlibServerDropped += OnAuthlibServerDropped;

        Loaded += (_, _) =>
        {
            if (ViewModel.IsFirstLoadStarted)
                RefreshPage(false);
            InitializePage();
            AttachVersionScale();
            ResetAprilState();
            RefreshButtonsUI();
            RefreshProgressGradient();
        };
    }

    public static PageLaunchLeft? Current { get; private set; }

    public PageLaunchLeftViewModel ViewModel { get; } = new();

    public bool IsShowingLaunching => ViewModel.IsShowingLaunching;

    public override void OnNavigatedTo(object? parameter)
    {
        InitializePage();
        RefreshButtonsUI();
    }

    public override void OnNavigatedFrom()
    {
    }

    #region 初始化

    private void InitializePage()
    {
        ViewModel.InitializePage();
    }

    private void OnRefreshPageRequested()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshPage(false);
        else
            Dispatcher.UIThread.Post(() => RefreshPage(false));
    }

    private void OnLanguageChanged()
    {
        ViewModel.RefreshLocalization();
        RefreshButtonsUI();
    }

    #endregion

    #region 启动按钮与实例操作

    public void RefreshButtonsUI()
    {
        ViewModel.RefreshButtonState();
    }

    public void LaunchButtonClick()
    {
        ViewModel.LaunchCommand.Execute(null);
    }

    #endregion

    #region 启动中界面细节

    private void RefreshProgressGradient()
    {
        var start = ResolveColor("ColorObject4", Color.FromArgb(0xFF, 0x0, 0x0, 0x0));
        var end = ResolveColor("ColorObject3", Color.FromArgb(0xFF, 0x0, 0x0, 0x0));
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(start, 0d));
        brush.GradientStops.Add(new GradientStop(end, 0.6d));
        _progressFinished.Fill = brush;
    }

    private Color ResolveColor(string key, Color fallback)
    {
        return this.TryFindResource(key, out var value) && value is Color color ? color : fallback;
    }

    /// <summary>
    ///     让版本号跟随启动按钮内层控件的真实缩放，包含按下、松开与悬停动画。
    /// </summary>
    private void AttachVersionScale()
    {
        if (_btnLaunch.RealRenderTransform is not ScaleTransform scale)
            return;
        if (ReferenceEquals(scale, _launchButtonScale))
            return;

        if (_launchButtonScale is not null)
            _launchButtonScale.PropertyChanged -= OnLaunchButtonScaleChanged;
        _launchButtonScale = scale;
        _launchButtonScale.PropertyChanged += OnLaunchButtonScaleChanged;
        ApplyVersionScale();
    }

    private void OnLaunchButtonScaleChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScaleTransform.ScaleXProperty
            || e.Property == ScaleTransform.ScaleYProperty)
            ApplyVersionScale();
    }

    private void ApplyVersionScale()
    {
        if (_labVersion.RenderTransform is not ScaleTransform scale)
            return;
        scale.ScaleX = _launchButtonScale?.ScaleX ?? 1d;
        scale.ScaleY = _launchButtonScale?.ScaleY ?? 1d;
    }

    private void OnLaunchingInfoSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var deltaWidth = e.NewSize.Width - e.PreviousSize.Width;
        if (e.PreviousSize.Width > 0d
            && !_isInfoWidthAnimating
            && Math.Abs(deltaWidth) >= 1d
            && _panLaunchingInfo.Bounds.Width > 0d)
        {
            _isInfoWidthAnimating = true;
            _actualUsedWidth = _panLaunchingInfo.Width;
            var previousWidth = e.PreviousSize.Width;
            AnimationService.Run(
                new List<IAnimation>
                {
                    new WidthAnimation(
                        _panLaunchingInfo,
                        deltaWidth,
                        180,
                        ease: new FluentEaseOut()),
                    new ActionAnimation(
                        () =>
                        {
                            _isInfoWidthAnimating = false;
                            _panLaunchingInfo.Width = _actualUsedWidth;
                        },
                        true)
                },
                "Launching Info Width");
            _panLaunchingInfo.Width = previousWidth;
        }

        var deltaHeight = e.NewSize.Height - e.PreviousSize.Height;
        if (e.PreviousSize.Height > 0d
            && !_isInfoHeightAnimating
            && Math.Abs(deltaHeight) >= 1d
            && _panLaunchingInfo.Bounds.Height > 0d)
        {
            _isInfoHeightAnimating = true;
            _actualUsedHeight = _panLaunchingInfo.Height;
            var previousHeight = e.PreviousSize.Height;
            AnimationService.Run(
                new List<IAnimation>
                {
                    new HeightAnimation(
                        _panLaunchingInfo,
                        deltaHeight,
                        180,
                        ease: new FluentEaseOut()),
                    new ActionAnimation(
                        () =>
                        {
                            _isInfoHeightAnimating = false;
                            _panLaunchingInfo.Height = _actualUsedHeight;
                        },
                        true)
                },
                "Launching Info Height");
            _panLaunchingInfo.Height = previousHeight;
        }
    }

    #endregion

    #region 启动中页面

    public void PageChangeToLaunching()
    {
        ViewModel.BeginLaunching();
        _gridProgress.ColumnDefinitions[0].Width = new GridLength(0d, GridUnitType.Star);
        _gridProgress.ColumnDefinitions[1].Width = new GridLength(1d, GridUnitType.Star);
        _panLaunchingHint.IsVisible = false;
        _panLaunchingHint.Opacity = 0d;
        _panLaunchingInfo.Width = double.NaN;
        _panLaunchingInfo.Height = double.NaN;

        _panInput.IsHitTestVisible = false;
        _panLaunching.IsHitTestVisible = false;
        _loadLaunching.Text = string.Empty;
        _loadLaunching.State.LoadingState = LoadingState.Run;
        _panLaunching.IsVisible = true;

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(_panInput, -_panInput.Opacity, 110, ease: new FluentEaseIn()),
                new ScaleTransformAnimation(_panInput, 1.2d - GetScale(_panInput).ScaleX, 160),
                new OpacityAnimation(_panLaunching, 1d - _panLaunching.Opacity, 150, 100),
                new ScaleTransformAnimation(
                    _panLaunching,
                    1d - GetScale(_panLaunching).ScaleX,
                    500,
                    100,
                    new BackEaseWithPowerOut(EasePower.Weak)),
                new ActionAnimation(() => _panLaunching.IsHitTestVisible = true, 150)
            },
            "Launch State Page");
    }

    public void PageChangeToLogin()
    {
        ViewModel.EndLaunching();
        if (FindLoginPage(_loginPageCurrent) is null)
            RefreshPage(false);
        if (FindLoginPage(_loginPageCurrent) is ILoginPage loginPage)
            loginPage.Reload();
        _panInput.IsHitTestVisible = false;
        _panLaunching.IsHitTestVisible = false;
        _loadLaunching.State.LoadingState = LoadingState.Stop;
        _panInput.IsVisible = true;

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(_panLaunching, -_panLaunching.Opacity, 150),
                new ScaleTransformAnimation(
                    _panLaunching,
                    0.8d - GetScale(_panLaunching).ScaleX,
                    150,
                    ease: new FluentEaseOut(EasePower.Weak)),
                new OpacityAnimation(_panInput, 1d - _panInput.Opacity, 250, 50),
                new ScaleTransformAnimation(
                    _panInput,
                    1d - GetScale(_panInput).ScaleX,
                    300,
                    50,
                    new BackEaseWithPowerOut(EasePower.Weak)),
                new ActionAnimation(() => _panInput.IsHitTestVisible = true, 200)
            },
            "Launch State Page",
            true);
    }

    public void LaunchingRefresh()
    {
        if (!LauncherCommon.RunInUi())
        {
            LauncherCommon.RunInUi(LaunchingRefresh);
            return;
        }

        if (!ViewModel.RefreshLaunching(AnimationService.IsAnimationRunning("Launch State Page")))
            return;

        var downloadText = this.FindControl<TextBlock>("LabLaunchingDownload")!;
        var downloadLeft = this.FindControl<TextBlock>("LabLaunchingDownloadLeft")!;
        var downloadOpacity = ViewModel.HasLaunchDownloader ? 1d : 0d;
        if (Math.Abs(downloadText.Opacity - downloadOpacity) > 0.01d)
            AnimationService.Run(
                new List<IAnimation>
                {
                    new OpacityAnimation(downloadText, downloadOpacity - downloadText.Opacity, 100),
                    new OpacityAnimation(
                        downloadLeft,
                        (downloadOpacity > 0d ? 0.5d : 0d) - downloadLeft.Opacity,
                        100)
                },
                "Launching Download");

        var shouldShowHint = ViewModel.ShouldShowHint;
        if (shouldShowHint != _panLaunchingHint.IsVisible)
        {
            if (shouldShowHint)
                _panLaunchingHint.IsVisible = true;

            AnimationService.Run(
                new OpacityAnimation(
                    _panLaunchingHint,
                    (shouldShowHint ? 1d : 0d) - _panLaunchingHint.Opacity,
                    100),
                "Launching Hint");
            if (!shouldShowHint)
                _panLaunchingHint.IsVisible = false;
        }

        var columns = _gridProgress.ColumnDefinitions;
        AnimationService.Run(
            new List<IAnimation>
            {
                new GridLengthWidthAnimation(
                    columns[0],
                    ViewModel.ShowProgress - columns[0].Width.Value,
                    260,
                    ease: new FluentEaseOut()),
                new GridLengthWidthAnimation(
                    columns[1],
                    1d - ViewModel.ShowProgress - columns[1].Width.Value,
                    260,
                    ease: new FluentEaseOut())
            },
            "Launching Progress");
    }

    private static ScaleTransform GetScale(Visual visual)
    {
        if (visual.RenderTransform is ScaleTransform scale)
            return scale;
        scale = new ScaleTransform();
        visual.RenderTransform = scale;
        return scale;
    }

    #endregion

    #region 登录页面切换

    public void RefreshPage(bool anim, GameLaunch.McLoginType targetLoginType = default)
    {
        var kind = ViewModel.ResolveLoginPageKind(targetLoginType);

        if (_loginPageCurrent == kind)
        {
            (FindLoginPage(kind) as ILoginPage)?.Reload();
            return;
        }

        PageChange(kind, anim);
    }

    private Control GetLoginPage(LaunchPageKind kind)
    {
        foreach (var pair in _loginPages.Where(pair => pair.Key == kind))
            return pair.Value;

        Control page = kind switch
        {
            LaunchPageKind.Auth => new PageLoginAuth(),
            LaunchPageKind.Ms => new PageLoginMs(),
            LaunchPageKind.Profile => new PageLoginProfile(),
            LaunchPageKind.ProfileSkin => new PageLoginProfileSkin(),
            LaunchPageKind.Offline => new PageLoginOffline(),
            LaunchPageKind.None or _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "登录分页种类无效")
        };

        if (page is ILoginPage_Bindable bindable)
        {
            bindable.BackRequested += () => RefreshPage(true);
            bindable.LoginTypeRequested += type => RefreshPage(true, type);
            bindable.RefreshRequested += () =>
            {
                RefreshPage(true);
                RefreshButtonsUI();
            };
        }

        _loginPages.Add(new KeyValuePair<LaunchPageKind, Control>(kind, page));
        return page;
    }

    private Control? FindLoginPage(LaunchPageKind kind)
    {
        return (
            from pair in _loginPages
            where pair.Key == kind
            select pair.Value).FirstOrDefault();
    }

    private void PageChange(LaunchPageKind kind, bool anim)
    {
        try
        {
            AnimationService.CancelAnimationByName("FrmLogin PageChange");
            var pageNew = GetLoginPage(kind);

            if (!anim)
            {
                SwapLoginPage(pageNew);
                _loginPageCurrent = kind;
                return;
            }

            AnimationService.Run(
                new List<IAnimation>
                {
                    new OpacityAnimation(
                        _panLogin,
                        -_panLogin.Opacity,
                        100,
                        ease: new FluentEaseOut()),
                    new ActionAnimation(() => SwapLoginPage(pageNew), 100),
                    new OpacityAnimation(
                        _panLogin,
                        1d - _panLogin.Opacity,
                        100,
                        120,
                        new FluentEaseIn())
                },
                "FrmLogin PageChange");
            _loginPageCurrent = kind;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text(
                    "Launch.Account.Error.SwitchPage",
                    LauncherCommon.GetStringFromEnum(kind) ?? ""),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text(
                    "Launch.Account.Error.SwitchPage",
                    LauncherCommon.GetStringFromEnum(kind) ?? ""));
        }
    }

    private void SwapLoginPage(Control pageNew)
    {
        AnimationService.BeginControl();
        _panLogin.Opacity = 1d;
        _panLogin.Children.Clear();
        _panLogin.Children.Add(pageNew);
        (pageNew as ILoginPage)?.Reload();
        AnimationService.EndControl();
    }

    #endregion

    #region 壳与业务 UI 接口

    /// <inheritdoc />
    public void RefreshLaunchButtons()
    {
        RefreshButtonsUI();
    }

    /// <inheritdoc />
    public void RefreshLaunchPage(bool show)
    {
        RefreshButtonsUI();
        RefreshPage(show);
    }

    /// <inheritdoc />
    public void ShowLogin()
    {
        PageChangeToLogin();
    }

    /// <inheritdoc />
    public void ShowLaunching()
    {
        PageChangeToLaunching();
    }

    /// <inheritdoc />
    public void RefreshLaunching()
    {
        LaunchingRefresh();
    }

    /// <inheritdoc />
    public bool ToggleHiddenInstances()
    {
        return false;
    }

    /// <inheritdoc />
    public bool LaunchGame()
    {
        if (!ViewModel.IsLaunchButtonEnabled)
            return false;
        LaunchButtonClick();
        return true;
    }

    private void ResetAprilState()
    {
        if (_panLaunch.RenderTransform is not TransformGroup group)
            return;
        foreach (var transform in group.Children)
            switch (transform)
            {
                case ScaleTransform scale:
                    scale.ScaleX = 1d;
                    scale.ScaleY = 1d;
                    break;
                case TranslateTransform translate:
                    translate.X = 0d;
                    translate.Y = 0d;
                    break;
            }
    }

    private void OnAuthlibServerDropped(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
            return;
        ProfileService.IsCreatingProfile = true;
        PageLoginAuth.PendingAuthServer = server;
        _loginPageCurrent = LaunchPageKind.None;
        RefreshPage(true, GameLaunch.McLoginType.Auth);
    }

    #endregion
}