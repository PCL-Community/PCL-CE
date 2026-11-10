namespace PCL;

/// <summary>将实例内存设置桥接到实例设置页。</summary>
public sealed class WpfMemorySettings : IMemorySettings
{
    public double GetRam(McInstance version, bool? is32BitJava = null)
    {
        return PageInstanceSetup.GetRam(version, is32BitJava);
    }

    public double? GetInitialRam(McInstance version, bool? is32BitJava = null)
    {
        return PageInstanceSetup.GetInitialRam(version, is32BitJava);
    }
}