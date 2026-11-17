using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Windows 自定义命令通过 cmd.exe 执行。</summary>
public sealed class WindowsShellCommandService : IShellCommandService
{
    public ProcessStartInfo CreateCommand(string command, string workingDirectory)
    {
        return new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c \"{command}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}