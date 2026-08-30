using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace PCL;

public interface ILauncherKeyValueStore
{
    string Read(string fileName, string key, string defaultValue = "");

    bool ContainsKey(string fileName, string key);

    void Write(string fileName, string key, string? value);

    void ClearCache(string fileName);
}

/// <summary>
///     PCL2 专属 “key:value” ini 文件格式的读写与缓存。
/// </summary>
public sealed class LauncherIniStore : ILauncherKeyValueStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _cache = new();
    private readonly Lock _writeLock = new();

    private LauncherIniStore()
    {
    }

    public static LauncherIniStore Shared { get; } = new();

    public void ClearCache(string fileName)
    {
        _cache.Remove(LauncherPaths.ResolveLauncherIniPath(fileName), out _);
    }

    public string Read(string fileName, string key, string defaultValue = "")
    {
        var content = GetContent(fileName);

        if (content is null || !content.TryGetValue(key, out var value))
            return defaultValue;

        return value;
    }

    public bool ContainsKey(string fileName, string key)
    {
        var content = GetContent(fileName);
        return content is not null && content.ContainsKey(key);
    }

    public void Write(string fileName, string key, string? value)
    {
        try
        {
            if (key.Contains(':'))
                throw new Exception($"尝试写入 ini 文件 {fileName} 的键名中包含了冒号：{key}");

            key = key.Replace("\r", "").Replace("\n", "");
            value = value?.Replace("\r", "").Replace("\n", "");

            lock (_writeLock)
            {
                var resolvedPath = LauncherPaths.ResolveLauncherIniPath(fileName);
                var content = GetContent(fileName);
                if (content is null)
                {
                    content = new ConcurrentDictionary<string, string>();
                    _cache[resolvedPath] = content;
                }

                if (value is null)
                {
                    if (!content.ContainsKey(key))
                        return;

                    content.Remove(key, out _);
                }
                else
                {
                    if (content.TryGetValue(key, out var oldValue) &&
                        (oldValue ?? "") == (value ?? ""))
                        return;

                    content[key] = value;
                }

                var fileContent = new StringBuilder();

                foreach (var pair in content)
                {
                    fileContent.Append(pair.Key);
                    fileContent.Append(':');
                    fileContent.Append(pair.Value);
                    fileContent.Append("\r\n");
                }

                Files
                    .WriteFileAsync(
                        resolvedPath,
                        fileContent.ToString())
                    .GetAwaiter()
                    .GetResult();
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Log(
                ex,
                $"写入文件失败（{fileName} → {key}:{value}）",
                LauncherLogLevel.Hint);
        }
    }

    public void Delete(string fileName, string key)
    {
        Write(fileName, key, null);
    }

    private ConcurrentDictionary<string, string>? GetContent(string fileName)
    {
        try
        {
            var resolvedPath = LauncherPaths.ResolveLauncherIniPath(fileName);

            if (_cache.TryGetValue(resolvedPath, out var cached))
                return cached;

            if (!File.Exists(resolvedPath))
                return null;

            var ini = new ConcurrentDictionary<string, string>();

            foreach (var line in Files
                         .ReadAllTextOrEmptyAsync(resolvedPath)
                         .GetAwaiter()
                         .GetResult()
                         .Split([.. "\r\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOfF(":");

                if (index > 0)
                    ini[line[..index]] = line[(index + 1)..];
            }

            _cache[resolvedPath] = ini;
            return ini;
        }
        catch (Exception ex)
        {
            LauncherLog.Log(
                ex,
                $"生成 ini 文件缓存失败（{fileName}）",
                LauncherLogLevel.Hint);

            return null;
        }
    }
}