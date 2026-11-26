using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PCL.Core.Utils;

namespace PCL.Core.App.Tools;

/// <summary>背景内容的格式识别与目录枚举。</summary>
public static class BackgroundContent
{
    private static readonly HashSet<string> _ImageExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".webp",
        ".avif",
        ".tif",
        ".tiff",
        ".heic",
        ".heif"
    ];

    private static readonly HashSet<string> _AnimatedImageExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".gif",
        ".webp",
        ".avif"
    ];

    private static readonly HashSet<string> _VideoExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".mp4",
        ".mkv",
        ".webm",
        ".mov",
        ".m4v",
        ".avi",
        ".wmv",
        ".flv",
        ".ts",
        ".m2ts",
        ".mpg",
        ".mpeg",
        ".ogv",
        ".3gp",
        ".rmvb",
        ".rm",
        ".asf",
        ".vob",
        ".f4v"
    ];

    /// <summary>判断路径是否为受支持的静态或动态图片。</summary>
    public static bool IsImage(string path)
    {
        return _ImageExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>判断路径是否为需要 FFmpeg 解码的动态图片。</summary>
    public static bool IsAnimatedImage(string path)
    {
        return _AnimatedImageExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>判断路径是否为受支持的视频。</summary>
    public static bool IsVideo(string path)
    {
        return _VideoExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>判断路径是否为受支持的背景内容。</summary>
    public static bool IsSupported(string path)
    {
        return IsImage(path) || IsVideo(path);
    }

    /// <summary>递归枚举目录中的背景内容并随机打乱顺序。</summary>
    public static List<string> Collect(
        string directory,
        CancellationToken cancellationToken = default)
    {
        var files = _Enumerate(directory, cancellationToken).ToList();
        RandomUtils.ShuffleInPlace(files);
        return files;
    }

    /// <summary>统计目录中的背景内容数量。</summary>
    public static int Count(string directory)
    {
        return _Enumerate(directory, CancellationToken.None).Count();
    }

    private static IEnumerable<string> _Enumerate(
        string directory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var files = Directory.EnumerateFiles(
            directory,
            "*",
            new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSupported(file)) yield return file;
        }
    }
}