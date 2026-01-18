using System.Windows;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class PointValueProcessor : IValueProcessor<Point>
{
    public Point Filter(Point value) => value;

    public Point Add(Point value1, Point value2) => new(value1.X + value2.X, value1.Y + value2.Y);

    public Point Subtract(Point value1, Point value2) => new(value1.X - value2.X, value1.Y - value2.Y);

    public Point Scale(Point value, double factor) =>  new(value.X * factor, value.Y * factor);
    
    public Point DefaultValue() => new();
    
    public bool Equal(Point value1, Point value2) => value1 == value2;
}