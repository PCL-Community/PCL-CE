namespace PCL.Core.UI.Animation.Animatable;

public sealed class EmptyAnimatable : IAnimatable
{
    private EmptyAnimatable()
    {
    }

    public static EmptyAnimatable Instance { get; } = new();

    public object? GetValue()
    {
        return null;
    }

    public void SetValue(object value)
    {
        // 空
    }

    public void SetValue<T>(T value)
    {
        // 空
    }
}