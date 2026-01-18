using System;

namespace PCL.Core.UI.Animation.Easings;

public class CombinedEasing(IEasing ease1, IEasing ease2, double split = 0.5) : Easing
{
    private readonly IEasing _ease1 = ease1 ?? throw new ArgumentNullException(nameof(ease1));
    private readonly IEasing _ease2 = ease2 ?? throw new ArgumentNullException(nameof(ease2));

    private readonly double _split = Math.Clamp(split, 0.00001, 0.99999); 
    
    protected override double EaseCore(double t)
    {
        if (t < _split)
        {
            return _split * _ease1.Ease(t / _split);
        }

        return (1.0 - _split) * _ease2.Ease((t - _split) / (1.0 - _split)) + _split;
    }
}