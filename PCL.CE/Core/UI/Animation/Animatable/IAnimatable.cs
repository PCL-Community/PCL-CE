namespace PCL.CE.Core.UI.Animation.Animatable;

public interface IAnimatable
{
    public object? GetValue();
    public void SetValue(object value);
}