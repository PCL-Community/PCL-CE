using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xaml;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class Common
{
    private static int _dpiFallback = _GetSystemDpi();

    /// <summary>
    /// 主窗口所在显示器的 DPI。<br/>
    /// 进程在 app.manifest 中声明为 PerMonitorV2，因此不能像原先那样把系统 DPI 缓存进
    /// 一个 <c>readonly</c> 字段：双屏不同缩放、或拖动窗口到另一块屏幕后，缓存值必然失真，
    /// 并连带影响窗口尺寸换算（<see cref="ArgumentsGame"/>）与提示文本渲染。
    /// </summary>
    public static int Dpi
    {
        get
        {
            var dpi = _GetMainWindowDpi();
            if (dpi > 0) _dpiFallback = dpi;
            return _dpiFallback;
        }
    }

    /// <summary>
    /// 读取主窗口当前所在显示器的 DPI，窗口尚未创建时返回 0。
    /// </summary>
    private static int _GetMainWindowDpi()
    {
        try
        {
            var window = Application.Current?.MainWindow;
            if (window is null) return 0;

            return (int)Math.Round(VisualTreeHelper.GetDpi(window).PixelsPerInchX);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// 系统 DPI，仅在主窗口尚未创建时用作回退值。
    /// </summary>
    private static int _GetSystemDpi()
    {
        try
        {
            return (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);
        }
        catch (Exception)
        {
            return 96;
        }
    }

    private static readonly int UiThreadId = Environment.CurrentManagedThreadId;

    public static void SetLaunchFont(string? fontName = null)
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

    /// <summary>
    ///     相对增减控件的左边距。
    /// </summary>
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            window.Left += newValue;
        else
            switch (control.HorizontalAlignment)
            {
                case HorizontalAlignment.Left or HorizontalAlignment.Stretch:
                {
                    control.Margin = new Thickness(
                        control.Margin.Left + newValue,
                        control.Margin.Top,
                        control.Margin.Right,
                        control.Margin.Bottom);
                    break;
                }
                case HorizontalAlignment.Right:
                {
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top,
                        control.Margin.Right - newValue,
                        control.Margin.Bottom);
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
        control.Margin = new Thickness(
            newValue,
            control.Margin.Top,
            control.Margin.Right,
            control.Margin.Bottom);
    }

    /// <summary>
    ///     相对增减控件的上边距。
    /// </summary>
    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        DebugAssert(!double.IsNaN(newValue));
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window window)
            window.Top += newValue;
        else
            switch (control.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                {
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top + newValue,
                        control.Margin.Right,
                        control.Margin.Bottom);
                    break;
                }

                case VerticalAlignment.Bottom:
                {
                    control.Margin = new Thickness(
                        control.Margin.Left,
                        control.Margin.Top,
                        control.Margin.Right,
                        control.Margin.Bottom - newValue);
                    break;
                }

                case VerticalAlignment.Center:
                case VerticalAlignment.Stretch:
                default:
                {
                    DebugAssert(false);
                    break;
                }
            }
    }

    /// <summary>
    ///     将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    /// </summary>
    public static double GetPixelSize(double wpfSize)
    {
        return wpfSize / 96d * Dpi;
    }

    /// <summary>
    ///     将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    /// </summary>
    public static double GetWpfSize(double pixelSize)
    {
        return pixelSize * 96d / Dpi;
    }

    /// <summary>
    ///     将某个控件的呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement uI)
    {
        var width = uI.ActualWidth;
        var height = uI.ActualHeight;
        if (width < 1d || height < 1d)
            return new ImageBrush();
        var bmp = new RenderTargetBitmap(
            (int)Math.Round(GetPixelSize(width)),
            (int)Math.Round(GetPixelSize(height)),
            Dpi,
            Dpi,
            PixelFormats.Pbgra32);
        bmp.Render(uI);
        return new ImageBrush(bmp);
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXml(string str)
    {
        return GetObjectFromXml(str, out _);
    }

    /// <summary>
    ///     将 XML 转换为对应 UI 对象，并输出 XAML 清理结果。
    /// </summary>
    public static object GetObjectFromXml(
        string str,
        out XamlEventSanitizer.SanitizeResult sanitizeResult)
    {
        str = str
            .Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        str = str.Replace(
            "local:CustomEventService.local:CustomEventService.",
            "local:CustomEventService.");

        sanitizeResult = XamlEventSanitizer.Sanitize(str);
        str = sanitizeResult.SanitizedXaml;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(str));
        using (var reader = new XamlXmlReader(stream))
        {
            while (reader.Read())
            {
                foreach (var blackListType in new[]
                         {
                             typeof(WebBrowser),
                             typeof(Frame),
                             typeof(MediaElement),
                             typeof(ObjectDataProvider),
                             typeof(XamlReader),
                             typeof(Window),
                             typeof(XmlDataProvider)
                         })
                {
                    if (reader.Type is not null
                        && blackListType.IsAssignableFrom(reader.Type.UnderlyingType))
                        throw new UnauthorizedAccessException(
                            $"不允许使用 {blackListType.Name} 类型。");
                    if (reader.Value is not null && Equals(reader.Value, blackListType.Name))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 值。");
                }

                foreach (var blackListMember in new[]
                         {
                             "Code",
                             "FactoryMethod",
                             "Static"
                         })
                    if (reader.Member is not null
                        && (reader.Member.Name ?? "") == (blackListMember ?? ""))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListMember} 成员。");
            }
        }

        stream.Position = 0L;
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(str);
            writer.Flush();
            stream.Position = 0L;
            return System.Windows.Markup.XamlReader.Load(stream);
        }
    }

    /// <summary>
    ///     当前线程是否为主线程。
    /// </summary>
    public static bool RunInUi()
    {
        return Environment.CurrentManagedThreadId == UiThreadId;
    }
}
