// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1711, CA1716
public partial class MyLoading : Grid
{
    public delegate void ClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void IsErrorChangedEventHandler(object sender, bool isError);

    public delegate void StateChangedEventHandler(object sender, MyLoadingState newState, MyLoadingState oldState);

    public enum MyLoadingState
    {
        Unloaded = -1,
        Run = 0,
        Stop = 1,
        Error = 2
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyLoading, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> TextErrorProperty =
        AvaloniaProperty.Register<MyLoading, string>(nameof(TextError), "加载失败");

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyLoading, IBrush?>(nameof(Foreground), new SolidColorBrush(Color.Parse("#1370f3")));

    private readonly TextBlock? _label;
    private readonly PathShape? _pickaxe;
    private readonly PathShape? _leftShard;
    private readonly PathShape? _rightShard;
    private readonly PathShape? _errorIcon;
    private readonly Rectangle? _bottomLine;
    private readonly int _uuid = Random.Shared.Next();
    private ILoadingTrigger? _state;
    private MyLoadingState _outerState = MyLoadingState.Unloaded;
    private MyLoadingState _innerState = MyLoadingState.Unloaded;
    private bool _showProgress;
    private bool _isLooping;
    private bool _isAttached;
    private bool _isMouseDown;
    private bool _errorAnimationWaiting;

    public MyLoading()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _pickaxe = this.FindControl<PathShape>("PathPickaxe");
        _leftShard = this.FindControl<PathShape>("PathLeft");
        _rightShard = this.FindControl<PathShape>("PathRight");
        _errorIcon = this.FindControl<PathShape>("PathError");
        _bottomLine = this.FindControl<Rectangle>("LineBottom");

        this.GetObservable(TextProperty).Subscribe(_ => RefreshText());
        this.GetObservable(TextErrorProperty).Subscribe(_ => RefreshText());
        this.GetObservable(ForegroundProperty).Subscribe(SyncForeground);

