namespace PCL.UI.Controls.Common;

/// <summary>
///     支持负数与浮点数的矩形。
/// </summary>
public class MyRect(
    double left,
    double top,
    double width,
    double height)
{
    public double Width { get; set; } = width;
    public double Height { get; set; } = height;
    public double Left { get; set; } = left;
    public double Top { get; set; } = top;
}