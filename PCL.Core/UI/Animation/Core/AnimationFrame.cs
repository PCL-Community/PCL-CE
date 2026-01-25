using System;
using System.Numerics;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Core;

public readonly struct AnimationFrame<T> : IAnimationFrame where T : struct
{
    public IAnimatable Target { get; init; }
    public T Value { get; init; }
    public T StartValue { get; init; }
    public T GetAbsoluteValue() => ValueProcessorManager.Add(StartValue, Value);

    object IAnimationFrame.GetAbsoluteValue() => GetAbsoluteValue();
}