        IsErrorChanged += (_, _) => RefreshText();
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            InitState();
            RefreshText();
            RefreshState();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAttached = false;
            InnerState = MyLoadingState.Stop;
            StopLoopAnimation();
        };
        PointerPressed += Button_PointerPressed;
        PointerReleased += Button_PointerReleased;
        PointerExited += (_, _) => _isMouseDown = false;
        PointerReleased += (_, _) => _isMouseDown = false;
    }

    public bool AutoRun { get; set; } = true;

    public bool HasAnimation { get; set; } = true;

    public bool TextErrorInherit { get; set; } = true;

    public event IsErrorChangedEventHandler? IsErrorChanged;

    public event StateChangedEventHandler? StateChanged;

    public event ClickEventHandler? Click;

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

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public ILoadingTrigger State
    {
        get
        {
            InitState();
            return _state!;
        }
        set
        {
            SetState(value);
            RefreshState();
        }
    }

    private MyLoadingState OuterState
    {
        get => _outerState;
        set
        {
            if (_outerState == value)
                return;

            MyLoadingState oldValue = _outerState;
            _outerState = value;
            StateChanged?.Invoke(this, value, oldValue);
            if (oldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                IsErrorChanged?.Invoke(this, value == MyLoadingState.Error);
        }
    }

    private MyLoadingState InnerState
    {
        get => _innerState;
        set
        {
            if (_innerState == value)
                return;

            MyLoadingState oldValue = _innerState;
            _innerState = value;
            StartLoopAnimation();
            if (oldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                ErrorAnimation(value == MyLoadingState.Error);
        }
    }

    private void InitState()
    {
        if (_state is not null)
            return;

        MyLoadingStateSimulator simulator = new();
        SetState(simulator);
        if (AutoRun)
            simulator.LoadingState = MyLoadingState.Run;
    }

    private void SetState(ILoadingTrigger value)
    {
        if (_state is not null)
        {
            _state.ProgressChanged -= State_ProgressChanged;
            _state.LoadingStateChanged -= State_LoadingStateChanged;
        }

        _state = value;
        _state.ProgressChanged += State_ProgressChanged;
        _state.LoadingStateChanged += State_LoadingStateChanged;
    }

    private void State_ProgressChanged(double newProgress, double oldProgress) =>
        RefreshText();

    private void State_LoadingStateChanged(MyLoadingState newState, MyLoadingState oldState) =>
        RefreshState();

    private void RefreshState()
    {
        InitState();
        MyLoadingState state = _state!.LoadingState;
        if (state == MyLoadingState.Run && !_isAttached)
            InnerState = MyLoadingState.Stop;

        InnerState = state;
        OuterState = state;
        StartLoopAnimation();
    }

    private void RefreshText()
    {
        if (_label is null)
            return;

        if (InnerState == MyLoadingState.Error)
        {
            if (TextErrorInherit && State.IsLoader)
            {
                Exception? exception = State.Error;
                if (exception is null)
                {
                    _label.Text = "未知错误";
                }
                else
                {
                    while (exception.InnerException is not null)
                        exception = exception.InnerException;

                    string message = TrimErrorMessage(exception.Message);
                    _label.Text = IsNetworkErrorMessage(message) ? "网络环境不佳，请稍后重试" : message;
                }
            }
            else
            {
                _label.Text = TextError;
            }

            return;
        }

        _label.Text = ShowProgress && State.IsLoader
            ? Text + " - " + State.Progress.ToString("P0", CultureInfo.CurrentCulture)
            : Text;
    }

    private void StartLoopAnimation()
    {
        if (!HasAnimation ||
            _isLooping ||
            InnerState != MyLoadingState.Run ||
            ModAnimation.aniSpeed > 10d ||
            !_isAttached)
        {
            return;
        }

        if (IsStrikeFreezeEnabled())
        {
            SetPickaxeAngle(-20d);
            ResetShards();
            return;
        }

        if (_pickaxe is null)
            return;

        EnsurePickaxeRotate();
        _isLooping = true;
        _errorAnimationWaiting = true;
        List<ModAnimation.AniData> animations =
        [
            ModAnimation.AaRotateTransform(
                _pickaxe,
                -20d - GetPickaxeAngle(),
                350,
                250,
                new ModAnimation.AniEaseInBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaRotateTransform(_pickaxe, 50d, 900, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaRotateTransform(
                _pickaxe,
                25d,
                900,
                ease: new ModAnimation.AniEaseOutElastic(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                ResetShards();
                _errorAnimationWaiting = false;
            })
        ];

        AddShardAnimation(animations, _leftShard, left: -5d, top: -6d);
        AddShardAnimation(animations, _rightShard, left: 5d, top: -6d);
        animations.Add(ModAnimation.AaCode(() =>
        {
            _isLooping = false;
            StartLoopAnimation();
        }, after: true));
        ModAnimation.AniStart(animations, $"MyLoader Loop {_uuid}");
    }

    private void StopLoopAnimation()
    {
        _isLooping = false;
        ModAnimation.AniStop($"MyLoader Loop {_uuid}");
    }

    private void ErrorAnimation(bool isError)
    {
        if (_errorIcon is null)
            return;

        if (isError)
        {
            int wait = _errorAnimationWaiting ? 400 : 0;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, "ColorBrushRedLight", 300),
                    ModAnimation.AaOpacity(_errorIcon, 1d - _errorIcon.Opacity, 100, 300 + wait),
                    ModAnimation.AaScaleTransform(
                        _errorIcon,
                        1d - GetErrorIconScale(),
                        400,
                        300 + wait,
                        new ModAnimation.AniEaseOutBack())
                },
                $"MyLoader Error {_uuid}");
        }
        else
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(_errorIcon, -_errorIcon.Opacity, 100),
                    ModAnimation.AaScaleTransform(_errorIcon, 0.5d - GetErrorIconScale(), 200),
                    ModAnimation.AaColor(this, ForegroundProperty, "ColorBrush3", 300)
                },
                $"MyLoader Error {_uuid}");
        }
    }

    private void SetPickaxeAngle(double angle)
    {
        RotateTransform? rotate = EnsurePickaxeRotate();
        if (rotate is null)
            return;

        rotate.Angle = angle;
    }

    private double GetPickaxeAngle() =>
        EnsurePickaxeRotate()?.Angle ?? 55d;

    private double GetErrorIconScale() =>
        _errorIcon?.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1d;

    private RotateTransform? EnsurePickaxeRotate()
    {
        if (_pickaxe is null)
            return null;

        _pickaxe.RenderTransformOrigin = new RelativePoint(0d, 0d, RelativeUnit.Relative);
        if (_pickaxe.RenderTransform is RotateTransform rotate)
            return rotate;

        // WPF leaves RenderTransformOrigin at 0,0 and rotates around this off-center pivot.
        rotate = new RotateTransform { CenterX = 30d, CenterY = 30d };
        _pickaxe.RenderTransform = rotate;
        return rotate;
    }

    private void ResetShards()
    {
        ResetShard(_leftShard, left: 7d);
        ResetShard(_rightShard, left: 14d);
    }

    private void SyncForeground(IBrush? brush)
    {
        brush ??= new SolidColorBrush(Color.Parse("#1370f3"));
        if (_label is not null)
            _label.Foreground = brush;
        if (_pickaxe is not null)
            _pickaxe.Stroke = brush;
        if (_leftShard is not null)
            _leftShard.Fill = brush;
        if (_rightShard is not null)
            _rightShard.Fill = brush;
        if (_errorIcon is not null)
            _errorIcon.Fill = brush;
        if (_bottomLine is not null)
            _bottomLine.Fill = brush;
    }

    private void Button_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
    }

    private void Button_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isMouseDown)
            return;

        Click?.Invoke(this, e);
    }

    private static void ResetShard(PathShape? shard, double left)
    {
        if (shard is null)
            return;

        shard.Opacity = 1d;
        shard.Margin = new Thickness(left, 41d, 0d, 0d);
    }

    private static void AddShardAnimation(List<ModAnimation.AniData> animations, PathShape? shard, double left, double top)
    {
        if (shard is null)
            return;

        animations.Add(ModAnimation.AaOpacity(shard, -1d, 100, 50));
        animations.Add(ModAnimation.AaX(shard, left, 180, ease: new ModAnimation.AniEaseOutFluent()));
        animations.Add(ModAnimation.AaY(shard, top, 180, ease: new ModAnimation.AniEaseOutFluent()));
    }

    private static bool IsStrikeFreezeEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("PCL_DESKTOP_FREEZE_LOADING_STRIKE"),
            "1",
            StringComparison.Ordinal);

    private static string TrimErrorMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();

    private static bool IsNetworkErrorMessage(string message)
    {
        string[] markers =
        [
            "远程主机强迫关闭了",
            "远程方已关闭传输流",
            "未能解析此远程名称",
            "由于目标计算机积极拒绝",
            "操作已超时",
            "操作超时",
            "服务器超时",
            "连接超时"
        ];

        return markers.Any(marker => message.Contains(marker, StringComparison.Ordinal));
    }
}

