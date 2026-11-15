using System.Threading.Tasks;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.UI;

/// <summary>不产生任何界面交互的弹窗服务实现。</summary>
public sealed class NullDialogService : IDialogService
{
    public static readonly NullDialogService Instance = new();

    public DialogViewModel Show(DialogRequest request)
    {
        var dialog = new DialogViewModel(request);
        dialog.Close(DialogResult.None);
        return dialog;
    }

    public Task<DialogResult> ShowAsync(DialogRequest request)
    {
        Show(request);
        return Task.FromResult(DialogResult.None);
    }
}
