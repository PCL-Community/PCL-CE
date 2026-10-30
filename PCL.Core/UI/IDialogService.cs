using System.Collections.Generic;
using System.Threading.Tasks;

namespace PCL.Core.UI;

/// <summary>供视图模型使用的异步弹窗服务。</summary>
public interface IDialogService
{
    Task<int> ShowAsync(
        string title,
        string content,
        string button1,
        string? button2 = null,
        string? button3 = null,
        bool isWarn = false);

    Task<string?> InputAsync(string title, string content, string? defaultValue = null);

    Task<int?> SelectAsync(string title, IReadOnlyList<string> options);

    Task<List<int>> MultiSelectAsync(string title, IReadOnlyList<string> options);
}
