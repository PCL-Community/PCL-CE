using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class ModAnimation
{
    private static int aniCount;
    private static int aniFPSCounter;
    private static long aniFPSTimer;

    /// <summary>
    ///     当前的动画 FPS。
    /// </summary>
    public static int aniFPS;

    /// <summary>
    ///     开始动画执行。
    /// </summary>
    public static void AniStart()
    {
        // 初始化计时器
        aniLastTick = TimeUtils.GetTimeTick();
        aniFPSTimer = aniLastTick;
        aniRunning = true; // 标记动画执行开始

        var minFrameGap = 1000d / (Config.System.AnimationFpsLimit + 1) / 2;


        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModBase.Log("[Animation] 动画线程开始");
                while (true)
                {
                    // 两帧之间的间隔时间
                    var deltaTime =
                        (long)Math.Round(ModBase.MathClamp(TimeUtils.GetTimeTick() - aniLastTick, 0, 100000));
                    if (deltaTime < minFrameGap)
                    {
                        // 限制 FPS
                        Thread.Sleep(1);
                        continue;
                    }

                    aniLastTick = TimeUtils.GetTimeTick();
                    // 记录 FPS
                    if (ModBase.modeDebug)
                    {
                        if (ModBase.MathClamp(aniLastTick - aniFPSTimer, 0d, 100000d) >= 500d)
                        {
                            aniFPS = aniFPSCounter;
                            aniFPSCounter = 0;
                            aniFPSTimer = aniLastTick;
                        }

                        aniFPSCounter += 2;
                    }

                    // 执行动画
                    ModBase.RunInUiWait(() =>
                    {
                        aniCount = 0;
                        AniTimer((int)Math.Round(deltaTime * aniSpeed));
                        // #If DEBUG Then
                        // FrmMain.Title = "F " & AniFPS & ", A " & AniCount & ", R " & NetManage.FileRemain
                        // #Else
                        // If ModeDebug Then FrmMain.Title = "FPS " & AniFPS & ", 动画 " & AniCount & ", 下载中 " & NetManage.FileRemain
                        // #End If
                        if (RandomUtils.NextInt(0, 64 * (ModBase.modeDebug ? 5 : 30)) == 0 &&
                            ((aniFPS < 62 && aniFPS > 0) || aniCount > 4 || ModNet.NetManager.FileRemain != 0))
                            ModBase.Log("[Report] FPS " + aniFPS + ", 动画 " + aniCount + ", 下载中 " +
                                        ModNet.NetManager.FileRemain + "（" +
                                        ModBase.GetString(ModNet.NetManager.Speed) + "/s）");
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "动画帧执行失败", ModBase.LogLevel.Critical);
            }
        }, "Animation", ThreadPriority.AboveNormal);
    }

    /// <summary>
    ///     动画定时器事件。
    /// </summary>
    public static void AniTimer(int deltaTick)
    {
        try
        {
            if (deltaTick / aniSpeed > 100d)
                ModBase.Log("[Animation] 两个动画帧间隔 " + deltaTick + " ms", ModBase.LogLevel.Developer);
            var i = -1;
            // 循环每个动画组
            while (i + 1 < aniGroups.Count)
            {
                i += 1;
                // 初始化
                var entry = aniGroups.Values.ElementAtOrDefault(i);
                if (entry.startTick > aniLastTick)
                    continue; // 跳过本刻之后开始的动画
                var canRemoveAfter = true; // 是否应该去除“之后”标记
                var ii = 0;

                // 循环每个动画
                while (ii < entry.data.Count)
                {
                    var anim = entry.data[ii];
                    // 执行种类
                    if (!anim.isAfter) // 之前
                    {
                        canRemoveAfter = false; // 取消“之后”标记 
                        // 增加执行时间
                        anim.timeFinished += deltaTick;
                        // 执行动画
                        if (anim.timeFinished > 0)
                        {
                            anim = AniRun(anim);
                            aniCount += 1;
                        }

                        // 如果当前动画已执行完毕
                        if (anim.timeFinished >= anim.timeTotal)
                        {
                            // 如果是去向颜色资源的动画，设置引用
                            if (anim.typeMain == AniType.Color &&
                                !string.Equals(((dynamic)anim.obj)[2] as string, "", StringComparison.Ordinal))
                                ((dynamic)anim.obj)[0]
                                    .SetResourceReference(((dynamic)anim.obj)[1], ((dynamic)anim.obj)[2]);
                            // 删除
                            entry.data.RemoveAt(ii);
                            goto NextAni;
                        }

                        entry.data[ii] = anim;
                    }
                    else if (canRemoveAfter) // 之后
                    {
                        // 之后改为之前
                        canRemoveAfter = false;
                        anim.isAfter = false;
                        entry.data[ii] = anim;
                        // 重新循环该动画
                        goto NextAni;
                    }
                    else
                    {
                        // 不能去除该“之后”标记，结束该动画组
                        break;
                    }

                    ii += 1;
                    NextAni: ;
                }

                // 如果当前动画组都执行完毕则删除
                if (!entry.data.Any())
                {
                    // 为了避免新添加的动画影响顺序，不能 RemoveAt(i)
                    // 为了允许动画在执行中添加同名动画组，不能按名字移除
                    for (int current = 0, loopTo = aniGroups.Count - 1; current <= loopTo; current++)
                        if (aniGroups.ElementAt(current).Value.Uuid == entry.Uuid)
                        {
                            aniGroups.Remove(aniGroups.ElementAt(current).Key, out _);
                            break;
                        }

                    i -= 1;
                }
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "动画刻执行失败", ModBase.LogLevel.Hint);
        }
    }

    /// <summary>
    ///     执行一个动画。
    /// </summary>
    /// <param name="ani">执行的动画对象。</param>
    private static AniData AniRun(AniData ani)
    {
        try
        {
            switch (ani.typeMain)
            {
                case AniType.Number:
                {
                    var delta = ModBase.MathPercent(0d, (double)ani.value,
                        ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    if (delta != 0d)
                        switch (ani.typeSub)
                        {
                            case AniTypeSub.X:
                            {
                                ModBase.DeltaLeft((FrameworkElement)ani.obj, delta);
                                break;
                            }
                            case AniTypeSub.Y:
                            {
                                ModBase.DeltaTop((FrameworkElement)ani.obj, delta);
                                break;
                            }
                            case AniTypeSub.Opacity:
                            {
                                ((dynamic)ani.obj).Opacity = ModBase.MathClamp(
                                    Convert.ToDouble(((dynamic)ani.obj).Opacity) + delta, 0d, 1d);
                                break;
                            }
                            case AniTypeSub.Width:
                            {
                                var obj = (FrameworkElement)ani.obj;
                                obj.Width = Math.Max((double.IsNaN(obj.Width) ? obj.ActualWidth : obj.Width) + delta,
                                    0d);
                                break;
                            }
                            case AniTypeSub.Height:
                            {
                                var obj = (FrameworkElement)ani.obj;
                                obj.Height =
                                    Math.Max((double.IsNaN(obj.Height) ? obj.ActualHeight : obj.Height) + delta, 0d);
                                break;
                            }
                            case AniTypeSub.Value:
                            {
                                ((dynamic)ani.obj).Value += delta;
                                break;
                            }
                            case AniTypeSub.Radius:
                            {
                                ((dynamic)ani.obj).Radius += delta;
                                break;
                            }
                            case AniTypeSub.StrokeThickness:
                            {
                                ((dynamic)ani.obj).StrokeThickness =
                                    Math.Max(Convert.ToDouble(((dynamic)ani.obj).StrokeThickness) + delta, 0);
                                break;
                            }
                            case AniTypeSub.BorderThickness:
                            {
                                ((dynamic)ani.obj).BorderThickness =
                                    new Thickness(((Thickness)((dynamic)ani.obj).BorderThickness).Bottom + delta);
                                break;
                            }
                            case AniTypeSub.TranslateX:
                            {
                                if (((dynamic)ani.obj).RenderTransform is null ||
                                    !(((dynamic)ani.obj).RenderTransform is TranslateTransform))
                                    ((dynamic)ani.obj).RenderTransform = new TranslateTransform(0d, 0d);
                                ((TranslateTransform)((dynamic)ani.obj).RenderTransform).X += delta;
                                break;
                            }
                            case AniTypeSub.TranslateY:
                            {
                                if (((dynamic)ani.obj).RenderTransform is null ||
                                    !(((dynamic)ani.obj).RenderTransform is TranslateTransform))
                                    ((dynamic)ani.obj).RenderTransform = new TranslateTransform(0d, 0d);
                                ((TranslateTransform)((dynamic)ani.obj).RenderTransform).Y += delta;
                                break;
                            }
                            case AniTypeSub.Double:
                            {
                                ((dynamic)ani.obj)[0].SetValue(((dynamic)ani.obj)[1],
                                    Convert.ToDouble(((dynamic)ani.obj)[0].GetValue(((dynamic)ani.obj)[1])) + delta);
                                break;
                            }
                            case AniTypeSub.DoubleParam:
                            {
                                ((ParameterizedThreadStart)ani.obj)(delta);
                                break;
                            }
                            case AniTypeSub.GridLengthWidth:
                            {
                                ((dynamic)ani.obj).Width =
                                    new GridLength(
                                        Convert.ToDouble(
                                            Math.Max(Convert.ToDouble(((dynamic)ani.obj).Width.Value) + delta, 0)),
                                        GridUnitType.Star);
                                break;
                            }
                        }

                    break;
                }

                case AniType.Color:
                {
                    // 利用 Last 记录了余下的小数值
                    var delta = ModBase.MathPercent(new ModBase.MyColor(0d, 0d, 0d, 0d), (ModBase.MyColor)ani.value,
                                    ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent)) +
                                (ModBase.MyColor)ani.valueLast;
                    var obj = (FrameworkElement)((dynamic)ani.obj)[0];
                    var prop = (DependencyProperty)((dynamic)ani.obj)[1];
                    var newColor = new ModBase.MyColor(obj.GetValue(prop)) + delta;
                    obj.SetValue(prop, prop.PropertyType.Name == "Color" ? (Color)newColor : (SolidColorBrush)newColor);
                    ani.valueLast = newColor - new ModBase.MyColor(obj.GetValue(prop));
                    break;
                }

                case AniType.Scale:
                {
                    var obj = (FrameworkElement)ani.obj;
                    var delta = ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent);
                    obj.Margin = new Thickness(
                        obj.Margin.Left +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Left), delta),
                        obj.Margin.Top + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Top), delta),
                        obj.Margin.Right +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Left), delta),
                        obj.Margin.Bottom +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Top), delta));
                    obj.Width = Math.Max(
                        obj.Width + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Width), delta), 0d);
                    obj.Height =
                        Math.Max(
                            obj.Height + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)ani.value).Height), delta), 0d);
                    break;
                }

                case AniType.TextAppear:
                {
                    var hideFlag = (bool)((dynamic)ani.value)[1];
                    var textLength = ((dynamic)ani.value)[0].ToString().Length;
                    var textCount = (int)Math.Round(
                        (double)(hideFlag ? textLength : 0) + Math.Round(
                            textLength *
                            (hideFlag ? -1 : 1) *
                            ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, 0d)));
                    var originalText = ((dynamic)ani.value)[0].ToString();
                    var newText = originalText.Substring(0, Math.Min(textCount, originalText.Length));
                    // 添加乱码
                    if (textCount < originalText.Length)
                    {
                        var nextText = originalText.Substring(textCount, 1);
                        if (Convert.ToInt32(Convert.ToChar(nextText)) >= Convert.ToInt32(Convert.ToChar(128)))
                            newText += Encoding.GetEncoding("GB18030").GetString(new[]
                            {
                                (byte)RandomUtils.NextInt(16 + 160, 87 + 160),
                                (byte)RandomUtils.NextInt(1 + 160, 89 + 160)
                            });
                        else
                            newText += RandomUtils.PickRandom(
                                @"0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM"
                                    .ToCharArray());
                    }

                    // 设置文本
                    if (ani.obj is TextBlock)
                        ((dynamic)ani.obj).Text = newText;
                    else
                        ((dynamic)ani.obj).Context = newText;

                    break;
                }

                case AniType.Code:
                {
                    ((ThreadStart)ani.value)();
                    break;
                }

                case AniType.ScaleTransform:
                {
                    var obj = (FrameworkElement)ani.obj;
                    if (!(obj.RenderTransform is ScaleTransform))
                    {
                        obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                        obj.RenderTransform = new ScaleTransform(1d, 1d);
                    }

                    var delta = ModBase.MathPercent(0d, (double)ani.value,
                        ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    ((ScaleTransform)obj.RenderTransform).ScaleX =
                        Math.Max(((ScaleTransform)obj.RenderTransform).ScaleX + delta, 0d);
                    ((ScaleTransform)obj.RenderTransform).ScaleY =
                        Math.Max(((ScaleTransform)obj.RenderTransform).ScaleY + delta, 0d);
                    break;
                }

                case AniType.RotateTransform:
                {
                    var obj = (FrameworkElement)ani.obj;
                    if (!(obj.RenderTransform is RotateTransform))
                    {
                        obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                        obj.RenderTransform = new RotateTransform(0d);
                    }

                    var delta = ModBase.MathPercent(0d, (double)ani.value,
                        ani.ease.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    ((RotateTransform)obj.RenderTransform).Angle = ((RotateTransform)obj.RenderTransform).Angle + delta;
                    break;
                }
            }

            ani.timePercent = ani.timeFinished / (double)ani.timeTotal; // 修改执行百分比
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "执行动画失败：" + ani, ModBase.LogLevel.Hint);
        }

        return ani;
    }

    #region 声明

    /// <summary>
    ///     动画速度。最大为 200。
    /// </summary>
    public static double aniSpeed = 1d;

    /// <summary>
    ///     动画组列表。
    /// </summary>
    public static ConcurrentDictionary<string, AniGroupEntry> aniGroups = new();

    public class AniGroupEntry
    {
        public List<AniData> data;
        public long startTick;
        public int Uuid = ModBase.GetUuid();
    }

    /// <summary>
    ///     上一次记刻的时间。
    /// </summary>
    private static long aniLastTick;

    /// <summary>
    ///     动画模块是否正在运行。
    /// </summary>
    public static bool aniRunning;

    private static int _AniControlEnabled;
    private static readonly object aniControlEnabledLock = new();

    /// <summary>
    ///     控件动画执行是否开启。先 +1，再 -1。
    /// </summary>
    public static int AniControlEnabled
    {
        get => _AniControlEnabled;
        set
        {
            lock (aniControlEnabledLock)
            {
                _AniControlEnabled = value;
            }
        }
    }

    #endregion

    #region 类与枚举

    /// <summary>
    ///     单个动画对象。
    /// </summary>
    /// <remarks></remarks>
    public struct AniData
    {
        /// <summary>
        ///     动画种类。
        /// </summary>
        /// <remarks></remarks>
        public AniType typeMain;

        /// <summary>
        ///     动画副种类。
        /// </summary>
        /// <remarks></remarks>
        public AniTypeSub typeSub;

        /// <summary>
        ///     动画总长度。
        /// </summary>
        /// <remarks></remarks>
        public int timeTotal;

        /// <summary>
        ///     已经执行的动画长度。如果为负数则为延迟。
        /// </summary>
        /// <remarks></remarks>
        public int timeFinished;

        /// <summary>
        ///     已经完成的百分比。
        /// </summary>
        /// <remarks></remarks>
        public double timePercent;

        /// <summary>
        ///     是否为“以后”。
        /// </summary>
        /// <remarks></remarks>
        public bool isAfter;

        /// <summary>
        ///     插值器类型。
        /// </summary>
        /// <remarks></remarks>
        public AniEase ease;

        /// <summary>
        ///     动画对象。
        /// </summary>
        /// <remarks></remarks>
        public object obj;

        /// <summary>
        ///     动画值。
        /// </summary>
        /// <remarks></remarks>
        public object value;

        /// <summary>
        ///     上次执行时的动画值。
        /// </summary>
        /// <remarks></remarks>
        public object valueLast;

        public override string ToString()
        {
            return ModBase.GetStringFromEnum(typeMain) + " | " + timeFinished + "/" + timeTotal + "(" +
                   Math.Round(timePercent * 100d) + "%)" +
                   (obj is null ? "" : " | " + obj + "(" + obj.GetType().Name + ")");
        }
    }

    /// <summary>
    ///     动画基础种类。
    /// </summary>
    public enum AniType
    {
        /// <summary>
        ///     单个Double的动画，包括位置、长宽、透明度等。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
        Number,

        /// <summary>
        ///     颜色属性的动画。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
        Color,

        /// <summary>
        ///     缩放控件大小。比起4个DoubleAnimation来说效率更高。
        /// </summary>
        /// <remarks></remarks>
        Scale,

        /// <summary>
        ///     文字一个个出现。
        /// </summary>
        /// <remarks></remarks>
        TextAppear,

        /// <summary>
        ///     执行代码。
        /// </summary>
        /// <remarks></remarks>
        Code,

        /// <summary>
        ///     以 WPF 方式缩放控件。
        /// </summary>
        ScaleTransform,

        /// <summary>
        ///     以 WPF 方式旋转控件。
        /// </summary>
        RotateTransform
    }

    /// <summary>
    ///     动画扩展种类。
    /// </summary>
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
        DoubleParam,
        GridLengthWidth
    }

    #endregion

    #region 种类

    // DoubleAnimation

    /// <summary>
    ///     移动X轴的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaX(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.X,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     移动Y轴的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaY(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Y,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变宽度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">宽度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaWidth(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Width,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变高度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">高度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaHeight(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Height,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变透明度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">透明度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaOpacity(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Opacity,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变对象的Value属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">Value属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaValue(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Value,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变对象的Radius属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">Radius属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaRadius(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Radius,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变对象的BorderThickness属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">BorderThickness属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaBorderThickness(object obj, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.BorderThickness,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变对象的StrokeThickness属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">StrokeThickness属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaStrokeThickness(object obj, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.StrokeThickness,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变 Width 的 GridLength 属性的动画。必须为 Star。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">GridLength.Value 改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaGridLengthWidth(object obj, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.GridLengthWidth,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // DoubleAnimation（Obj, Prop, [Res]）

    /// <summary>
    ///     改变数字属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(object obj, DependencyProperty prop, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.Double, timeTotal = time,
            ease = ease ?? new AniEaseLinear(), obj = new[] { obj, prop, "" }, value = value, isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     获取数字动画值。
    /// </summary>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(ParameterizedThreadStart lambda, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.DoubleParam, timeTotal = time,
            ease = ease ?? new AniEaseLinear(), obj = lambda, value = value, isAfter = after, timeFinished = -delay
        };
    }

    // ColorAnimation（Obj, Prop, [Res]）

    /// <summary>
    ///     改变颜色属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="value">颜色改变的值。以RGB加减法进行计算。不用担心超额。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaColor(FrameworkElement obj, DependencyProperty prop, ModBase.MyColor value, int time = 400,
        int delay = 0, AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = time, ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, "" }, value = value, isAfter = after, timeFinished = -delay,
            valueLast = new ModBase.MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>
    ///     改变颜色属性为一个资源的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画的依赖属性。</param>
    /// <param name="res">要将颜色改变为该资源值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaColor(FrameworkElement obj, DependencyProperty prop, string res, int time = 400,
        int delay = 0, AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = time, ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, res },
            value = new ModBase.MyColor(System.Windows.Application.Current.FindResource(res)) -
                    new ModBase.MyColor(obj.GetValue(prop)),
            isAfter = after, timeFinished = -delay, valueLast = new ModBase.MyColor(0d, 0d, 0d, 0d)
        };
    }

    // Scale

    /// <summary>
    ///     缩放控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">大小改变的百分比（如-0.6）或值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <param name="absolute">大小改变是否为绝对值。若为 True 则为绝对像素，若为 False 则为相对百分比。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaScale(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false, bool absolute = false)
    {
        ModBase.MyRect changeRect;
        if (absolute)
            changeRect = new ModBase.MyRect(-0.5d * value, -0.5d * value, value, value);
        else
            changeRect = new ModBase.MyRect(
                Convert.ToDouble(-0.5d * ((dynamic)obj).ActualWidth * value),
                Convert.ToDouble(-0.5d * ((dynamic)obj).ActualHeight * value),
                Convert.ToDouble(((dynamic)obj).ActualWidth * value),
                Convert.ToDouble(((dynamic)obj).ActualHeight * value));
        return new AniData
        {
            typeMain = AniType.Scale, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = changeRect, isAfter = after, timeFinished = -delay
        };
    }

    // TextAppear

    /// <summary>
    ///     让一段文字一个个字出现或消失的动画。
    /// </summary>
    /// <param name="obj">动画的对象。必须是Label或TextBlock。</param>
    /// <param name="hide">是否为一个个字隐藏。默认为False（一个个字出现）。这些字必须已经存在了。</param>
    /// <param name="timePerText">是否采用根据文本长度决定时间的方式。</param>
    /// <param name="time">动画长度（毫秒）。若TimePerText为True，这代表每个字所占据的时间。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaTextAppear(object obj, bool hide = false, bool timePerText = true, int time = 70,
        int delay = 0, AniEase ease = null, bool after = false)
    {
        // Are we cool yet？
        return new AniData
        {
            typeMain = AniType.TextAppear, ease = ease ?? new AniEaseLinear(),
            timeTotal = timePerText
                ? time * (obj is TextBlock ? ((dynamic)obj).Text : ((dynamic)obj).Context.ToString()).ToString().Length
                : time,
            obj = obj,
            value = new[] { obj is TextBlock ? ((dynamic)obj).Text : ((dynamic)obj).Context.ToString(), hide },
            isAfter = after, timeFinished = -delay
        };
    }

    // Code

    /// <summary>
    ///     执行代码。
    /// </summary>
    /// <param name="Code">一个ThreadStart。这将会在执行时在主线程调用。</param>
    /// <param name="delay">代码延迟执行的时间（毫秒）。</param>
    /// <param name="after">是否等到以前的动画完成后才执行。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaCode(ThreadStart code, int delay = 0, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Code,
            timeTotal = 1,
            value = code,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // ScaleTransform

    /// <summary>
    ///     按照 WPF 方式缩放控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="value">大小改变的百分比（如-0.6）。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaScaleTransform(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.ScaleTransform, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = value, isAfter = after, timeFinished = -delay
        };
    }

    // RotateTransform

    /// <summary>
    ///     按照 WPF 方式旋转控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="value">大小改变的百分比（如-0.6）。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaRotateTransform(object obj, double value, int time = 400, int delay = 0,
        AniEase ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.RotateTransform, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = value, isAfter = after, timeFinished = -delay
        };
    }

    // TranslateTransform

    /// <summary>
    ///     利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateX(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateX,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     利用 TranslateTransform 移动 Y 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateY(object obj, double value, int time = 400, int delay = 0, AniEase ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateY,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // 特殊

    /// <summary>
    ///     将一个StackPanel中的各个项目依次显示。
    /// </summary>
    /// <remarks></remarks>
    public static List<AniData> AaStack(StackPanel stack, int time = 100, int delay = 25)
    {
        List<AniData> aaStackRet = default;
        aaStackRet = new List<AniData>();
        var aniDelay = 0;
        foreach (var Item in stack.Children)
        {
            ((dynamic)Item).Opacity = 0;
            aaStackRet.Add(AaOpacity(Item, 1d, time, aniDelay));
            aniDelay += delay;
        }

        return aaStackRet;
    }

    #endregion

    #region 缓动函数

    // 基类
    public enum AniEasePower
    {
        Weak = 2,
        Middle = 3,
        Strong = 4,
        ExtraStrong = 5
    }

    /// <summary>
    ///     缓动函数基类。
    /// </summary>
    public abstract class AniEase
    {
        /// <summary>
        ///     获取函数值。
        /// </summary>
        /// <param name="t">时间百分比。</param>
        public abstract double GetValue(double t);

        /// <summary>
        ///     获取增量值。
        /// </summary>
        /// <param name="t1">较大的 X。</param>
        /// <param name="t0">较小的 X。</param>
        public virtual double GetDelta(double t1, double t0)
        {
            return GetValue(t1) - GetValue(t0);
        }
    }

    /// <summary>
    ///     渐入渐出组合。
    /// </summary>
    public class AniEaseInout : AniEase
    {
        private readonly AniEase easeIn;
        private readonly double easeInPercent;
        private readonly AniEase easeOut;

        public AniEaseInout(AniEase easeIn, AniEase easeOut, double easeInPercent = 0.5d)
        {
            this.easeIn = easeIn;
            this.easeOut = easeOut;
            this.easeInPercent = easeInPercent;
        }

        public override double GetValue(double t)
        {
            if (t < easeInPercent) return easeInPercent * easeIn.GetValue(t / easeInPercent);

            return (1d - easeInPercent) * easeOut.GetValue((t - easeInPercent) / (1d - easeInPercent)) + easeInPercent;
        }
    }

    // Linear / 线性
    /// <summary>
    ///     线性，无缓动。
    /// </summary>
    public class AniEaseLinear : AniEase
    {
        public override double GetValue(double t)
        {
            return ModBase.MathClamp(t, 0d, 1d);
        }

        public override double GetDelta(double t1, double t0)
        {
            return ModBase.MathClamp(t1, 0d, 1d) - ModBase.MathClamp(t0, 0d, 1d);
        }
    }

    // Fluent / 平滑
    /// <summary>
    ///     平滑开始。
    /// </summary>
    public class AniEaseInFluent : AniEase
    {
        private readonly AniEasePower p;

        public AniEaseInFluent(AniEasePower power = AniEasePower.Middle)
        {
            p = power;
        }

        public override double GetValue(double t)
        {
            return Math.Pow(ModBase.MathClamp(t, 0d, 1d), (double)p);
        }
    }

    /// <summary>
    ///     平滑结束。
    /// </summary>
    public class AniEaseOutFluent : AniEase
    {
        private readonly AniEasePower p;

        public AniEaseOutFluent(AniEasePower power = AniEasePower.Middle)
        {
            p = power;
        }

        public override double GetValue(double t)
        {
            return 1d - Math.Pow(ModBase.MathClamp(1d - t, 0d, 1d), (double)p);
        }
    }

    /// <summary>
    ///     平滑开始与结束。
    /// </summary>
    public class AniEaseInoutFluent : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseInoutFluent(AniEasePower power = AniEasePower.Middle, double middle = 0.5d)
        {
            ease = new AniEaseInout(new AniEaseInFluent(power), new AniEaseOutFluent(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    /// <summary>
    ///     以特定速度开始的平滑结束。
    /// </summary>
    public class AniEaseOutFluentWithInitial : AniEase
    {
        private readonly double alpha; // (初速度 / 平均速度) – 1

        /// <param name="initialPixelPerSecond">初速度，px/s</param>
        /// <param name="totalSecond">总时长，s</param>
        /// <param name="totalDistance">总路程，px</param>
        public AniEaseOutFluentWithInitial(double initialPixelPerSecond, double totalSecond, double totalDistance)
        {
            var v0_norm = initialPixelPerSecond * totalSecond / totalDistance; // 归一化初速度
            alpha = v0_norm - 1.0d;
            if (alpha < 0d)
                alpha = 0d; // 初速度小于平均速度时，退化为线性
        }

        public override double GetValue(double percent)
        {
            var p = ModBase.MathClamp(percent, 0d, 1d);
            if (alpha == 0d)
                return p; // 退化到线性
            return (alpha + 1d) * p / (1d + alpha * p);
        }
    }

    // Back / 回弹
    /// <summary>
    ///     回弹开始。有效时间为 1/3。
    /// </summary>
    public class AniEaseInBack : AniEase
    {
        private readonly double p;

        public AniEaseInBack(AniEasePower power = AniEasePower.Middle)
        {
            p = 3d - (double)power * 0.5d;
        }

        public override double GetValue(double t)
        {
            t = ModBase.MathClamp(t, 0d, 1d);
            return Math.Pow(t, p) * Math.Cos(1.5d * Math.PI * (1d - t));
        }
    }

    /// <summary>
    ///     回弹结束。有效时间为 1/3。
    /// </summary>
    public class AniEaseOutBack : AniEase
    {
        private readonly double p;

        public AniEaseOutBack(AniEasePower power = AniEasePower.Middle)
        {
            p = 3d - (double)power * 0.5d;
        }

        public override double GetValue(double t)
        {
            t = ModBase.MathClamp(t, 0d, 1d);
            return 1d - Math.Pow(1d - t, p) * Math.Cos(1.5d * Math.PI * t);
        }
    }

    // Car / 平滑-回弹
    /// <summary>
    ///     回弹开始，短平滑结束。
    /// </summary>
    public class AniEaseInCar : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseInCar(double middle = 0.7d, AniEasePower power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInBack(power), new AniEaseOutFluent(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    /// <summary>
    ///     短平滑开始，回弹结束。
    /// </summary>
    public class AniEaseOutCar : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseOutCar(double middle = 0.3d, AniEasePower power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInFluent(power), new AniEaseOutBack(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    // Elastic / 弹簧
    /// <summary>
    ///     弹簧开始。约在 60% 到达最小值。
    /// </summary>
    public class AniEaseInElastic : AniEase
    {
        private readonly int p; // 6~9

        public AniEaseInElastic(AniEasePower power = AniEasePower.Middle)
        {
            p = (int)power + 4;
        }

        public override double GetValue(double t)
        {
            t = ModBase.MathClamp(t, 0d, 1d);
            return Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    /// <summary>
    ///     弹簧结束。约在 40% 到达最大值。
    /// </summary>
    public class AniEaseOutElastic : AniEase
    {
        private readonly int p;

        public AniEaseOutElastic(AniEasePower power = AniEasePower.Middle)
        {
            p = (int)power + 4;
        }

        public override double GetValue(double t)
        {
            t = 1d - ModBase.MathClamp(t, 0d, 1d);
            return 1d - Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    #endregion

    #region 接口（开始、中断、检测）

    /// <summary>
    ///     开始一个动画组。
    /// </summary>
    /// <param name="aniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    /// <param name="name">动画组的名称。如果重复会直接停止同名动画组。</param>
    public static void AniStart(IList aniGroup, string name = "", bool refreshTime = false)
    {
        if (refreshTime)
            aniLastTick = TimeUtils.GetTimeTick(); // 避免处理动画时已经造成了极大的延迟，导致动画突然结束
        // 添加到正在执行的动画组
        var newEntry = new AniGroupEntry
            { data = ModBase.GetFullList<AniData>(aniGroup), startTick = TimeUtils.GetTimeTick() };
        if (string.IsNullOrEmpty(name))
            name = newEntry.Uuid.ToString();
        else
            AniStop(name);
        aniGroups.TryAdd(name, newEntry);
    }

    /// <summary>
    ///     开始一个动画组。
    /// </summary>
    public static void AniStart(AniData aniGroup, string name = "", bool refreshTime = false)
    {
        AniStart(new List<AniData> { aniGroup }, name, refreshTime);
    }

    /// <summary>
    ///     直接停止一个动画组。
    /// </summary>
    /// <param name="name">需要停止的动画组的名称。</param>
    public static void AniStop(string name)
    {
        aniGroups.Remove(name, out _);
    }

    /// <summary>
    ///     获取动画是否正在进行中。
    /// </summary>
    public static bool AniIsRun(string name)
    {
        return aniGroups.ContainsKey(name);
    }

    #endregion
}
