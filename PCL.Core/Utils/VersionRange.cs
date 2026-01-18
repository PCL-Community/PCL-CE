using System;

namespace PCL.Core.Utils;

public class VersionRange(Version? minVersion, Version? maxVersion)
{
    public Version? MinVersion { get; set; } = minVersion;
    
    public Version? MaxVersion { get; set; } = maxVersion;

    public bool IsInRange(Version target) => (MinVersion != null || MaxVersion != null)
                                             && (MinVersion ?? target) <= target
                                             && (MaxVersion ?? target) >= target;
    
    /// <summary>
    /// 缩减版本号范围，如果 <paramref name="target"/> 比当 <see cref="MinVersion"/> 大则应用
    /// </summary>
    /// <param name="target">目标版本号</param>
    /// <returns>是否使用</returns>
    public bool SetMin(Version target) => (MinVersion = (MinVersion == null || target > MinVersion) ? target : MinVersion) == target;
    
    /// <summary>
    /// 缩减版本号范围，如果 <paramref name="target"/> 比当 <see cref="MaxVersion"/> 小则应用
    /// </summary>
    /// <param name="target">目标版本号</param>
    /// <returns>是否使用</returns>
    public bool SetMax(Version target) => (MaxVersion = (MaxVersion == null || target < MaxVersion) ? target : MaxVersion) == target;
}
