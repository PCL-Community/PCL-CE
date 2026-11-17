using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Unix 自定义命令通过 /bin/sh 执行。</summary>
public sealed class PosixShellCommandService : IShellCommandService
{
    public ProcessStartInfo CreateCommand(string command, string workingDirectory)
    {
        return new ProcessStartInfo("/bin/sh")
        {
            Arguments = $"-c \"{command}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}