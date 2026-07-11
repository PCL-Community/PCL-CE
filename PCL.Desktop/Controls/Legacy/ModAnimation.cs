// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1051, CA1720, CA2211

public static partial class ModAnimation
{
    private static readonly Dictionary<string, AniGroupEntry> AniGroups = [];
    private static readonly Stopwatch AniClock = new();
    private static DispatcherTimer? _aniTimer;
    private static double _aniLastTick;

    public static int AniControlEnabled { get; set; }

    public static double aniSpeed = 1d;

    public static bool AniIsRun(string name) => AniGroups.ContainsKey(name);

    public static void AniStart()
    {
        EnsureTimer();
    }

    public static void AniStart(IList aniGroup, string name = "", bool refreshTime = false)
    {
        List<AniData> data = aniGroup.OfType<AniData>().ToList();
        if (data.Count == 0)
            return;

        EnsureTimer();
        if (string.IsNullOrEmpty(name))
            name = Guid.NewGuid().ToString("N");
        else
            AniStop(name);

        AniGroups[name] = new AniGroupEntry(data);
        if (refreshTime)
            _aniLastTick = AniClock.Elapsed.TotalMilliseconds;
    }

    public static void AniStart(AniData aniGroup, string name = "", bool refreshTime = false) =>
        AniStart(new List<AniData> { aniGroup }, name, refreshTime);

    public static void AniStop(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        AniGroups.Remove(name);
    }

    public static void AdvanceForTesting(int deltaTick = 16, int count = 1)
    {
        for (int i = 0; i < count; i++)
            AniTimer(deltaTick);
    }

    public static void AdvanceUntilIdleForTesting(int deltaTick = 16, int maxFrameCount = 120)
    {
        for (int i = 0; i < maxFrameCount && AniGroups.Count > 0; i++)
            AniTimer(deltaTick);
    }

    public static void ResetForTesting()
    {
        AniGroups.Clear();
        _aniTimer?.Stop();
        _aniTimer = null;
        _aniLastTick = 0d;
        AniControlEnabled = 0;
        aniSpeed = 1d;
    }

    public static void AniTimer(int deltaTick)
    {
        if (AniGroups.Count == 0)
            return;

        deltaTick = (int)Math.Round(Math.Clamp(deltaTick * aniSpeed, 0d, 100000d));
        foreach (KeyValuePair<string, AniGroupEntry> pair in AniGroups.ToArray())
        {
            AniGroupEntry entry = pair.Value;
            bool canRemoveAfter = true;
            int index = 0;
            while (index < entry.Data.Count)
            {
                AniData anim = entry.Data[index];
                if (!anim.isAfter)
                {
                    canRemoveAfter = false;
                    anim.timeFinished += deltaTick;
                    if (anim.timeFinished > 0)
                        anim = AniRun(anim);

                    if (anim.timeFinished >= anim.timeTotal)
                    {
                        AniFinish(anim);
                        entry.Data.RemoveAt(index);
                        continue;
                    }

                    entry.Data[index] = anim;
                }
                else if (canRemoveAfter)
                {
                    canRemoveAfter = false;
                    anim.isAfter = false;
                    entry.Data[index] = anim;
                    continue;
                }
                else
                {
                    break;
                }

                index++;
            }

            if (entry.Data.Count == 0 && AniGroups.TryGetValue(pair.Key, out AniGroupEntry? current) && ReferenceEquals(current, entry))
                AniGroups.Remove(pair.Key);
        }
    }

    public static AniData AaX(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.X, obj, value, time, delay, ease, after);

    public static AniData AaY(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Y, obj, value, time, delay, ease, after);

    public static AniData AaWidth(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Width, obj, value, time, delay, ease, after);

    public static AniData AaHeight(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Height, obj, value, time, delay, ease, after);

    public static AniData AaOpacity(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Opacity, obj, value, time, delay, ease, after);

    public static AniData AaValue(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Value, obj, value, time, delay, ease, after);

    public static AniData AaRadius(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Radius, obj, value, time, delay, ease, after);

    public static AniData AaBorderThickness(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.BorderThickness, obj, value, time, delay, ease, after);

    public static AniData AaStrokeThickness(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.StrokeThickness, obj, value, time, delay, ease, after);

