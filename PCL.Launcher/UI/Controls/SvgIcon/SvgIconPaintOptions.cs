using Avalonia.Media;

namespace PCL.UI.Controls.SvgIcon;

internal readonly record struct SvgIconPaintOptions(
    IBrush IconBrush,
    double StrokeThickness,
    bool UseOriginalColor);