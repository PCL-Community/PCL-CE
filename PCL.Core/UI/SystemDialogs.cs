using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     提供文件和文件夹对话框相关的实用方法。具体实现由 UI 层注册到 <see cref="PlatformServices.FileDialogs" />。
/// </summary>
public static class SystemDialogs
{
    private static IFileDialogService? Service => PlatformServices.FileDialogs;

    /// <summary>
    ///     显示保存文件对话框，要求用户选择保存位置。
    /// </summary>
    /// <returns>用户选择的完整文件路径，如果取消或无可用的 UI 实现则返回空字符串。</returns>
    public static string SelectSaveFile(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null)
    {
        return Service?.SelectSaveFile(
                title,
                fileName,
                fileFilter,
                initialDirectory)
            ?? "";
    }

    /// <summary>
    ///     显示打开文件对话框，要求用户选择单个文件。
    /// </summary>
    /// <returns>用户选择的完整文件路径，如果取消则返回空字符串。</returns>
    public static string SelectFile(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null)
    {
        return Service?.SelectFile(fileFilter, title, initialDirectory) ?? "";
    }

    /// <summary>
    ///     显示打开文件对话框，要求用户选择文件。
    /// </summary>
    /// <returns>用户选择的文件路径数组，如果取消则返回空数组。</returns>
    public static string[] SelectFiles(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true)
    {
        return Service?.SelectFiles(
                fileFilter,
                title,
                initialDirectory,
                allowMultiSelect)
            ?? [];
    }

    /// <summary>
    ///     显示文件夹选择对话框，要求用户选择一个文件夹。
    /// </summary>
    /// <returns>用户选择的文件夹路径（以 \ 结尾），如果取消则返回空字符串。</returns>
    public static string SelectFolder(string? title = null, string? initialDirectory = null)
    {
        return Service?.SelectFolder(title, initialDirectory) ?? "";
    }
}