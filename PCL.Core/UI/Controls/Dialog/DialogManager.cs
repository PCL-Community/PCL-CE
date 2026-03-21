using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PCL.Core.UI.Controls.Dialog;

/// <summary>
/// 负责调度弹窗以及数据传递
/// </summary>
/// <param name="dialogPresent"></param>
public class DialogManager(IDialogPresent dialogPresent)
{
    private readonly ConcurrentDictionary<DialogBase, DialogHandler<object>> _dialogHandlers = new();
    private readonly ConcurrentQueue<DialogBase> _dialogs = new();

    private readonly IDialogPresent _dialogPresent = dialogPresent;

    public async Task<TResult> ShowAsync<TResult>(DialogBase ui)
    {
        var handler = new DialogHandler<object>() { TaskCallback = new TaskCompletionSource<object>() };

        _dialogHandlers.TryAdd(ui, handler);
        _dialogs.Enqueue(ui);
        await _dialogPresent.PresentAsync(ui).ConfigureAwait(false);

        return (TResult)(await handler.TaskCallback.Task.ConfigureAwait(false));
    }

    public async Task SetResult<TResult>(TResult result)
    {
        _dialogs.TryDequeue(out var current);
        if (current == null || !current.Equals(_dialogPresent.CurrentDialog))
        {
            throw new Exception("Dialog oder incorrect");
        }

        _dialogHandlers.TryRemove(current, out var handler);
        if (handler == null) return;

        await _dialogPresent.DismissAsync().ConfigureAwait(false);
        handler.TaskCallback.SetResult(result!);
    }

}
