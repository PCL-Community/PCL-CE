using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static PCL.MyLoading;
using PCL.Core.App.Localization;

namespace PCL;

public partial class MyLoading
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    public delegate void IsErrorChangedEventHandler(object sender, bool isError);

    public delegate void StateChangedEventHandler(object sender, MyLoadingState newState, MyLoadingState oldState);

    private readonly int uuid = ModBase.GetUuid();

    public bool AutoRun { get; set; } = true;

    public event IsErrorChangedEventHandler? IsErrorChanged;
    public event StateChangedEventHandler? StateChanged;
    public event ClickEventHandler? Click;

    #region 颜色

    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register("Foreground", typeof(SolidColorBrush), typeof(MyLoading));

    public MyLoading()
    {
        InitializeComponent();
        SetCurrentValue(TextProperty, Lang.Text("Application.Control.Loading.Default"));
        SetCurrentValue(TextErrorProperty, Lang.Text("Application.Control.Loading.Failed"));
        SetResourceReference(ForegroundProperty, "ColorBrush3");
        IsErrorChanged += (_, _) => RefreshText();
        Loaded += (_, _) => RefreshText();
        Loaded += (_, _) => InitState();
        Loaded += (_, _) => RefreshState();
        Unloaded += (_, _) => RefreshState();
        MouseLeftButtonUp += Button_MouseUp;
        MouseLeftButtonDown += Button_MouseDown;
        MouseLeave += Button_MouseLeave;
        MouseLeftButtonUp += Button_MouseLeave;
    }

    #endregion

    #region 文本

    private bool _ShowProgress { get; set; }

    public bool ShowProgress
    {
        get => _ShowProgress;
        set
        {
            if (_ShowProgress == value)
                return;
            _ShowProgress = value;
            RefreshText();
        }
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(MyLoading),
            new PropertyMetadata("", (d, e) => ((MyLoading)d).RefreshText()));

    public string TextError
    {
        get => (string)GetValue(TextErrorProperty);
        set => SetValue(TextErrorProperty, value);
    }

    public static readonly DependencyProperty TextErrorProperty =
        DependencyProperty.Register("TextError", typeof(string), typeof(MyLoading),
            new PropertyMetadata("", (d, e) => ((MyLoading)d).RefreshText()));

    /// <summary>
    ///     是否在使用 Loader 时使用 Loader 的错误输出来替换默认的错误文本显示。
    /// </summary>
    public bool TextErrorInherit { get; set; } = true;

    private void RefreshText()
    {
        ModBase.RunInUi(() =>
        {
            if (InnerState == MyLoadingState.Error)
            {
                if (TextErrorInherit && State.IsLoader)
                {
                    var ex = State.Error;
                    if (ex is null)
                    {
                        LabText.Text = Lang.Text("Application.Control.Loading.UnknownError");
                    }
                    else
                    {
                        while (ex.InnerException is not null) ex = ex.InnerException;
                        LabText.Text = ModBase.StrTrim(ex.Message).ToString();
                        if (new[]
                            {
                                "远程主机强迫关闭了", "远程方已关闭传输流", "未能解析此远程名称", "由于目标计算机积极拒绝", "操作已超时", "操作超时", "服务器超时", "连接超时"
                            }.Any(s => LabText.Text.Contains(s)))
                            LabText.Text = Lang.Text("Application.Control.Loading.NetworkUnstable");
                    }
                }
                else
                {
                    LabText.Text = TextError;
                }
            }
            else if (ShowProgress && State.IsLoader)
            {
                LabText.Text = Text + " - " + Lang.Number(State.Progress, "P0");
            }
            else
            {
                LabText.Text = Text;
            }
        });
    }

    #endregion

    #region 状态改变

    // 状态枚举
    public enum MyLoadingState
    {
        Unloaded = -1,
        Run = 0,
        Stop = 1,
        Error = 2
    }

    // 用于外部改变的公开状态
    private ILoadingTrigger _State
    {
        [MethodImpl(MethodImplOptions.Synchronized)]
        get => field;

        [MethodImpl(MethodImplOptions.Synchronized)]
        set
        {
            if (field is not null)
            {
                field.ProgressChanged -= (_, _) => RefreshText();
                field.LoadingStateChanged -= (_, _) => RefreshState();
            }

            field = value;
            if (field is not null)
            {
                field.ProgressChanged += (_, _) => RefreshText();
                field.LoadingStateChanged += (_, _) => RefreshState();
            }
        }
    }

    public ILoadingTrigger State
    {
        get
        {
            InitState();
            return _State;
        }
        set
        {
            _State = value;
            RefreshState();
        }
    }

    private void InitState()
    {
        if (_State is null)
        {
            _State = new MyLoadingStateSimulator();
            if (AutoRun)
                _State.LoadingState = MyLoadingState.Run;
        }
    }

    private void RefreshState()
    {
        if (_State.LoadingState == MyLoadingState.Run && !IsLoaded)
            InnerState = MyLoadingState.Stop;
        InnerState = _State.LoadingState;
        OuterState = _State.LoadingState;
        AniLoop();
    }

    // 用于引发外部事件的状态
    private MyLoadingState _OuterState { get; set; } = MyLoadingState.Unloaded;

    private MyLoadingState OuterState
    {
        get => _OuterState;
        set
        {
            if (_OuterState == value)
                return;
            var oldValue = _OuterState;
            _OuterState = value;
            // 引发事件
            StateChanged?.Invoke(this, value, oldValue);
            if (oldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                IsErrorChanged?.Invoke(this, value == MyLoadingState.Error);
        }
    }


    // 用于引发内部动画事件的状态
    private MyLoadingState _InnerState { get; set; } = MyLoadingState.Unloaded;

    private MyLoadingState InnerState
    {
        get => _InnerState;
        set
        {
            if (_InnerState == value)
                return;
            var oldValue = _InnerState;
            _InnerState = value;
            // 引发事件
            AniLoop();
            if (oldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                ErrorAnimation(this, value == MyLoadingState.Error);
        }
    }

    #endregion

    #region 动画

    /// <summary>
    ///     是否需要动画。
    /// </summary>
    public bool HasAnimation { get; set; } = true;

    /// <summary>
    ///     主动画循环是否正在运行中。
    /// </summary>
    private bool isLooping;

    private void AniLoop()
    {
        // 这坨循环代码也是老屎坑了，救救.jpg
        if (!HasAnimation || isLooping || !(InnerState == MyLoadingState.Run) || ModAnimation.aniSpeed > 10d ||
            !IsLoaded)
            return;
        isLooping = true;
        errorAnimationWaiting = true;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaRotateTransform(PathPickaxe, -20 - ((RotateTransform)PathPickaxe.RenderTransform).Angle, 350,
                250, new ModAnimation.AniEaseInBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaRotateTransform(PathPickaxe, 50d, 900, ease: new ModAnimation.AniEaseOutFluent(),
                after: true),
            ModAnimation.AaRotateTransform(PathPickaxe, 25d, 900,
                ease: new ModAnimation.AniEaseOutElastic(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                PathLeft.Opacity = 1d;
                PathLeft.Margin = new Thickness(7d, 41d, 0d, 0d);
                PathRight.Opacity = 1d;
                PathRight.Margin = new Thickness(14d, 41d, 0d, 0d);
                errorAnimationWaiting = false;
            }),
            ModAnimation.AaOpacity(PathLeft, -1, 100, 50),
            ModAnimation.AaX(PathLeft, -5, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaY(PathLeft, -6, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(PathRight, -1, 100, 50),
            ModAnimation.AaX(PathRight, 5d, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaY(PathRight, -6, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
            {
                isLooping = false;
                AniLoop();
            }, after: true)
        }, "MyLoader Loop " + uuid + "/" + ModBase.GetUuid());
    }

    /// <summary>
    ///     镐子是否还没挥下去，要求错误动画等待。
    /// </summary>
    private bool errorAnimationWaiting;

    private void ErrorAnimation(object sender, bool isError)
    {
        if (isError)
        {
            // 非错误变为错误
            var wait = errorAnimationWaiting ? 400 : 0;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(PanBack, ForegroundProperty, "ColorBrushRedLight", 300),
                    ModAnimation.AaOpacity(PathError, 1d - PathError.Opacity, 100, 300 + wait),
                    ModAnimation.AaScaleTransform(PathError, 1d - ((ScaleTransform)PathError.RenderTransform).ScaleX,
                        400, 300 + wait, new ModAnimation.AniEaseOutBack())
                }, "MyLoader Error " + uuid);
        }
        else
        {
            // 错误变为非错误
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(PathError, -PathError.Opacity, 100),
                    ModAnimation.AaScaleTransform(PathError, 0.5d - ((ScaleTransform)PathError.RenderTransform).ScaleX,
                        200),
                    ModAnimation.AaColor(PanBack, ForegroundProperty, "ColorBrush3", 300)
                }, "MyLoader Error " + uuid);
        }
    }

    #endregion

    #region 点击事件

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        Click?.Invoke(sender, e);
    }

    private bool isMouseDown;

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
        isMouseDown = true;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        isMouseDown = false;
    }

    #endregion
}

public interface ILoadingTrigger
{
    delegate void LoadingStateChangedEventHandler(MyLoadingState newState, MyLoadingState oldState);

    delegate void ProgressChangedEventHandler(double newProgress, double oldProgress);

    bool IsLoader { get; }

    double Progress { get; }
    Exception? Error { get; }

    MyLoadingState LoadingState { get; set; }
    event LoadingStateChangedEventHandler? LoadingStateChanged;
    event ProgressChangedEventHandler? ProgressChanged;
}

public class MyLoadingStateSimulator : ILoadingTrigger
{
    private MyLoadingState _LoadingState { get; set; } = MyLoadingState.Unloaded;

    public MyLoadingState LoadingState
    {
        get => _LoadingState;
        set
        {
            if (_LoadingState == value)
                return;
            var oldState = _LoadingState;
            _LoadingState = value;
            LoadingStateChanged?.Invoke(value, oldState);
        }
    }

    public bool IsLoader { get; } = false;

    public double Progress => 0;
    public Exception? Error => null;

    public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;
    public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;
}
