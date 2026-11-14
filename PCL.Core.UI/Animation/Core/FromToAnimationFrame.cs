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

    public Action GetAction()
    {
        var target = Target;
        var value = Value;
        var startValue = StartValue;
        var isRelative = IsRelative;

        return () =>
        {
            // 相对动画按帧应用增量，保留旧引擎的可叠加特性，并避免并行动画互相覆盖。
            var current = isRelative ? target.GetValue() : null;
            var absolute = current is null
                ? ValueProcessorManager.Add(startValue, value)
                : ValueProcessorManager.Add(current, value);
            target.SetValue(absolute!);
        };
    }
}