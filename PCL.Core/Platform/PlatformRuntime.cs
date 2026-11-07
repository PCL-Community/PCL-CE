using System;

namespace PCL.Core.Platform;

/// <summary>当前平台模块。必须在 UI 初始化前由组合根填充。</summary>
public static class PlatformRuntime
{
    private static PlatformModule? _current;

    public static PlatformModule Current =>
        _current ?? throw new InvalidOperationException("平台模块尚未初始化");

    public static bool IsInitialized => _current is not null;

    public static void Initialize(PlatformModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _current = module;
    }
}