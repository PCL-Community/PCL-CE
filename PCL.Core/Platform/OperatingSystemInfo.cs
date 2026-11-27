namespace PCL.Core.Platform;

/// <summary>平台无关的操作系统标识。发行版/产品版本与内核版本分开表示。</summary>
public sealed record OperatingSystemInfo(
    string Name,
    string? Version,
    string? Build,
    string KernelName,
    string KernelVersion);