using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.UI.Threading;

namespace PCL.Core.UI.Dialogs;

/// <summary>弹窗调度：负责排队、派发与结果回传，界面层只实现 <see cref="IDialogPresenter" />。</summary>
public sealed class DialogService : IDialogService
{
    private const int MaxPresentRetries = 40;
    private readonly Lock _lock = new();

    private readonly Queue<DialogViewModel> _queue = [];
    private DialogViewModel? _current;
    public static DialogService Instance { get; } = new();

    /// <summary>界面层注册的呈现器。</summary>
    public IDialogPresenter? Presenter { get; set; }

    public bool IsShowing => _current is not null;

    public bool HasPending
    {
        get
        {
            lock (_lock)
            {
                return _current is not null || _queue.Count > 0;
            }
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count + (_current is null ? 0 : 1);
            }
        }
    }

    /// <summary>异步显示弹窗，等待用户关闭后返回结果。</summary>
    public async Task<DialogResult> ShowAsync(
        DialogRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = _Enqueue(request);
        await using var registration = cancellationToken.Register(() => _Cancel(dialog));
        return await dialog.Completion.Task.ConfigureAwait(false);
    }

    /// <summary>显示不需要返回结果的通知弹窗，不阻塞调用线程。</summary>
    public void ShowNotification(DialogRequest request)
    {
        try
        {
            var dialog = _Enqueue(request);
            _ = dialog.Completion.Task.ContinueWith(
                static (task, _) =>
                    LogWrapper.Error(
                        task.Exception?.GetBaseException(),
                        "UI.Dialogs",
                        "通知弹窗显示失败"),
                null,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "通知弹窗创建失败");
        }
    }

    /// <summary>
    /// 仅供工作线程使用的兼容入口。界面线程不得调用，否则会抛出异常；新代码应使用 <see cref="ShowAsync" /> 或
    /// <see cref="ShowNotification" />。<br /> 该入口用于尚未支持异步的 <c>LoaderTask</c>
    /// 等同步回调，等待的是后台线程而不是界面线程。
    /// </summary>
    public DialogResult ShowBlockingOnWorkerThread(DialogRequest request)
    {
        return UiDispatcher.CheckAccess()
            ? throw new InvalidOperationException(
                "不能在界面线程同步等待弹窗，请改用 ShowAsync 或 ShowNotification。")
            : ShowAsync(request).GetAwaiter().GetResult();
    }

    /// <summary>排队并交给呈现器，不等待结果。界面不可用时立即以 <see cref="DialogResult.None" /> 关闭。</summary>
    private DialogViewModel _Enqueue(DialogRequest request)
    {
        var dialog = new DialogViewModel(request);

        if (!UiDispatcher.IsAvailable)
        {
            LogWrapper.Warn((Exception?)null, "界面不可用，弹窗请求被丢弃：" + request.Title);
            dialog.Close(DialogResult.None);
            return dialog;
        }

        lock (_lock)
        {
            _queue.Enqueue(dialog);
        }

        // `Post` instead of `Invoke` to avoid potential dead lock
        UiDispatcher.Post(Pump);

        return dialog;
    }

    private void _Cancel(DialogViewModel dialog)
    {
        if (dialog.IsClosed) return;

        var wasQueued = false;
        lock (_lock)
        {
            if (_queue.Contains(dialog))
            {
                var pending = _queue
                    .Where(item => !ReferenceEquals(item, dialog))
                    .ToArray();
                _queue.Clear();
                foreach (var item in pending) _queue.Enqueue(item);
                wasQueued = true;
            }
        }

        if (wasQueued || !dialog.IsClosed)
            dialog.Close(DialogResult.Cancel);
    }

    /// <summary>取出队首弹窗并交给呈现器。必须在 UI 线程调用。</summary>
    public void Pump()
    {
        DialogViewModel next;

        lock (_lock)
        {
            if (_current is not null || _queue.Count == 0) return;
            next = _queue.Dequeue();
            if (next.IsClosed) return;
            _current = next;
        }

        var presenter = Presenter;
        if (presenter is null)
        {
            LogWrapper.Warn((Exception?)null, "未注册弹窗呈现器，弹窗请求被丢弃：" + next.Title);
            next.Close(DialogResult.None);
            return;
        }

        // Presenter may throw an error
        // Pump should throw it back to `Completion`
        try
        {
            if (presenter.IsAvailable)
            {
                presenter.Present(next);
                return;
            }

            // 上一个弹窗的退场动画期间界面仍被占用，稍后重试，避免误退化到原生弹窗
            next.PresentAttempts++;
            if (next.PresentAttempts > MaxPresentRetries)
            {
                presenter.ShowFallback(next);
                return;
            }
        }
        catch (Exception ex)
        {
            next.Completion.TrySetException(ex);
            return;
        }

        lock (_lock)
        {
            _current = null;
            var pending = _queue.ToArray();
            _queue.Clear();
            _queue.Enqueue(next);
            foreach (var item in pending) _queue.Enqueue(item);
        }

        _ = Task.Delay(150).ContinueWith(_ => UiDispatcher.Post(Pump), TaskScheduler.Default);
    }

    internal void OnDialogClosed(DialogViewModel dialog)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_current, dialog)) return;
            _current = null;
        }

        UiDispatcher.Post(Pump);
    }

    /// <summary>丢弃所有排队中的弹窗（例如主窗体关闭时）。</summary>
    public void ClearPending()
    {
        List<DialogViewModel> pending;
        lock (_lock)
        {
            pending = [.. _queue];
            _queue.Clear();
        }

        foreach (var dialog in pending)
            dialog.Close(DialogResult.None);
    }
}