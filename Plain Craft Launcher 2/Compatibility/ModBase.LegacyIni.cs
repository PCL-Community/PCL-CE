namespace PCL;

public static partial class ModBase
{
    #region LegacyIni

    /// <summary>
    ///     清除某 ini 文件的运行时缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    public static void IniClearCache(string fileName)
    {
        LegacyIniStore.Shared.ClearCache(fileName);
    }

    /// <summary>
    ///     读取 ini 文件。这可能会使用到缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">没有找到键时返回的默认值。</param>
    public static string ReadIni(string fileName, string key, string defaultValue = "")
    {
        return LegacyIniStore.Shared.Read(fileName, key, defaultValue);
    }

    /// <summary>
    ///     判断 ini 文件中是否包含某个键。这可能会使用到缓存。
    /// </summary>
    public static bool HasIniKey(string fileName, string key)
    {
        return LegacyIniStore.Shared.ContainsKey(fileName, key);
    }

    /// <summary>
    ///     从 ini 文件中移除某个键。这会更新缓存。
    /// </summary>
    public static void DeleteIniKey(string fileName, string key)
    {
        LegacyIniStore.Shared.Delete(fileName, key);
    }

    /// <summary>
    ///     写入 ini 文件，这会更新缓存。
    ///     若 Value 为 Nothing，则删除该键。
    /// </summary>
    public static void WriteIni(string fileName, string key, string value)
    {
        LegacyIniStore.Shared.Write(fileName, key, value);
    }

    #endregion
}