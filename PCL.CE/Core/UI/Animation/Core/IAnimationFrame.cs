using System.Windows;
using PCL.CE.Core.UI.Animation.Animatable;

namespace PCL.CE.Core.UI.Animation.Core;

public interface IAnimationFrame
{
    IAnimatable Target { get; }
    object GetAbsoluteValue();
}