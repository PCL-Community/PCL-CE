using System;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class DoubleValueProcessor : IValueProcessor<double>
{
    public double Filter(double value) => value;
    
    public double Add(double value1, double value2) => value1 + value2;
    
    public double Subtract(double value1, double value2) => value1 - value2;
    
    public double Scale(double value, double factor) => value * factor;
    
    public double DefaultValue() => 0;
    
    public bool Equal(double value1, double value2) => Math.Abs(value1 - value2) < 1e-6;
}