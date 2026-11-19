using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Unix 启动脚本：POSIX shell 文件。</summary>
public sealed class PosixLaunchScriptService : ILaunchScriptService
{
    public string FileExtension => ".sh";

    public string CreateScript(LaunchScriptOptions options)
    {
        return "#!/bin/sh\n"
            + $"echo {options.StartingText}\n"
            + $"cd \"{options.WorkingDirectory}\"\n"
            + $"{options.GlobalCommand}\n"
            + $"{options.VersionCommand}\n"
            + $"\"{options.JavaExecutablePath}\" {options.Arguments}\n"
            + $"echo {options.ExitedText}\n";
    }

    public void MakeExecutable(string path)
    {
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute);
    }
}