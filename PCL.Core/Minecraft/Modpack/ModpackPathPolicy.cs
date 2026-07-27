using System;
using System.IO;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包内相对路径的安全策略。
/// <para>
/// 整合包中的路径来自不可信来源，必须在拼接到实例目录之前逐段校验，
/// 否则形如 <c>../../</c> 或 <c>C:\Windows\</c> 的路径会写出到实例目录之外（Zip Slip）。
/// 规则取自 Modrinth 官方格式规范，并对全部格式统一执行。
/// </para>
/// </summary>
public static class ModpackPathPolicy
{
    /// <summary>
    /// 校验并规范化一个「相对于实例目录」的路径。
    /// </summary>
    /// <param name="rawPath">清单中声明的原始路径，允许使用 <c>/</c> 或 <c>\</c> 作为分隔符。</param>
    /// <param name="normalized">规范化后的相对路径，使用 <see cref="Path.DirectorySeparatorChar"/> 分隔，且不含前导分隔符。</param>
    /// <returns>路径合法时返回 <c>true</c>。</returns>
    public static bool TryNormalizeRelativePath(string? rawPath, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath)) return false;

        var path = rawPath.Replace('\\', '/').Trim();

        // 绝对路径：以分隔符开头，或形如 "C:/..."，或 UNC "//server/share"
        if (path.StartsWith('/')) return false;
        if (path.Length >= 2 && path[1] == ':') return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;

        var parts = new string[segments.Length];
        var count = 0;
        foreach (var segment in segments)
        {
            // "." 无意义但无害，直接丢弃；".." 一律拒绝，不做抵消处理，
            // 因为抵消会让 "a/../../b" 这类路径通过逐段计数的检查。
            if (segment == ".") continue;
            if (segment == "..") return false;

            // Windows 备用数据流（NTFS ADS）与非法字符
            if (segment.Contains(':')) return false;
            if (segment.IndexOfAny(_InvalidNameChars) >= 0) return false;

            // 结尾的 "." 与空格会被 Win32 静默去除，可能导致写入位置与校验位置不一致
            if (segment.EndsWith('.') || segment.EndsWith(' ')) return false;

            parts[count++] = segment;
        }

        if (count == 0) return false;

        normalized = string.Join(Path.DirectorySeparatorChar, parts, 0, count);
        return true;
    }

    /// <summary>
    /// 将相对路径安全地拼接到基准目录下，越界或路径非法时抛出 <see cref="ModpackUnsafePathException"/>。
    /// </summary>
    /// <param name="baseDirectory">基准目录（通常为实例目录）的绝对路径。</param>
    /// <param name="rawRelativePath">清单中声明的原始相对路径。</param>
    /// <returns>位于 <paramref name="baseDirectory"/> 之内的绝对路径。</returns>
    /// <exception cref="ModpackUnsafePathException" />
    public static string ResolveWithin(string baseDirectory, string? rawRelativePath)
    {
        if (!TryNormalizeRelativePath(rawRelativePath, out var relative))
            throw new ModpackUnsafePathException(rawRelativePath ?? "<null>");

        var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, relative));

        // 规范化后再次确认 —— 兼顾符号链接、短文件名等 TryNormalizeRelativePath 无法覆盖的情况
        if (!IsWithin(baseDirectory, fullPath))
            throw new ModpackUnsafePathException(rawRelativePath!);

        return fullPath;
    }

    /// <summary>
    /// 判断 <paramref name="candidateFullPath"/> 是否位于 <paramref name="baseDirectory"/> 之内。
    /// </summary>
    public static bool IsWithin(string baseDirectory, string candidateFullPath)
    {
        var root = Path.GetFullPath(baseDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;

        var candidate = Path.GetFullPath(candidateFullPath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly char[] _InvalidNameChars = ['<', '>', '"', '|', '?', '*', '\0'];
}
