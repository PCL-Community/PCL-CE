using Avalonia.Media;
using PCL.Core.UI.Animation;
using PCL.Core.UI.Theme;

namespace PCL.Core.UI.Theming;

public static class OklabColorExtensions
{
    public static Color ToAvaloniaColor(this OklabColor color)
    {
        var (alpha, red, green, blue) = color.ScRgb;
        return NColor.FromScRgb(alpha, red, green, blue).ToColor();
    }

    public static Color ToAvaloniaColor(this CatColorResource color)
    {
        return NColor
            .FromScRgb(color.Alpha, color.Red, color.Green, color.Blue)
            .ToColor();
    }
}