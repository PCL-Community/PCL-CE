using System.Windows;
using System.Windows.Controls;

namespace PCL;

public static partial class Animations
{
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">宽度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">高度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">透明度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
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
    ///     改变对象的StrokeThickness属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">StrokeThickness属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">GridLength.Value 改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
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
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(
        object obj,
        DependencyProperty prop,
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
    ///     获取数字动画值。
    /// </summary>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaDouble(
        ParameterizedThreadStart lambda,
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
    /// <param name="Obj">动画的对象。</param>
    /// <param name="Prop">动画的依赖属性。</param>
    /// <param name="value">颜色改变的值。以RGB加减法进行计算。不用担心超额。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static AniData AaColor(
        FrameworkElement obj,
        DependencyProperty prop,
        Common.MyColor value,
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
            valueLast = new Common.MyColor(0d, 0d, 0d, 0d)
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
    public static AniData AaColor(
        FrameworkElement obj,
        DependencyProperty prop,
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
            value = new Common.MyColor(System.Windows.Application.Current.FindResource(res))
                - new Common.MyColor(obj.GetValue(prop)),
            isAfter = after,
            timeFinished = -delay,
            valueLast = new Common.MyColor(0d, 0d, 0d, 0d)
        };
    }

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
    public static AniData AaScale(
        object obj,
        double value,
        int time = 400,
        int delay = 0,
        AniEase? ease = null,
        bool after = false,
        bool absolute = false)
    {
        return new AniData
        {
            typeMain = AniType.Scale,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = absolute
                ? new Common.MyRect(
                    -0.5d * value,
                    -0.5d * value,
                    value,
                    value)
                : new Common.MyRect(
                    Convert.ToDouble(-0.5d * ((dynamic)obj).ActualWidth * value),
                    Convert.ToDouble(-0.5d * ((dynamic)obj).ActualHeight * value),
                    Convert.ToDouble(((dynamic)obj).ActualWidth * value),
                    Convert.ToDouble(((dynamic)obj).ActualHeight * value)),
            isAfter = after,
            timeFinished = -delay
        };
    }

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
    public static AniData AaTextAppear(
        object obj,
        bool hide = false,
        bool timePerText = true,
        int time = 70,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.TextAppear,
            ease = ease ?? new AniEaseLinear(),
            timeTotal = timePerText
                ? time
                * (
                    obj is TextBlock
                        ? ((dynamic)obj).Text
                        : ((dynamic)obj).Context.ToString())
                .ToString()
                .Length
                : time,
            obj = obj,
            value = new[]
            {
                obj is TextBlock
                    ? ((dynamic)obj).Text
                    : ((dynamic)obj).Context.ToString(),
                hide
            },
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    ///     执行代码。
    /// </summary>
    /// <param name="code">一个ThreadStart。这将会在执行时在主线程调用。</param>
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
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
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
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
    ///     将一个StackPanel中的各个项目依次显示。
    /// </summary>
    /// <remarks></remarks>
    public static List<AniData> AaStack(
        StackPanel stack,
        int time = 100,
        int delay = 25)
    {
        List<AniData> aaStackRet = [];
        var aniDelay = 0;
        foreach (var item in stack.Children)
        {
            ((dynamic)item).Opacity = 0;
            aaStackRet.Add(AaOpacity(item, 1d, time, aniDelay));
            aniDelay += delay;
        }

        return aaStackRet;
    }
}