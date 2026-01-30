using System.Windows;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

public interface IAnimationFrame
{
    IAnimatable Target { get; }
    object GetAbsoluteValue();
}