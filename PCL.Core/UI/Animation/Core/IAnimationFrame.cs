using System;
using System.Windows;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

public interface IAnimationFrame
{
    Action GetAction();
}