    public static AniData AaGridLengthWidth(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.GridLengthWidth, obj, value, time, delay, ease, after);

    public static AniData AaScale(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false,
        bool absolute = false)
    {
        AniRect changeRect;
        if (absolute)
        {
            changeRect = new AniRect(-0.5d * value, -0.5d * value, value, value);
        }
        else
        {
            double width = obj is Control widthControl ? GetControlWidth(widthControl) : 0d;
            double height = obj is Control heightControl ? GetControlHeight(heightControl) : 0d;
            changeRect = new AniRect(-0.5d * width * value, -0.5d * height * value, width * value, height * value);
        }

        return new AniData
        {
            typeMain = AniType.Scale,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = changeRect,
            isAfter = after,
            timeFinished = -delay
        };
    }

    public static AniData AaDouble(Action<double> lambda, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Double, lambda, value, time, delay, ease, after);

    public static AniData AaDouble(AvaloniaObject obj, AvaloniaProperty prop, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.Double, new object[] { obj, prop }, value, time, delay, ease, after);

    public static AniData AaTranslateX(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.TranslateX, obj, value, time, delay, ease, after);

    public static AniData AaTranslateY(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        Number(AniTypeSub.TranslateY, obj, value, time, delay, ease, after);

    public static AniData AaScaleTransform(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        new()
        {
            typeMain = AniType.ScaleTransform,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };

    public static AniData AaRotateTransform(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null, bool after = false) =>
        new()
        {
            typeMain = AniType.RotateTransform,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };

    public static AniData AaColor(Control obj, AvaloniaProperty prop, string res, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        AniColor start = GetColor(obj, prop);
        AniColor target = FindColor(obj, res);
        return new AniData
        {
            typeMain = AniType.Color,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, res },
            value = target,
            valueLast = start,
            isAfter = after,
            timeFinished = -delay
        };
    }

    public static AniData AaColor(Control obj, AvaloniaProperty prop, Color target, int time = 400, int delay = 0, AniEase? ease = null, bool after = false)
    {
        AniColor start = GetColor(obj, prop);
        AniColor targetColor = new(target);
        return new AniData
        {
            typeMain = AniType.Color,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, "" },
            value = targetColor,
            valueLast = start,
            isAfter = after,
            timeFinished = -delay
        };
    }

    public static AniData AaTextAppear(
        object obj,
        bool hide = false,
        bool timePerText = true,
        int time = 70,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        string text = GetTextAppearTarget(obj);
        return new AniData
        {
            typeMain = AniType.TextAppear,
            timeTotal = Math.Max(1, timePerText ? time * text.Length : time),
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = new object[] { text, hide },
            isAfter = after,
            timeFinished = -delay
        };
    }

    public static AniData AaCode(Action code, int delay = 0, bool after = false) =>
        new()
        {
            typeMain = AniType.Code,
            timeTotal = 1,
            value = code,
            isAfter = after,
            timeFinished = -delay
        };

    public static void AniDispose(MyHint control, bool removeFromChildren, ParameterizedThreadStart? callBack = null)
    {
        if (!control.IsHitTestVisible)
            return;

        control.IsHitTestVisible = false;
        double height = GetControlHeight(control);
        AniStart(
            new[]
            {
                AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
                AaOpacity(control, -1d, 200, ease: new AniEaseOutFluent()),
                AaHeight(control, -height, 150, 100, new AniEaseOutFluent()),
                AaCode(() =>
                {
                    if (removeFromChildren && control.Parent is Panel panel)
                        panel.Children.Remove(control);
                    else
                        control.IsVisible = false;

                    callBack?.Invoke(control);
                }, after: true)
            },
            "MyCard Dispose " + control.Uuid);
    }

    public static void AniDispose(MyCard control, bool removeFromChildren, ParameterizedThreadStart? callBack = null)
    {
        if (control.IsHitTestVisible)
        {
            control.IsHitTestVisible = false;
            double height = GetControlHeight(control);
            AniStart(
                new[]
                {
                    AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
                    AaOpacity(control, -1d, 200, ease: new AniEaseOutFluent()),
                    AaHeight(control, -height, 150, 100, new AniEaseOutFluent()),
                    AaCode(() => DisposeControl(control, removeFromChildren, callBack), after: true)
                },
                "MyCard Dispose " + control.uuid);
            return;
        }

        DisposeControl(control, removeFromChildren, callBack);
    }

