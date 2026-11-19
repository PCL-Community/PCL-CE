using System.Collections.Generic;
using System.Text;

namespace PCL.Core.Platform;

/// <summary>生成独立 Minecraft 服务端启动脚本的平台服务。</summary>
public interface IServerLaunchScriptService
{
    /// <summary>脚本扩展名，例如 <c>.bat</c>、<c>.sh</c> 或 <c>.command</c>。</summary>
    string FileExtension { get; }

    /// <summary>写入脚本时应使用的文本编码。</summary>
    Encoding ScriptEncoding { get; }

    string CreateScript(ServerLaunchScriptOptions options);

    /// <summary>为 POSIX 脚本设置执行权限；Windows 上无操作。</summary>
    void MakeExecutable(string path);
}

/// <summary>服务端启动脚本的业务参数。</summary>
public sealed record ServerLaunchScriptOptions(
    string ServerJarName,
    string TitleText,
    IReadOnlyList<string> InstructionLines,
    string StoppedText,
    string JavaCommand = "java",
    string JvmArguments = "-server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops");