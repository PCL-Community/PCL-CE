using System.Diagnostics;

namespace PCL.Core.Platform;

/// <summary>普通进程启动能力，供业务流程避免直接依赖平台进程 API。</summary>
public interface IProcessLauncher
{
    /// <summary>启动进程；无法启动时返回 <see langword="null" />，由调用方记录诊断信息。</summary>
    Process? Start(string fileName, string? arguments = null);
}