    private static void DisposeControl(Control control, bool removeFromChildren, ParameterizedThreadStart? callBack)
    {
        if (removeFromChildren && control.Parent is Panel panel)
            panel.Children.Remove(control);
        else
            control.IsVisible = false;

        callBack?.Invoke(control);
    }

    public static List<AniData> AaStack(StackPanel stack, int time = 100, int delay = 25)
    {
        List<AniData> animations = [];
        int aniDelay = 0;
        foreach (Control child in stack.Children.OfType<Control>())
        {
            child.Opacity = 0d;
            animations.Add(AaOpacity(child, 1d, time, aniDelay));
            aniDelay += delay;
        }

        return animations;
    }

    public enum AniEasePower
    {
        Weak = 2,
        Middle = 3,
        Strong = 4,
        ExtraStrong = 5
    }

    public abstract class AniEase
    {
        public abstract double GetValue(double t);

        public virtual double GetDelta(double t1, double t0) => GetValue(t1) - GetValue(t0);
    }

    public sealed class AniEaseInout(AniEase easeIn, AniEase easeOut, double easeInPercent = 0.5d) : AniEase
    {
        public override double GetValue(double t)
        {
            if (t < easeInPercent)
                return easeInPercent * easeIn.GetValue(t / easeInPercent);

            return (1d - easeInPercent) * easeOut.GetValue((t - easeInPercent) / (1d - easeInPercent)) + easeInPercent;
        }
    }

    public sealed class AniEaseLinear : AniEase
    {
        public override double GetValue(double t) => Math.Clamp(t, 0d, 1d);

        public override double GetDelta(double t1, double t0) => Math.Clamp(t1, 0d, 1d) - Math.Clamp(t0, 0d, 1d);
    }

    public sealed class AniEaseInFluent(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        public override double GetValue(double t) => Math.Pow(Math.Clamp(t, 0d, 1d), (double)power);
    }

    public sealed class AniEaseOutFluent(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        public override double GetValue(double t) => 1d - Math.Pow(Math.Clamp(1d - t, 0d, 1d), (double)power);
    }

    public sealed class AniEaseInoutFluent(AniEasePower power = AniEasePower.Middle, double middle = 0.5d) : AniEase
    {
        private readonly AniEaseInout _ease = new(new AniEaseInFluent(power), new AniEaseOutFluent(power), middle);

        public override double GetValue(double t) => _ease.GetValue(t);
    }

    public sealed class AniEaseOutFluentWithInitial : AniEase
    {
        private readonly double _alpha;

        public AniEaseOutFluentWithInitial(double initialPixelPerSecond, double totalSecond, double totalDistance)
        {
            if (Math.Abs(totalDistance) < 0.000001d)
            {
                _alpha = 0d;
                return;
            }

            double normalizedInitialSpeed = initialPixelPerSecond * totalSecond / totalDistance;
            _alpha = Math.Max(normalizedInitialSpeed - 1d, 0d);
        }

        public override double GetValue(double t)
        {
            double p = Math.Clamp(t, 0d, 1d);
            if (_alpha == 0d)
                return p;

            return (_alpha + 1d) * p / (1d + _alpha * p);
        }
    }

    public sealed class AniEaseInBack(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly double _power = 3d - (double)power * 0.5d;

        public override double GetValue(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return Math.Pow(t, _power) * Math.Cos(1.5d * Math.PI * (1d - t));
        }
    }

    public sealed class AniEaseOutBack(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly double _power = 3d - (double)power * 0.5d;

        public override double GetValue(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return 1d - Math.Pow(1d - t, _power) * Math.Cos(1.5d * Math.PI * t);
        }
    }

    public sealed class AniEaseInCar(double middle = 0.7d, AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly AniEaseInout _ease = new(new AniEaseInBack(power), new AniEaseOutFluent(power), middle);

        public override double GetValue(double t) => _ease.GetValue(t);
    }

    public sealed class AniEaseOutCar(double middle = 0.3d, AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly AniEaseInout _ease = new(new AniEaseInFluent(power), new AniEaseOutBack(power), middle);

        public override double GetValue(double t) => _ease.GetValue(t);
    }

