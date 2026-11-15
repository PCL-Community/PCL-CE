namespace PCL.Core.UI.Dialogs;

/// <summary>由界面层实现的弹窗呈现器。</summary>
public interface IDialogPresenter
{
    /// <summary>当前是否可以显示弹窗（主窗体已就绪且未被最小化）。</summary>
    bool IsAvailable { get; }

    /// <summary>显示弹窗，界面层只负责呈现。</summary>
    void Present(DialogViewModel dialog);

    /// <summary>移除弹窗视图，通常在关闭动画结束后调用。</summary>
    void Dismiss(DialogViewModel dialog);

    /// <summary>界面不可用时的兜底呈现，实现方必须最终调用 <see cref="DialogViewModel.Close"/>。</summary>
    void ShowFallback(DialogViewModel dialog);
}
