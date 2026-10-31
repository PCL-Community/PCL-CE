using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PCL.Core.UI.Hints;

namespace PCL;

/// <summary>把 <see cref="HintService" /> 的提示呈现到主窗体，只负责界面显示。</summary>
public sealed class HintPresenter : IHintPresenter
{
    public static HintPresenter Instance { get; } = new();

    /// <summary>注册到 Core 提示服务。</summary>
    public static void Install() => HintService.Instance.Presenter = Instance;

    public bool IsAvailable => Launcher.frmMain?.PanHint is not null;

    public void Present(HintViewModel hint)
    {
        var main = Launcher.frmMain;
        if (main?.PanHint is null) return;

        main.PanHint.HorizontalAlignment = HorizontalAlignment.Right;
        main.PanHint.VerticalAlignment = VerticalAlignment.Bottom;

        var extraHeight = main.PanExtraButtons.ActualHeight;
        main.PanHint.Margin = new Thickness(0, 0, 0, extraHeight > 0 ? extraHeight + 20 : 20);

        var toast = new MyToast(hint);
        main.PanHint.Children.Add(toast);
        toast.Show();
    }

    public void Dismiss(HintViewModel hint)
    {
        var toast = FindToast(hint);
        toast?.Dismiss();
    }

    public void DismissAll()
    {
        var main = Launcher.frmMain;
        if (main?.PanHint is null) return;

        foreach (var toast in main.PanHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }

    private static MyToast? FindToast(HintViewModel hint)
    {
        var main = Launcher.frmMain;
        if (main?.PanHint is null) return null;

        return main.PanHint.Children.OfType<MyToast>().FirstOrDefault(toast => ReferenceEquals(toast.Hint, hint));
    }
}
