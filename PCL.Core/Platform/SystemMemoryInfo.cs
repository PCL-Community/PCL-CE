namespace PCL.Core.Platform;

/// <summary>系统物理内存信息，单位为字节。</summary>
public readonly record struct SystemMemoryInfo(ulong TotalBytes, ulong AvailableBytes);