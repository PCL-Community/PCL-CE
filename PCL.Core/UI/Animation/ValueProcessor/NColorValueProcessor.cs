namespace PCL.Core.UI.Animation.ValueProcessor;

public class NColorValueProcessor : IValueProcessor<NColor>
{
    public NColor Filter(NColor value)
    {
        if (value.A < 0) value.A = 0;
        if (value.R < 0) value.R = 0;
        if (value.G < 0) value.G = 0;
        if (value.B < 0) value.B = 0;
        
        return value;
    }

    public NColor Add(NColor value1, NColor value2) => value1 + value2;

    public NColor Subtract(NColor value1, NColor value2) => value1 - value2;
    public NColor Scale(NColor value, double factor) => value * (float)factor;
}