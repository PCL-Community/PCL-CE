using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PCL.Core.IO;

/// <summary>简单的 <c>key:value</c> 文本配置读写。</summary>
public static class Ini
{
    public static string Read(string fileName, string key, string defaultValue = "")
    {
        try
        {
            if (!File.Exists(fileName)) return defaultValue;
            foreach (var line in File.ReadAllLines(fileName))
            {
                var index = line.IndexOf(':');
                if (index > 0 && line[..index] == key)
                    return line[(index + 1)..];
            }
        }
        catch
        {
            // 读取失败时回退默认值。
        }

        return defaultValue;
    }

    public static void Write(string fileName, string key, string? value)
    {
        try
        {
            var content = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(fileName))
                foreach (var line in File.ReadAllLines(fileName))
                {
                    var index = line.IndexOf(':');
                    if (index > 0) content[line[..index]] = line[(index + 1)..];
                }

            if (value is null) content.Remove(key);
            else content[key] = value;

            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllLines(
                fileName,
                content.Select(pair => $"{pair.Key}:{pair.Value}"),
                Encoding.UTF8);
        }
        catch
        {
            // 写入失败不应阻断调用方流程。
        }
    }
}