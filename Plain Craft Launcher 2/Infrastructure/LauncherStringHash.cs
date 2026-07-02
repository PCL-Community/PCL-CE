namespace PCL;

/// <summary>
///     PCL2 历史字符串哈希算法。用于保持缓存文件名和登录头像编号等业务值稳定。
/// </summary>
public static class LauncherStringHash
{
    public static ulong Compute(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var hash = value.Aggregate(
            5381UL,
            (current, character) => (current << 5) ^ current ^ character);
        return hash ^ 0xA98F501BC684032FUL;
    }
}