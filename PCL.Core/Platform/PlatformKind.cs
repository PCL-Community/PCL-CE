namespace PCL.Core.Platform;

/// <summary>当前运行平台。仅用于显示、日志和组合根选择，不参与业务分支。</summary>
public enum PlatformKind
{
    Windows,
    Linux,
    MacOs
}