using System.Diagnostics;

namespace PCL.Core.Platform;

/// <summary>平台 Shell 命令的进程启动参数构造。</summary>
public interface IShellCommandService
{
    ProcessStartInfo CreateCommand(string command, string workingDirectory);
}