using System;

namespace PCL.Core.Platform;

/// <summary>
///     主窗口抽象。
/// </summary>
public interface IWindowHost
{
    /// <summary>窗口是否处于最小化状态。</summary>
    bool IsMinimized { get; }

    /// <summary>窗口是否置顶。</summary>
    bool IsTopmost { get; set; }

    /// <summary>窗口加载完成事件。</summary>
    event Action? Loaded;

    /// <summary>显示窗口。</summary>
    void Show();

    /// <summary>激活窗口。</summary>
    void Activate();

    /// <summary>将窗口从最小化状态恢复。</summary>
    void Restore();
}