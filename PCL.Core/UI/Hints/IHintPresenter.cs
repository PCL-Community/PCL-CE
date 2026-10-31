namespace PCL.Core.UI.Hints;

/// <summary>提示的界面呈现器，由界面层实现。</summary>
public interface IHintPresenter
{
    /// <summary>当前是否可以呈现提示。</summary>
    bool IsAvailable { get; }

    /// <summary>呈现一条提示。</summary>
    void Present(HintViewModel hint);

    /// <summary>关闭一条提示。</summary>
    void Dismiss(HintViewModel hint);

    /// <summary>关闭全部提示。</summary>
    void DismissAll();
}
