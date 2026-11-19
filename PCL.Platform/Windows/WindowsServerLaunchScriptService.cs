using System.Text;
using PCL.Core.Platform;
using PCL.Core.Utils.Codecs;

namespace PCL.Platform;

/// <summary>Windows 服务端启动脚本：批处理文件。</summary>
public sealed class WindowsServerLaunchScriptService : IServerLaunchScriptService
{
    public string FileExtension => ".bat";

    public Encoding ScriptEncoding =>
        Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encodings.GB18030;

    public string CreateScript(ServerLaunchScriptOptions options)
    {
        List<string> lines =
        [
            "@echo off",
            $"title {options.TitleText}",
            .. options.InstructionLines.Select(line => $"echo {line}"),
            "echo ------------------------------",
            "cd /D \"%~dp0\"",
            $"\"{options.JavaCommand}\" {options.JvmArguments} -jar \"{options.ServerJarName}\" nogui",
            "echo ----------------------",
            $"echo {options.StoppedText}",
            "pause"
        ];
        return string.Join("\r\n", lines);
    }

    public void MakeExecutable(string path)
    {
        // no-op
    }
}