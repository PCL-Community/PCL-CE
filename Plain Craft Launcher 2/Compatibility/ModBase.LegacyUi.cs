using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace PCL;

public static partial class ModBase
{
    #region UI

    public static void SetLaunchFont(string fontName = null)
    {
        LauncherFontService.SetLaunchFont(fontName);
    }

    // 边距改变
    /// <summary>
    ///     相对增减控件的左边距。
    /// </summary>
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        LayoutExtensions.DeltaLeft(control, newValue);
    }

    /// <summary>
    ///     设置控件的左边距。（仅针对置左控件）
    /// </summary>
    public static void SetLeft(FrameworkElement control, double newValue)
    {
        LayoutExtensions.SetLeft(control, newValue);
    }

    /// <summary>
    ///     相对增减控件的上边距。
    /// </summary>
    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        LayoutExtensions.DeltaTop(control, newValue);
    }

    /// <summary>
    ///     设置控件的顶边距。（仅针对置上控件）
    /// </summary>
    public static void SetTop(FrameworkElement control, double newValue)
    {
        LayoutExtensions.SetTop(control, newValue);
    }

    // DPI 转换
    public static int dpi => DpiUtils.Dpi;

    /// <summary>
    ///     将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    /// </summary>
    public static double GetPixelSize(double wPFSize)
    {
        return DpiUtils.GetPixelSize(wPFSize);
    }

    /// <summary>
    ///     将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    /// </summary>
    public static double GetWPFSize(double pixelSize)
    {
        return DpiUtils.GetWpfSize(pixelSize);
    }

    // UI 截图
    /// <summary>
    ///     将某个控件的呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement uI)
    {
        return VisualCapture.ControlBrush(uI);
    }

    /// <summary>
    ///     将某个控件的模拟呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement uI, double width, double height, double left = 0d,
        double top = 0d)
    {
        return VisualCapture.ControlBrush(uI, width, height, left, top);
    }

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Panel uI)
    {
        VisualCapture.ControlFreeze(uI);
    }

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Border uI)
    {
        VisualCapture.ControlFreeze(uI);
    }

    /// <summary>
    ///     将 XElement 转换为对应 UI 对象（不返回 XAML 清理结果）。
    /// </summary>
    public static object GetObjectFromXML(XElement str)
    {
        return CustomXamlLoader.Load(str);
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXML(string str)
    {
        return CustomXamlLoader.Load(str);
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象，并输出 XAML 清理结果。
    /// </summary>
    public static object GetObjectFromXML(string str, out XamlEventSanitizer.SanitizeResult sanitizeResult)
    {
        return CustomXamlLoader.Load(str, out sanitizeResult);
    }

    /// <summary>
    ///     当前线程是否为主线程。
    /// </summary>
    public static bool RunInUi()
    {
        return UiThread.CheckAccess();
    }

    #endregion
}