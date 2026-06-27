using System.Collections.Generic;

namespace PCL.Desktop.Controls.Legacy;

internal sealed class SvgIconModel
{
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double Width { get; init; } = 24D;
    public double Height { get; init; } = 24D;
    public IReadOnlyList<SvgIconElement> Elements { get; init; } = [];
}