using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private static string StripXmlDeclaration(string xaml)
    {
        var trimmed = xaml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
        return end < 0 ? xaml : trimmed[(end + 2)..];
    }

    private static string? FindAttribute(XElement element, string name)
    {
        return element
            .Attributes()
            .FirstOrDefault(attribute =>
                !attribute.IsNamespaceDeclaration
                && attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? GetProp(
        XElement element,
        IReadOnlyDictionary<string, string> styles,
        string name)
    {
        var attribute = FindAttribute(element, name);
        return attribute ?? styles.GetValueOrDefault(name);
    }

    private static bool? ParseBool(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : raw.Trim().ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => null
            };
    }

    private static int? ParseInt(string? raw)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ParseDouble(string? raw)
    {
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static TEnum? ParseEnum<TEnum>(string? raw) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(raw, true, out var value) ? value : null;
    }

    private static TextTrimming? ParseTextTrimming(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "characterellipsis" or "character_ellipsis" => TextTrimming.CharacterEllipsis,
            "wordellipsis" or "word_ellipsis" => TextTrimming.WordEllipsis,
            "none" => TextTrimming.None,
            _ => null
        };
    }

    private static FontWeight ParseFontWeight(string raw)
    {
        return raw.Trim() switch
        {
            "Bold" => FontWeight.Bold,
            "SemiBold" or "DemiBold" => FontWeight.SemiBold,
            "Light" => FontWeight.Light,
            "ExtraLight" => FontWeight.ExtraLight,
            "Thin" => FontWeight.Thin,
            "Medium" => FontWeight.Medium,
            "ExtraBold" => FontWeight.ExtraBold,
            "Black" => FontWeight.Black,
            _ => FontWeight.Normal
        };
    }

    private static FontStyle ParseFontStyle(string raw)
    {
        return raw.Trim() switch
        {
            "Italic" => FontStyle.Italic,
            "Oblique" => FontStyle.Oblique,
            _ => FontStyle.Normal
        };
    }

    private static HorizontalAlignment? ParseHorizontalAlignment(string? raw)
    {
        return raw?.Trim() switch
        {
            "Left" => HorizontalAlignment.Left,
            "Center" => HorizontalAlignment.Center,
            "Right" => HorizontalAlignment.Right,
            "Stretch" => HorizontalAlignment.Stretch,
            _ => null
        };
    }

    private static VerticalAlignment? ParseVerticalAlignment(string? raw)
    {
        return raw?.Trim() switch
        {
            "Top" => VerticalAlignment.Top,
            "Center" => VerticalAlignment.Center,
            "Bottom" => VerticalAlignment.Bottom,
            "Stretch" => VerticalAlignment.Stretch,
            _ => null
        };
    }

    private static Thickness? ParseThickness(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var uniform = ParseDouble(parts[0]);
            return uniform is null ? null : new Thickness(uniform.Value);
        }

        if (parts.Length != 4)
            return null;
        if (ParseDouble(parts[0]) is not { } left
            || ParseDouble(parts[1]) is not { } top
            || ParseDouble(parts[2]) is not { } right
            || ParseDouble(parts[3]) is not { } bottom)
            return null;
        return new Thickness(left, top, right, bottom);
    }

    private static CornerRadius? ParseCornerRadius(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 when ParseDouble(parts[0]) is { } uniform =>
                new CornerRadius(uniform),

            4 when ParseDouble(parts[0]) is { } topLeft
                && ParseDouble(parts[1]) is { } topRight
                && ParseDouble(parts[2]) is { } bottomRight
                && ParseDouble(parts[3]) is { } bottomLeft =>
                new CornerRadius(
                    topLeft,
                    topRight,
                    bottomRight,
                    bottomLeft),

            _ => null
        };
    }

    private static List<GridLength>? ParseGridDefinitions(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? null
            :
            [
                .. raw
                    .Split(',', StringSplitOptions.TrimEntries)
                    .Where(part => part.Length > 0)
                    .Select(ParseGridLength)
            ];
    }

    private static GridLength ParseGridLength(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return GridLength.Auto;

        if (!raw.EndsWith('*'))
            return ParseDouble(raw) is { } pixels
                ? new GridLength(pixels)
                : GridLength.Auto;

        var star = raw[..^1];
        return double.TryParse(
            star,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var factor)
            ? new GridLength(factor, GridUnitType.Star)
            : new GridLength(1d, GridUnitType.Star);
    }
}