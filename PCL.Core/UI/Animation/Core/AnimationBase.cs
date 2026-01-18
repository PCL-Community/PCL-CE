using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

public abstract class AnimationBase : DependencyObject, IAnimation
{
    public string Name { get; set; } = string.Empty;
    private volatile int _status = (int)AnimationStatus.NotStarted;
    public AnimationStatus Status
    {
        get => (AnimationStatus)_status;
        internal set => Interlocked.Exchange(ref _status, (int)value);
    }
    public abstract int CurrentFrame { get; set; }
    
    public abstract Task<IAnimation> RunAsync(IAnimatable target);
    public abstract IAnimation RunFireAndForget(IAnimatable target);
    public abstract void Cancel();
    public abstract IAnimationFrame? ComputeNextFrame(IAnimatable target);
    
    public void RaiseStarted() => Started?.Invoke(this, EventArgs.Empty);
    public void RaiseCompleted() => Completed?.Invoke(this, EventArgs.Empty);
    
    public event EventHandler? Started;
    public event EventHandler? Completed;
}