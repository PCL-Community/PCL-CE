using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PCL.Core.Minecraft.Modpack.Installation;

/// <summary>
/// 把 MultiMC JAR Mod 依次覆盖进游戏主 JAR。
/// <para>
/// 所有修改先写入同目录临时文件，全部成功后才替换目标。任一 JAR Mod 损坏时，
/// 原游戏 JAR 保持不变。
/// </para>
/// </summary>
public static class ModpackJarModMerger
{
    /// <summary>
    /// 按给定顺序应用 JAR Mod；后出现的归档覆盖前面及原游戏 JAR 中的同名条目。
    /// </summary>
    public static void Merge(string gameJarPath, IReadOnlyList<string> jarModPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameJarPath);
        ArgumentNullException.ThrowIfNull(jarModPaths);
        if (jarModPaths.Count == 0) return;
        if (!File.Exists(gameJarPath)) throw new FileNotFoundException("Minecraft 主 JAR 不存在", gameJarPath);

        foreach (var jarModPath in jarModPaths)
        {
            if (!File.Exists(jarModPath)) throw new FileNotFoundException("JAR Mod 不存在", jarModPath);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(gameJarPath))!;
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(gameJarPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(gameJarPath, temporaryPath, overwrite: false);

            using (var targetStream = new FileStream(
                       temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var targetArchive = new ZipArchive(targetStream, ZipArchiveMode.Update, leaveOpen: false))
            {
                foreach (var jarModPath in jarModPaths)
                    _ApplyJarMod(targetArchive, jarModPath);
            }

            File.Move(temporaryPath, gameJarPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // 保留最初的合并异常；临时文件可由常规缓存清理回收。
            }

            throw;
        }
    }

    private static void _ApplyJarMod(ZipArchive targetArchive, string jarModPath)
    {
        using var sourceStream = new FileStream(jarModPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var sourceEntry in sourceArchive.Entries)
        {
            if (_IsDirectoryEntry(sourceEntry)) continue;

            var entryName = _NormalizeEntryName(sourceEntry.FullName);
            foreach (var existing in targetArchive.Entries
                         .Where(entry => string.Equals(entry.FullName, entryName, StringComparison.Ordinal))
                         .ToArray())
                existing.Delete();

            var targetEntry = targetArchive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var source = sourceEntry.Open();
            using var destination = targetEntry.Open();
            source.CopyTo(destination);
        }
    }

    private static string _NormalizeEntryName(string rawName)
    {
        var normalized = rawName.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"JAR Mod 包含非法条目路径：{rawName}");

        return string.Join('/', segments);
    }

    private static bool _IsDirectoryEntry(ZipArchiveEntry entry)
        => entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
}
