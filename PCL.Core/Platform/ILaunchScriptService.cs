namespace PCL.Core.Platform;

/// <summary>启动脚本的平台相关生成与权限设置。</summary>
public interface ILaunchScriptService
{
    string FileExtension { get; }

    string CreateScript(LaunchScriptOptions options);

    void MakeExecutable(string path);
}

/// <summary>生成启动脚本所需的业务参数。</summary>
public sealed record LaunchScriptOptions(
    string WorkingDirectory,
    string JavaExecutablePath,
    string Arguments,
    string GlobalCommand,
    string VersionCommand,
    int JavaMajorVersion,
    string TitleText,
    string StartingText,
    string ExitedText);