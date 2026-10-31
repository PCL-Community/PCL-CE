namespace PCL.Core.UI.Animation.Animatable;

public interface IAnimatable
{
    public object? GetValue();
    public void SetValue(object value);
    public void SetValue<T>(T value);
}