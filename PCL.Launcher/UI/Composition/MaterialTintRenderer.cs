using Avalonia;
using Avalonia.Media;

namespace PCL.UI.Composition;

internal static class MaterialTintRenderer
{
    public static void Render(DrawingContext context, Rect bounds, CornerRadius radius, MaterialBase? material)
    {
        switch (material)
        {
            case BlurMaterial blur:
                DrawTint(context, bounds, radius, WithOpacity(blur.Tint, blur.TintOpacity));
                if (blur.Noise > 0)
                    DrawNoise(context, bounds, radius, blur.Noise);
                break;
            case MicaMaterial mica when mica.TintOpacity > 0:
                DrawTint(context, bounds, radius, WithOpacity(mica.Tint, mica.TintOpacity));
                break;
        }
    }

    private static void DrawTint(DrawingContext context, Rect bounds, CornerRadius radius, Color color)
    {
        context.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(bounds, radius));
    }

    private static void DrawNoise(DrawingContext context, Rect bounds, CornerRadius radius, double opacity)
    {
        if (opacity <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var clip = context.PushClip(new RoundedRect(bounds, radius));
        var state = 0x9E3779B9u;
        var count = Math.Clamp((int)(bounds.Width * bounds.Height / 900), 24, 180);
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), 255, 255, 255));
        for (var index = 0; index < count; index++)
        {
            state = state * 1664525u + 1013904223u;
            var x = bounds.X + state / (double)uint.MaxValue * bounds.Width;
            state = state * 1664525u + 1013904223u;
            var y = bounds.Y + state / (double)uint.MaxValue * bounds.Height;
            context.DrawRectangle(brush, null, new Rect(x, y, 0.55, 0.55));
        }
    }

    private static Color WithOpacity(Color color, double opacity)
        => Color.FromArgb((byte)Math.Clamp(Math.Round(color.A * opacity), 0, 255), color.R, color.G, color.B);
}
