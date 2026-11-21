namespace PCL.Platform;

/// <summary>macOS 运行环境检查：被系统转移（App Translocation）运行的应用位于临时目录下，不视为从压缩包运行。</summary>
public sealed class MacOsRuntimeEnvironmentService : RuntimeEnvironmentServiceBase
{
    protected override bool IsInTempFolder(string executablePath) =>
        !executablePath.Contains("/AppTranslocation/", StringComparison.Ordinal)
        && base.IsInTempFolder(executablePath);
}
