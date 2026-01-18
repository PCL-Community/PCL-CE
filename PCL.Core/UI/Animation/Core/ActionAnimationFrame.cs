using System;

namespace PCL.Core.UI.Animation.Core;

public struct ActionAnimationFrame(Action action) : IAnimationFrame
{
    public Action Action { get; set; } = action;

    public Action GetAction() => Action;
}