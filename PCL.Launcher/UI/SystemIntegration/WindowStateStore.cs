using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;

namespace PCL.UI.SystemIntegration;

/// <summary>
///     窗口尺寸持久化：只保存宽高，不保存位置。WPF 主窗口始终由 CenterScreen 居中，
///     位置持久化会让二次启动偏离屏幕中心甚至落到已拔出的副屏上。
/// </summary>
public sealed class WindowStateStore
{
    private readonly DispatcherTimer _saveTimer;
    private readonly Window _window;
    private bool _normalizing;
    private bool _restoring;
    private bool _saveEnabled;

    public WindowStateStore(Window window)
    {
        _window = window;
        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Save();
        };

        // 尺寸必须在 Show() 之前恢复，否则 CenterScreen 会先按 XAML 的 850×500 居中，
        // 再在 Opened 后改成 900×550，造成 +25/+25 的偏移。
        RestoreSize();

        _window.Opened += (_, _) => NormalizeWindowState();
        _window.Closing += (_, _) => Save(true);
        _window.SizeChanged += (_, _) => ScheduleSave();
        _window.PropertyChanged += (_, e) =>
        {
            if (e.Property != Window.WindowStateProperty) return;
            NormalizeWindowState();
            ScheduleSave();
        };
    }

    private void NormalizeWindowState()
    {
        if (_normalizing || _window.WindowState != WindowState.Maximized) return;
        _normalizing = true;
        try
        {
            // 在属性变更回调里同步改回去会与平台窗口过程重入，推迟到下一轮派发。
            Dispatcher.UIThread.Post(() =>
            {
                if (_window.WindowState == WindowState.Maximized)
                    _window.WindowState = WindowState.Normal;
            });
        }
        finally
        {
            _normalizing = false;
        }
    }

    public void RestoreSize()
    {
        _restoring = true;
        try
        {
            _window.Width = Math.Max(
                _window.MinWidth > 0 ? _window.MinWidth : 800d,
                States.UI.WindowWidth);
            _window.Height = Math.Max(
                _window.MinHeight > 0 ? _window.MinHeight : 520d,
                States.UI.WindowHeight);
        }
        finally
        {
            _restoring = false;
            _saveEnabled = true;
        }
    }

    private void ScheduleSave()
    {
        if (_restoring || !_saveEnabled) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Save(bool force = false)
    {
        if (_restoring || (!_saveEnabled && !force)) return;
        if (_window.WindowState != WindowState.Normal) return;

        States.UI.WindowWidth = _window.Width;
        States.UI.WindowHeight = _window.Height;
    }
}
