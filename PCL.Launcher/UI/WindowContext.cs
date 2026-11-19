using Avalonia.Controls;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;

namespace PCL.UI;

/// <summary>一个窗口的 UI 生命周期上下文。窗口级服务先从这里取得，避免直接访问全局主窗口。</summary>
public interface IWindowContext
{
    Window Window { get; }
    IWindowHost Host { get; }
    DialogPresenter DialogPresenter { get; }
    HintPresenter HintPresenter { get; }
}

/// <summary>主窗口上下文。当前仍复用应用级弹窗服务，后续可替换为窗口作用域实例。</summary>
public sealed class WindowContext : IWindowContext
{
    public WindowContext(Window window)
    {
        Window = window;
        Host = new WindowHost(window);
        DialogPresenter = new DialogPresenter();
        HintPresenter = new HintPresenter();

        // 在窗口创建阶段把呈现器接入现有弹窗/提示服务。
        // 多窗口页面栈完成后，这两个服务也会随 WindowContext 改为窗口作用域。
        DialogService.Instance.Presenter = DialogPresenter;
        HintService.Instance.Presenter = HintPresenter;
    }

    public Window Window { get; }
    public IWindowHost Host { get; }
    public DialogPresenter DialogPresenter { get; }
    public HintPresenter HintPresenter { get; }
}