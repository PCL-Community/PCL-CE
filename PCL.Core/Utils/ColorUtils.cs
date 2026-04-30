using System;
using System.Windows.Media;

namespace PCL.Core.Utils;

public static class ColorUtils
{
    extension(SolidColorBrush val1)
    {
        public SolidColorBrush Add(SolidColorBrush val2)
        {
            var color1 = val1.Color;
            var color2 = val2.Color;
            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Min(255, color1.A + color2.A),
                (byte)Math.Min(255, color1.R + color2.R),
                (byte)Math.Min(255, color1.G + color2.G),
                (byte)Math.Min(255, color1.B + color2.B)
            ));
        }

        public SolidColorBrush Subtract(SolidColorBrush val2)
        {
            var color1 = val1.Color;
            var color2 = val2.Color;
            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Max(0, color1.A - color2.A),
                (byte)Math.Max(0, color1.R - color2.R),
                (byte)Math.Max(0, color1.G - color2.G),
                (byte)Math.Max(0, color1.B - color2.B)
            ));
        }

        public SolidColorBrush Multiply(double factor)
        {
            var color = val1.Color;
            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Min(255, color.A * factor),
                (byte)Math.Min(255, color.R * factor),
                (byte)Math.Min(255, color.G * factor),
                (byte)Math.Min(255, color.B * factor)
            ));
        }

        public SolidColorBrush Divide(double factor)
        {
            var color = val1.Color;
            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Min(255, color.A / factor),
                (byte)Math.Min(255, color.R / factor),
                (byte)Math.Min(255, color.G / factor),
                (byte)Math.Min(255, color.B / factor)
            ));
        }
    }
}