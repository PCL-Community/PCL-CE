using PCL.Core.App;
using PCL.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO;

/// <summary>
/// 高性能、线程安全的 INI 文件读写器。<br/>
/// 每个实例对应一个物理文件，内部使用缓存和异步 I/O。<br/>
/// 相同路径的实例可安全共享：内部通过锁序列化写入。
/// </summary>
public sealed class IniFile
{

    /// <summary>
    /// 根据文件名或完整路径打开（或创建）一个 IniFile 实例。<br/>
    /// 短文件名（不含 \ 或 /）会解析至 {ExecutableDirectory}\PCL\{name}.ini。
    /// </summary>
    /// <param name="fileName">文件名或完整路径。短名无需 .ini 后缀。</param>
    /// <param name="baseDirectory">短名解析的基础目录。为 <see langword="null"/> 时使用可执行文件目录。</param>
    public static IniFile Open(string fileName, string? baseDirectory = null)
    {
        var path = ResolvePath(fileName, baseDirectory);
        return _Instances.GetOrAdd(path, static p => new IniFile(p));
    }

    private static string ResolvePath(string fileName, string? baseDirectory)
    {
        if (fileName.AsSpan().IndexOfAny('\\', '/', ':') >= 0)
            return fileName;

        var dir = baseDirectory ?? Path.Combine(Basics.ExecutableDirectory, "PCL");
        return fileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(dir, fileName)
            : Path.Combine(dir, fileName + ".ini");
    }

    private static readonly ConcurrentDictionary<string, IniFile> _Instances =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private ConcurrentDictionary<string, string>? _items;

    private IniFile(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// 文件完整路径。
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// 清除内存缓存。下次读取时将从磁盘重新加载。
    /// </summary>
    public void Invalidate()
    {
        Volatile.Write(ref _items, null);
    }

    /// <summary>
    /// 确保缓存已加载（若无则从磁盘读取）。
    /// 读取操作无需锁，但触发首次加载时会串行化写锁。
    /// </summary>
    private async ValueTask<ConcurrentDictionary<string, string>> _EnsureLoadedAsync()
    {
        var cache = Volatile.Read(ref _items);
        if (cache is not null)
            return cache;

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            cache = Volatile.Read(ref _items);
            if (cache is not null)
                return cache;

            cache = await _LoadFromDiskAsync().ConfigureAwait(false);
            Volatile.Write(ref _items, cache);
            return cache;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<ConcurrentDictionary<string, string>> _LoadFromDiskAsync()
    {
        var dict = new ConcurrentDictionary<string, string>();
        if (!File.Exists(_filePath))
            return dict;

        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                    continue;

                var colon = line.AsSpan().IndexOf(':');
                if (colon > 0)
                {
                    var key = line[..colon];
                    var value = line[(colon + 1)..];
                    dict[new string(key)] = new string(value);
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"Failed to load INI: {_filePath}");
        }

        return dict;
    }


    /// <summary>
    /// 读取键值。若键不存在或文件未加载则返回 <paramref name="defaultValue"/>。
    /// </summary>
    public async ValueTask<string?> ReadAsync(string key, string? defaultValue = null)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;

        var items = await _EnsureLoadedAsync().ConfigureAwait(false);
        return items.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 同步读取键值。（用于低竞争或已知已加载的场景）
    /// </summary>
    public string? Read(string key, string? defaultValue = null)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;

        var items = Volatile.Read(ref _items);
        if (items is null)
        {
            _EnsureLoadedAsync().AsTask().GetAwaiter().GetResult();
            items = Volatile.Read(ref _items);
        }

        return items is not null && items.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 判定键是否存在。
    /// </summary>
    public async ValueTask<bool> ContainsKeyAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        var items = await _EnsureLoadedAsync().ConfigureAwait(false);
        return items.ContainsKey(key);
    }

    /// <summary>
    /// 写入键值。若 <paramref name="value"/> 为 <see langword="null"/>，则删除该键。
    /// 相等写入（值未变）会自动跳过磁盘 I/O。
    /// </summary>
    public async ValueTask WriteAsync(string key, string? value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (key.Contains(':'))
            throw new ArgumentException("INI key cannot contain colon (:)", nameof(key));

        key = key.ReplaceLineEndings(string.Empty);
        value = value?.ReplaceLineEndings(string.Empty);

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var items = Volatile.Read(ref _items) ?? await _LoadFromDiskAsync().ConfigureAwait(false);

            if (value is null)
            {
                if (!items.ContainsKey(key))
                    return;
                items.TryRemove(key, out _);
            }
            else
            {
                if (items.TryGetValue(key, out var existing) && existing == value)
                    return;
                items[key] = value;
            }

            Volatile.Write(ref _items, items);
            await _FlushToDiskAsync(items).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 同步写入。（内部执行异步 IO）
    /// </summary>
    public void Write(string key, string? value)
        => WriteAsync(key, value).AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// 异步删除键。（等效于 <c>WriteAsync(key, null)</c>）
    /// </summary>
    public ValueTask DeleteAsync(string key)
    {
        return WriteAsync(key, null);
    }

    /// <summary>
    /// 同步删除键。
    /// </summary>
    public void Delete(string key)
        => DeleteAsync(key).AsTask().GetAwaiter().GetResult();

    private async Task _FlushToDiskAsync(ConcurrentDictionary<string, string> items)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder(items.Count * 32);
            foreach (var (k, v) in items)
            {
                sb.Append(k);
                sb.Append(':');
                sb.Append(v);
                sb.Append("\r\n");
            }

            await File.WriteAllTextAsync(_filePath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"Failed to write INI: {_filePath}");
        }
    }


    /// <summary>
    /// 读取并解析为 <see cref="int"/>。
    /// </summary>
    public async ValueTask<int?> ReadInt32Async(string key, int? defaultValue = null)
    {
        var raw = await ReadAsync(key).ConfigureAwait(false);
        return raw is not null && int.TryParse(raw, out var v) ? v : defaultValue;
    }

    /// <summary>
    /// 写入 <see cref="int"/> 值。
    /// </summary>
    public ValueTask WriteInt32Async(string key, int value)
        => WriteAsync(key, value.ToString());

    /// <summary>
    /// 读取并解析为 <see cref="bool"/>。
    /// </summary>
    public async ValueTask<bool?> ReadBooleanAsync(string key, bool? defaultValue = null)
    {
        var raw = await ReadAsync(key).ConfigureAwait(false);
        return raw is not null && bool.TryParse(raw, out var v) ? v : defaultValue;
    }

    /// <summary>
    /// 写入 <see cref="bool"/> 值。
    /// </summary>
    public ValueTask WriteBooleanAsync(string key, bool value)
        => WriteAsync(key, value.ToString());

    // ── 批量操作 ────────────────────────────────────

    /// <summary>
    /// 返回当前缓存中所有键值对的快照。
    /// 若尚未加载则从磁盘读取。
    /// </summary>
    public async ValueTask<IReadOnlyDictionary<string, string>> SnapshotAsync()
    {
        var items = await _EnsureLoadedAsync().ConfigureAwait(false);
        return new Dictionary<string, string>(items, StringComparer.Ordinal);
    }

    /// <summary>
    /// 强制写入当前缓存到磁盘（通常不需要，<see cref="WriteAsync"/> 自动写入）。
    /// </summary>
    public async ValueTask FlushAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var items = Volatile.Read(ref _items);
            if (items is not null)
                await _FlushToDiskAsync(items).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 释放内部锁。
    /// </summary>
    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
