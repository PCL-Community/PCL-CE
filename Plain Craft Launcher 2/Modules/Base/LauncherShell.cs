using System.Diagnostics;

namespace PCL;

/// <summary>
/// Owns shell execution, process output capture, exit-code capture, and explorer/browser launch helpers.
/// </summary>
public static class LauncherShell
{
    public static void OpenWebsite(string url)
    {
        try
        {
            if (!url.StartsWithF("http", true) && !url.StartsWithF("minecraft://", true))
                throw new Exception(url + " 不是一个有效的网址，它必须以 http 开头！");
            LauncherLogger.Log("[System] 正在打开网页：" + url);
            var psi = new ProcessStartInfo(url) { UseShellExecute = true };
            _ = Task.Run(() => Process.Start(psi));
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "无法打开网页（" + url + "）");
            LauncherClipboard.ClipboardSet(url, false);
            LauncherFeedback.ShowMessage("可能由于浏览器未正确配置，PCL 无法为你打开网页。" + "\r\n" +
                                         "网址已经复制到剪贴板，若有需要可以手动粘贴访问。" + "\r\n" + $"网址：{url}",
                "无法打开网页");
        }
    }

    public static void OpenExplorer(string location)
    {
        try
        {
            location = LauncherPaths.ShortenPath(location.Replace("/", @"\").Trim(' ', '"'));
            LauncherLogger.Log("[System] 正在打开资源管理器：" + location);
            if (location.EndsWithF(@"\"))
                ShellOnly(location);
            else
                ShellOnly("explorer", $"/select,\"{location}\"");
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "打开资源管理器失败，请尝试关闭安全软件（如 360 安全卫士）", LauncherLogger.LogLevel.Msgbox);
        }
    }

    public static void ShellOnly(string fileName, string arguments = "")
    {
        try
        {
            fileName = LauncherPaths.ShortenPath(fileName);
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            program.StartInfo.UseShellExecute = true;
            LauncherLogger.Log("[System] 执行外部命令：" + fileName + " " + arguments);
            program.Start();
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "打开文件或程序失败：" + fileName, LauncherLogger.LogLevel.Msgbox);
        }
    }

    public static ProcessReturnValues ShellAndGetExitCode(string fileName, string arguments = "", int timeout = 1000000)
    {
        try
        {
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            LauncherLogger.Log("[System] 执行外部命令并等待返回码：" + fileName + " " + arguments);
            program.Start();
            if (program.WaitForExit(timeout)) return (ProcessReturnValues)program.ExitCode;
            return ProcessReturnValues.Timeout;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "执行命令失败：" + fileName, LauncherLogger.LogLevel.Msgbox);
            return ProcessReturnValues.Fail;
        }
    }

    public static string ShellAndGetOutput(string fileName, string arguments = "", int timeout = 1000000, string workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory.TrimEnd('\\');
        LauncherLogger.Log("[System] 执行外部命令并等待返回结果：" + fileName + " " + arguments);
        using var program = new Process { StartInfo = info };
        program.Start();
        var outputTask = program.StandardOutput.ReadToEndAsync();
        var errorTask = program.StandardError.ReadToEndAsync();
        if (program.WaitForExit(timeout))
            Task.WaitAll(outputTask, errorTask);
        else
        {
            program.Kill();
            Task.WaitAll(outputTask, errorTask);
        }
        return outputTask.Result + errorTask.Result;
    }

    public static void CreateSymbolicLink(string linkPath, string targetPath, int flags)
    {
        using var cmdProcess = new Process();
        var linkDPath = ModLaunch.ExtractLinkD();
        cmdProcess.StartInfo.FileName = linkDPath;
        cmdProcess.StartInfo.Arguments = $"\"{linkPath}\" \"{targetPath}\"";
        cmdProcess.StartInfo.CreateNoWindow = true;
        cmdProcess.StartInfo.UseShellExecute = false;
        cmdProcess.Start();
        while (!cmdProcess.HasExited)
        {
        }
    }
}
