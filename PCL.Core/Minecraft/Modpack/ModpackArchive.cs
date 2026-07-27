using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包压缩包的只读访问层。
/// <para>
/// 在原始 ZIP 之上提供三件事，使 Provider 不必重复处理：
/// </para>
/// <list type="bullet">
/// <item>条目名编码回退 —— 未设置 UTF-8 标志位的条目按 GB18030 解码，处理中文环境导出的压缩包。</item>
/// <item>根目录探测 —— 部分整合包把内容整体放在一个一级子目录下，本类将其抹平，
/// 使 <see cref="TryGetEntry"/> 等方法接受的路径始终相对于逻辑根。</item>
/// <item>条目索引 —— 以不区分大小写的字典缓存全部条目，避免 Provider 反复线性扫描。</item>
/// </list>
/// </summary>
public sealed class ModpackArchive : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    /// <summary>整合包文件的绝对路径。</summary>
    public string FilePath { get; }

    /// <summary>
    /// 逻辑根在压缩包内的前缀，形如 <c>"MyPack/"</c>；内容位于压缩包根目录时为空字符串。
    /// </summary>
    public string RootPrefix { get; }

    private ModpackArchive(string filePath, ZipArchive archive, string rootPrefix,
        Dictionary<string, ZipArchiveEntry> entries)
    {
        FilePath = filePath;
        _archive = archive;
        RootPrefix = rootPrefix;
        _entries = entries;
    }

    /// <summary>
    /// 打开一个整合包压缩包。
    /// </summary>
    /// <param name="filePath">整合包文件的绝对路径。</param>
    /// <exception cref="ModpackArchiveException">文件损坏、被加密或不是受支持的归档格式。</exception>
    public static ModpackArchive Open(string filePath)
    {
        ZipArchive? archive = null;
        try
        {
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // entryNameEncoding 只作用于「未设置 UTF-8 标志位」的条目，
            // 已正确标记 UTF-8 的条目仍按 UTF-8 解码，因此这里指定 GB18030 不会破坏合规压缩包。
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, _EntryNameEncoding);

            var fileEntries = archive.Entries.Where(e => !_IsDirectoryEntry(e)).ToList();
            if (fileEntries.Count == 0)
                throw new ModpackArchiveException(filePath, "压缩包内没有任何文件");

            if (fileEntries.Any(e => e.IsEncrypted))
                throw new ModpackArchiveException(filePath, "压缩包已加密，无法读取", isEncrypted: true);

            var rootPrefix = _DetectRootPrefix(fileEntries);
            var entries = _BuildIndex(fileEntries, rootPrefix);

            return new ModpackArchive(filePath, archive, rootPrefix, entries);
        }
        catch (ModpackArchiveException)
        {
            archive?.Dispose();
            throw;
        }
        catch (InvalidDataException ex)
        {
            archive?.Dispose();
            throw new ModpackArchiveException(filePath, "压缩包已损坏或不是 zip 格式", inner: ex);
        }
        catch (Exception ex)
        {
            archive?.Dispose();
            throw new ModpackArchiveException(filePath, $"打开压缩包失败：{ex.Message}", inner: ex);
        }
    }

    /// <summary>
    /// 按逻辑根相对路径查找条目，不区分大小写，分隔符可用 <c>/</c> 或 <c>\</c>。
    /// </summary>
    public ZipArchiveEntry? TryGetEntry(string relativePath)
        => _entries.GetValueOrDefault(_NormalizeKey(relativePath));

    /// <summary>指定的逻辑根相对路径是否存在。</summary>
    public bool HasEntry(string relativePath) => TryGetEntry(relativePath) is not null;

    /// <summary>
    /// 指定的逻辑根相对目录下是否存在任何文件。
    /// </summary>
    public bool HasDirectory(string relativeDirectory)
    {
        var prefix = _NormalizeKey(relativeDirectory);
        if (prefix.Length == 0) return _entries.Count > 0;
        prefix += '/';
        return _entries.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// 枚举指定逻辑根相对目录下的全部文件（含子目录）。
    /// </summary>
    /// <param name="relativeDirectory">相对目录，传入空字符串表示逻辑根。</param>
    /// <returns>条目及其「相对于 <paramref name="relativeDirectory"/>」的路径。</returns>
    public IEnumerable<ModpackArchiveItem> EnumerateFiles(string relativeDirectory = "")
    {
        var prefix = _NormalizeKey(relativeDirectory);
        if (prefix.Length > 0) prefix += '/';

        foreach (var (key, entry) in _entries)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            yield return new ModpackArchiveItem(entry, key[prefix.Length..]);
        }
    }

    /// <summary>打开条目内容流。</summary>
    /// <exception cref="ModpackArchiveException">条目不存在。</exception>
    public Stream OpenRead(string relativePath)
    {
        var entry = TryGetEntry(relativePath)
                    ?? throw new ModpackArchiveException(FilePath, $"压缩包内不存在条目：{relativePath}");
        return entry.Open();
    }

    /// <summary>
    /// 将条目按文本读取，自动检测 BOM 并在 UTF-8 解码失败时回退到 GB18030。
    /// </summary>
    /// <exception cref="ModpackArchiveException">条目不存在。</exception>
    public string ReadAllText(string relativePath)
    {
        using var stream = OpenRead(relativePath);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return EncodingUtils.DecodeBytes(buffer.ToArray());
    }

    /// <summary>
    /// 将条目反序列化为 <typeparamref name="T"/>，使用 PCL 统一的宽松 JSON 配置。
    /// </summary>
    /// <returns>条目不存在时返回 <c>default</c>。</returns>
    /// <exception cref="JsonException">JSON 无法解析。</exception>
    public T? ReadJson<T>(string relativePath)
    {
        if (!HasEntry(relativePath)) return default;
        return JsonSerializer.Deserialize<T>(ReadAllText(relativePath), JsonCompat.SerializerOptions);
    }

    /// <summary>
    /// 将条目解析为 <see cref="JsonObject"/>。
    /// </summary>
    /// <returns>条目不存在或根节点不是对象时返回 <c>null</c>。</returns>
    /// <exception cref="JsonException">JSON 无法解析。</exception>
    public JsonObject? ReadJsonObject(string relativePath)
    {
        if (!HasEntry(relativePath)) return null;
        return JsonCompat.ParseNode(ReadAllText(relativePath)) as JsonObject;
    }

    public void Dispose() => _archive.Dispose();

    /// <summary>
    /// 探测逻辑根前缀。
    /// <para>
    /// 当且仅当「压缩包根目录下没有任何文件」且「所有文件都位于同一个一级子目录下」时，
    /// 该子目录被视为逻辑根。这一条件比「找到特征文件所在目录」更严格，
    /// 可避免把 <c>overrides/</c> 之类的内容目录误判为根。
    /// </para>
    /// </summary>
    private static string _DetectRootPrefix(List<ZipArchiveEntry> fileEntries)
    {
        string? candidate = null;

        foreach (var entry in fileEntries)
        {
            var name = entry.FullName.Replace('\\', '/');
            var slash = name.IndexOf('/');

            // 根目录下存在文件 —— 逻辑根就是压缩包根目录
            if (slash < 0) return string.Empty;

            var topLevel = name[..slash];
            if (candidate is null) candidate = topLevel;
            else if (!string.Equals(candidate, topLevel, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        }

        return candidate is null ? string.Empty : candidate + "/";
    }

    private static Dictionary<string, ZipArchiveEntry> _BuildIndex(
        List<ZipArchiveEntry> fileEntries, string rootPrefix)
    {
        var index = new Dictionary<string, ZipArchiveEntry>(fileEntries.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in fileEntries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (rootPrefix.Length > 0)
            {
                if (!name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                name = name[rootPrefix.Length..];
            }

            if (name.Length == 0) continue;

            // 同名条目取首个，与多数解压实现的行为一致
            index.TryAdd(name, entry);
        }

        return index;
    }

    /// <summary>
    /// 未标记 UTF-8 的条目名所使用的编码。
    /// <para>
    /// GB18030 由 <c>CodePagesEncodingProvider</c> 提供，需在进程启动时注册。
    /// 宿主未注册时（例如单元测试或被作为库引用）此处退回 UTF-8，
    /// 只损失中文文件名的兼容性，而不会让整个压缩包无法打开。
    /// </para>
    /// </summary>
    private static readonly Encoding _EntryNameEncoding = _ResolveEntryNameEncoding();

    private static Encoding _ResolveEntryNameEncoding()
    {
        try
        {
            return Encodings.GB18030;
        }
        catch (Exception ex) when (ex is ArgumentException or TypeInitializationException)
        {
            return Encoding.UTF8;
        }
    }

    private static bool _IsDirectoryEntry(ZipArchiveEntry entry)
        => entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

    private static string _NormalizeKey(string relativePath)
        => relativePath.Replace('\\', '/').Trim('/');
}

/// <summary>
/// 压缩包内的一个文件条目及其相对路径。
/// </summary>
/// <param name="Entry">底层 ZIP 条目。</param>
/// <param name="RelativePath">相对于枚举起点目录的路径，使用 <c>/</c> 分隔。</param>
public readonly record struct ModpackArchiveItem(ZipArchiveEntry Entry, string RelativePath);
