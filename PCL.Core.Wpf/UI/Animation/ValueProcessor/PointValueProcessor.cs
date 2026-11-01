using System.Windows;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class PointValueProcessor : IValueProcessor<Point>
{
    public Point Filter(Point value)
    {
        return value;
    }

    public Point Add(Point value1, Point value2)
    {
        return new Point(value1.X + value2.X, value1.Y + value2.Y);
    }

    public Point Subtract(Point value1, Point value2)
    {
        return new Point(value1.X - value2.X, value1.Y - value2.Y);
    }

    public Point Scale(Point value, double factor)
    {
        return new Point(value.X * factor, value.Y * factor);
    }

    public Point DefaultValue()
    {
        return new Point();
    }

    public bool Equal(Point value1, Point value2)
    {
        return value1 == value2;
    }
}