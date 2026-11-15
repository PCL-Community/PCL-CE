using System.Windows.Media;

namespace PCL.Core.UI.Theme;

/// <summary>WPF 颜色转换扩展。</summary>
public static class LabColorWpfExtensions
{
    public static Color ToWpfColor(this LabColor lab)
    {
        var (a, r, g, b) = lab.ScRgb;
        return Color.FromScRgb(a, r, g, b);
    }
}