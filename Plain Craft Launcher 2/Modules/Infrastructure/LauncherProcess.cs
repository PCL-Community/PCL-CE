using System.Diagnostics;
using System.IO;
using System.Windows;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     进程、网页、资源管理器与剪贴板操作。
/// </summary>
public static class LauncherProcess
{
    public static void ShellOnly(string fileName, string arguments = "")
    {
        try
        {
            fileName = PathUtils.ShortenPath(fileName);

            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            program.StartInfo.UseShellExecute = true;

            LauncherLog.Log($"[System] 执行外部命令：{fileName} {arguments}");
            program.Start();
        }
        catch (Exception ex)
        {
            LauncherLog.Log(
                ex,
                $"打开文件或程序失败：{fileName}",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("SystemDialog.File.OpenFailed.Message", fileName));
        }
    }

    public static LauncherExitCode ShellAndGetExitCode(
        string fileName,
        string arguments = "",
        int timeout = 1000000)
    {
        try
        {
            LauncherLog.Log($"[System] 执行外部命令并等待返回码：{fileName} {arguments}");

            var result = ProcessRunner
                .CaptureAsync(fileName, arguments, timeout)
                .GetAwaiter()
                .GetResult();

            if (result.TimedOut)
                return LauncherExitCode.Timeout;

            return result.ExitCode.HasValue
                ? (LauncherExitCode)result.ExitCode.Value
                : LauncherExitCode.Fail;
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, $"执行命令失败：{fileName}", LauncherLogLevel.Msgbox);
            return LauncherExitCode.Fail;
        }
    }

    public static string ShellAndGetOutput(
        string fileName,
        string arguments = "",
        int timeout = 1000000,
        string? workingDirectory = null)
    {
        LauncherLog.Log($"[System] 执行外部命令并等待返回结果：{fileName} {arguments}");

        var result = ProcessRunner
            .CaptureAsync(fileName, arguments, timeout, workingDirectory)
            .GetAwaiter()
            .GetResult();

        return result.CombinedOutput;
    }

    public static void OpenWebsite(string url)
    {
        try
        {
            if (!url.StartsWithF("http", true) &&
                !url.StartsWithF("minecraft://", true))
                throw new Exception($"{url} 不是一个有效的网址，它必须以 http 开头！");

            LauncherLog.Log($"[System] 正在打开网页：{url}");

            var psi = new ProcessStartInfo(url)
            {
                UseShellExecute = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, $"无法打开网页（{url}）");

            ClipboardSet(url, false);

            var message = ExceptionDetails.Compose(
                Lang.Text("SystemDialog.Browser.OpenFailed.Message", url),
                ex);

            ModMain.MyMsgBox(
                message,
                Lang.Text("SystemDialog.Browser.OpenFailed.Title"));
        }
    }

    public static void OpenExplorer(string location)
    {
        try
        {
            location = PathUtils.ShortenPath(location.Replace("/", @"\").Trim(' ', '"'));
            LauncherLog.Log($"[System] 正在打开资源管理器：{location}");

            if (location.EndsWithF(@"\"))
                ShellOnly(location);
            else
                ShellOnly("explorer", $"/select,\"{location}\"");
        }
        catch (Exception ex)
        {
            LauncherLog.Log(
                ex,
                "打开资源管理器失败，请尝试关闭安全软件（如 360 安全卫士）",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("SystemDialog.Folder.OpenFailed.Message", location));
        }
    }

    public static void ClipboardSet(string text, bool showSuccessHint = true)
    {
        UiThread.RunInThread(() =>
        {
            var success = false;

            for (var attempt = 0; attempt <= 5; attempt++)
                try
                {
                    UiThread.Invoke(() => Clipboard.SetText(text));
                    success = true;
                    break;
                }
                catch (Exception) when (attempt < 5)
                {
                    Thread.Sleep(20);
                }
                catch (Exception finalEx)
                {
                    LauncherLog.Log(
                        finalEx,
                        "剪贴板被占用，文本复制失败",
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text("Common.Hint.CopyFailed"));
                }

            if (success && showSuccessHint)
                UiThread.Post(() =>
                    HintService.Hint(
                        Lang.Text("Common.Hint.Copied"),
                        HintType.Success));
        });
    }

    public static int PasteFileFromClipboard(
        string dest,
        bool copyFile = true,
        bool copyDir = true)
    {
        LauncherLog.Log($"[System] 从剪贴板粘贴文件到：{dest}");

        try
        {
            var files = UiThread.Invoke(() => Clipboard.GetFileDropList());

            if (files.Count.Equals(0))
            {
                LauncherLog.Log("[System] 剪贴板内无文件可粘贴");
                return 0;
            }

            var copiedFiles = 0;
            var copiedFolders = 0;

            foreach (var i in files)
            {
                if (copyFile && File.Exists(i))
                    try
                    {
                        var thisDest = dest + PathUtils.GetFileNameFromUrlOrPath(i);

                        if (File.Exists(thisDest))
                        {
                            LauncherLog.Log($"[System] 已存在同名文件：{thisDest}");
                        }
                        else
                        {
                            File.Copy(i, thisDest);
                            copiedFiles += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        LauncherLog.Log(ex, "[System] 复制文件时出错");
                        continue;
                    }

                if (copyDir && Directory.Exists(i))
                    try
                    {
                        var thisDest = dest + PathUtils.GetDirectoryNameLeaf(i);

                        if (Directory.Exists(thisDest))
                        {
                            LauncherLog.Log($"[System] 已存在同名文件夹：{thisDest}");
                        }
                        else
                        {
                            Directories
                                .CopyDirectoryAsync(i, thisDest)
                                .GetAwaiter()
                                .GetResult();

                            copiedFolders += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        LauncherLog.Log(ex, "[System] 复制文件时出错");
                    }
            }

            HintService.Hint(
                Lang.Text("Common.Hint.FilesPasted", copiedFiles, copiedFolders));
            return copiedFiles + copiedFolders;
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[System] 从剪切板粘贴文件失败", LauncherLogLevel.Hint);
            return 0;
        }
    }
}