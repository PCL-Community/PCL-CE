using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Platform;

/// <summary>文件与文件夹对话框。具体实现由组合根注册到 <see cref="ApplicationRuntime" />。</summary>
public interface IFileDialogService
{
    /// <summary>当前平台是否有可用的文件对话框实现。</summary>
    bool IsAvailable { get; }

    /// <summary>显示保存文件对话框；取消时返回空字符串。</summary>
    Task<string> SelectSaveFileAsync(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>显示打开文件对话框；取消时返回空字符串。</summary>
    Task<string> SelectFileAsync(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>显示打开文件对话框；取消时返回空数组。</summary>
    Task<string[]> SelectFilesAsync(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true,
        CancellationToken cancellationToken = default);

    /// <summary>显示文件夹选择对话框；取消时返回空字符串。</summary>
    Task<string> SelectFolderAsync(
        string? title = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default);
}