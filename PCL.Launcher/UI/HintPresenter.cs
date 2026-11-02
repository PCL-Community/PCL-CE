using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.UI;

/// <summary>
///     把 Core 的提示显示到主窗口右下角的提示层。每条提示对应一个 <see cref="MyToast" />，
///     显示时长、拖动关闭与出入场动画由控件自身负责；主窗口尚未创建时提示先排队，窗口就绪后按原顺序补显示。
/// </summary>
public sealed class HintPresenter : IHintPresenter
{
    /// <summary>提示层与窗口底边的基础间距（像素）。</summary>
    private const double LayerBottomMargin = 20d;

    /// <summary>等待主窗口就绪的提示，保持到达顺序。</summary>
    private readonly List<HintViewModel> _pending = [];

    private Panel? _layer;

    private Window? _window;

    public static HintPresenter Instance { get; } = new();

    /// <summary>提示总会被接收：界面未就绪时进入排队，不丢弃。</summary>
    public bool IsAvailable => true;

    public void Present(HintViewModel hint)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Present(hint));
            return;
        }

        if (hint.IsClosed)
            return;

        if (_layer is not { } layer)
        {
            _pending.Add(hint);
            return;
        }

        _ApplyLayout(layer);
        _Show(layer, hint);
    }

    public void Dismiss(HintViewModel hint)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Dismiss(hint));
            return;
        }

        if (_pending.Remove(hint))
        {
            hint.Close();
            return;
        }

        if (_FindToast(hint) is { } toast)
        {
            toast.Dismiss();
            return;
        }

        hint.Close();
    }

    public void DismissAll()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(DismissAll);
            return;
        }

        foreach (var toast in _GetToasts())
            toast.Dismiss();

        foreach (var hint in _pending)
            hint.Close();
        _pending.Clear();
    }

    public static void Install()
    {
        HintService.Instance.Presenter = Instance;
    }

    /// <summary>绑定主窗口及其提示层，由窗口构造时调用；已有排队提示会立即补显示。</summary>
    public void Attach(Window window, Panel layer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Attach(window, layer));
            return;
        }

        if (ReferenceEquals(_window, window) && ReferenceEquals(_layer, layer))
        {
            _Flush();
            return;
        }

        _Detach();
        _window = window;
        _layer = layer;
        window.Opened += _OnWindowOpened;
        window.Closed += _OnWindowClosed;
        _ApplyLayout(layer);
        _Flush();
    }

    private static void _Show(Panel layer, HintViewModel hint)
    {
        // 入场动画要求控件完成布局，因此把 Show 交给 Loaded；在此之前不参与布局、不可见，
        // 避免新提示先按自然高度撑开堆叠，导致已有提示跳动。
        var toast = new MyToast(hint)
        {
            Opacity = 0d,
            IsVisible = false
        };
        toast.Loaded += _OnToastLoaded;
        layer.Children.Add(toast);
    }

    private static void _OnToastLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not MyToast toast)
            return;

        toast.Loaded -= _OnToastLoaded;
        if (toast.Parent is null || toast.Hint.IsDismissing || toast.Hint.IsClosed)
            return;

        toast.IsVisible = true;
        toast.Show();
    }

    private void _ApplyLayout(Panel layer)
    {
        layer.HorizontalAlignment = HorizontalAlignment.Right;
        layer.VerticalAlignment = VerticalAlignment.Bottom;
        layer.Margin = new Thickness(0d, 0d, 0d, _GetBottomMargin());
    }

    private double _GetBottomMargin()
    {
        // TODO(FIX-E-01): 主窗口尚无右下角附加按钮面板，提示层固定按 20 像素底边留白；
        // 面板补齐后改为按面板高度 + 20 计算，避免提示压住按钮。
        var extraButtons = _window?.FindControl<Control>("PanExtraButtons");
        var extraHeight = extraButtons?.Bounds.Height ?? 0d;
        return extraHeight > 0d ? extraHeight + LayerBottomMargin : LayerBottomMargin;
    }

    private MyToast? _FindToast(HintViewModel hint)
    {
        return _GetToasts().FirstOrDefault(toast => ReferenceEquals(toast.Hint, hint));
    }

    private IEnumerable<MyToast> _GetToasts()
    {
        return _layer is null ? [] : _layer.Children.OfType<MyToast>().ToArray();
    }

    private void _Flush()
    {
        if (_layer is not { } layer || _pending.Count == 0)
            return;

        var pending = _pending.ToArray();
        _pending.Clear();

        foreach (var hint in pending)
        {
            if (hint.IsClosed)
                continue;
            _Show(layer, hint);
        }
    }

    private void _OnWindowOpened(object? sender, EventArgs e)
    {
        if (_layer is { } layer)
            _ApplyLayout(layer);
        _Flush();
    }

    private void _OnWindowClosed(object? sender, EventArgs e)
    {
        // 窗口关闭后提示已无处显示，直接结束，避免提示服务继续占用活动名额。
        _CloseImmediately();
        _Detach();
    }

    private void _CloseImmediately()
    {
        if (_layer is not null)
            foreach (var toast in _GetToasts())
            {
                _layer.Children.Remove(toast);
                toast.Hint.Close();
            }

        foreach (var hint in _pending)
            hint.Close();
        _pending.Clear();
    }

    private void _Detach()
    {
        if (_window is not null)
        {
            _window.Opened -= _OnWindowOpened;
            _window.Closed -= _OnWindowClosed;
        }

        _window = null;
        _layer = null;
    }
}