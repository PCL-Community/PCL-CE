namespace PCL.Platform;

/// <summary>Linux 运行环境检查：从 AppImage 挂载点运行时位于临时目录下，不视为从压缩包运行。</summary>
public sealed class LinuxRuntimeEnvironmentService : RuntimeEnvironmentServiceBase
{
    protected override bool IsInTempFolder(string executablePath)
    {
        var appDir = Environment.GetEnvironmentVariable("APPDIR");
        if (!string.IsNullOrEmpty(appDir)
            && executablePath.StartsWith(
                Path.TrimEndingDirectorySeparator(appDir) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            return false;
        return base.IsInTempFolder(executablePath);
    }
}
