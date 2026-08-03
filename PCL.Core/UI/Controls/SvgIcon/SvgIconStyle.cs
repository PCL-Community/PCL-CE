using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace PCL.Core.UI.Controls.SvgIcon;

internal sealed record SvgIconStyle
{
    public string? Fill { get; init; }
    public string? Stroke { get; init; }
    public string? StrokeLineCap { get; init; }
    public string? StrokeLineJoin { get; init; }
    public string? FillRule { get; init; }
    public double? StrokeWidth { get; init; }
    public double Opacity { get; init; } = 1D;
    public double FillOpacity { get; init; } = 1D;
    public double StrokeOpacity { get; init; } = 1D;

    public SvgIconStyle Merge(XElement element)
    {
        var inlineStyle = _ParseStyle(element.Attribute("style")?.Value);

        var opacity = _GetAttributeOrStyle(element, inlineStyle, "opacity");
        var fillOpacity = _GetAttributeOrStyle(element, inlineStyle, "fill-opacity");
        var strokeOpacity = _GetAttributeOrStyle(element, inlineStyle, "stroke-opacity");

        return new SvgIconStyle
        {
            Fill = _GetAttributeOrStyle(element, inlineStyle, "fill") ?? Fill,
            Stroke = _GetAttributeOrStyle(element, inlineStyle, "stroke") ?? Stroke,
            StrokeLineCap = _GetAttributeOrStyle(element, inlineStyle, "stroke-linecap") ?? StrokeLineCap,
            StrokeLineJoin = _GetAttributeOrStyle(element, inlineStyle, "stroke-linejoin") ?? StrokeLineJoin,
            FillRule = _GetAttributeOrStyle(element, inlineStyle, "fill-rule") ?? FillRule,
            StrokeWidth = SvgNumberParser.TryParseNullable(
                _GetAttributeOrStyle(element, inlineStyle, "stroke-width")) ?? StrokeWidth,
            Opacity = Opacity * SvgNumberParser.TryParse(opacity, 1D),
            FillOpacity = SvgNumberParser.TryParseNullable(fillOpacity) ?? FillOpacity,
            StrokeOpacity = SvgNumberParser.TryParseNullable(strokeOpacity) ?? StrokeOpacity
        };
    }

    private static string? _GetAttributeOrStyle(
        XElement element,
        IReadOnlyDictionary<string, string> style,
        string name)
    {
        var directValue = element.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(directValue))
            return directValue.Trim();

        return style.TryGetValue(name, out var styleValue) && !string.IsNullOrWhiteSpace(styleValue)
            ? styleValue.Trim()
            : null;
    }

    private static Dictionary<string, string> _ParseStyle(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pair is [{ Length: > 0 }, _])
                result[pair[0]] = pair[1];
        }

        return result;
    }
}