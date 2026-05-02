using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace PCL;

/// <summary>
/// Owns JSON, INI, Base64, XML escaping, and general serialization helpers.
/// </summary>
public static class LauncherSerialization
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> IniCache = new();
    private static readonly object WriteIniLock = new();

    public static void IniClearCache(string fileName)
    {
        fileName = NormalizeIniFileName(fileName);
        if (IniCache.ContainsKey(fileName))
            IniCache.Remove(fileName, out _);
    }

    public static string ReadIni(string fileName, string key, string defaultValue = "")
    {
        var content = IniGetContent(fileName);
        if (content is null || !content.ContainsKey(key))
            return defaultValue;
        return content[key];
    }

    public static bool HasIniKey(string fileName, string key)
    {
        var content = IniGetContent(fileName);
        return content is not null && content.ContainsKey(key);
    }

    public static void DeleteIniKey(string fileName, string key)
    {
        WriteIni(fileName, key, null);
    }

    public static void WriteIni(string fileName, string key, string value)
    {
        try
        {
            if (key.Contains(":"))
                throw new Exception($"尝试写入 ini 文件 {fileName} 的键名中包含了冒号：{key}");
            key = key.Replace("\r", "").Replace("\n", "");
            value = value?.Replace("\r", "").Replace("\n", "");
            lock (WriteIniLock)
            {
                var content = IniGetContent(fileName) ?? new ConcurrentDictionary<string, string>();
                if (value is null)
                {
                    if (!content.ContainsKey(key))
                        return;
                    content.Remove(key, out _);
                }
                else
                {
                    if (content.ContainsKey(key) && (content[key] ?? "") == (value ?? ""))
                        return;
                    content[key] = value;
                }

                var fileContent = new StringBuilder();
                foreach (var pair in content)
                    fileContent.Append(pair.Key).Append(":").Append(pair.Value).Append("\r\n");

                LauncherFileSystem.WriteFile(NormalizeIniFileName(fileName), fileContent.ToString());
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, $"写入文件失败（{fileName} → {key}:{value}）", LauncherLogger.LogLevel.Hint);
        }
    }

    public static object GetJson(string data)
    {
        try
        {
            return JsonConvert.DeserializeObject(data, new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Local });
        }
        catch (Exception)
        {
            var length = (data ?? "").Length;
            throw new Exception("格式化 JSON 失败：" + (length > 2000
                ? data.Substring(0, 500) + $"...(全长 {length} 个字符)..." + Strings.Right(data, 500)
                : data));
        }
    }

    public static string EscapeXml(string str)
    {
        if (str.StartsWithF("{"))
            str = "{}" + str;
        return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;")
            .Replace("\"", "&quot;").Replace("\r\n", "&#xa;");
    }

    public static string Base64Decode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var decodedBytes = Convert.FromBase64String(text);
        return Encoding.UTF8.GetString(decodedBytes);
    }

    public static string Base64Encode(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes);
    }

    public static string Base64Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }

    private static ConcurrentDictionary<string, string> IniGetContent(string fileName)
    {
        try
        {
            fileName = NormalizeIniFileName(fileName);
            if (IniCache.ContainsKey(fileName))
                return IniCache[fileName];
            if (!File.Exists(fileName))
                return null;
            var ini = new ConcurrentDictionary<string, string>();
            foreach (var line in LauncherFileSystem.ReadFile(fileName).Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOfF(":");
                if (index > 0)
                    ini[line.Substring(0, index)] = line.Substring(index + 1);
            }

            IniCache[fileName] = ini;
            return ini;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, $"生成 ini 文件缓存失败（{fileName}）", LauncherLogger.LogLevel.Hint);
            return null;
        }
    }

    private static string NormalizeIniFileName(string fileName)
    {
        return fileName.Contains(@":\") ? fileName : $@"{LauncherPaths.ExecutableDirectory}PCL\{fileName}.ini";
    }
}
