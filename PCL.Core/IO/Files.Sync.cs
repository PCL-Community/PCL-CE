using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.IO;

/// <summary>同步文件操作兼容入口。异步实现仍以 <see cref="Files" /> 现有 Async 方法为准。</summary>
public static partial class Files
{
    public static string ReadAllTextOrEmpty(string filePath, Encoding? encoding = null)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            return (encoding ?? EncodingDetector.DetectEncoding(bytes)).GetString(bytes);
        }
        catch
        {
            return "";
        }
    }

    public static string ReadAllTextOrEmpty(Stream stream, Encoding? encoding = null)
    {
        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public static byte[] ReadAllBytesOrEmpty(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }
        catch
        {
            return [];
        }
    }

    public static void WriteAllText(string filePath, string content, Encoding? encoding = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, content, encoding ?? Encoding.UTF8);
    }

    public static void WriteAllBytes(string filePath, byte[] content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(filePath, content);
    }

    public static void WriteAllStream(string filePath, Stream stream)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var output = File.Create(filePath);
        stream.CopyTo(output);
    }

    public static void CreateSymbolicLink(string linkPath, string targetPath)
    {
        if (Directory.Exists(targetPath)) Directory.CreateSymbolicLink(linkPath, targetPath);
        else File.CreateSymbolicLink(linkPath, targetPath);
    }

    public static void CopyFile(string fromPath, string toPath)
    {
        var directory = Path.GetDirectoryName(toPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.Copy(fromPath, toPath, true);
    }

    public static void CopyDirectory(
        string source,
        string destination,
        Action<double>? progress = null)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var done = 0;
        Directory.CreateDirectory(destination);
        foreach (var file in files)
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
            done += 1;
            if (files.Length > 0) progress?.Invoke((double)done / files.Length);
        }
    }

    public static IEnumerable<FileInfo> EnumerateFiles(string path)
    {
        return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories);
    }

    public static string GetFileMd5(string filePath)
    {
        return _GetFileHash(filePath, MD5.Create());
    }

    public static string GetFileSha1(string filePath)
    {
        return _GetFileHash(filePath, SHA1.Create());
    }

    public static string GetFileSha256(string filePath)
    {
        return _GetFileHash(filePath, SHA256.Create());
    }

    private static string _GetFileHash(string filePath, HashAlgorithm algorithm)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }

    public static string GetPathFromFullPath(string filePath)
    {
        if (!(filePath.Contains('\\') || filePath.Contains('/')))
            throw new Exception($"不包含路径：{filePath}");

        if (filePath.EndsWith('\\') || filePath.EndsWith('/'))
        {
            var isRight = filePath.EndsWith('\\');
            filePath = filePath[..^1];
            return filePath[..filePath.LastIndexOfAny(['\\', '/'])] + (isRight ? "\\" : "/");
        }

        var result = filePath[..(filePath.LastIndexOfAny(['\\', '/']) + 1)];
        return string.IsNullOrEmpty(result)
            ? throw new Exception($"不包含路径：{filePath}")
            : result;
    }

    public static string GetFileNameFromPath(string filePath)
    {
        filePath = filePath.Replace("/", "\\");
        if (filePath.EndsWith('\\')) throw new Exception($"不包含文件名：{filePath}");
        if (filePath.Contains('?')) filePath = filePath[..filePath.IndexOf('?')];
        if (filePath.Contains('\\')) filePath = filePath[(filePath.LastIndexOf('\\') + 1)..];

        return filePath.Length switch
        {
            0 => throw new Exception($"不包含文件名：{filePath}"),
            > 250 => throw new PathTooLongException($"文件名过长：{filePath}"),
            _ => filePath
        };
    }

    public static string GetFolderNameFromPath(string folderPath)
    {
        if (folderPath.EndsWith(":\\") || folderPath.EndsWith(":\\\\"))
            return folderPath[..1];
        if (folderPath.EndsWith('\\') || folderPath.EndsWith('/'))
            folderPath = folderPath[..^1];
        return GetFileNameFromPath(folderPath);
    }

    public static void ExtractFile(
        string compressFilePath,
        string destDirectory,
        Encoding? encoding = null,
        Action<double>? progressIncrementHandler = null)
    {
        Directory.CreateDirectory(destDirectory);
        var fullDestDirectory = Path.GetFullPath(destDirectory);
        if (!fullDestDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            fullDestDirectory += Path.DirectorySeparatorChar;

        if (compressFilePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var compressedFile = new FileStream(
                compressFilePath,
                FileMode.Open,
                FileAccess.Read);
            using var decompressStream = new GZipStream(compressedFile, CompressionMode.Decompress);
            var fileName = Path.GetFileName(compressFilePath);
            if (fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^3];
            if (fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^4];
            using var extractFileStream = new FileStream(
                Path.Combine(fullDestDirectory, fileName.ToLowerInvariant()),
                FileMode.OpenOrCreate,
                FileAccess.Write);
            decompressStream.CopyTo(extractFileStream);
            progressIncrementHandler?.Invoke(1d);
            return;
        }

        using var archive = ZipFile.Open(
            compressFilePath,
            ZipArchiveMode.Read,
            encoding ?? Encoding.GetEncoding("GB18030"));
        var total = archive.Entries.Count;
        foreach (var entry in archive.Entries)
        {
            if (total > 0) progressIncrementHandler?.Invoke(1d / total);

            var path = Path.GetFullPath(Path.Combine(fullDestDirectory, entry.FullName));
            if (!path.StartsWith(fullDestDirectory, StringComparison.OrdinalIgnoreCase))
                throw new Exception(
                    $"解压文件 {entry.FullName} 错误：解压路径 {path} 不在目标目录 {fullDestDirectory} 内");

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            entry.ExtractToFile(path, true);
        }
    }
}