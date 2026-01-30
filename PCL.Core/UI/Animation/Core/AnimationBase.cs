using System;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

public abstract class AnimationBase : DependencyObject, IAnimation
{
    public abstract bool IsCompleted { get; }
    public abstract int CurrentFrame { get; set; }
    
    public abstract Task RunAsync(IAnimatable target);
    public abstract void RunFireAndForget(IAnimatable target);
    public abstract void Cancel();
    
    public abstract IAnimationFrame? ComputeNextFrame(IAnimatable target);
    
    public void RaiseStarted() => Started?.Invoke(this, EventArgs.Empty);
    public void RaiseCompleted() => Completed?.Invoke(this, EventArgs.Empty);
    
    public event EventHandler? Started;
    public event EventHandler? Completed;
}