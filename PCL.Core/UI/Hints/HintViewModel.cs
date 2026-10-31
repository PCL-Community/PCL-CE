using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL.Core.UI.Hints;

/// <summary>提示的响应式状态，界面层据此展示并回报用户操作。</summary>
public sealed partial class HintViewModel : ObservableObject
{
    public HintViewModel(HintRequest request)
    {
        Request = request;
    }

    public HintRequest Request { get; }

    public HintKind Kind => Request.Kind;

    public string Message => Request.Message;

    public bool Log => Request.Log;

    /// <summary>显示时长（毫秒），未指定时按正文长度估算。</summary>
    public double DisplayDuration => Request.Duration ?? 800d + Math.Clamp(Message.Length, 5d, 23d) * 180d;

    /// <summary>图标（lucide 路径）。</summary>
    public string Icon => Request.Icon ?? Kind switch
    {
        HintKind.Success => "lucide/circle-check",
        HintKind.Error => "lucide/circle-minus",
        HintKind.Warning => "lucide/triangle-alert",
        _ => "lucide/info"
    };

    /// <summary>是否正在退场。</summary>
    [ObservableProperty] private bool _isDismissing;

    /// <summary>是否已关闭。</summary>
    [ObservableProperty] private bool _isClosed;

    /// <summary>界面层请求重新强调该提示。</summary>
    public event Action<HintViewModel>? EmphasizeRequested;

    /// <summary>界面层请求关闭该提示。</summary>
    public event Action<HintViewModel>? DismissRequested;

    /// <summary>关闭通知，供服务回收。</summary>
    internal TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>请求界面层强调（重复提示时使用）。</summary>
    public void RequestEmphasize() => EmphasizeRequested?.Invoke(this);

    /// <summary>请求关闭（界面层执行退场动画后调用 <see cref="Close" />）。</summary>
    public void Dismiss()
    {
        if (IsDismissing || IsClosed) return;
        IsDismissing = true;
        DismissRequested?.Invoke(this);
    }

    /// <summary>标记已关闭，重复调用无副作用。</summary>
    public void Close()
    {
        if (IsClosed) return;
        IsClosed = true;
        Closed.TrySetResult();
        HintService.Instance.OnHintClosed(this);
    }
}
