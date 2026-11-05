using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using Path = Avalonia.Controls.Shapes.Path;

namespace PCL.UI.Controls;

/// <summary>
///     加载状态提示：镐子图标加一行状态文本，可叠加进度值或切换为错误态。
///     状态由 <see cref="State" />、<see cref="ShowProgress" /> 与错误文本继承决定，
///     鼠标左键点击会触发 <c>Click</c> 并调用 <see cref="Command" />；镐子挥舞循环与错误叉过渡
///     由 <c>PCL.Animations</c> 驱动。
/// </summary>
public class MyLoading : Grid
{
    public delegate void ClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void IsErrorChangedEventHandler(object sender, bool isError);

    public delegate void StateChangedEventHandler(
        object sender,
        LoadingState newState,
        LoadingState oldState);

    private const string PickaxeData =
        "M 963.6 858.2 410.816 305.504 C 508.116 213.304 609.204 196.8 711.104 128.6 "
        + "837.11367 49.573762 879.34045 50.334062 751.5 49.5 611.3 52 471.8 96.2 353.3 182.4 "
        + "309.8 155.7 252.1 161.2 214.5 198.9 176.9 236.6 171.3 294.2 198 337.7 "
        + "111.8 456.3 67.6 595.8 65.1 735.9 63.315254 883.82034 65.077966 837.29308 144.2 695.488 "
        + "212.4 593.588 228.888 492.4 321.088 395.2 L 873.9 948 c 0.60001 0.59999 1.6 0.6 2.2 0 "
        + "l 87.5 -87.5 c 0.6 -0.7 0.6 -1.6 0 -2.3 z";

    private const string ArrowData = "M0,0L2,0L1,4";

    private const string CrossData =
        "M2,0 L0,2 8,10 0,18 2,20 10,12 18,20 20,18 12,10 20,2 18,0 10,8 2,0Z";

    private static readonly string[] NetworkUnstableKeywords =
    [
        "远程主机强迫关闭了", "远程方已关闭传输流", "未能解析此远程名称", "由于目标计算机积极拒绝",
        "操作已超时", "操作超时", "服务器超时", "连接超时"
    ];

    private static int _uuidSeed;

