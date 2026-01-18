using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
/// 同时执行的动画集合。
/// </summary>
public sealed class ParallelAnimationGroup : AnimationGroup
{
    private TaskCompletionSource? _cancelTcs;

    public override async Task<IAnimation> RunAsync(IAnimatable target)
    {
        Status = AnimationStatus.Running;
        AnimationService.PushAnimationFireAndForget(this, target);
        
        _cancelTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var childrenSnapshot = Children.ToList();
        var childWaitTasks = new List<Task>();

        lock (ChildrenCore)
        {
            if (Status == AnimationStatus.Canceled) return this;
            
            // 立即启动所有子动画，并收集它们的完成 Task
            foreach (var child in childrenSnapshot)
            {
                var childTarget = ResolveTarget(child, target);
                
                // 立即拿到实例
                var instance = child.RunFireAndForget(childTarget);
                ChildrenCore.Add(instance);
                
                // 收集 Task
                childWaitTasks.Add(CreateChildAwaiter(instance));
            }
        }

        try
        {
            // 等待到所有子动画完成或组被取消
            await Task.WhenAny(Task.WhenAll(childWaitTasks), _cancelTcs.Task);
        }
        finally
        {
            if (Status != AnimationStatus.Canceled)
            {
                Status = AnimationStatus.Completed;
            }
            _cancelTcs = null;
        }

        return this;
    }

    public override void Cancel()
    {
        base.Cancel();
        _cancelTcs?.TrySetResult();
    }

    public override IAnimation RunFireAndForget(IAnimatable target)
    {
        _ = RunAsync(target);
        return this;
    }
}