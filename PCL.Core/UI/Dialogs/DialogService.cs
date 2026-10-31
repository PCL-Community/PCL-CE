using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;
using FluentValidation;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Threading;

namespace PCL.Core.UI.Dialogs;

/// <summary>弹窗调度：负责排队、派发与结果回传，界面层只实现 <see cref="IDialogPresenter"/>。</summary>
public sealed class DialogService : IDialogService
{
    public static DialogService Instance { get; } = new();

    private readonly Queue<DialogViewModel> _queue = new();
    private readonly Lock _lock = new();
    private DialogViewModel? _current;

    private const int MaxPresentRetries = 40;

    /// <summary>界面层注册的呈现器。</summary>
    public IDialogPresenter? Presenter { get; set; }

    public bool IsShowing => _current is not null;

    public bool HasPending
    {
        get
        {
            lock (_lock) return _current is not null || _queue.Count > 0;
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_lock) return _queue.Count + (_current is null ? 0 : 1);
        }
    }

    /// <summary>提交弹窗请求并立即返回其状态对象。</summary>
    public DialogViewModel Show(DialogRequest request)
    {
        var dialog = Enqueue(request);

        // 单按钮弹窗没有可选分支，显示后立即返回，避免阻塞调用线程。
        var waitable = request.Buttons.Length > 1;
        if (!waitable) dialog.PresetResult(new DialogResult(1));

        if (!waitable || !request.ForceWait || dialog.IsClosed) return dialog;

        void PumpFrame()
        {
            if (dialog.IsClosed) return;

            var frame = dialog.WaitFrame;
            if (!frame.Continue) return;

            ComponentDispatcher.PushModal();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
        }

        // DispatcherFrame 只能由界面线程创建与驱动：
        // 界面线程直接泵帧；后台线程改为等待 Completion，避免跨线程驱动消息泵。
        if (UiDispatcher.CheckAccess()) PumpFrame();
        else dialog.Completion.Task.GetAwaiter().GetResult();

        return dialog;
    }

    /// <summary>排队并交给呈现器，不等待结果。界面不可用时立即以 <see cref="DialogResult.None" /> 关闭。</summary>
    private DialogViewModel Enqueue(DialogRequest request)
    {
        var dialog = new DialogViewModel(request);

        if (!UiDispatcher.IsAvailable)
        {
            LogWrapper.Warn((Exception?)null, "界面不可用，弹窗请求被丢弃：" + request.Title);
            dialog.Close(DialogResult.None);
            return dialog;
        }

        UiDispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                _queue.Enqueue(dialog);
            }

            Pump();
        });

        return dialog;
    }

    public async Task<DialogResult> ShowAsync(DialogRequest request)
    {
        var dialog = Enqueue(request);
        return await dialog.Completion.Task.ConfigureAwait(false);
    }

    /// <summary>取出队首弹窗并交给呈现器。必须在 UI 线程调用。</summary>
    public void Pump()
    {
        DialogViewModel next;

        lock (_lock)
        {
            if (_current is not null || _queue.Count == 0) return;
            next = _queue.Dequeue();
            _current = next;
        }

        var presenter = Presenter;
        if (presenter is null)
        {
            LogWrapper.Warn((Exception?)null, "未注册弹窗呈现器，弹窗请求被丢弃：" + next.Title);
            next.Close(DialogResult.None);
            return;
        }

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
            pending = _queue.ToList();
            _queue.Clear();
        }

        foreach (var dialog in pending)
            dialog.Close(DialogResult.None);
    }
}
