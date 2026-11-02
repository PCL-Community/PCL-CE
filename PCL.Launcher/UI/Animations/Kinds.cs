using Avalonia;
using Avalonia.Controls;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     移动 X 轴的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaX(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    ///     移动 Y 轴的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaY(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    public static AniData AaWidth(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    public static AniData AaHeight(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    public static AniData AaOpacity(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    ///     改变对象的 Value 属性的动画。
    /// </summary>
    public static AniData AaValue(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    ///     改变对象的 Radius 属性的动画。
    /// </summary>
    public static AniData AaRadius(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    ///     改变对象的 BorderThickness 属性的动画。
    /// </summary>
    public static AniData AaBorderThickness(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
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
    ///     改变对象的 StrokeThickness 属性的动画。
    /// </summary>
    public static AniData AaStrokeThickness(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
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
    public static AniData AaGridLengthWidth(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
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

    /// <summary>
    ///     改变数字属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画目标的属性。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaDouble(
        object obj,
        AvaloniaProperty prop,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Double,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = new[]
            {
                obj,
                prop,
                ""
            },
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     通过回调改变数字值的动画。
    /// </summary>
    /// <param name="lambda">每一帧以增量值调用的回调。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaDouble(
        Action<double> lambda,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.DoubleParam,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = lambda,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     改变颜色属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画目标的属性。</param>
    /// <param name="value">颜色改变的值。以 RGB 加减法进行计算。不用担心超额。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaColor(
        Control obj,
        AvaloniaProperty prop,
        MyColor value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Color,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = new object[]
            {
                obj,
                prop,
                ""
            },
            value = value,
            isAfter = after,
            timeFinished = -delay,
            valueLast = new MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>
    ///     改变颜色属性为一个资源的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画目标的属性。</param>
    /// <param name="res">要将颜色改变为该资源值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaColor(
        Control obj,
        AvaloniaProperty prop,
        string res,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Color,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = new object[]
            {
                obj,
                prop,
                res
            },
            value = new MyColor(_FindResource(res)) - _ReadColorForCompat(obj, prop),
            isAfter = after,
            timeFinished = -delay,
            valueLast = new MyColor(0d, 0d, 0d, 0d)
        };
    }

    private static MyColor _ReadColorForCompat(Control obj, AvaloniaProperty prop)
    {
        return _ReadColor(obj, prop);
    }

    /// <summary>
    ///     缩放控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">大小改变的百分比（如 -0.6）或值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <param name="absolute">大小改变是否为绝对值。若为 true 则为绝对像素，若为 false 则为相对百分比。</param>
    public static AniData AaScale(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false,
        bool absolute = false)
    {
        MyRect rect;

        if (absolute)
        {
            rect = new MyRect(-0.5d * value, -0.5d * value, value, value);
        }
        else if (obj is Control control)
        {
            var width = _GetEffectiveWidth(control);
            var height = _GetEffectiveHeight(control);
            rect = new MyRect(
                -0.5d * width * value,
                -0.5d * height * value,
                width * value,
                height * value);
        }
        else
        {
            rect = new MyRect(-0.5d * value, -0.5d * value, value, value);
        }

        return new AniData
        {
            typeMain = AniType.Scale,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = rect,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     让一段文字一个个字出现或消失的动画。
    /// </summary>
    /// <param name="obj">动画的对象。应为 TextBlock 或带 Context 属性的控件。</param>
    /// <param name="hide">是否为一个个字隐藏。默认为 false（一个个字出现）。这些字必须已经存在了。</param>
    /// <param name="timePerText">是否采用根据文本长度决定时间的方式。</param>
    /// <param name="time">动画长度（毫秒）。若 timePerText 为 true，这代表每个字所占据的时间。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTextAppear(
        object obj,
        bool hide = false,
        bool timePerText = true,
        int time = 70,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        var text = _GetTextOf(obj);

        return new AniData
        {
            typeMain = AniType.TextAppear,
            ease = ease ?? new AniEaseLinear(),
            timeTotal = timePerText
                ? Math.Max(1, time * text.Length)
                : time,
            obj = obj,
            value = new object[]
            {
                text,
                hide
            },
            isAfter = after,
            timeFinished = -delay
        };
    }

    private static string _GetTextOf(object obj)
    {
        return obj switch
        {
            TextBlock textBlock => textBlock.Text ?? string.Empty,
            _ => (_GetMember(obj, "Context") ?? string.Empty).ToString() ?? string.Empty
        };
    }

    /// <summary>
    ///     执行代码。
    /// </summary>
    /// <param name="code">一个 Action。这将会在执行时在主线程调用。</param>
    /// <param name="delay">代码延迟执行的时间（毫秒）。</param>
    /// <param name="after">是否等到以前的动画完成后才执行。</param>
    public static AniData AaCode(Action code, int delay = 0, bool after = false)
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

    /// <summary>
    ///     按照变换方式缩放控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">大小改变的百分比（如 -0.6）。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaScaleTransform(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.ScaleTransform,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     按照变换方式旋转控件的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">旋转的角度。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaRotateTransform(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.RotateTransform,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    /// </summary>
    public static AniData AaTranslateX(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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
    public static AniData AaTranslateY(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
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

    /// <summary>
    ///     将一个 StackPanel 中的各个项目依次显示。
    /// </summary>
    public static List<AniData> AaStack(
        StackPanel stack,
        int time = 100,
        int delay = 25)
    {
        List<AniData> result = [];
        var aniDelay = 0;
        foreach (var item in stack.Children)
        {
            item.Opacity = 0;
            result.Add(AaOpacity(item, 1d, time, aniDelay));
            aniDelay += delay;
        }

        return result;
    }
}