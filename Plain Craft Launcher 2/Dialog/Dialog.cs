using System.Threading.Tasks;
using System.Windows;

namespace PCL;

public class Dialog<T> where T : FrameworkElement, new()
{
    private readonly DialogControl _dialog;

    public T Content { get; }

    public Dialog(DialogControl dialog)
    {
        _dialog = dialog;
        Content = new T();
        _dialog.DialogContent = Content;
    }

    public Task<int> GetResultAsync()
    {
        var tcs = new TaskCompletionSource<int>();
        _dialog.OnClosed += result => tcs.TrySetResult(result);
        return tcs.Task;
    }

    public void Close(int result) => _dialog.Close(result);
}
