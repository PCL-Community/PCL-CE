using PCL.Core.Platform;

namespace PCL;

/// <summary>实例内存设置读取。</summary>
public interface IMemorySettings
{
    double GetRam(McInstance version, bool? is32BitJava = null);
    double? GetInitialRam(McInstance version, bool? is32BitJava = null);
}

public static class MemorySettings
{
    public static IMemorySettings? Current => ApplicationRuntime.GetService<IMemorySettings>();

    public static double GetRam(McInstance version, bool? is32BitJava = null)
    {
        return ApplicationRuntime
            .GetRequiredService<IMemorySettings>()
            .GetRam(version, is32BitJava);
    }

    public static double? GetInitialRam(McInstance version, bool? is32BitJava = null)
    {
        return ApplicationRuntime
            .GetRequiredService<IMemorySettings>()
            .GetInitialRam(version, is32BitJava);
    }
}