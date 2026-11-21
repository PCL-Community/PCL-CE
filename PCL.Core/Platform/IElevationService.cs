using System.Diagnostics;

namespace PCL.Core.Platform;

/// <summary>平台提权能力。业务层通过该服务判断和启动提权进程。</summary>
public interface IElevationService
{
    /// <summary>当前进程是否已经具有平台管理员/root 权限。</summary>
    bool IsElevated { get; }

    /// <summary>以平台提权方式启动进程；用户取消或平台不支持时返回 <see langword="null" />。</summary>
    Process? StartElevated(string fileName, string? arguments = null);
}