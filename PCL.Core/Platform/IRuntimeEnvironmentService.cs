using System.Collections.Generic;

namespace PCL.Core.Platform;

/// <summary>启动器运行环境中可能导致问题的情况。</summary>
public enum RuntimeEnvironmentIssue
{
    /// <summary>操作系统版本低于推荐要求。</summary>
    OutdatedSystem,

    /// <summary>32 位操作系统。</summary>
    System32Bit,

    /// <summary>启动器位于临时目录，通常是直接从压缩包中运行。</summary>
    TempFolder,

    /// <summary>启动器位于 QQ、微信、TIM 等社交软件的下载目录。</summary>
    SocialSoftwareFolder
}

/// <summary>检查启动器的运行环境。业务代码通过该服务获取环境问题，不直接判断平台。</summary>
public interface IRuntimeEnvironmentService
{
    /// <summary>检查当前运行环境，返回发现的问题；没有问题时返回空列表。</summary>
    IReadOnlyList<RuntimeEnvironmentIssue> GetIssues(string executablePath);
}
