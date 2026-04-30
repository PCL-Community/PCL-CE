using PCL.Core.Utils.Exts;
using System;
using System.IO;

namespace PCL.Core.Utils;

public static class PathUtils
{
    /// <summary>
    /// 从文件路径或者 Url 获取不包含文件名的路径，或获取文件夹的父文件夹路径。
    /// 取决于原路径格式，路径以 / 或 \ 结尾。
    /// 不包含路径将会抛出异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">在不包含路径时抛出</exception>
    public static string GetPathFromFullPath(string filePath)
    {
        var lastSep = filePath.LastIndexOfAny(['\\', '/']);
        if (lastSep < 0)
            throw new InvalidOperationException("不包含路径：" + filePath);

        if (filePath.EndsWith('\\') || filePath.EndsWith('/'))
        {
            // 是文件夹路径：去掉末尾分隔符，取上一级目录
            filePath = filePath[..lastSep];
            lastSep = filePath.LastIndexOfAny(['\\', '/']);
            return lastSep >= 0
                ? filePath[..(lastSep + 1)]
                : throw new InvalidOperationException("不包含路径：" + filePath);
        }

        // 是文件路径：取包含末尾分隔符的目录部分
        var result = filePath[..(lastSep + 1)];
        return !string.IsNullOrEmpty(result)
            ? result
            : throw new InvalidOperationException("不包含路径：" + filePath);
    }


    /// <summary>
    /// 从文件路径或者 Url 获取不包含路径的文件名。不包含文件名将会抛出异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">在不包含路径时抛出</exception>
    /// <exception cref="PathTooLongException">在文件名过长时抛出</exception>
    public static string GetFileNameFromPath(string filePath)
    {
        filePath = filePath.Replace('/', '\\');
        if (filePath.EndsWith('\\'))
        {
            throw new InvalidOperationException("不包含文件名：" + filePath);
        }

        if (filePath.Contains('?'))
        {
            filePath = filePath[..filePath.IndexOf('?')]; // 去掉网络参数后的 ?
        }

        if (filePath.Contains('\\'))
        {
            filePath = filePath[..(filePath.LastIndexOf('\\') + 1)];
        }

        var length = filePath.Length;
        if (length == 0)
        {
            throw new InvalidOperationException("不包含文件名：" + filePath);
        }

        if (length > 250)
        {
            throw new PathTooLongException("文件名过长：" + filePath);
        }

        return filePath;
    }



    /// <summary>
    /// 从文件夹路径获取文件夹名。
    /// </summary>
    public static string GetFolderNameFromPath(string folderPath)
    {
        if (folderPath.EndsWithF(@":\") || folderPath.EndsWithF(@":\\"))
        {
            return folderPath.Substring(0, 1);
        }

        if (folderPath.EndsWith('\\') || folderPath.EndsWith('/'))
        {
            folderPath = folderPath[..^1];
        }

        return GetFileNameFromPath(folderPath);
    }

}