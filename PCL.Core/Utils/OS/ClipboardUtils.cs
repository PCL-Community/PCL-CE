namespace PCL.Core.Utils.OS;

using System;
using System.Windows;

public static class ClipboardUtils {
    /// <summary>
    /// 将剪贴板内容设置为用于复制/粘贴操作的文件或文件夹路径列表。
    /// </summary>
    /// <param name="paths">要设置到剪贴板的文件或文件夹路径数组。</param>
    public static void SetClipboardFiles(string[] paths) {
        if (paths == null || paths.Length == 0) {
            throw new ArgumentException("Paths cannot be null or empty.", nameof(paths));
        }

        var dataObject = new DataObject();
        dataObject.SetData(DataFormats.FileDrop, paths);
        Clipboard.SetDataObject(dataObject);
    }
}
