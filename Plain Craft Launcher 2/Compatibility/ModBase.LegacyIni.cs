using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace PCL;

public static partial class ModBase
{
    #region LegacyIni

    // =============================
    // ini
    // =============================

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> iniCache = new();

    /// <summary>
    ///     清除某 ini 文件的运行时缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    public static void IniClearCache(string fileName)
    {
        if (!fileName.Contains(@":\"))
            fileName = $@"{exePath}PCL\{fileName}.ini";
        iniCache.Remove(fileName, out _);
    }

    /// <summary>
    ///     获取 ini 文件缓存。如果没有，则新读取 ini 文件内容。
    ///     在文件不存在或读取失败时返回 Nothing。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    private static ConcurrentDictionary<string, string> IniGetContent(string fileName)
    {
        try
        {
            // 还原文件路径
            if (!fileName.Contains(@":\"))
                fileName = $@"{exePath}PCL\{fileName}.ini";
            // 检索缓存
            if (iniCache.TryGetValue(fileName, out var value))
                return value;
            // 读取文件
            if (!File.Exists(fileName))
                return null;
            var ini = new ConcurrentDictionary<string, string>();
            foreach (var line in ReadFile(fileName)
                         .Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOfF(":");
                if (index > 0)
                    ini[line[..index]] = line[(index + 1)..]; // 可能会有重复键，见 #3616
            }

            iniCache[fileName] = ini;
            return ini;
        }
        catch (Exception ex)
        {
            Log(ex, $"生成 ini 文件缓存失败（{fileName}）", LogLevel.Hint);
            return null;
        }
    }

    /// <summary>
    ///     读取 ini 文件。这可能会使用到缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">没有找到键时返回的默认值。</param>
    public static string ReadIni(string fileName, string key, string defaultValue = "")
    {
        var content = IniGetContent(fileName);
        if (content is null || !content.TryGetValue(key, out var value))
            return defaultValue;
        return value;
    }

    /// <summary>
    ///     判断 ini 文件中是否包含某个键。这可能会使用到缓存。
    /// </summary>
    public static bool HasIniKey(string fileName, string key)
    {
        var content = IniGetContent(fileName);
        return content is not null && content.ContainsKey(key);
    }

    /// <summary>
    ///     从 ini 文件中移除某个键。这会更新缓存。
    /// </summary>
    public static void DeleteIniKey(string fileName, string key)
    {
        WriteIni(fileName, key, null);
    }

    /// <summary>
    ///     写入 ini 文件，这会更新缓存。
    ///     若 Value 为 Nothing，则删除该键。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    /// <param name="key">键。</param>
    /// <param name="value">值。</param>
    /// <remarks></remarks>
    public static void WriteIni(string fileName, string key, string value)
    {
        try
        {
            // 预处理
            if (key.Contains(':'))
                throw new Exception($"尝试写入 ini 文件 {fileName} 的键名中包含了冒号：{key}");
            key = key.Replace("\r", "").Replace("\n", "");
            value = value?.Replace("\r", "").Replace("\n", "");
            // 防止争用
            lock (writeIniLock)
            {
                // 获取目前文件
                var content = IniGetContent(fileName)
                              ?? new ConcurrentDictionary<string, string>();
                // 更新值
                if (value is null)
                {
                    if (!content.ContainsKey(key))
                        return; // 无需处理
                    content.Remove(key, out _);
                }
                else
                {
                    if (content.TryGetValue(key, out var value1) && (value1 ?? "") == (value ?? ""))
                        return; // 无需处理
                    content[key] = value;
                }

                // 写入文件
                var fileContent = new StringBuilder();
                foreach (var pair in content)
                {
                    fileContent.Append(pair.Key);
                    fileContent.Append(':');
                    fileContent.Append(pair.Value);
                    fileContent.Append("\r\n");
                }

                if (!fileName.Contains(@":\"))
                    fileName = $@"{exePath}PCL\{fileName}.ini";
                WriteFile(fileName, fileContent.ToString());
            }
        }
        catch (Exception ex)
        {
            Log(ex, $"写入文件失败（{fileName} → {key}:{value}）", LogLevel.Hint);
        }
    }

    private static readonly object writeIniLock = new();

    #endregion
}