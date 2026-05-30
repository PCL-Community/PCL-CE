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
    public static void AniTimer(int DeltaTick)
    {
        try
        {
            if (DeltaTick / aniSpeed > 100d)
                ModBase.Log("[Animation] 两个动画帧间隔 " + DeltaTick + " ms", ModBase.LogLevel.Developer);
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
                        anim.timeFinished += DeltaTick;
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
                        if (aniGroups.ElementAt(current).Value.uuid == entry.uuid)
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
    /// <param name="Ani">执行的动画对象。</param>
    private static AniData AniRun(AniData Ani)
    {
        try
        {
            switch (Ani.typeMain)
            {
                case AniType.Number:
                {
                    var delta = ModBase.MathPercent(0d, (double)Ani.value,
                        Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, Ani.timePercent));
                    if (delta != 0d)
                        switch (Ani.typeSub)
                        {
                            case AniTypeSub.X:
                            {
                                ModBase.DeltaLeft((FrameworkElement)Ani.obj, delta);
                                break;
                            }
                            case AniTypeSub.Y:
                            {
                                ModBase.DeltaTop((FrameworkElement)Ani.obj, delta);
                                break;
                            }
                            case AniTypeSub.Opacity:
                            {
                                ((dynamic)Ani.obj).Opacity = ModBase.MathClamp(
                                    Convert.ToDouble(((dynamic)Ani.obj).Opacity) + delta, 0d, 1d);
                                break;
                            }
                            case AniTypeSub.Width:
                            {
                                var obj = (FrameworkElement)Ani.obj;
                                obj.Width = Math.Max((double.IsNaN(obj.Width) ? obj.ActualWidth : obj.Width) + delta,
                                    0d);
                                break;
                            }
                            case AniTypeSub.Height:
                            {
                                var obj = (FrameworkElement)Ani.obj;
                                obj.Height =
                                    Math.Max((double.IsNaN(obj.Height) ? obj.ActualHeight : obj.Height) + delta, 0d);
                                break;
                            }
                            case AniTypeSub.Value:
                            {
                                ((dynamic)Ani.obj).Value += delta;
                                break;
                            }
                            case AniTypeSub.Radius:
                            {
                                ((dynamic)Ani.obj).Radius += delta;
                                break;
                            }
                            case AniTypeSub.StrokeThickness:
                            {
                                ((dynamic)Ani.obj).StrokeThickness =
                                    Math.Max(Convert.ToDouble(((dynamic)Ani.obj).StrokeThickness) + delta, 0);
                                break;
                            }
                            case AniTypeSub.BorderThickness:
                            {
                                ((dynamic)Ani.obj).BorderThickness =
                                    new Thickness(((Thickness)((dynamic)Ani.obj).BorderThickness).Bottom + delta);
                                break;
                            }
                            case AniTypeSub.TranslateX:
                            {
                                if (((dynamic)Ani.obj).RenderTransform is null ||
                                    !(((dynamic)Ani.obj).RenderTransform is TranslateTransform))
                                    ((dynamic)Ani.obj).RenderTransform = new TranslateTransform(0d, 0d);
                                ((TranslateTransform)((dynamic)Ani.obj).RenderTransform).X += delta;
                                break;
                            }
                            case AniTypeSub.TranslateY:
                            {
                                if (((dynamic)Ani.obj).RenderTransform is null ||
                                    !(((dynamic)Ani.obj).RenderTransform is TranslateTransform))
                                    ((dynamic)Ani.obj).RenderTransform = new TranslateTransform(0d, 0d);
                                ((TranslateTransform)((dynamic)Ani.obj).RenderTransform).Y += delta;
                                break;
                            }
                            case AniTypeSub.Double:
                            {
                                ((dynamic)Ani.obj)[0].SetValue(((dynamic)Ani.obj)[1],
                                    Convert.ToDouble(((dynamic)Ani.obj)[0].GetValue(((dynamic)Ani.obj)[1])) + delta);
                                break;
                            }
                            case AniTypeSub.DoubleParam:
                            {
                                ((ParameterizedThreadStart)Ani.obj)(delta);
                                break;
                            }
                            case AniTypeSub.GridLengthWidth:
                            {
                                ((dynamic)Ani.obj).Width =
                                    new GridLength(
                                        Convert.ToDouble(
                                            Math.Max(Convert.ToDouble(((dynamic)Ani.obj).Width.Value) + delta, 0)),
                                        GridUnitType.Star);
                                break;
                            }
                        }

                    break;
                }

                case AniType.Color:
                {
                    // 利用 Last 记录了余下的小数值
                    var delta = ModBase.MathPercent(new ModBase.MyColor(0d, 0d, 0d, 0d), (ModBase.MyColor)Ani.value,
                                    Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, Ani.timePercent)) +
                                (ModBase.MyColor)Ani.valueLast;
                    var obj = (FrameworkElement)((dynamic)Ani.obj)[0];
                    var prop = (DependencyProperty)((dynamic)Ani.obj)[1];
                    var newColor = new ModBase.MyColor(obj.GetValue(prop)) + delta;
                    obj.SetValue(prop, prop.PropertyType.Name == "Color" ? (Color)newColor : (SolidColorBrush)newColor);
                    Ani.valueLast = newColor - new ModBase.MyColor(obj.GetValue(prop));
                    break;
                }

                case AniType.Scale:
                {
                    var obj = (FrameworkElement)Ani.obj;
                    var delta = Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, Ani.timePercent);
                    obj.Margin = new Thickness(
                        obj.Margin.Left +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Left), delta),
                        obj.Margin.Top + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Top), delta),
                        obj.Margin.Right +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Left), delta),
                        obj.Margin.Bottom +
                        ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Top), delta));
                    obj.Width = Math.Max(
                        obj.Width + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Width), delta), 0d);
                    obj.Height =
                        Math.Max(
                            obj.Height + ModBase.MathPercent(0d, Convert.ToDouble(((dynamic)Ani.value).Height), delta), 0d);
                    break;
                }

                case AniType.TextAppear:
                {
                    var hideFlag = (bool)((dynamic)Ani.value)[1];
                    var textLength = ((dynamic)Ani.value)[0].ToString().Length;
                    var textCount = (int)Math.Round(
                        (double)(hideFlag ? textLength : 0) + Math.Round(
                            textLength *
                            (hideFlag ? -1 : 1) *
                            Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, 0d)));
                    var originalText = ((dynamic)Ani.value)[0].ToString();
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
                    if (Ani.obj is TextBlock)
                        ((dynamic)Ani.obj).Text = newText;
                    else
                        ((dynamic)Ani.obj).Context = newText;

                    break;
                }

                case AniType.Code:
                {
                    ((ThreadStart)Ani.value)();
                    break;
                }

                case AniType.ScaleTransform:
                {
                    var obj = (FrameworkElement)Ani.obj;
                    if (!(obj.RenderTransform is ScaleTransform))
                    {
                        obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                        obj.RenderTransform = new ScaleTransform(1d, 1d);
                    }

                    var delta = ModBase.MathPercent(0d, (double)Ani.value,
                        Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, Ani.timePercent));
                    ((ScaleTransform)obj.RenderTransform).ScaleX =
                        Math.Max(((ScaleTransform)obj.RenderTransform).ScaleX + delta, 0d);
                    ((ScaleTransform)obj.RenderTransform).ScaleY =
                        Math.Max(((ScaleTransform)obj.RenderTransform).ScaleY + delta, 0d);
                    break;
                }

                case AniType.RotateTransform:
                {
                    var obj = (FrameworkElement)Ani.obj;
                    if (!(obj.RenderTransform is RotateTransform))
                    {
                        obj.RenderTransformOrigin = new Point(0.5d, 0.5d);
                        obj.RenderTransform = new RotateTransform(0d);
                    }

                    var delta = ModBase.MathPercent(0d, (double)Ani.value,
                        Ani.ease.GetDelta(Ani.timeFinished / (double)Ani.timeTotal, Ani.timePercent));
                    ((RotateTransform)obj.RenderTransform).Angle = ((RotateTransform)obj.RenderTransform).Angle + delta;
                    break;
                }
            }

            Ani.timePercent = Ani.timeFinished / (double)Ani.timeTotal; // 修改执行百分比
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "执行动画失败：" + Ani, ModBase.LogLevel.Hint);
        }

        return Ani;
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
        public int uuid = ModBase.GetUuid();
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
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaX(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.X,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     移动Y轴的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaY(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Y,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变宽度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">宽度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaWidth(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Width,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变高度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">高度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaHeight(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Height,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变透明度的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">透明度改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaOpacity(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Opacity,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变对象的Value属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">Value属性改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaValue(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Value,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变对象的Radius属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">Radius属性改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaRadius(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Radius,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变对象的BorderThickness属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">BorderThickness属性改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaBorderThickness(object Obj, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.BorderThickness,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变对象的StrokeThickness属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">StrokeThickness属性改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaStrokeThickness(object Obj, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.StrokeThickness,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     改变 Width 的 GridLength 属性的动画。必须为 Star。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">GridLength.Value 改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaGridLengthWidth(object Obj, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.GridLengthWidth,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    // DoubleAnimation（Obj, Prop, [Res]）

    /// <summary>
    ///     改变数字属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Value">改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(object Obj, DependencyProperty Prop, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.Double, timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(), obj = new[] { Obj, Prop, "" }, value = Value, isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     获取数字动画值。
    /// </summary>
    /// <param name="Value">改变的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(ParameterizedThreadStart Lambda, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.DoubleParam, timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(), obj = Lambda, value = Value, isAfter = After, timeFinished = -Delay
        };
    }

    // ColorAnimation（Obj, Prop, [Res]）

    /// <summary>
    ///     改变颜色属性的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Value">颜色改变的值。以RGB加减法进行计算。不用担心超额。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaColor(FrameworkElement Obj, DependencyProperty Prop, ModBase.MyColor Value, int Time = 400,
        int Delay = 0, AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = Time, ease = Ease ?? new AniEaseLinear(),
            obj = new object[] { Obj, Prop, "" }, value = Value, isAfter = After, timeFinished = -Delay,
            valueLast = new ModBase.MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>
    ///     改变颜色属性为一个资源的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="Res">要将颜色改变为该资源值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaColor(FrameworkElement Obj, DependencyProperty Prop, string Res, int Time = 400,
        int Delay = 0, AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = Time, ease = Ease ?? new AniEaseLinear(),
            obj = new object[] { Obj, Prop, Res },
            value = new ModBase.MyColor(System.Windows.Application.Current.FindResource(Res)) -
                    new ModBase.MyColor(Obj.GetValue(Prop)),
            isAfter = After, timeFinished = -Delay, valueLast = new ModBase.MyColor(0d, 0d, 0d, 0d)
        };
    }

    // Scale

    /// <summary>
    ///     缩放控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">大小改变的百分比（如-0.6）或值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <param name="Absolute">大小改变是否为绝对值。若为 True 则为绝对像素，若为 False 则为相对百分比。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaScale(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false, bool Absolute = false)
    {
        ModBase.MyRect changeRect;
        if (Absolute)
            changeRect = new ModBase.MyRect(-0.5d * Value, -0.5d * Value, Value, Value);
        else
            changeRect = new ModBase.MyRect(
                Convert.ToDouble(-0.5d * ((dynamic)Obj).ActualWidth * Value),
                Convert.ToDouble(-0.5d * ((dynamic)Obj).ActualHeight * Value),
                Convert.ToDouble(((dynamic)Obj).ActualWidth * Value),
                Convert.ToDouble(((dynamic)Obj).ActualHeight * Value));
        return new AniData
        {
            typeMain = AniType.Scale, timeTotal = Time, ease = Ease ?? new AniEaseLinear(), obj = Obj,
            value = changeRect, isAfter = After, timeFinished = -Delay
        };
    }

    // TextAppear

    /// <summary>
    ///     让一段文字一个个字出现或消失的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。必须是Label或TextBlock。</param>
    /// <param name="Hide">是否为一个个字隐藏。默认为False（一个个字出现）。这些字必须已经存在了。</param>
    /// <param name="TimePerText">是否采用根据文本长度决定时间的方式。</param>
    /// <param name="Time">动画长度（毫秒）。若TimePerText为True，这代表每个字所占据的时间。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaTextAppear(object Obj, bool Hide = false, bool TimePerText = true, int Time = 70,
        int Delay = 0, AniEase Ease = null, bool After = false)
    {
        // Are we cool yet？
        return new AniData
        {
            typeMain = AniType.TextAppear, ease = Ease ?? new AniEaseLinear(),
            timeTotal = TimePerText
                ? Time * (Obj is TextBlock ? ((dynamic)Obj).Text : ((dynamic)Obj).Context.ToString()).ToString().Length
                : Time,
            obj = Obj,
            value = new[] { Obj is TextBlock ? ((dynamic)Obj).Text : ((dynamic)Obj).Context.ToString(), Hide },
            isAfter = After, timeFinished = -Delay
        };
    }

    // Code

    /// <summary>
    ///     执行代码。
    /// </summary>
    /// <param name="Code">一个ThreadStart。这将会在执行时在主线程调用。</param>
    /// <param name="Delay">代码延迟执行的时间（毫秒）。</param>
    /// <param name="After">是否等到以前的动画完成后才执行。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaCode(ThreadStart Code, int Delay = 0, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Code,
            timeTotal = 1,
            value = Code,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    // ScaleTransform

    /// <summary>
    ///     按照 WPF 方式缩放控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="Value">大小改变的百分比（如-0.6）。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaScaleTransform(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.ScaleTransform, timeTotal = Time, ease = Ease ?? new AniEaseLinear(), obj = Obj,
            value = Value, isAfter = After, timeFinished = -Delay
        };
    }

    // RotateTransform

    /// <summary>
    ///     按照 WPF 方式旋转控件的动画。
    /// </summary>
    /// <param name="Obj">动画的对象。它必须已经拥有了单一的 ScaleTransform 值。</param>
    /// <param name="Value">大小改变的百分比（如-0.6）。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaRotateTransform(object Obj, double Value, int Time = 400, int Delay = 0,
        AniEase Ease = null, bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.RotateTransform, timeTotal = Time, ease = Ease ?? new AniEaseLinear(), obj = Obj,
            value = Value, isAfter = After, timeFinished = -Delay
        };
    }

    // TranslateTransform

    /// <summary>
    ///     利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateX(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateX,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    /// <summary>
    ///     利用 TranslateTransform 移动 Y 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Value">进行移动的值。</param>
    /// <param name="Time">动画长度（毫秒）。</param>
    /// <param name="Delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="Ease">插值器类型。</param>
    /// <param name="After">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateY(object Obj, double Value, int Time = 400, int Delay = 0, AniEase Ease = null,
        bool After = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateY,
            timeTotal = Time,
            ease = Ease ?? new AniEaseLinear(),
            obj = Obj,
            value = Value,
            isAfter = After,
            timeFinished = -Delay
        };
    }

    // 特殊

    /// <summary>
    ///     将一个StackPanel中的各个项目依次显示。
    /// </summary>
    /// <remarks></remarks>
    public static List<AniData> AaStack(StackPanel Stack, int Time = 100, int Delay = 25)
    {
        List<AniData> aaStackRet = default;
        aaStackRet = new List<AniData>();
        var aniDelay = 0;
        foreach (var Item in Stack.Children)
        {
            ((dynamic)Item).Opacity = 0;
            aaStackRet.Add(AaOpacity(Item, 1d, Time, aniDelay));
            aniDelay += Delay;
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

        public AniEaseInout(AniEase EaseIn, AniEase EaseOut, double EaseInPercent = 0.5d)
        {
            this.easeIn = EaseIn;
            this.easeOut = EaseOut;
            this.easeInPercent = EaseInPercent;
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

        public AniEaseInFluent(AniEasePower Power = AniEasePower.Middle)
        {
            p = Power;
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

        public AniEaseOutFluent(AniEasePower Power = AniEasePower.Middle)
        {
            p = Power;
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

        public AniEaseInoutFluent(AniEasePower Power = AniEasePower.Middle, double Middle = 0.5d)
        {
            ease = new AniEaseInout(new AniEaseInFluent(Power), new AniEaseOutFluent(Power), Middle);
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

        /// <param name="InitialPixelPerSecond">初速度，px/s</param>
        /// <param name="TotalSecond">总时长，s</param>
        /// <param name="TotalDistance">总路程，px</param>
        public AniEaseOutFluentWithInitial(double InitialPixelPerSecond, double TotalSecond, double TotalDistance)
        {
            var v0_norm = InitialPixelPerSecond * TotalSecond / TotalDistance; // 归一化初速度
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

        public AniEaseInBack(AniEasePower Power = AniEasePower.Middle)
        {
            p = 3d - (double)Power * 0.5d;
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

        public AniEaseOutBack(AniEasePower Power = AniEasePower.Middle)
        {
            p = 3d - (double)Power * 0.5d;
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

        public AniEaseInCar(double Middle = 0.7d, AniEasePower Power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInBack(Power), new AniEaseOutFluent(Power), Middle);
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

        public AniEaseOutCar(double Middle = 0.3d, AniEasePower Power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInFluent(Power), new AniEaseOutBack(Power), Middle);
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

        public AniEaseInElastic(AniEasePower Power = AniEasePower.Middle)
        {
            p = (int)Power + 4;
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

        public AniEaseOutElastic(AniEasePower Power = AniEasePower.Middle)
        {
            p = (int)Power + 4;
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
    /// <param name="AniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    /// <param name="Name">动画组的名称。如果重复会直接停止同名动画组。</param>
    public static void AniStart(IList AniGroup, string Name = "", bool RefreshTime = false)
    {
        if (RefreshTime)
            aniLastTick = TimeUtils.GetTimeTick(); // 避免处理动画时已经造成了极大的延迟，导致动画突然结束
        // 添加到正在执行的动画组
        var newEntry = new AniGroupEntry
            { data = ModBase.GetFullList<AniData>(AniGroup), startTick = TimeUtils.GetTimeTick() };
        if (string.IsNullOrEmpty(Name))
            Name = newEntry.uuid.ToString();
        else
            AniStop(Name);
        aniGroups.TryAdd(Name, newEntry);
    }

    /// <summary>
    ///     开始一个动画组。
    /// </summary>
    public static void AniStart(AniData AniGroup, string Name = "", bool RefreshTime = false)
    {
        AniStart(new List<AniData> { AniGroup }, Name, RefreshTime);
    }

    /// <summary>
    ///     直接停止一个动画组。
    /// </summary>
    /// <param name="name">需要停止的动画组的名称。</param>
    public static void AniStop(string Name)
    {
        aniGroups.Remove(Name, out _);
    }

    /// <summary>
    ///     获取动画是否正在进行中。
    /// </summary>
    public static bool AniIsRun(string Name)
    {
        return aniGroups.ContainsKey(Name);
    }

    #endregion
}
