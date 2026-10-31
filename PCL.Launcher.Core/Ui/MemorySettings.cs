namespace PCL;

/// <summary>实例内存设置读取。</summary>
public interface IMemorySettings
{
    double GetRam(McInstance version, bool? is32BitJava = null);
    double? GetInitialRam(McInstance version, bool? is32BitJava = null);
}

public static class MemorySettings
{
    public static IMemorySettings? Current { get; set; }

    public static double GetRam(McInstance version, bool? is32BitJava = null)
    {
        return Current?.GetRam(version, is32BitJava) ?? 0;
    }

    public static double? GetInitialRam(McInstance version, bool? is32BitJava = null)
    {
        return Current?.GetInitialRam(version, is32BitJava);
    }
}