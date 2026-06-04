using System.Windows.Media;

namespace PCL.Core.UI.Icons.Svg;

internal readonly record struct SvgIconPaintOptions(
    Brush IconBrush,
    double StrokeThickness,
    bool UseOriginalColor);