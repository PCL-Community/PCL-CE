using System;

namespace PCL.Core.UI.Animation.Core;

public interface IAnimationFrame
{
    Action GetAction();
}