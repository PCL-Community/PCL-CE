namespace PCL.Core.Platform;

/// <summary>
///     文件与文件夹对话框。具体实现由 UI 层注册到 <see cref="PlatformServices.FileDialogs" />。
/// </summary>
public interface IFileDialogService
{
    /// <summary>显示保存文件对话框；取消时返回空字符串。</summary>
    string SelectSaveFile(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null);

    /// <summary>显示打开文件对话框；取消时返回空字符串。</summary>
    string SelectFile(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null);

    /// <summary>显示打开文件对话框；取消时返回空数组。</summary>
    string[] SelectFiles(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true);

    /// <summary>显示文件夹选择对话框；取消时返回空字符串。</summary>
    string SelectFolder(string? title = null, string? initialDirectory = null);
}