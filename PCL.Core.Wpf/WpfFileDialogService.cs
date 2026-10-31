using System;
using System.IO;
using Microsoft.Win32;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF 文件对话框的 <see cref="IFileDialogService" /> 实现。</summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    public string SelectSaveFile(
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
            InitialDirectory = !string.IsNullOrEmpty(initialDirectory)
                && Directory.Exists(initialDirectory)
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

    public string SelectFile(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null)
    {
        var result = SelectFiles(fileFilter, title, initialDirectory, false);
        return result.Length == 0 ? "" : result[0];
    }

    public string[] SelectFiles(
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
            InitialDirectory = !string.IsNullOrEmpty(initialDirectory)
                && Directory.Exists(initialDirectory)
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

    public string SelectFolder(string? title = null, string? initialDirectory = null)
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

        var normalizedPath = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        LogWrapper.Info("Dialog", $"选择文件夹返回: {normalizedPath}");
        return normalizedPath;
    }
}