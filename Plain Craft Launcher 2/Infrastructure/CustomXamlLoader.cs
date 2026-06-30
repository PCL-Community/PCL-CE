using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xaml;
using System.Xml.Linq;
using XamlReader = System.Windows.Markup.XamlReader;

namespace PCL;

/// <summary>
///     自定义 XAML 安全加载器。
/// </summary>
public static class CustomXamlLoader
{
    public static object Load(XElement element)
    {
        return Load(element.ToString());
    }

    public static object Load(string xaml)
    {
        return Load(xaml, out _);
    }

    public static object Load(string xaml, out XamlEventSanitizer.SanitizeResult sanitizeResult)
    {
        xaml = NormalizeLegacyCustomEventSyntax(xaml);

        sanitizeResult = XamlEventSanitizer.Sanitize(xaml);
        xaml = sanitizeResult.SanitizedXaml;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        ValidateXaml(stream);

        stream.Position = 0L;
        using var writer = new StreamWriter(stream);
        writer.Write(xaml);
        writer.Flush();
        stream.Position = 0L;
        return XamlReader.Load(stream);
    }

    private static string NormalizeLegacyCustomEventSyntax(string xaml)
    {
        xaml = xaml
            .Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        return xaml.Replace("local:CustomEventService.local:CustomEventService.", "local:CustomEventService.");
    }

    private static void ValidateXaml(Stream stream)
    {
        using var reader = new XamlXmlReader(stream);
        while (reader.Read())
        {
            foreach (var blackListType in new[]
                     {
                         typeof(WebBrowser), typeof(Frame), typeof(MediaElement), typeof(ObjectDataProvider),
                         typeof(System.Xaml.XamlReader), typeof(Window), typeof(XmlDataProvider)
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
}