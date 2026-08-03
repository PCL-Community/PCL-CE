using System;
using System.IO;
using Microsoft.Win32;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL.Core.UI;

/// <summary>
///     提供文件和文件夹对话框相关的实用方法。
/// </summary>
public static class SystemDialogs
{
    /// <summary>
    ///     显示保存文件对话框，要求用户选择保存位置。
    /// </summary>
    /// <param name="title">对话框标题。为 <c>null</c> 时使用本地化的默认标题。</param>
    /// <param name="fileName">默认文件名。</param>
    /// <param name="fileFilter">文件格式过滤器，例如 "常用图片文件|*.png;*.jpg"。为 <c>null</c> 时使用本地化的全部文件筛选器。</param>
    /// <param name="initialDirectory">初始目录，默认为 <c>null</c>。</param>
    /// <returns>用户选择的完整文件路径，如果取消则返回空字符串。</returns>
    public static string SelectSaveFile(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null)
    {
        var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
        var dialogFilter = fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter");
        var fileDialog = new SaveFileDialog
        {
            AddExtension = true,
            Title = dialogTitle,
            FileName = fileName,
            Filter = dialogFilter,
            InitialDirectory = !string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)
                ? initialDirectory
                : null
        };

        LogWrapper.Info("Dialog", $"打开保存文件对话框：{dialogTitle}");
        var result = fileDialog.ShowDialog();
        if (result != true)
        {
            LogWrapper.Info("Dialog", "选择文件被取消");
            return "";
        }

        var selectedPath = fileDialog.FileName;
        LogWrapper.Info("Dialog", $"选择文件返回：{selectedPath}");
        return string.IsNullOrEmpty(selectedPath) ? "" : Path.GetFullPath(selectedPath);
    }

    /// <summary>
    ///     显示打开文件对话框，要求用户选择单个文件。
    /// </summary>
    /// <param name="fileFilter">文件格式过滤器，例如 <c>常用图片文件|*.png;*.jpg</c>。为 <c>null</c> 时使用本地化的全部文件筛选器。</param>
    /// <param name="title">对话框标题。为 <c>null</c> 时使用本地化的默认标题。</param>
    /// <param name="initialDirectory">初始目录，默认由系统决定。</param>
    /// <returns>用户选择的完整文件路径，如果取消则返回空字符串。</returns>
    public static string SelectFile(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null)
    {
        var result = SelectFiles(fileFilter, title, initialDirectory, false);
        return result.Length == 0 ? "" : result[0];
    }

    /// <summary>
    ///     显示打开文件对话框，要求用户选择文件。
    /// </summary>
    /// <param name="fileFilter">文件格式过滤器，例如 <c>常用图片文件|*.png;*.jpg</c>。为 <c>null</c> 时使用本地化的全部文件筛选器。</param>
    /// <param name="title">对话框标题。为 <c>null</c> 时使用本地化的默认标题。</param>
    /// <param name="initialDirectory">初始目录，默认由系统决定。</param>
    /// <param name="allowMultiSelect">是否允许选择多个文件，默认允许。</param>
    /// <returns>用户选择的文件路径数组，如果取消则返回空数组。</returns>
    public static string[] SelectFiles(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true)
    {
        var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
        var dialogFilter = fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter");
        var fileDialog = new OpenFileDialog
        {
            AddExtension = true,
            CheckFileExists = true,
            Filter = dialogFilter,
            Multiselect = allowMultiSelect,
            Title = dialogTitle,
            ValidateNames = true,
            InitialDirectory = !string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)
                ? initialDirectory
                : null
        };

        var num = allowMultiSelect ? "多" : "单";
        LogWrapper.Info("Dialog", $"打开选择{num}个文件对话框: {dialogTitle}");
        var result = fileDialog.ShowDialog();
        if (result != true)
        {
            LogWrapper.Info("Dialog", "选择文件被取消");
            return [];
        }

        string[] selectedFiles = fileDialog.FileNames;
        LogWrapper.Info("Dialog", $"选择{num}个文件返回: {string.Join(",", selectedFiles)}");
        return selectedFiles.Length == 0 ? [] : Array.ConvertAll(selectedFiles, Path.GetFullPath);
    }

    /// <summary>
    ///     显示文件夹选择对话框，要求用户选择一个文件夹。
    /// </summary>
    /// <param name="title">对话框标题。为 <c>null</c> 时使用本地化的默认标题。</param>
    /// <param name="initialDirectory">初始目录，默认为桌面。</param>
    /// <returns>用户选择的文件夹路径（以 \ 结尾），如果取消则返回空字符串。</returns>
    public static string SelectFolder(string? title = null, string? initialDirectory = null)
    {
        var dialogTitle = title ?? Lang.Text("SystemDialog.Folder.SelectTitle");
        var folderDialog = new OpenFolderDialog
        {
            Title = dialogTitle,
            InitialDirectory = initialDirectory
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = false
        };

        LogWrapper.Info("Dialog", $"打开选择文件夹对话框: {dialogTitle}");
        var result = folderDialog.ShowDialog();
        if (result != true)
        {
            LogWrapper.Info("Dialog", "选择文件夹被取消");
            return "";
        }

        var selectedPath = folderDialog.FolderName;
        if (string.IsNullOrEmpty(selectedPath))
        {
            LogWrapper.Info("Dialog", "选择文件夹返回: 空");
            return "";
        }

        var normalizedPath = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
        LogWrapper.Info("Dialog", $"选择文件夹返回: {normalizedPath}");
        return normalizedPath;
    }
}