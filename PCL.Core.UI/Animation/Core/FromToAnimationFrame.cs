using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Core;

public readonly struct FromToAnimationFrame<T>(
    IAnimatable target,
    T value,
    T startValue,
    bool isRelative = false) : IAnimationFrame
{
    public IAnimatable Target { get; init; } = target;
    public T Value { get; init; } = value;
    public T StartValue { get; init; } = startValue;
    public bool IsRelative { get; init; } = isRelative;

    public void Apply()
    {
        var current = IsRelative ? Target.GetValue() : null;
        var absolute = current is null
            ? ValueProcessorManager.Add(StartValue, Value)
            : ValueProcessorManager.Add(current, Value);
        Target.SetValue(absolute!);
    }

    public Action GetAction()
    {
        return Apply;
    }
}