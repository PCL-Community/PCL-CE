using System.Windows.Media;

namespace PCL.Core.UI.Controls.SvgIcon;

internal readonly record struct SvgIconPaintOptions(
    Brush IconBrush,
    double StrokeThickness,
    bool UseOriginalColor);