namespace PCL.Core.Platform;

/// <summary>界面层模态消息泵抽象，用于同步弹窗等待。</summary>
public interface IUiModalFrame
{
    /// <summary>退出模态消息泵。</summary>
    void Exit();

    /// <summary>在当前 UI 线程运行模态消息泵，直到 <see cref="Exit" /> 被调用或已经退出。</summary>
    void Run();
}