public interface ILoadingTrigger
{
    delegate void LoadingStateChangedEventHandler(MyLoading.MyLoadingState newState, MyLoading.MyLoadingState oldState);

    delegate void ProgressChangedEventHandler(double newProgress, double oldProgress);

    bool IsLoader { get; }

    double Progress { get; }

    Exception? Error { get; }

    MyLoading.MyLoadingState LoadingState { get; set; }

    event LoadingStateChangedEventHandler? LoadingStateChanged;

    event ProgressChangedEventHandler? ProgressChanged;
}

public class MyLoadingStateSimulator : ILoadingTrigger
{
    private MyLoading.MyLoadingState _loadingState = MyLoading.MyLoadingState.Unloaded;
    private double _progress;

    public bool IsLoader { get; init; }

    public Exception? Error { get; set; }

    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) < 0.0001d)
                return;

            double oldProgress = _progress;
            _progress = value;
            ProgressChanged?.Invoke(value, oldProgress);
        }
    }

    public MyLoading.MyLoadingState LoadingState
    {
        get => _loadingState;
        set
        {
            if (_loadingState == value)
                return;

            MyLoading.MyLoadingState oldState = _loadingState;
            _loadingState = value;
            LoadingStateChanged?.Invoke(value, oldState);
        }
    }

    public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;

    public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;
}
#pragma warning restore CA1711, CA1716
