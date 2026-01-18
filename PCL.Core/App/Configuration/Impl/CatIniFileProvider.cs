using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Configuration.Impl;

/// <summary>
/// 提供 LTCat-style ini 格式的键值文件读写。
/// </summary>
public class CatIniFileProvider : CommonFileProvider, IEnumerableKeyProvider
{
    private readonly Dictionary<string, string> _dict = [];

    public CatIniFileProvider(string path) : base(path)
    {
        if (!File.Exists(path)) return;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.IsNullOrWhiteSpace()) continue;
            var split = line.Split(':', 2);
            _dict[split[0]] = split[1];
        }
    }

    public IEnumerable<string> Keys => _dict.Keys;

    public override T Get<T>(string key)
    {
        if (!_dict.TryGetValue(key, out var value)) throw new KeyNotFoundException($"Not found: '{key}");
        return value.Convert<T>() ?? throw new NullReferenceException();
    }

    public override void Set<T>(string key, T value)
        => _dict[key] = value.ConvertToString() ?? throw new NullReferenceException();

    public override bool Exists(string key) => _dict.ContainsKey(key);

    public override void Remove(string key) => _dict.Remove(key);

    protected override void WriteToStream(Stream stream)
    {
        var writer = new StreamWriter(stream, Encoding.UTF8);
        foreach (var (key, value) in _dict)
        {
            var keyStr = key.ReplaceLineBreak();
            var valueStr = value.ReplaceLineBreak();
            writer.WriteLine($"{keyStr}:{valueStr}");
        }
        writer.Flush();
    }
}
