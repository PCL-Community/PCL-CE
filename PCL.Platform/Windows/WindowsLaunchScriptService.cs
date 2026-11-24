using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Windows 启动脚本：批处理文件。</summary>
public sealed class WindowsLaunchScriptService : ILaunchScriptService
{
    public string FileExtension => ".bat";

    public string CreateScript(LaunchScriptOptions options)
    {
        var chcpCommand = options.JavaMajorVersion > 8 ? "chcp 65001>nul\r\n" : "";
        return $"{chcpCommand}@echo off\r\n"
            + $"title {options.TitleText}\r\n"
            + $"echo {options.StartingText}\r\n"
            + $"cd /D \"{options.WorkingDirectory}\"\r\n"
            + $"{options.GlobalCommand}\r\n"
            + $"{options.VersionCommand}\r\n"
            + $"\"{options.JavaExecutablePath}\" {options.Arguments}\r\n"
            + $"echo {options.ExitedText}\r\n"
            + "pause";
    }

    public void MakeExecutable(string path)
    {
    }
}