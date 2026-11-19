using System;
using System.Diagnostics;

namespace PCL.Core.Platform;

/// <summary>为游戏进程准备平台相关的环境变量。</summary>
public interface ILaunchEnvironmentService
{
    /// <summary>当前平台是否通过进程环境变量配置游戏启动。</summary>
    bool IsSupported { get; }

    /// <summary>请求游戏进程使用独立显卡。Windows 通过注册表配置，Linux 通过 PRIME 环境变量。</summary>
    void ApplyGpuPreference(ProcessStartInfo startInfo, bool enabled, Action<string> log);

    /// <summary>应用渲染器选择。编号与配置中的渲染器值一致，0 表示默认。</summary>
    void ApplyRenderer(ProcessStartInfo startInfo, int renderer, Action<string> log);
}
