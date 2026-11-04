using PCL.Core.App.Localization;
using PCL.Core.UI.Effects;
using PCL.Core.Utils;
using PCL.Network;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PCL;

public static partial class Animations
{
    private static int _aniCount;
    private static int _aniFpsCounter;
    private static long _aniFpsTimer;
    private static bool _aniInFrame;
    private static int _aniFps;

    /// <summary>
    ///     开始动画执行。
    /// </summary>
    public static void AniStart()
    {
        _aniLastTick = Stopwatch.GetTimestamp();
        _aniFpsTimer = _aniLastTick;
        aniRunning = true;

        Common.RunInUi(() =>
        {
            CompositionTarget.Rendering -= _AniOnRendering;
            CompositionTarget.Rendering += _AniOnRendering;
            Common.Log("[Animation] 动画帧循环已挂载到渲染管线", Common.LogLevel.Developer);
        });
    }

    /// <summary>
    ///     Advance the animations by one frame. Raised once per rendered frame, on the UI thread.
    /// </summary>
    private static void _AniOnRendering(object? sender, EventArgs e)
    {
        if (_aniInFrame) return; // WM_SIZE re-enters this synchronously
        _aniInFrame = true;

        try
        {
            var now = Stopwatch.GetTimestamp();
            var deltaTick = (int)Math.Round(
                Common.MathClamp(_AniMs(now - _aniLastTick), 0, 100000) * aniSpeed);
            _aniLastTick = now;

            if (Common.modeDebug)
            {
                if (_AniMs(now - _aniFpsTimer) >= 500d)
                {
                    _aniFps = _aniFpsCounter;
                    _aniFpsCounter = 0;
                    _aniFpsTimer = now;
                }

                _aniFpsCounter += 2; // 500 ms window -> frames per second
            }

            if (deltaTick <= 0)
            {
                _AniFlushGroups(); // still publish starts and stops, so nothing piles up
                return;
            }

            _aniCount = 0;
            AniTimer(deltaTick);

            if (RandomUtils.NextInt(0, 64 * (Common.modeDebug ? 5 : 30)) == 0
                && (_aniFps is < 62 and > 0
                    || _aniCount > 4
                    || NetFacade.NetManager.FileRemain != 0))
                Common.Log($"[Report] FPS {_aniFps}, 动画 {_aniCount}, 下载中 {NetFacade.NetManager.FileRemain}（{Common.GetString(NetFacade.NetManager.Speed)}/s）");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "动画帧执行失败",
                Common.LogLevel.Critical,
                userSummary: Lang.Text("Application.Animation.Error.OperationFailed"));
        }
        finally
        {
            _aniInFrame = false;
        }
    }

    /// <summary>
    ///     动画定时器事件。
    /// </summary>
    public static void AniTimer(int deltaTick)
    {
        try
        {
            if (deltaTick / aniSpeed > 100d)
                Common.Log(
                    "[Animation] 两个动画帧间隔 " + deltaTick + " ms",
                    Common.LogLevel.Developer);

            _AniFlushGroups();

            var groups = _aniOrder;
            foreach (var entry in groups)
            {
                if (entry.Dead) continue;
                if (entry.startTick > _aniLastTick) continue;

                try
                {
                    _AniRunGroup(entry, deltaTick);
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        "动画组执行失败",
                        Common.LogLevel.Hint,
                        userSummary: Lang.Text("Application.Animation.Error.OperationFailed"));
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "动画刻执行失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Animation.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     Apply the changes collected since the last frame.
    /// </summary>
    private static void _AniFlushGroups()
    {
        lock (_aniGroupsLock)
        {
            if (_aniPendingAdds.Count > 0)
            {
                foreach (var entry in _aniPendingAdds)
                    if (!entry.Dead)
                        _aniOrder.Add(entry);
                _aniPendingAdds.Clear();
            }

            if (_aniOrderDirty)
            {
                _aniOrder.RemoveAll(static e => e.Dead);
                _aniOrderDirty = false;
            }
        }
    }

    /// <summary>
    ///     Advance one animation group.
    /// </summary>
    private static void _AniRunGroup(AniGroupEntry entry, int deltaTick)
    {
        var data = entry.data;
        var span = CollectionsMarshal.AsSpan(data);
        var write = 0;
        var blocked = false;
        int read;

        for (read = 0; read < span.Length; read++)
        {
            ref var anim = ref span[read];

            if (anim.isAfter)
            {
                if (blocked) break;
                anim.isAfter = false;
            }
            blocked = true;

            anim.timeFinished += deltaTick;
            if (anim.timeFinished > 0)
            {
                _AniRun(ref anim);
                _aniCount++;
            }

            if (anim.timeFinished >= anim.timeTotal)
            {
                _AniFinishColor(ref anim);
                continue;
            }

            if (write != read) span[write] = anim;
            write++;
        }

        if (write < read) data.RemoveRange(write, read - write);
        if (data.Count == 0) _AniKillGroup(entry);
    }

    /// <summary>
    ///     Release a finished color animation.
    /// </summary>
    private static void _AniFinishColor(ref AniData anim)
    {
        if (anim.typeMain == AniType.Color &&
            anim.obj is object[] { Length: >= 3 } target &&
            target[2] is not string { Length: 0 })
            ((FrameworkElement)target[0]).SetResourceReference(
                (DependencyProperty)target[1],
                target[2]);
    }

    /// <summary>
    ///     Mark a group as dead and unregister it by name.
    /// </summary>
    private static void _AniKillGroup(AniGroupEntry entry)
    {
        entry.Dead = true;
        lock (_aniGroupsLock)
        {
            if (_aniByName.TryGetValue(entry.Key, out var current)
                && ReferenceEquals(current, entry))
                _aniByName.Remove(entry.Key);
            _aniOrderDirty = true;
        }
    }

    /// <summary>Convert a stopwatch tick delta to milliseconds.</summary>
    private static double _AniMs(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    /// <summary>
    ///     执行一个动画。
    /// </summary>
    /// <param name="ani">要执行的动画对象。</param>
    private static void _AniRun(ref AniData ani)
    {
        try
        {
            switch (ani.typeMain)
            {
                case AniType.Number:
                    _RunNumberAnimation(ani);
                    break;

                case AniType.Color:
                    _RunColorAnimation(ref ani);
                    break;

                case AniType.Scale:
                    _RunScaleAnimation(ani);
                    break;

                case AniType.TextAppear:
                    _RunTextAppearAnimation(ani);
                    break;

                case AniType.Code:
                    ((ThreadStart)ani.value)();
                    break;

                case AniType.ScaleTransform:
                    _RunScaleTransformAnimation(ani);
                    break;

                case AniType.RotateTransform:
                    _RunRotateTransformAnimation(ani);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ani.timePercent = _GetAnimationProgress(ani);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"执行动画失败：{ani}",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Animation.Error.OperationFailed"));
        }
    }

    private static double _GetAnimationProgress(AniData ani)
    {
        return ani.timeFinished / (double)ani.timeTotal;
    }

    private static double _GetAnimationDelta(AniData ani)
    {
        return ani.ease.GetDelta(
            _GetAnimationProgress(ani),
            ani.timePercent);
    }

    private static void _RunNumberAnimation(AniData ani)
    {
        var delta = Common.MathPercent(
            0d,
            (double)ani.value,
            _GetAnimationDelta(ani));

        if (delta == 0d)
            return;

        switch (ani.typeSub)
        {
            case AniTypeSub.X:
                Common.DeltaLeft((FrameworkElement)ani.obj, delta);
                break;

            case AniTypeSub.Y:
                Common.DeltaTop((FrameworkElement)ani.obj, delta);
                break;

            case AniTypeSub.Opacity:
                {
                    var target = ani.obj as UIElement ?? throw new NotSupportedException(
                        "Opacity 动画的目标必须是 UIElement，实际为 " + (ani.obj?.GetType().FullName ?? "null"));

                    target.Opacity = Common.MathClamp(target.Opacity + delta, 0d, 1d);
                    break;
                }

            case AniTypeSub.Width:
                {
                    var element = (FrameworkElement)ani.obj;
                    var width = element.Width is double.NaN
                        ? element.ActualWidth
                        : element.Width;

                    element.Width = Math.Max(width + delta, 0d);
                    break;
                }

            case AniTypeSub.Height:
                {
                    var element = (FrameworkElement)ani.obj;
                    var height = element.Height is double.NaN
                        ? element.ActualHeight
                        : element.Height;

                    element.Height = Math.Max(height + delta, 0d);
                    break;
                }

            case AniTypeSub.Value:
                {
                    var target = ani.obj as System.Windows.Controls.Primitives.RangeBase
                                 ?? throw new NotSupportedException(
                                     "Value 动画的目标必须是 RangeBase，实际为 "
                                     + (ani.obj?.GetType().FullName ?? "null"));

                    target.Value += delta;
                    break;
                }

            case AniTypeSub.Radius:
                {
                    switch (ani.obj)
                    {
                        case BlurEffect blur:
                            blur.Radius += delta;
                            break;
                        case OptimizedBlurEffect optimizedBlur:
                            optimizedBlur.Radius += delta;
                            break;
                        case EnhancedBlurEffect enhancedBlur:
                            enhancedBlur.Radius += delta;
                            break;
                        case AdaptiveBlurEffect adaptiveBlur:
                            adaptiveBlur.Radius += delta;
                            break;
                        default:
                            throw new NotSupportedException(
                                "Radius 动画的目标必须是模糊效果，实际为 "
                                + (ani.obj?.GetType().FullName ?? "null"));
                    }

                    break;
                }

            case AniTypeSub.StrokeThickness:
                {
                    var shape = ani.obj as System.Windows.Shapes.Shape
                                ?? throw new NotSupportedException(
                                    "StrokeThickness 动画的目标必须是 Shape，实际为 "
                                    + (ani.obj?.GetType().FullName ?? "null"));

                    shape.StrokeThickness = Math.Max(shape.StrokeThickness + delta, 0d);
                    break;
                }

            case AniTypeSub.BorderThickness:
                {
                    switch (ani.obj)
                    {
                        case Border border:
                            border.BorderThickness = new Thickness(border.BorderThickness.Bottom + delta);
                            break;
                        case Control control:
                            control.BorderThickness = new Thickness(control.BorderThickness.Bottom + delta);
                            break;
                        default:
                            throw new NotSupportedException(
                                "BorderThickness 动画的目标必须是 Border 或 Control，实际为 "
                                + (ani.obj?.GetType().FullName ?? "null"));
                    }

                    break;
                }

            case AniTypeSub.TranslateX:
            case AniTypeSub.TranslateY:
                {
                    var target = ani.obj as UIElement ?? throw new NotSupportedException(
                        "TranslateX/Y 动画的目标必须是 UIElement，实际为 "
                        + (ani.obj?.GetType().FullName ?? "null"));

                    if (target.RenderTransform is not TranslateTransform transform)
                    {
                        transform = new TranslateTransform(0d, 0d);
                        target.RenderTransform = transform;
                    }

                    if (ani.typeSub == AniTypeSub.TranslateX)
                        transform.X += delta;
                    else
                        transform.Y += delta;

                    break;
                }

            case AniTypeSub.Double:
                {
                    var args = (object[])ani.obj!;
                    var target = (DependencyObject)args[0];
                    var property = (DependencyProperty)args[1];

                    target.SetValue(
                        property,
                        Convert.ToDouble(target.GetValue(property)) + delta);
                    break;
                }

            case AniTypeSub.DoubleParam:
                ((ParameterizedThreadStart)ani.obj)(delta);
                break;

            case AniTypeSub.GridLengthWidth:
                {
                    var target = ani.obj as ColumnDefinition ?? throw new NotSupportedException(
                        "GridLengthWidth 动画的目标必须是 ColumnDefinition，实际为 "
                        + (ani.obj?.GetType().FullName ?? "null"));

                    target.Width = new GridLength(
                        Math.Max(target.Width.Value + delta, 0d),
                        GridUnitType.Star);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    ///     颜色动画复用的 Brush 缓存。key 为该动画的 obj（即 AaColor 创建的 object[]），
    ///     value 为整条动画共用、始终未被冻结的 SolidColorBrush。
    ///     用 ConditionalWeakTable 是为了让动画结束后缓存随 obj 一起被回收，不会造成泄漏。
    /// </summary>
    private static readonly ConditionalWeakTable<object, SolidColorBrush> AniColorBrushes = new();

    private static void _RunColorAnimation(ref AniData ani)
    {
        var args = (object[])ani.obj!;
        var element = (FrameworkElement)args[0];
        var property = (DependencyProperty)args[1];

        var delta = Common.MathPercent(
            new Common.MyColor(0d, 0d, 0d, 0d),
            (Common.MyColor)ani.value,
            _GetAnimationDelta(ani));

        delta += (Common.MyColor)ani.valueLast;

        var valueLast = element.GetValue(property);
        var newColor = new Common.MyColor(valueLast) + delta;
        var color = (Color)newColor;

        if (property.PropertyType == typeof(Color))
        {
            element.SetValue(property, color);
        }
        else
        {
            var brush = AniColorBrushes.GetValue(ani.obj!, _ => new SolidColorBrush());
            // 先更新颜色再挂到元素上，避免首帧出现一次透明色
            brush.Color = color;
            if (!ReferenceEquals(valueLast, brush))
                element.SetValue(property, brush);
        }

        ani.valueLast = newColor - new Common.MyColor(element.GetValue(property));
    }

    private static void _RunScaleAnimation(AniData ani)
    {
        var element = (FrameworkElement)ani.obj;

        if (ani.value is not Common.MyRect scale)
            throw new NotSupportedException(
                "Scale 动画的目标值必须是 MyRect，实际为 "
                + (ani.value?.GetType().FullName ?? "null"));

        var progressDelta = _GetAnimationDelta(ani);

        var marginX = Common.MathPercent(
            0d,
            scale.Left,
            progressDelta);

        var marginY = Common.MathPercent(
            0d,
            scale.Top,
            progressDelta);

        var widthDelta = Common.MathPercent(
            0d,
            scale.Width,
            progressDelta);

        var heightDelta = Common.MathPercent(
            0d,
            scale.Height,
            progressDelta);

        var margin = element.Margin;

        element.Margin = new Thickness(
            margin.Left + marginX,
            margin.Top + marginY,
            margin.Right + marginX,
            margin.Bottom + marginY);

        element.Width = Math.Max(element.Width + widthDelta, 0d);
        element.Height = Math.Max(element.Height + heightDelta, 0d);
    }

    private static void _RunTextAppearAnimation(AniData ani)
    {
        dynamic args = ani.value;

        var originalText = args[0].ToString();
        var isHiding = (bool)args[1];
        var textLength = originalText.Length;

        var progress = ani.ease.GetDelta(
            _GetAnimationProgress(ani),
            0d);

        var startCount = isHiding ? textLength : 0;
        var direction = isHiding ? -1 : 1;

        var visibleCount = (int)Math.Round(
            startCount + Math.Round(textLength * direction * progress));

        var newText = originalText.Substring(
            0,
            Math.Min(visibleCount, textLength));

        if (visibleCount < textLength)
            newText += _GetRandomTextCharacter(originalText[visibleCount]);

        if (ani.obj is TextBlock textBlock)
            textBlock.Text = newText;
        else
            ((dynamic)ani.obj).Context = newText;
    }

    /// <summary>
    ///     GB18030 编码实例的缓存。原先每次调用都会走 Encoding.GetEncoding("GB18030")，
    ///     内部需要查询编码表，逐帧调用开销明显；这里只在首次使用时解析一次。
    ///     刻意使用惰性初始化而不是静态字段初始化器，以免改变编码提供程序的注册时机。
    /// </summary>
    private static Encoding? _gb18030Encoding;

    private static string _GetRandomTextCharacter(char character)
    {
        if (character >= 128)
            return (_gb18030Encoding ??= Encoding.GetEncoding("GB18030")).GetString(
                new[]
                {
                    (byte)RandomUtils.NextInt(176, 247),
                    (byte)RandomUtils.NextInt(161, 249)
                });

        const string characters =
            @"0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

        return RandomUtils.PickRandom(characters.ToCharArray()).ToString();
    }

    private static void _RunScaleTransformAnimation(AniData ani)
    {
        var element = (FrameworkElement)ani.obj;

        if (element.RenderTransform is not ScaleTransform transform)
        {
            element.RenderTransformOrigin = new Point(0.5d, 0.5d);
            transform = new ScaleTransform(1d, 1d);
            element.RenderTransform = transform;
        }

        var delta = Common.MathPercent(
            0d,
            (double)ani.value,
            _GetAnimationDelta(ani));

        transform.ScaleX = Math.Max(transform.ScaleX + delta, 0d);
        transform.ScaleY = Math.Max(transform.ScaleY + delta, 0d);
    }

    private static void _RunRotateTransformAnimation(AniData ani)
    {
        var element = (FrameworkElement)ani.obj;

        if (element.RenderTransform is not RotateTransform transform)
        {
            element.RenderTransformOrigin = new Point(0.5d, 0.5d);
            transform = new RotateTransform(0d);
            element.RenderTransform = transform;
        }

        var delta = Common.MathPercent(
            0d,
            (double)ani.value,
            _GetAnimationDelta(ani));

        transform.Angle += delta;
    }
}