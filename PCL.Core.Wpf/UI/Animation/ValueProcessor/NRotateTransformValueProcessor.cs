namespace PCL.Core.UI.Animation.ValueProcessor;

public class NRotateTransformValueProcessor : IValueProcessor<NRotateTransform>
{
    public NRotateTransform Filter(NRotateTransform value)
    {
        return value;
    }

    public NRotateTransform Add(NRotateTransform value1, NRotateTransform value2)
    {
        return value1 + value2;
    }

    public NRotateTransform Subtract(NRotateTransform value1, NRotateTransform value2)
    {
        return value1 - value2;
    }

    public NRotateTransform Scale(NRotateTransform value, double factor)
    {
        return value * (float)factor;
    }

    public NRotateTransform DefaultValue()
    {
        return new NRotateTransform();
    }

    public bool Equal(NRotateTransform value1, NRotateTransform value2)
    {
        return value1 == value2;
    }
}