using System.Text;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>POSIX 服务端启动脚本。macOS 使用 .command 以支持 Finder 双击。</summary>
public sealed class PosixServerLaunchScriptService(string fileExtension)
    : IServerLaunchScriptService
{
    public string FileExtension { get; } = fileExtension;

    public Encoding ScriptEncoding => Encoding.UTF8;

    public string CreateScript(ServerLaunchScriptOptions options)
    {
        List<string> lines =
        [
            "#!/bin/sh",
            "cd \"$(dirname \"$0\")\"",
            .. options.InstructionLines.Select(line => $"echo {line}"),
            "echo ------------------------------",
            "JAVA_BIN=\"${JAVA_HOME:+$JAVA_HOME/bin/}java\"",
            $"\"$JAVA_BIN\" {options.JvmArguments} -jar \"{options.ServerJarName}\" nogui",
            "echo ----------------------",
            $"echo {options.StoppedText}",
            "printf 'Press Enter to exit...'",
            "read -r _"
        ];
        return string.Join("\n", lines);
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