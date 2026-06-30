using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xaml;
using System.Xml.Linq;
using PCL.Core.App.Localization;
using Size = System.Windows.Size;

namespace PCL;

public static partial class ModBase
{
    #region UI

    public static void SetLaunchFont(string fontName = null)
    {
        try
        {
            LocalizationFontService.ApplyLaunchFont(fontName, LocalizationService.CurrentLanguage);
        }
        catch (Exception ex)
        {
            Log(ex, "设置字体失败", LogLevel.Hint);
        }
    }

    // 边距改变
    /// <summary>
    ///     相对增减控件的左边距。
    /// </summary>
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            // 窗口改变
            window.Left += newValue;
        else
            // 根据 HorizontalAlignment 改变数值
            switch (control.HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                {
                    control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                        control.Margin.Right, control.Margin.Bottom);
                    break;
                }
                case HorizontalAlignment.Right:
                {
                    // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                    control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                        control.Margin.Right - newValue, control.Margin.Bottom);
                    break;
                }

                default:
                {
                    DebugAssert(false);
                    break;
                }
            }
    }

    /// <summary>
    ///     设置控件的左边距。（仅针对置左控件）
    /// </summary>
    public static void SetLeft(FrameworkElement control, double newValue)
    {
        DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }

    /// <summary>
    ///     相对增减控件的上边距。
    /// </summary>
    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            // 窗口改变
            window.Top += newValue;
        else
            // 根据 VerticalAlignment 改变数值
            switch (control.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                {
                    control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                        control.Margin.Right, control.Margin.Bottom);
                    break;
                }
                case VerticalAlignment.Bottom:
                {
                    // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                    control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                        control.Margin.Bottom - newValue);
                    break;
                }

                default:
                {
                    DebugAssert(false);
                    break;
                }
            }
    }

    /// <summary>
    ///     设置控件的顶边距。（仅针对置上控件）
    /// </summary>
    public static void SetTop(FrameworkElement control, double newValue)
    {
        DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
    }

    // DPI 转换
    public static readonly int dpi = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

    /// <summary>
    ///     将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    /// </summary>
    public static double GetPixelSize(double wPFSize)
    {
        return wPFSize / 96d * dpi;
    }

    /// <summary>
    ///     将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    /// </summary>
    public static double GetWPFSize(double pixelSize)
    {
        return pixelSize * 96d / dpi;
    }

    // UI 截图
    /// <summary>
    ///     将某个控件的呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement uI)
    {
        var width = uI.ActualWidth;
        var height = uI.ActualHeight;
        if (width < 1d || height < 1d)
            return new ImageBrush();
        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            dpi, dpi, PixelFormats.Pbgra32);
        bmp.Render(uI);
        return new ImageBrush(bmp);
    }

    /// <summary>
    ///     将某个控件的模拟呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement uI, double width, double height, double left = 0d,
        double top = 0d)
    {
        uI.Measure(new Size(width, height));
        uI.Arrange(new Rect(0d, 0d, width, height));
        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            dpi, dpi, PixelFormats.Default);
        bmp.Render(uI);
        if (left != 0d || top != 0d)
            uI.Arrange(new Rect(left, top, width, height));
        return new ImageBrush(bmp);
    }

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Panel uI)
    {
        uI.Background = ControlBrush(uI);
        uI.Children.Clear();
    }

    /// <summary>
    ///     将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Border uI)
    {
        uI.Background = ControlBrush(uI);
        uI.Child = null;
    }

    /// <summary>
    ///     将 XElement 转换为对应 UI 对象（不返回 XAML 清理结果）。
    /// </summary>
    public static object GetObjectFromXML(XElement str)
    {
        return GetObjectFromXML(str.ToString());
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXML(string str)
    {
        return GetObjectFromXML(str, out _);
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象，并输出 XAML 清理结果。
    /// </summary>
    public static object GetObjectFromXML(string str, out XamlEventSanitizer.SanitizeResult sanitizeResult)
    {
        str = str. // 兼容旧版自定义事件写法
            Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        // 修复因上述替换导致重复前缀的情况：local:CustomEventService.local:CustomEventService.EventType
        str = str.Replace("local:CustomEventService.local:CustomEventService.", "local:CustomEventService.");

        sanitizeResult = XamlEventSanitizer.Sanitize(str);
        str = sanitizeResult.SanitizedXaml;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(str));
        // 类型检查
        using (var reader = new XamlXmlReader(stream))
        {
            while (reader.Read())
            {
                foreach (var blackListType in new[]
                         {
                             typeof(WebBrowser), typeof(Frame), typeof(MediaElement), typeof(ObjectDataProvider),
                             typeof(XamlReader), typeof(Window), typeof(XmlDataProvider)
                         })
                {
                    if (reader.Type is not null && blackListType.IsAssignableFrom(reader.Type.UnderlyingType))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 类型。");
                    if (reader.Value is not null && Equals(reader.Value, blackListType.Name))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 值。");
                }

                foreach (var blackListMember in new[] { "Code", "FactoryMethod", "Static" })
                    if (reader.Member is not null && (reader.Member.Name ?? "") == (blackListMember ?? ""))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListMember} 成员。");
            }
        }

        // 实际的加载
        stream.Position = 0L;
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(str);
            writer.Flush();
            stream.Position = 0L;
            return System.Windows.Markup.XamlReader.Load(stream);
        }
    }

    private static readonly int uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    ///     当前线程是否为主线程。
    /// </summary>
    public static bool RunInUi()
    {
        return Environment.CurrentManagedThreadId == uiThreadId;
    }

    #endregion
}