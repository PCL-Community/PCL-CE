using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Essentials;

public static class StartupValidation
{
    /// <summary>
    ///     确保 WPF 字体渲染环境正常（修复缺失 %windir% 环境变量导致的字体渲染异常 #3555）
    /// </summary>
    public static void EnsureWpfFont()
    {
        try
        {
            _ = new FormattedText("", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Fonts.SystemTypefaces.First(), 96d, Brushes.Black, 96d);
        }
        catch (UriFormatException)
        {
            Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"),
                EnvironmentVariableTarget.User);
            _ = new FormattedText("", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Fonts.SystemTypefaces.First(), 96d, Brushes.Black, 96d);
        }
    }

    /// <summary>
    ///     检测当前文件夹权限，若不满足则弹窗提示并退出进程
    /// </summary>
    public static void EnsureFolderPermission()
    {
        var dataPath = Paths.Data;
        try
        {
            Directory.CreateDirectory(dataPath);
        }
        catch
        {
            ShowPermissionError(dataPath);
            Environment.Exit(1);
        }

        if (!CheckWritePermission(dataPath))
        {
            ShowPermissionError(dataPath);
            Environment.Exit(1);
        }
    }

    private static bool CheckWritePermission(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.EndsWithF(@"\"))
                path += @"\";
            if (path.EndsWithF(@":\System Volume Information\") || path.EndsWithF(@":\$RECYCLE.BIN\"))
                return false;
            if (!Directory.Exists(path))
                return false;
            var fileName = "CheckPermission" + Guid.NewGuid().ToString("N")[..8];
            if (File.Exists(path + fileName))
                File.Delete(path + fileName);
            File.Create(path + fileName).Dispose();
            File.Delete(path + fileName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowPermissionError(string dataPath)
    {
        var isC = Basics.ExecutableDirectory.StartsWithF("C:", true);
        var suggestion = isC ? "，例如 C 盘和桌面以外的其他位置。" : "。";
        MessageBox.Show(
            $$"""
              PCL 无法创建 PCL 文件夹（{{dataPath}}），请尝试：
              1. 将 PCL 移动到其他文件夹{{suggestion}}
              2. 删除当前目录中的 PCL 文件夹，然后再试。
              3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。
              """,
            "运行环境错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
