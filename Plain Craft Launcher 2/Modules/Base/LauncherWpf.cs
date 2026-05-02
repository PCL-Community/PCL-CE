using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xaml;
using System.Xml.Linq;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

/// <summary>
/// Owns WPF-only DPI, XAML loading, resource stream, and UI screenshot helpers.
/// </summary>
public static class LauncherWpf
{
    public static readonly int DPI = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

    public static double GetPixelSize(double wpfSize)
    {
        return wpfSize / 96d * DPI;
    }

    public static double GetWPFSize(double pixelSize)
    {
        return pixelSize * 96d / DPI;
    }

    public static object GetObjectFromXML(XElement element)
    {
        return GetObjectFromXML(element.ToString());
    }

    public static object GetObjectFromXML(string xaml)
    {
        xaml = xaml.Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        using (var reader = new XamlXmlReader(stream))
        {
            while (reader.Read())
            {
                foreach (var blackListType in new[]
                         {
                             typeof(WebBrowser), typeof(Frame), typeof(MediaElement), typeof(ObjectDataProvider),
                             typeof(System.Windows.Markup.XamlReader), typeof(Window), typeof(XmlDataProvider)
                         })
                {
                    if (reader.Type is not null && blackListType.IsAssignableFrom(reader.Type.UnderlyingType))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 类型。");
                    if (reader.Value is not null && Conversions.ToBoolean(
                            Operators.ConditionalCompareObjectEqual(reader.Value, blackListType.Name, false)))
                        throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 值。");
                }

                foreach (var blackListMember in new[] { "Code", "FactoryMethod", "Static" })
                    if (reader.Member is not null && (reader.Member.Name ?? string.Empty) == blackListMember)
                        throw new UnauthorizedAccessException($"不允许使用 {blackListMember} 成员。");
            }
        }

        stream.Position = 0L;
        using var writer = new StreamWriter(stream);
        writer.Write(xaml);
        writer.Flush();
        stream.Position = 0L;
        return System.Windows.Markup.XamlReader.Load(stream);
    }

    public static ImageBrush ControlBrush(FrameworkElement ui)
    {
        var width = ui.ActualWidth;
        var height = ui.ActualHeight;
        if (width < 1d || height < 1d)
            return new ImageBrush();

        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            DPI, DPI, PixelFormats.Pbgra32);
        bmp.Render(ui);
        return new ImageBrush(bmp);
    }

    public static ImageBrush ControlBrush(FrameworkElement ui, double width, double height, double left = 0d,
        double top = 0d)
    {
        ui.Measure(new System.Windows.Size(width, height));
        ui.Arrange(new Rect(0d, 0d, width, height));
        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            DPI, DPI, PixelFormats.Default);
        bmp.Render(ui);
        if (!(left == 0d && top == 0d))
            ui.Arrange(new Rect(left, top, width, height));
        return new ImageBrush(bmp);
    }
}
