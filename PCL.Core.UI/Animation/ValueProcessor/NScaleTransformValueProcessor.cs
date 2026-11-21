namespace PCL.Core.UI.Animation.ValueProcessor;

public class NScaleTransformValueProcessor : IValueProcessor<NScaleTransform>
{
    public NScaleTransform Filter(NScaleTransform value)
    {
        return value;
    }

    public NScaleTransform Add(NScaleTransform value1, NScaleTransform value2)
    {
        return value1 + value2;
    }

    public NScaleTransform Subtract(NScaleTransform value1, NScaleTransform value2)
    {
        return value1 - value2;
    }

    public NScaleTransform Scale(NScaleTransform value, double factor)
    {
        return value * (float)factor;
    }

    public NScaleTransform DefaultValue()
    {
        return new NScaleTransform();
    }

    public bool Equal(NScaleTransform value1, NScaleTransform value2)
    {
        return value1 == value2;
    }
}