using System;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class DoubleValueProcessor : IValueProcessor<double>
{
    public double Filter(double value)
    {
        return value;
    }

    public double Add(double value1, double value2)
    {
        return value1 + value2;
    }

    public double Subtract(double value1, double value2)
    {
        return value1 - value2;
    }

    public double Scale(double value, double factor)
    {
        return value * factor;
    }

    public double DefaultValue()
    {
        return 0;
    }

    public bool Equal(double value1, double value2)
    {
        return Math.Abs(value1 - value2) < 1e-6;
    }
}