    public sealed class AniEaseInElastic(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly int _power = (int)power + 4;

        public override double GetValue(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return Math.Pow(t, (_power - 1) * 0.25d) *
                Math.Cos((_power - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    public sealed class AniEaseOutElastic(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly int _power = (int)power + 4;

        public override double GetValue(double t)
        {
            t = 1d - Math.Clamp(t, 0d, 1d);
            return 1d - Math.Pow(t, (_power - 1) * 0.25d) *
                Math.Cos((_power - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    public sealed class AniData
    {
        public AniType typeMain;
        public AniTypeSub typeSub;
        public int timeTotal;
        public double timeFinished;
        public double timePercent;
        public bool isAfter;
        public AniEase ease = new AniEaseLinear();
        public object? obj;
        public object? value;
        public object? valueLast;
    }

    public enum AniType
    {
        Number,
        Scale,
        Color,
        Code,
        ScaleTransform,
        RotateTransform,
        TextAppear
    }

    public enum AniTypeSub
    {
        X,
        Y,
        Width,
        Height,
        Opacity,
        Value,
        Radius,
        BorderThickness,
        StrokeThickness,
        TranslateX,
        TranslateY,
        Double,
        GridLengthWidth
    }

    private static AniData Number(AniTypeSub subType, object obj, double value, int time, int delay, AniEase? ease, bool after) =>
        new()
        {
            typeMain = AniType.Number,
            typeSub = subType,
            timeTotal = Math.Max(1, time),
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };

    private static AniData AniRun(AniData ani)
    {
        double progress = ani.timeFinished / ani.timeTotal;
        double delta = ani.ease.GetDelta(progress, ani.timePercent);

        switch (ani.typeMain)
        {
            case AniType.Number:
                ApplyNumber(ani, delta);
                break;
            case AniType.Scale:
                ApplyScale(ani, delta);
                break;
            case AniType.Color:
                ApplyColor(ani, progress);
                break;
            case AniType.TextAppear:
                ApplyTextAppear(ani, progress);
                break;
            case AniType.ScaleTransform:
                ApplyScaleTransform(ani, delta);
                break;
            case AniType.RotateTransform:
                ApplyRotateTransform(ani, delta);
                break;
            case AniType.Code:
                if (ani.value is Action action)
                    action();
                ani.timeFinished = ani.timeTotal;
                break;
        }

        ani.timePercent = progress;
        return ani;
    }

    private static void AniFinish(AniData ani)
    {
        if (ani.typeMain != AniType.Color ||
            ani.obj is not object[] colorObj ||
            colorObj.Length < 2 ||
            ani.value is not AniColor)
        {
            return;
        }

        if (colorObj[0] is Control control &&
            colorObj[1] is AvaloniaProperty property)
        {
            if (colorObj.Length >= 3 &&
                colorObj[2] is string resourceKey &&
                !string.IsNullOrWhiteSpace(resourceKey))
            {
                SetBrush(control, property, FindColor(control, resourceKey).ToBrush());
            }
        }
    }

    private static void ApplyNumber(AniData ani, double progressDelta)
    {
        if (ani.obj is null || ani.value is not double total)
            return;

        double delta = Percent(total, progressDelta);
        switch (ani.typeSub)
        {
            case AniTypeSub.X:
                AddHorizontalMargin(ani.obj, delta);
                break;
            case AniTypeSub.Y:
                AddVerticalMargin(ani.obj, delta);
                break;
            case AniTypeSub.Width:
                if (ani.obj is Control widthControl)
                    widthControl.Width = Math.Max(0d, widthControl.Width + delta);
                break;
            case AniTypeSub.Height:
                if (ani.obj is Control heightControl)
                    heightControl.Height = Math.Max(0d, heightControl.Height + delta);
                break;
            case AniTypeSub.Opacity:
                if (ani.obj is Control opacityControl)
                    opacityControl.Opacity = Math.Clamp(opacityControl.Opacity + delta, 0d, 1d);
                break;
            case AniTypeSub.Value:
                AddValue(ani.obj, delta);
                break;
            case AniTypeSub.Radius:
                AddRadius(ani.obj, delta);
                break;
            case AniTypeSub.BorderThickness:
                AddBorderThickness(ani.obj, delta);
                break;
            case AniTypeSub.StrokeThickness:
                AddStrokeThickness(ani.obj, delta);
                break;
            case AniTypeSub.TranslateX:
                EnsureTranslate(ani.obj).X += delta;
                break;
            case AniTypeSub.TranslateY:
                EnsureTranslate(ani.obj).Y += delta;
                break;
            case AniTypeSub.Double:
                if (ani.obj is Action<double> action)
                    action(delta);
                else if (ani.obj is object[] { Length: >= 2 } args &&
                         args[0] is AvaloniaObject avaloniaObject &&
                         args[1] is AvaloniaProperty property)
                    TryAddAvaloniaNumericProperty(avaloniaObject, property, delta, clampToZero: false);
                break;
            case AniTypeSub.GridLengthWidth:
                AddGridLengthWidth(ani.obj, delta);
                break;
        }
    }

    private static void ApplyColor(AniData ani, double progress)
    {
        if (ani.obj is not object[] colorObj ||
            colorObj.Length < 2 ||
            colorObj[0] is not Control control ||
            colorObj[1] is not AvaloniaProperty property ||
            ani.value is not AniColor total)
        {
            return;
        }

        AniColor start = ani.valueLast is AniColor valueLast ? valueLast : GetColor(control, property);
        AniColor newColor = AniColor.Percent(start, total, ani.ease.GetValue(progress));
        SetBrush(control, property, newColor.ToBrush());
    }

    private static void ApplyTextAppear(AniData ani, double progress)
    {
        if (ani.value is not object[] { Length: >= 2 } args ||
            args[0] is not string originalText ||
            args[1] is not bool hide)
        {
            return;
        }

        int textLength = originalText.Length;
        if (textLength == 0)
        {
            SetTextAppearTarget(ani.obj, string.Empty);
            return;
        }

        int textCount = (int)Math.Round(
            (hide ? textLength : 0) +
            Math.Round(textLength * (hide ? -1 : 1) * ani.ease.GetDelta(progress, 0d)));
        textCount = Math.Clamp(textCount, 0, textLength);
        string newText = originalText[..Math.Min(textCount, originalText.Length)];
        if (textCount < originalText.Length)
            newText += CreateScrambleCharacter(originalText[textCount]);

        SetTextAppearTarget(ani.obj, newText);
    }

    private static void ApplyScale(AniData ani, double progressDelta)
    {
        if (ani.obj is not Control control || ani.value is not AniRect total)
            return;

        AniRect delta = total * progressDelta;
        AddScaleMargin(control, delta.Left, delta.Top);
        control.Width = Math.Max(0d, GetControlWidth(control) + delta.Width);
        control.Height = Math.Max(0d, GetControlHeight(control) + delta.Height);
    }

    private static void ApplyScaleTransform(AniData ani, double progressDelta)
    {
        if (ani.obj is null || ani.value is not double total)
            return;

        ScaleTransform scale = EnsureScale(ani.obj);
        double delta = Percent(total, progressDelta);
        scale.ScaleX = Math.Max(scale.ScaleX + delta, 0d);
        scale.ScaleY = Math.Max(scale.ScaleY + delta, 0d);
    }

    private static void ApplyRotateTransform(AniData ani, double progressDelta)
    {
        if (ani.obj is null || ani.value is not double total)
            return;

        EnsureRotate(ani.obj).Angle += Percent(total, progressDelta);
    }

    private static void AddHorizontalMargin(object obj, double value)
    {
        if (obj is not Control control)
            return;

        Thickness margin = control.Margin;
        control.Margin = control.HorizontalAlignment switch
        {
            HorizontalAlignment.Right => new Thickness(margin.Left, margin.Top, margin.Right - value, margin.Bottom),
            _ => new Thickness(margin.Left + value, margin.Top, margin.Right, margin.Bottom)
        };
    }

    private static void AddVerticalMargin(object obj, double value)
    {
        if (obj is not Control control)
            return;

        Thickness margin = control.Margin;
        control.Margin = control.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom - value),
            _ => new Thickness(margin.Left, margin.Top + value, margin.Right, margin.Bottom)
        };
    }

    private static void AddScaleMargin(Control control, double left, double top)
    {
        Thickness margin = control.Margin;
        control.Margin = new Thickness(
            margin.Left + left,
            margin.Top + top,
            margin.Right + left,
            margin.Bottom + top);
    }

    private static double Percent(double value, double percent) =>
        Math.Round(value * percent, 6);

    private static void AddValue(object obj, double delta)
    {
        if (obj is RangeBase range)
        {
            range.Value += delta;
            return;
        }

        if (obj is MySlider slider)
        {
            slider.Value = (int)Math.Round(slider.Value + delta);
        }
    }

    private static void AddRadius(object obj, double delta)
    {
        if (obj is MyDropShadow shadow)
            shadow.ShadowRadius = Math.Max(0d, shadow.ShadowRadius + delta);
    }

    private static void AddBorderThickness(object obj, double delta)
    {
        if (obj is Border border)
        {
            border.BorderThickness = new Thickness(Math.Max(border.BorderThickness.Bottom + delta, 0d));
            return;
        }

        if (obj is TemplatedControl templated)
            templated.BorderThickness = new Thickness(Math.Max(templated.BorderThickness.Bottom + delta, 0d));
    }

    private static void AddStrokeThickness(object obj, double delta)
    {
        if (obj is Shape shape)
        {
            shape.StrokeThickness = Math.Max(shape.StrokeThickness + delta, 0d);
            return;
        }

        if (obj is SvgIcon svgIcon)
            svgIcon.StrokeThickness = Math.Max(svgIcon.StrokeThickness + delta, 0d);
    }

    private static void AddGridLengthWidth(object obj, double delta)
    {
        if (obj is ColumnDefinition column)
        {
            column.Width = new GridLength(Math.Max(column.Width.Value + delta, 0d), GridUnitType.Star);
            return;
        }
    }

    private static bool TryAddAvaloniaNumericProperty(AvaloniaObject obj, AvaloniaProperty property, double delta, bool clampToZero)
    {
        switch (property)
        {
            case StyledProperty<double> doubleProperty:
            {
                double value = obj.GetValue(doubleProperty) + delta;
                obj.SetValue(doubleProperty, clampToZero ? Math.Max(value, 0d) : value);
                return true;
            }
            case StyledProperty<int> intProperty:
            {
                double value = obj.GetValue(intProperty) + delta;
                obj.SetValue(intProperty, (int)Math.Round(clampToZero ? Math.Max(value, 0d) : value));
                return true;
            }
            case StyledProperty<uint> uintProperty:
            {
                double value = obj.GetValue(uintProperty) + delta;
                obj.SetValue(uintProperty, (uint)Math.Round(Math.Max(value, 0d)));
                return true;
            }
            case StyledProperty<float> floatProperty:
            {
                double value = obj.GetValue(floatProperty) + delta;
                obj.SetValue(floatProperty, (float)(clampToZero ? Math.Max(value, 0d) : value));
                return true;
            }
        }

        return false;
    }

    private static string GetTextAppearTarget(object obj)
    {
        if (obj is TextBlock textBlock)
            return textBlock.Text ?? string.Empty;
        if (obj is ContentControl contentControl)
            return contentControl.Content?.ToString() ?? string.Empty;

        return string.Empty;
    }

    private static void SetTextAppearTarget(object? obj, string text)
    {
        if (obj is TextBlock textBlock)
        {
            textBlock.Text = text;
            return;
        }

        if (obj is ContentControl contentControl)
        {
            contentControl.Content = text;
            return;
        }

        if (obj is null)
            return;
    }

    private static char CreateScrambleCharacter(char nextText)
    {
        if (nextText >= 128)
            return (char)Random.Shared.Next(0x4E00, 0x9FA6);

        const string source = @"0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";
        return source[Random.Shared.Next(source.Length)];
    }

    private static double GetControlWidth(Control control) =>
        !double.IsNaN(control.Width) && control.Width > 0d ? control.Width : Math.Max(0d, control.Bounds.Width);

    private static double GetControlHeight(Control control) =>
        !double.IsNaN(control.Height) && control.Height > 0d ? control.Height : Math.Max(0d, control.Bounds.Height);

    private static TranslateTransform EnsureTranslate(object obj)
    {
        if (obj is TranslateTransform translate)
            return translate;

        if (obj is not Control control)
            throw new InvalidOperationException("Translate animation target must be a Control or TranslateTransform.");

        if (control.RenderTransform is TranslateTransform directTranslate)
            return directTranslate;

        translate = new TranslateTransform();
        control.RenderTransform = translate;
        return translate;
    }

    private static ScaleTransform EnsureScale(object obj)
    {
        if (obj is ScaleTransform scale)
            return scale;

        if (obj is not Control control)
            throw new InvalidOperationException("Scale animation target must be a Control or ScaleTransform.");

        control.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);

        if (control.RenderTransform is ScaleTransform directScale)
            return directScale;

        scale = new ScaleTransform();
        control.RenderTransform = scale;
        return scale;
    }

    private static RotateTransform EnsureRotate(object obj)
    {
        if (obj is RotateTransform rotate)
            return rotate;

        if (obj is not Control control)
            throw new InvalidOperationException("Rotate animation target must be a Control or RotateTransform.");

        if (control.RenderTransform is RotateTransform directRotate)
            return directRotate;

        rotate = new RotateTransform();
        control.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        control.RenderTransform = rotate;
        return rotate;
    }

    private static AniColor GetColor(Control control, AvaloniaProperty property)
    {
        if (property == Border.BackgroundProperty && control is Border backgroundBorder)
            return new AniColor(backgroundBorder.Background);
        if (property == MenuItem.BackgroundProperty && control is MenuItem menuItemBackground)
            return new AniColor(menuItemBackground.Background);
        if (property == ComboBoxItem.BackgroundProperty && control is ComboBoxItem comboBoxItemBackground)
            return new AniColor(comboBoxItemBackground.Background);
        if (property == ComboBox.BackgroundProperty && control is ComboBox comboBoxBackground)
            return new AniColor(comboBoxBackground.Background);
        if (property == TextBox.BackgroundProperty && control is TextBox textBoxBackground)
            return new AniColor(textBoxBackground.Background);
        if (property == TextBox.BorderBrushProperty && control is TextBox textBoxBorderBrush)
            return new AniColor(textBoxBorderBrush.BorderBrush);
        if (property == Border.BorderBrushProperty && control is Border borderBrushBorder)
            return new AniColor(borderBrushBorder.BorderBrush);
        if (property == TextBlock.ForegroundProperty && control is TextBlock textBlock)
            return new AniColor(textBlock.Foreground);
        if (property.Name == nameof(TextPresenter.Foreground) && control is TextPresenter textPresenter)
            return new AniColor(textPresenter.Foreground);
        if (property == MyListItem.ForegroundProperty && control is MyListItem listItem)
            return new AniColor(listItem.Foreground);
        if (property.Name == nameof(TemplatedControl.Foreground) && control is TemplatedControl templated)
            return new AniColor(templated.Foreground);
        if (property == Shape.FillProperty && control is Shape fillShape)
            return new AniColor(fillShape.Fill);
        if (property == Shape.StrokeProperty && control is Shape strokeShape)
            return new AniColor(strokeShape.Stroke);
        if (property == SvgIcon.IconBrushProperty && control is SvgIcon svgIcon)
            return new AniColor(svgIcon.IconBrush);
        if (property == MyDropShadow.ColorProperty && control is MyDropShadow shadow)
            return new AniColor(shadow.Color);
        if (property == MyLoading.ForegroundProperty && control is MyLoading loading)
            return new AniColor(loading.Foreground);

        return AniColor.Empty;
    }

    private static void SetBrush(Control control, AvaloniaProperty property, IBrush brush)
    {
        if (property == Border.BackgroundProperty && control is Border backgroundBorder)
            backgroundBorder.Background = brush;
        else if (property == MenuItem.BackgroundProperty && control is MenuItem menuItemBackground)
            menuItemBackground.Background = brush;
        else if (property == ComboBoxItem.BackgroundProperty && control is ComboBoxItem comboBoxItemBackground)
            comboBoxItemBackground.Background = brush;
        else if (property == ComboBox.BackgroundProperty && control is ComboBox comboBoxBackground)
            comboBoxBackground.Background = brush;
        else if (property == TextBox.BackgroundProperty && control is TextBox textBoxBackground)
            textBoxBackground.Background = brush;
        else if (property == TextBox.BorderBrushProperty && control is TextBox textBoxBorderBrush)
            textBoxBorderBrush.BorderBrush = brush;
        else if (property == Border.BorderBrushProperty && control is Border borderBrushBorder)
            borderBrushBorder.BorderBrush = brush;
        else if (property == TextBlock.ForegroundProperty && control is TextBlock textBlock)
            textBlock.Foreground = brush;
        else if (property.Name == nameof(TextPresenter.Foreground) && control is TextPresenter textPresenter)
            textPresenter.Foreground = brush;
        else if (property == MyListItem.ForegroundProperty && control is MyListItem listItem)
            listItem.Foreground = brush;
        else if (property.Name == nameof(TemplatedControl.Foreground) && control is TemplatedControl templated)
            templated.Foreground = brush;
        else if (property == Shape.FillProperty && control is Shape fillShape)
            fillShape.Fill = brush;
        else if (property == Shape.StrokeProperty && control is Shape strokeShape)
            strokeShape.Stroke = brush;
        else if (property == SvgIcon.IconBrushProperty && control is SvgIcon svgIcon)
            svgIcon.IconBrush = brush;
        else if (property == MyDropShadow.ColorProperty && control is MyDropShadow shadow && brush is ISolidColorBrush solid)
            shadow.Color = solid.Color;
        else if (property == MyLoading.ForegroundProperty && control is MyLoading loading)
            loading.Foreground = brush;
    }

    private static AniColor FindColor(Control control, string resourceKey)
    {
        if (LegacyResourceResolver.TryResolve(control, resourceKey, out object? resource))
            return new AniColor(resource);

        return AniColor.Empty;
    }

    private static void EnsureTimer()
    {
        if (_aniTimer is not null)
            return;

        AniClock.Restart();
        _aniLastTick = 0d;
        _aniTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16d)
        };
        _aniTimer.Tick += (_, _) =>
        {
            double now = AniClock.Elapsed.TotalMilliseconds;
            double delta = now - _aniLastTick;
            _aniLastTick = now;
            AniTimer((int)Math.Round(delta));
        };
        _aniTimer.Start();
    }

    private sealed class AniGroupEntry(List<AniData> data)
    {
        public List<AniData> Data { get; } = data;
    }

    private readonly struct AniColor
    {
        public static readonly AniColor Empty = new(0d, 0d, 0d, 0d);

        public AniColor(object? value)
        {
            Color color = value switch
            {
                Color direct => direct,
                ISolidColorBrush brush => brush.Color,
                IBrush => Colors.Transparent,
                _ => Colors.Transparent
            };
            A = color.A;
            R = color.R;
            G = color.G;
            B = color.B;
        }

        public AniColor(Color color)
            : this(color.A, color.R, color.G, color.B)
        {
        }

        private AniColor(double a, double r, double g, double b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        private double A { get; }
        private double R { get; }
        private double G { get; }
        private double B { get; }

        public SolidColorBrush ToBrush() =>
            new SolidColorBrush(Color.FromArgb(ClampByte(A), ClampByte(R), ClampByte(G), ClampByte(B)));

        public static AniColor operator +(AniColor left, AniColor right) =>
            new(left.A + right.A, left.R + right.R, left.G + right.G, left.B + right.B);

        public static AniColor operator -(AniColor left, AniColor right) =>
            new(left.A - right.A, left.R - right.R, left.G - right.G, left.B - right.B);

        public static AniColor operator *(AniColor color, double value) =>
            new(color.A * value, color.R * value, color.G * value, color.B * value);

        public static AniColor Percent(AniColor start, AniColor target, double progress) =>
            Round(start * (1d - progress) + target * progress, 6);

        private static AniColor Round(AniColor color, int digits) =>
            new(
                Math.Round(color.A, digits),
                Math.Round(color.R, digits),
                Math.Round(color.G, digits),
                Math.Round(color.B, digits));

        private static byte ClampByte(double value) =>
            (byte)Math.Round(Math.Clamp(value, 0d, 255d));
    }

    private readonly struct AniRect(double left, double top, double width, double height)
    {
        public double Left { get; } = left;

        public double Top { get; } = top;

        public double Width { get; } = width;

        public double Height { get; } = height;

        public static AniRect operator *(AniRect rect, double value) =>
            new(
                Percent(rect.Left, value),
                Percent(rect.Top, value),
                Percent(rect.Width, value),
                Percent(rect.Height, value));
    }
}
