namespace PCL.Core.Platform;

/// <summary>旧版平台模块访问入口。仅作为迁移期的只读兼容门面； 新代码请使用 <see cref="ApplicationRuntime" />。</summary>
public static class PlatformRuntime
{
    public static PlatformModule Current => ApplicationRuntime.Current.Platform;

    public static bool IsInitialized => ApplicationRuntime.IsInitialized;
}