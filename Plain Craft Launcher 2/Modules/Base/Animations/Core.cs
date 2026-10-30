using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class Animations
{
    private static int _aniCount;
    private static int _aniFpsCounter;
    private static long _aniFpsTimer;
    private static int _aniFps;

    /// <summary>
    ///     开始动画执行。
    /// </summary>
    public static void AniStart()
    {
        _aniLastTick = TimeUtils.GetTimeTick();
        _aniFpsTimer = _aniLastTick;
        aniRunning = true;

        var minFrameGap = 1000d / (Config.System.AnimationFpsLimit + 1) / 2;

        Common.RunInNewThread(
            () =>
            {
                try
                {
                    Common.Log("[Animation] 动画线程开始");
                    while (true)
                    {
                        var deltaTime = (long)Math.Round(
                            Common.MathClamp(
                                TimeUtils.GetTimeTick() - _aniLastTick,
                                0,
                                100000));
                        if (deltaTime < minFrameGap)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        _aniLastTick = TimeUtils.GetTimeTick();
                        if (Common.modeDebug)
                        {
                            if (Common.MathClamp(_aniLastTick - _aniFpsTimer, 0d, 100000d) >= 500d)
                            {
                                _aniFps = _aniFpsCounter;
                                _aniFpsCounter = 0;
                                _aniFpsTimer = _aniLastTick;
                            }

                            _aniFpsCounter += 2;
                        }

                        Common.RunInUiWait(() =>
                        {
                            _aniCount = 0;
                            AniTimer((int)Math.Round(deltaTime * aniSpeed));
                            if (RandomUtils.NextInt(0, 64 * (Common.modeDebug ? 5 : 30)) == 0
                                && (_aniFps is < 62 and > 0
                                    || _aniCount > 4
                                    || NetFacade.NetManager.FileRemain != 0))
                                Common.Log(
                                    $"[Report] FPS {_aniFps}, "
                                    + $"动画 {_aniCount}, "
                                    + $"下载中 {NetFacade.NetManager.FileRemain}"
                                    + $"（{Common.GetString(NetFacade.NetManager.Speed)}/s）");
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        "动画帧执行失败",
                        Common.LogLevel.Critical,
                        userSummary: Lang.Text("Application.Animation.Error.OperationFailed"));
                }
            },
            "Animation",
            ThreadPriority.AboveNormal);
    }

    /// <summary>
    ///     动画定时器事件。
    /// </summary>
    public static void AniTimer(int deltaTick)
    {
        try
        {
            if (deltaTick / aniSpeed > 100d)
                Common.Log($"[Animation] 两个动画帧间隔 {deltaTick} ms", Common.LogLevel.Developer);
            var i = -1;
            while (i + 1 < aniGroups.Count)
            {
                i += 1;
                var entry = aniGroups.Values.ElementAtOrDefault(i);
                if (entry is null)
                    continue;
                if (entry.startTick > _aniLastTick)
                    continue;
                var canRemoveAfter = true;
                var ii = 0;

                while (ii < entry.data.Count)
                {
                    var anim = entry.data[ii];
                    if (!anim.isAfter)
                    {
                        canRemoveAfter = false;
                        anim.timeFinished += deltaTick;
                        if (anim.timeFinished > 0)
                        {
                            anim = _AniRun(anim);
                            _aniCount += 1;
                        }

                        if (anim.timeFinished >= anim.timeTotal)
                        {
                            if (anim.typeMain == AniType.Color
                                && !string.Equals(
                                    ((dynamic)anim.obj)[2] as string,
                                    "",
                                    StringComparison.Ordinal))
                                ((dynamic)anim.obj)[0]
                                    .SetResourceReference(
                                        ((dynamic)anim.obj)[1],
                                        ((dynamic)anim.obj)[2]);
                            entry.data.RemoveAt(ii);
                            continue;
                        }

                        entry.data[ii] = anim;
                    }
                    else if (canRemoveAfter)
                    {
                        canRemoveAfter = false;
                        anim.isAfter = false;
                        entry.data[ii] = anim;
                        continue;
                    }
                    else
                    {
                        break;
                    }

                    ii += 1;
                }

                if (entry.data.Count != 0) continue;

                for (int current = 0, loopTo = aniGroups.Count - 1;
                     current <= loopTo;
                     current++)
                    if (aniGroups.ElementAt(current).Value.uuid == entry.uuid)
                    {
                        aniGroups.Remove(aniGroups.ElementAt(current).Key, out _);
                        break;
                    }

                i -= 1;
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
    ///     执行一个动画。
    /// </summary>
    /// <param name="ani">要执行的动画对象。</param>
    /// <returns>执行后的动画数据。</returns>
    private static AniData _AniRun(AniData ani)
    {
        try
        {
            switch (ani.typeMain)
            {
                case AniType.Number:
                    _RunNumberAnimation(ani);
                    break;

                case AniType.Color:
                    _RunColorAnimation(ani);
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

        return ani;
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
                dynamic target = ani.obj;
                target.Opacity = Common.MathClamp(
                    Convert.ToDouble(target.Opacity) + delta,
                    0d,
                    1d);
                break;
            }

            case AniTypeSub.Width:
            {
                var element = (FrameworkElement)ani.obj;
                var width = double.IsNaN(element.Width)
                    ? element.ActualWidth
                    : element.Width;

                element.Width = Math.Max(width + delta, 0d);
                break;
            }

            case AniTypeSub.Height:
            {
                var element = (FrameworkElement)ani.obj;
                var height = double.IsNaN(element.Height)
                    ? element.ActualHeight
                    : element.Height;

                element.Height = Math.Max(height + delta, 0d);
                break;
            }

            case AniTypeSub.Value:
            {
                dynamic target = ani.obj;
                target.Value += delta;
                break;
            }

            case AniTypeSub.Radius:
            {
                dynamic target = ani.obj;
                target.Radius += delta;
                break;
            }

            case AniTypeSub.StrokeThickness:
            {
                dynamic target = ani.obj;
                target.StrokeThickness = Math.Max(
                    Convert.ToDouble(target.StrokeThickness) + delta,
                    0d);
                break;
            }

            case AniTypeSub.BorderThickness:
            {
                dynamic target = ani.obj;
                var thickness = (Thickness)target.BorderThickness;

                target.BorderThickness = new Thickness(
                    thickness.Bottom + delta);
                break;
            }

            case AniTypeSub.TranslateX:
            case AniTypeSub.TranslateY:
            {
                dynamic target = ani.obj;

                if (target.RenderTransform is not TranslateTransform)
                    target.RenderTransform = new TranslateTransform(0d, 0d);

                var transform = (TranslateTransform)target.RenderTransform;

                if (ani.typeSub == AniTypeSub.TranslateX)
                    transform.X += delta;
                else
                    transform.Y += delta;

                break;
            }

            case AniTypeSub.Double:
            {
                dynamic args = ani.obj;
                var target = args[0];
                var property = args[1];

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
                dynamic target = ani.obj;
                var width = Convert.ToDouble(target.Width.Value);

                target.Width = new GridLength(
                    Math.Max(width + delta, 0d),
                    GridUnitType.Star);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void _RunColorAnimation(AniData ani)
    {
        dynamic args = ani.obj;
        var element = (FrameworkElement)args[0];
        var property = (DependencyProperty)args[1];

        var delta = Common.MathPercent(
            new Common.MyColor(0d, 0d, 0d, 0d),
            (Common.MyColor)ani.value,
            _GetAnimationDelta(ani));

        delta += (Common.MyColor)ani.valueLast;

        var newColor = new Common.MyColor(element.GetValue(property)) + delta;

        element.SetValue(
            property,
            property.PropertyType.Name == "Color"
                ? (Color)newColor
                : (SolidColorBrush)newColor);

        ani.valueLast = newColor - new Common.MyColor(element.GetValue(property));
    }

    private static void _RunScaleAnimation(AniData ani)
    {
        var element = (FrameworkElement)ani.obj;
        dynamic scale = ani.value;

        var progressDelta = _GetAnimationDelta(ani);

        var marginX = Common.MathPercent(
            0d,
            Convert.ToDouble(scale.Left),
            progressDelta);

        var marginY = Common.MathPercent(
            0d,
            Convert.ToDouble(scale.Top),
            progressDelta);

        var widthDelta = Common.MathPercent(
            0d,
            Convert.ToDouble(scale.Width),
            progressDelta);

        var heightDelta = Common.MathPercent(
            0d,
            Convert.ToDouble(scale.Height),
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

    private static string _GetRandomTextCharacter(char character)
    {
        if (character >= 128)
            return Encoding
                .GetEncoding("GB18030")
                .GetString(
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