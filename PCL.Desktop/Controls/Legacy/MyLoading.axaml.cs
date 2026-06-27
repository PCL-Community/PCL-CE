// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public partial class MyLoading : Grid
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyLoading, string>(nameof(Text), "Loading");

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyLoading, IBrush?>(nameof(Foreground), new SolidColorBrush(Color.Parse("#1370f3")));

    private readonly TextBlock? _label;
    private readonly PathShape? _pickaxe;
    private readonly PathShape? _leftShard;
    private readonly PathShape? _rightShard;
    private readonly PathShape? _errorIcon;
    private readonly Rectangle? _bottomLine;
    private readonly Stopwatch _loopClock = new();
    private readonly List<LoadingAnimationStep> _animationGroup = [];
    private DispatcherTimer? _loopTimer;
    private double _lastLoopTick;
    private double _pickaxeAngle = 55d;
    private bool _restartRequested;

    public MyLoading()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _pickaxe = this.FindControl<PathShape>("PathPickaxe");
        _leftShard = this.FindControl<PathShape>("PathLeft");
        _rightShard = this.FindControl<PathShape>("PathRight");
        _errorIcon = this.FindControl<PathShape>("PathError");
        _bottomLine = this.FindControl<Rectangle>("LineBottom");
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(ForegroundProperty).Subscribe(SyncForeground);
        AttachedToVisualTree += (_, _) => StartLoopAnimation();
        DetachedFromVisualTree += (_, _) => StopLoopAnimation();
    }

    public bool HasAnimation { get; set; } = true;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private void StartLoopAnimation()
    {
        if (!HasAnimation || _loopTimer is not null)
            return;

        if (IsStrikeFreezeEnabled())
        {
            SetPickaxeAngle(-20d);
            ResetShards();
            return;
        }

        _loopClock.Restart();
        _lastLoopTick = 0d;
        StartAnimationGroup();
        _loopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _loopTimer.Tick += LoopTimer_Tick;
        _loopTimer.Start();
        LoopTimer_Tick(this, EventArgs.Empty);
    }

    private void StopLoopAnimation()
    {
        _loopTimer?.Stop();
        _loopTimer = null;
    }

    private void LoopTimer_Tick(object? sender, EventArgs e)
    {
        double elapsed = _loopClock.Elapsed.TotalMilliseconds;
        double delta = elapsed - _lastLoopTick;
        _lastLoopTick = elapsed;
        if (delta <= 0d)
            return;

        RunAnimationGroup(delta);
    }

    private void StartAnimationGroup()
    {
        _restartRequested = false;
        _animationGroup.Clear();
        _animationGroup.Add(Rotate(-20d - _pickaxeAngle, 350d, 250d, EaseInBackWeak));
        _animationGroup.Add(Rotate(50d, 900d, 0d, EaseOutFluent, isAfter: true));
        _animationGroup.Add(Rotate(25d, 900d, 0d, EaseOutElasticWeak));
        _animationGroup.Add(Code(ResetShards));
        _animationGroup.Add(Number(delta => AddOpacity(_leftShard, delta), -1d, 100d, 50d, EaseLinear));
        _animationGroup.Add(Number(delta => AddMargin(_leftShard, left: delta), -5d, 180d, 0d, EaseOutFluent));
        _animationGroup.Add(Number(delta => AddMargin(_leftShard, top: delta), -6d, 180d, 0d, EaseOutFluent));
        _animationGroup.Add(Number(delta => AddOpacity(_rightShard, delta), -1d, 100d, 50d, EaseLinear));
        _animationGroup.Add(Number(delta => AddMargin(_rightShard, left: delta), 5d, 180d, 0d, EaseOutFluent));
        _animationGroup.Add(Number(delta => AddMargin(_rightShard, top: delta), -6d, 180d, 0d, EaseOutFluent));
        _animationGroup.Add(Code(() => _restartRequested = true, isAfter: true));
    }

    private void RunAnimationGroup(double deltaMilliseconds)
    {
        bool canRemoveAfter = true;
        int index = 0;
        while (index < _animationGroup.Count)
        {
            LoadingAnimationStep step = _animationGroup[index];
            if (!step.IsAfter)
            {
                canRemoveAfter = false;
                step.TimeFinished += deltaMilliseconds;
                if (step.TimeFinished > 0d)
                    step.Run();
                if (step.TimeFinished >= step.TimeTotal)
                {
                    _animationGroup.RemoveAt(index);
                    continue;
                }
            }
            else if (canRemoveAfter)
            {
                canRemoveAfter = false;
                step.IsAfter = false;
                continue;
            }
            else
            {
                break;
            }

            index++;
        }

        if (_restartRequested || _animationGroup.Count == 0)
            StartAnimationGroup();
    }

    private LoadingAnimationStep Rotate(
        double value,
        double time,
        double delay,
        Func<double, double> ease,
        bool isAfter = false) =>
        new(
            percentDelta => SetPickaxeAngle(_pickaxeAngle + value * percentDelta),
            time,
            delay,
            ease,
            isAfter);

    private static LoadingAnimationStep Number(
        Action<double> applyDelta,
        double value,
        double time,
        double delay,
        Func<double, double> ease,
        bool isAfter = false) =>
        new(percentDelta => applyDelta(value * percentDelta), time, delay, ease, isAfter);

    private static LoadingAnimationStep Code(Action action, double delay = 0d, bool isAfter = false) =>
        new(_ => action(), timeTotal: 1d, delay, EaseLinear, isAfter);

    private void SetPickaxeAngle(double angle)
    {
        _pickaxeAngle = angle;
        if (_pickaxe is null)
            return;

        _pickaxe.RenderTransformOrigin = new RelativePoint(0d, 0d, RelativeUnit.Relative);
        if (_pickaxe.RenderTransform is not RotateTransform rotate)
        {
            // WPF leaves RenderTransformOrigin at 0,0 and rotates around this off-center pivot.
            rotate = new RotateTransform { CenterX = 30d, CenterY = 30d };
            _pickaxe.RenderTransform = rotate;
        }

        rotate.Angle = angle;
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

    private static void ResetShard(PathShape? shard, double left)
    {
        if (shard is null)
            return;

        shard.Opacity = 1d;
        shard.Margin = new Thickness(left, 41d, 0d, 0d);
    }

    private static void AddOpacity(Control? control, double delta)
    {
        if (control is null)
            return;

        control.Opacity = Math.Clamp(control.Opacity + delta, 0d, 1d);
    }

    private static void AddMargin(Control? control, double left = 0d, double top = 0d)
    {
        if (control is null)
            return;

        Thickness margin = control.Margin;
        control.Margin = new Thickness(margin.Left + left, margin.Top + top, margin.Right, margin.Bottom);
    }

    private static bool IsStrikeFreezeEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("PCL_DESKTOP_FREEZE_LOADING_STRIKE"),
            "1",
            StringComparison.Ordinal);

    private static double EaseLinear(double progress)
    {
        return Math.Clamp(progress, 0d, 1d);
    }

    private static double EaseInBackWeak(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        const double power = 2d;
        return Math.Pow(progress, power) * Math.Cos(1.5d * Math.PI * (1d - progress));
    }

    private static double EaseOutFluent(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        const double power = 3d;
        return 1d - Math.Pow(1d - progress, power);
    }

    private static double EaseOutElasticWeak(double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        double inverse = 1d - progress;
        const double power = 6d;
        return 1d - Math.Pow(inverse, (power - 1d) * 0.25d) *
            Math.Cos((power - 3.5d) * Math.PI * Math.Pow(1d - inverse, 1.5d));
    }

    private sealed class LoadingAnimationStep(
        Action<double> applyPercentDelta,
        double timeTotal,
        double delay,
        Func<double, double> ease,
        bool isAfter)
    {
        public bool IsAfter { get; set; } = isAfter;

        public double TimeTotal { get; } = timeTotal;

        public double TimeFinished { get; set; } = -delay;

        private double TimePercent { get; set; }

        public void Run()
        {
            double currentPercent = TimeFinished / TimeTotal;
            double percentDelta = ease(currentPercent) - ease(TimePercent);
            applyPercentDelta(percentDelta);
            TimePercent = currentPercent;
        }
    }
}
