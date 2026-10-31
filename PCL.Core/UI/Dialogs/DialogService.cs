using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public Task<DialogResult> ShowAsync(DialogRequest request) => Show(request).Completion.Task;

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
            presenter.Present(next);
        else
            presenter.ShowFallback(next);
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

    #region IDialogService

    public async Task<int> ShowAsync(
        string title,
        string content,
        string button1,
        string? button2 = null,
        string? button3 = null,
        bool isWarn = false)
    {
        var result = await ShowAsync(new TextDialogRequest
        {
            Title = title,
            Content = content,
            IsWarn = isWarn,
            Buttons = DialogButtons.From(
                DialogButton.Create(button1),
                DialogButton.Create(button2),
                DialogButton.Create(button3))
        });

        return result.Button;
    }

    public async Task<string?> InputAsync(
        string title,
        string content,
        string? defaultValue = null,
        string? hintText = null,
        IReadOnlyList<IValidator<string>>? validateRules = null,
        string? button1 = null,
        string? button2 = null,
        bool isWarn = false)
    {
        var result = await ShowAsync(new InputDialogRequest
        {
            Title = title,
            Content = content,
            DefaultValue = defaultValue ?? "",
            HintText = hintText ?? "",
            ValidateRules = validateRules ?? [],
            IsWarn = isWarn,
            Buttons = DialogButtons.From(
                DialogButton.Create(button1 ?? Lang.Text("Common.Action.Confirm")),
                DialogButton.Create(button2 ?? Lang.Text("Common.Action.Cancel")))
        });

        return result.IsConfirmed ? result.InputText : null;
    }

    public async Task<int?> SelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
    {
        var result = await ShowAsync(new SelectDialogRequest
        {
            Title = title,
            Options = options.Select(option => new DialogOption { Text = option }).ToList(),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Confirm")),
                DialogButton.Create(button2))
        });

        return result.IsConfirmed ? result.SelectedIndex : null;
    }

    public async Task<List<int>> MultiSelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
    {
        var result = await ShowAsync(new SelectDialogRequest
        {
            Title = title,
            MultiSelect = true,
            Options = options.Select(option => new DialogOption { Text = option }).ToList(),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Confirm")),
                DialogButton.Create(button2))
        });

        return result.IsConfirmed ? result.SelectedIndices?.ToList() ?? [] : [];
    }

    #endregion
}
