namespace PCL.Core.UI.Animation.Animatable;

public sealed class EmptyAnimatable : IAnimatable
{
    public static EmptyAnimatable Instance { get; } = new();

    private EmptyAnimatable() { }
    
    public object? GetValue()
    {
        return null;
    }

    public void SetValue(object value)
    {
        // 空
    }
}