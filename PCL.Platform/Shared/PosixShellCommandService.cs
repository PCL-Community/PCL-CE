using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Unix 自定义命令通过 /bin/sh 执行。</summary>
public sealed class PosixShellCommandService : IShellCommandService
{
    public ProcessStartInfo CreateCommand(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }
}