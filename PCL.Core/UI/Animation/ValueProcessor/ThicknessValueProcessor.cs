using System.Windows;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class ThicknessValueProcessor : IValueProcessor<Thickness>
{
    public Thickness Filter(Thickness value) => value;

    public Thickness Add(Thickness value1, Thickness value2)
    {
        return new Thickness(value1.Left + value2.Left,
            value1.Top + value2.Top,
            value1.Right + value2.Right,
            value1.Bottom + value2.Bottom);
    }

    public Thickness Subtract(Thickness value1, Thickness value2)
    {
        return new Thickness(value1.Left - value2.Left,
            value1.Top - value2.Top,
            value1.Right - value2.Right,
            value1.Bottom - value2.Bottom);
    }

    public Thickness Scale(Thickness value, double factor)
    {
        return new Thickness(value.Left * factor,
            value.Top * factor, 
            value.Right * factor,
            value.Bottom * factor);
    }
    
    public Thickness DefaultValue() => new();
    
    public bool Equal(Thickness value1, Thickness value2) => value1 == value2;
}