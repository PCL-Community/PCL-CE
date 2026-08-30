using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PCL.Core.IO;

/// <summary>
///     路径和 URL 文件名处理工具。
/// </summary>
public static partial class PathUtils
{
    private static readonly char[] _DirectorySeparators = ['\\', '/'];

    /// <summary>
    ///     从文件路径或 URL 获取不包含文件名的目录部分。返回值保留原分隔符风格并以分隔符结尾。
    /// </summary>
    public static string GetDirectoryPart(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!path.Contains('\\') && !path.Contains('/'))
            throw new ArgumentException($"不包含路径：{path}", nameof(path));

        if (_EndsWithDirectorySeparator(path))
        {
            var separator = path[^1];
            path = path[..^1];
            var parentIndex = path.LastIndexOfAny(_DirectorySeparators);
            if (parentIndex < 0)
                throw new ArgumentException($"不包含路径：{path}", nameof(path));
            return path[..(parentIndex + 1)].TrimEnd('\\', '/') + separator;
        }

        var index = path.LastIndexOfAny(_DirectorySeparators);
        if (index < 0)
            throw new ArgumentException($"不包含路径：{path}", nameof(path));

        var result = path[..(index + 1)];
        return string.IsNullOrEmpty(result)
            ? throw new ArgumentException($"不包含路径：{path}", nameof(path))
            : result;
    }

    /// <summary>
    ///     从本地路径或 URL 中提取文件名；只有远程 URL 的查询字符串与片段会被忽略。
    /// </summary>
    public static string GetFileNameFromUrlOrPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        path = path.Trim(' ', '"');

        if (_TryCreateRemoteUri(path, out var uri) && !string.IsNullOrEmpty(uri.LocalPath))
            path = Uri.UnescapeDataString(uri.LocalPath);

        if (_EndsWithDirectorySeparator(path))
            throw new ArgumentException($"不包含文件名：{path}", nameof(path));

        var separatorIndex = path.LastIndexOfAny(_DirectorySeparators);
        var fileName = separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;

        return fileName.Length switch
        {
            0 => throw new ArgumentException($"不包含文件名：{path}", nameof(path)),
            > 250 => throw new PathTooLongException($"文件名过长：{fileName}"),
            _ => fileName
        };
    }

    private static bool _TryCreateRemoteUri(string path, out Uri uri)
    {
        if (!Uri.TryCreate(path, UriKind.Absolute, out uri!))
            return false;

        if (uri.IsFile || uri.Scheme.Length <= 1)
            return false;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFileNameWithoutExtensionFromUrlOrPath(string path)
    {
        return Path.GetFileNameWithoutExtension(GetFileNameFromUrlOrPath(path));
    }

    /// <summary>
    ///     从目录路径中提取目录名。根驱动器返回驱动器字母。
    /// </summary>
    public static string GetDirectoryNameLeaf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        path = path.Trim(' ', '"');

        if (path.EndsWith(@":\", StringComparison.Ordinal) || path.EndsWith(@":\\", StringComparison.Ordinal))
            return path[..1];

        path = path.TrimEnd('\\', '/');
        return GetFileNameFromUrlOrPath(path);
    }

    /// <summary>
    ///     当路径过长时尝试转换为 Windows 8.3 短路径；失败或非 Windows 系统时返回原路径。
    /// </summary>
    public static unsafe string ShortenPath(string path, int shortenThreshold = 247)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length <= shortenThreshold || !OperatingSystem.IsWindows()) return path;

        Span<char> buffer = stackalloc char[260];
        fixed (char* bufferPtr = buffer)
        {
            var length = _GetShortPathName(path, bufferPtr, buffer.Length);
            if (length <= buffer.Length) return length > 0
                ? buffer[..length].ToString()
                : path;
            
            var largerBuffer = new char[length];
            fixed (char* largerPtr = largerBuffer)
            {
                var newLength = _GetShortPathName(path, largerPtr, length);
                return newLength > 0
                    ? new string(largerBuffer, 0, newLength)
                    : path;
            }
        }
    }

    private static bool _EndsWithDirectorySeparator(string path)
    {
        return path.EndsWith('\\') || path.EndsWith('/');
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetShortPathNameW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    private static unsafe partial int _GetShortPathName(
        string lpszLongPath,
        char* lpszShortPath,
        int cchBuffer);
}