    public static readonly StyledProperty<SolidColorBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyLoading, SolidColorBrush?>(nameof(Foreground));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyLoading, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> TextErrorProperty =
        AvaloniaProperty.Register<MyLoading, string>(nameof(TextError), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyLoading, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyLoading, object?>(nameof(CommandParameter));

    private readonly TextBlock _labText;
    private readonly Rectangle _line;
    private readonly Path _pathError;
    private readonly Path _pathLeft;

    private readonly Path _pathPickaxe;
    private readonly Path _pathRight;

    private readonly object _stateLock = new();

    private readonly int _uuid = Interlocked.Increment(ref _uuidSeed);

    private bool _applyingThemeColors;
    private SolidColorBrush _foregroundBrush = new(Colors.Black);
    private bool _foregroundIsThemeOwned = true;
    private bool _isCommandRunning;

    private bool _isMouseDown;
    private ILoadingTrigger? _state;
    private bool _suppressDefaultTracking;

    private Action? _unwatchTheme;
    private bool _useDefaultText = true;
    private bool _useDefaultTextError = true;

    public MyLoading()
    {
        MinWidth = 50d;
        MinHeight = 50d;
        Background = Brushes.Transparent;
        RowDefinitions = new RowDefinitions("*,Auto,Auto,*");

        _pathPickaxe = new Path
        {
            Name = "PathPickaxe",
            Stretch = Stretch.Uniform,
            Height = 35d,
            Margin = new Thickness(10d, 6d, 0d, 0d),
            StrokeThickness = 2d,
            Data = Geometry.Parse(PickaxeData),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            RenderTransform = new RotateTransform
            {
                Angle = 55d,
                CenterX = 30d,
                CenterY = 30d
            }
        };
        _pathLeft = _CreateArrow("PathLeft", -45d, new Thickness(7d, 41d, 0d, 0d));
        _pathRight = _CreateArrow("PathRight", 45d, new Thickness(14d, 41d, 0d, 0d));
        _pathError = new Path
        {
            Name = "PathError",
            Opacity = 0d,
            Stretch = Stretch.Uniform,
            Width = 17d,
            Height = 17d,
            Data = Geometry.Parse(CrossData),
            Margin = new Thickness(4d, 23d, 0d, 0d),
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(0.6d, 0.6d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _line = new Rectangle
        {
            Width = 25d,
            Height = 2d,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _labText = new TextBlock
        {
            Name = "LabText",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = 16d,
            Margin = new Thickness(0d, 10d, 0d, 0d)
        };
        SetRow(_labText, 2);

        var iconGrid = new Grid
        {
            Width = 60d,
            Height = 47d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        iconGrid.Children.Add(_pathPickaxe);
        iconGrid.Children.Add(_pathLeft);
        iconGrid.Children.Add(_pathRight);
        iconGrid.Children.Add(_pathError);
        iconGrid.Children.Add(_line);
        SetRow(iconGrid, 1);

        Children.Add(iconGrid);
        Children.Add(_labText);

        FeedbackTheme.AttachStyles(this);

        _suppressDefaultTracking = true;
        SetCurrentValue(TextProperty, Lang.Text("Application.Control.Loading.Default"));
        SetCurrentValue(TextErrorProperty, Lang.Text("Application.Control.Loading.Failed"));
        _suppressDefaultTracking = false;

        IsErrorChanged += (_, _) => RefreshText();
        Loaded += (_, _) =>
        {
            _ApplyThemeColors();
            RefreshText();
            InitState();
            RefreshState();
        };
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(LoopKey);
            PCL.Animations.AniStop(ErrorKey);
            _isLooping = false;
            RefreshState();
        };
        AttachedToVisualTree += (_, _) =>
        {
            _unwatchTheme?.Invoke();
            _unwatchTheme = FeedbackTheme.WatchTheme(this, _ApplyThemeColors);
            LocalizationService.LanguageChanged += _OnLanguageChanged;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _unwatchTheme?.Invoke();
            _unwatchTheme = null;
            LocalizationService.LanguageChanged -= _OnLanguageChanged;
        };

        PointerReleased += _OnPointerReleased;
        PointerPressed += _OnPointerPressed;
        PointerExited += (_, _) => _isMouseDown = false;

        _ApplyThemeColors();
    }

    /// <summary>
    ///     控件是否在加载完成后自动进入运行状态。
    /// </summary>
    public bool AutoRun { get; set; } = true;

    #region 颜色

    public SolidColorBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    #endregion

    public event IsErrorChangedEventHandler? IsErrorChanged;

    public event StateChangedEventHandler? StateChanged;

    public event ClickEventHandler? Click;

    private static Path _CreateArrow(string name, double angle, Thickness margin)
    {
        return new Path
        {
            Name = name,
            Opacity = 0d,
            Stretch = Stretch.Uniform,
            Width = 3d,
            Height = 5d,
            Data = Geometry.Parse(ArrowData),
            Margin = margin,
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            RenderTransform = new RotateTransform
            {
                Angle = angle
            },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    #region 文本

    private bool _showProgress;

    public bool ShowProgress
    {
        get => _showProgress;
        set
        {
            if (_showProgress == value)
                return;
            _showProgress = value;
            RefreshText();
        }
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string TextError
    {
        get => GetValue(TextErrorProperty);
        set => SetValue(TextErrorProperty, value);
    }

    /// <summary>
    ///     是否在使用 Loader 时使用 Loader 的错误输出来替换默认的错误文本显示。
    /// </summary>
    public bool TextErrorInherit { get; set; } = true;

    private void RefreshText()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshTextCore();
        else
            Dispatcher.UIThread.Post(RefreshTextCore);
    }

    private void RefreshTextCore()
    {
        var state = State;
        if (InnerState == LoadingState.Error)
        {
            if (TextErrorInherit && state.IsLoader)
            {
                var ex = state.Error;
                if (ex is null)
                {
                    _labText.Text = Lang.Text("Application.Control.Loading.UnknownError");
                }
                else
                {
                    while (ex.InnerException is not null) ex = ex.InnerException;
                    var text = StrTrim(ex.Message);
                    _labText.Text = text;
                    if (NetworkUnstableKeywords.Any(text.Contains))
                        _labText.Text = Lang.Text("Application.Control.Loading.NetworkUnstable");
                }
            }
            else
            {
                _labText.Text = TextError;
            }
        }
        else if (ShowProgress && state.IsLoader)
        {
            _labText.Text = $"{Text} - {Lang.Number(state.Progress, "P0")}";
        }
        else
        {
            _labText.Text = Text;
        }
    }

    /// <summary>
    ///     移除字符串首尾的标点符号、回车，以及括号中、冒号后的补充说明内容，
    ///     用于把异常信息压成一行状态标题。
    /// </summary>
    private static string StrTrim(string text)
    {
        text = text.Split("（")[0].Split("：")[0].Split('(')[0].Split(':')[0];
        return text.Trim('.', '。', '！', ' ', '!', '?', '？', '\r', '\n');
    }

    private void _OnLanguageChanged()
    {
        if (!_useDefaultText && !_useDefaultTextError)
            return;

        _suppressDefaultTracking = true;
        if (_useDefaultText)
            SetCurrentValue(TextProperty, Lang.Text("Application.Control.Loading.Default"));
        if (_useDefaultTextError)
            SetCurrentValue(TextErrorProperty, Lang.Text("Application.Control.Loading.Failed"));
        _suppressDefaultTracking = false;
        RefreshText();
    }

    #endregion

    #region 状态改变

    private ILoadingTrigger? CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        set
        {
            lock (_stateLock)
            {
                if (_state is not null)
                {
                    _state.ProgressChanged -= _OnStateProgressChanged;
                    _state.LoadingStateChanged -= _OnStateLoadingStateChanged;
                }

                _state = value;
                if (_state is not null)
                {
                    _state.ProgressChanged += _OnStateProgressChanged;
                    _state.LoadingStateChanged += _OnStateLoadingStateChanged;
                }
            }
        }
    }

    private void _OnStateProgressChanged(double newProgress, double oldProgress)
    {
        RefreshText();
    }

    private void _OnStateLoadingStateChanged(LoadingState newState, LoadingState oldState)
    {
        RefreshState();
    }

    public ILoadingTrigger State
    {
        get
        {
            InitState();
            return CurrentState!;
        }
        set
        {
            CurrentState = value;
            RefreshState();
        }
    }

    private void InitState()
    {
        if (CurrentState is not null)
            return;

        var simulator = new MyLoadingStateSimulator();
        CurrentState = simulator;
        if (AutoRun)
            simulator.LoadingState = LoadingState.Run;
    }

    private void RefreshState()
    {
        var state = State;
        if (state.LoadingState == LoadingState.Run && !IsLoaded)
            InnerState = LoadingState.Stop;
        InnerState = state.LoadingState;
        OuterState = state.LoadingState;
        AniLoop();
    }

    private LoadingState _outerState = LoadingState.Unloaded;

    /// <summary>
    ///     用于引发外部事件的状态。
    /// </summary>
    private LoadingState OuterState
    {
        get => _outerState;
        set
        {
            if (_outerState == value)
                return;

            var oldValue = _outerState;
            _outerState = value;
            StateChanged?.Invoke(this, value, oldValue);
            if (oldValue == LoadingState.Error != (value == LoadingState.Error))
                IsErrorChanged?.Invoke(this, value == LoadingState.Error);
        }
    }

    private LoadingState _innerState = LoadingState.Unloaded;

    /// <summary>
    ///     用于引发内部动画事件的状态。
    /// </summary>
    private LoadingState InnerState
    {
        get => _innerState;
        set
        {
            if (_innerState == value)
                return;

            var oldValue = _innerState;
            _innerState = value;
            AniLoop();
            if (oldValue == LoadingState.Error != (value == LoadingState.Error))
                ErrorAnimation(value == LoadingState.Error);
        }
    }

    #endregion

    #region 动画

    /// <summary>
    ///     是否需要动画。
    /// </summary>
    public bool HasAnimation { get; set; } = true;

    private bool _isLooping;

    /// <summary>镐子是否还没挥下去，要求错误动画等待。</summary>
    private bool _errorAnimationWaiting;

    private string LoopKey => FeedbackAnimation.Name("MyLoading Loop", _uuid);

    private string ErrorKey => FeedbackAnimation.Name("MyLoading Error", _uuid);

    /// <summary>
    ///     镐子挥动循环：旋转镐子，挥舞结束后重置箭头位置并淡出，再在组末尾递归启动下一轮。
    ///     动画关闭或速度过高（<see cref="Animations.AniSpeed" /> &gt; 10）时保持静止。
    /// </summary>
    private void AniLoop()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(AniLoop);
            return;
        }

        // 这坨循环代码也是老屎坑了，救救.jpg
        if (!HasAnimation
            || _isLooping
            || InnerState != LoadingState.Run
            || !FeedbackAnimation.CanAnimate(this)
            || PCL.Animations.AniSpeed > 10d)
            return;

        _isLooping = true;
        _errorAnimationWaiting = true;

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaRotateTransform(
                    _pathPickaxe,
                    -20d - FeedbackAnimation.CurrentAngle(_pathPickaxe),
                    350,
                    250,
                    new PCL.Animations.AniEaseInBack(PCL.Animations.AniEasePower.Weak)),
                PCL.Animations.AaRotateTransform(
                    _pathPickaxe,
                    50d,
                    900,
                    ease: new PCL.Animations.AniEaseOutFluent(),
                    after: true),
                PCL.Animations.AaRotateTransform(
                    _pathPickaxe,
                    25d,
                    900,
                    ease: new PCL.Animations.AniEaseOutElastic(PCL.Animations.AniEasePower.Weak)),
                PCL.Animations.AaCode(() =>
                {
                    _pathLeft.Opacity = 1d;
                    _pathLeft.Margin = new Thickness(7d, 41d, 0d, 0d);
                    _pathRight.Opacity = 1d;
                    _pathRight.Margin = new Thickness(14d, 41d, 0d, 0d);
                    _errorAnimationWaiting = false;
                }),
                PCL.Animations.AaOpacity(_pathLeft, -1d, 100, 50),
                PCL.Animations.AaX(
                    _pathLeft,
                    -5d,
                    180,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaY(
                    _pathLeft,
                    -6d,
                    180,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaOpacity(_pathRight, -1d, 100, 50),
                PCL.Animations.AaX(
                    _pathRight,
                    5d,
                    180,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaY(
                    _pathRight,
                    -6d,
                    180,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaCode(
                    () =>
                    {
                        _isLooping = false;
                        AniLoop();
                    },
                    after: true)
            },
            LoopKey);
    }

    /// <summary>
    ///     错误状态过渡：图标与文字底色渐变到错误色，错误叉淡入/缩放弹出；恢复时反向收起。
    /// </summary>
    private void ErrorAnimation(bool isError)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ErrorAnimation(isError));
            return;
        }

        _foregroundIsThemeOwned = true;
        var target = FeedbackAnimation.ThemeColor(
            this,
            isError ? "ColorBrushError" : "ColorBrush3");
        var wait = _errorAnimationWaiting ? 400 : 0;

        if (target is null || !FeedbackAnimation.CanAnimate(this))
        {
            _ApplyThemeColors();
            _pathError.Opacity = isError ? 1d : 0d;
            SetErrorScale(isError ? 1d : 0.5d);
            return;
        }

        var animations = new List<PCL.Animations.AniData>
        {
            PCL.Animations.AaOpacity(
                _pathError,
                (isError ? 1d : 0d) - _pathError.Opacity,
                100,
                isError ? 300 + wait : 0),
            PCL.Animations.AaScaleTransform(
                _pathError,
                (isError ? 1d : 0.5d) - FeedbackAnimation.CurrentScale(_pathError),
                isError ? 400 : 200,
                isError ? 300 + wait : 0,
                isError ? new PCL.Animations.AniEaseOutBack() : null)
        };

        foreach (var (targetControl, property) in new (Control Control, AvaloniaProperty Property)[]
                 {
                     (_labText, TextBlock.ForegroundProperty),
                     (_pathPickaxe, Shape.StrokeProperty),
                     (_pathLeft, Shape.FillProperty),
                     (_pathRight, Shape.FillProperty),
                     (_pathError, Shape.FillProperty),
                     (_line, Shape.FillProperty)
                 })
            animations.Add(FeedbackAnimation.ColorTo(targetControl, property, target, 300));

        animations.Add(
            PCL.Animations.AaCode(
                () =>
                {
                    _foregroundIsThemeOwned = true;
                    _ApplyThemeColors();
                },
                after: true));

        PCL.Animations.AniStart(animations, ErrorKey);
    }

    private void SetErrorScale(double scale)
    {
        if (_pathError.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            return;
        }

        _pathError.RenderTransformOrigin = RelativePoint.Center;
        _pathError.RenderTransform = new ScaleTransform(scale, scale);
    }

    private void _ApplyThemeColors()
    {
        if (_applyingThemeColors) return;
        _applyingThemeColors = true;
        try
        {
            _ApplyThemeColorsCore();
        }
        finally
        {
            _applyingThemeColors = false;
        }
    }

    private void _ApplyThemeColorsCore()
    {
        if (_foregroundIsThemeOwned)
        {
            var brush = InnerState == LoadingState.Error
                ? FeedbackTheme.GetBrush(this, "ColorBrushError", Brushes.Red)
                : FeedbackTheme.GetBrush(this, "ColorBrush3", Brushes.DodgerBlue);
            _foregroundBrush = brush as SolidColorBrush ?? new SolidColorBrush(Colors.Black);

            // 只有画刷真的变化时才写自身属性，避免触发多余的属性变更与重入
            if (!ReferenceEquals(Foreground, _foregroundBrush))
                SetCurrentValue(ForegroundProperty, _foregroundBrush);
        }
        else
        {
            _foregroundBrush = Foreground ?? new SolidColorBrush(Colors.Black);
        }

        var foreground = _foregroundBrush;
        _labText.Foreground = foreground;
        _pathPickaxe.Stroke = foreground;
        _pathLeft.Fill = foreground;
        _pathRight.Fill = foreground;
        _pathError.Fill = foreground;
        _line.Fill = foreground;
    }

    #endregion

    #region 点击事件

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            if (!_suppressDefaultTracking)
                _useDefaultText = false;
            RefreshText();
        }
        else if (change.Property == TextErrorProperty)
        {
            if (!_suppressDefaultTracking)
                _useDefaultTextError = false;
            RefreshText();
        }
        else if (change.Property == ForegroundProperty)
        {
            if (_foregroundIsThemeOwned && ReferenceEquals(change.NewValue, _foregroundBrush))
                return;

            if (!_suppressDefaultTracking
                && change.NewValue is SolidColorBrush brush
                && !ReferenceEquals(brush, _foregroundBrush))
                _foregroundIsThemeOwned = false;
            _ApplyThemeColors();
        }
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        Click?.Invoke(this, e);
        ExecuteCommand();
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 鼠标点击判定（务必放在点击事件之后，以使得点击先于鼠标离开执行）
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            _isMouseDown = true;
    }

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;

        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }

    #endregion
}