using System.Collections.Concurrent;
using System.IO;
using System.Text;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;

namespace PCL;

public static partial class Common
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>>
        IniCache = new();

    private static readonly Lock WriteIniLock = new();

    /// <summary>
    ///     清除某 ini 文件的运行时缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    public static void IniClearCache(string fileName)
    {
        if (!fileName.Contains(@":\"))
            fileName = $@"{ExePath}PCL\{fileName}.ini";
        IniCache.Remove(fileName, out _);
    }

    /// <summary>
    ///     获取 ini 文件缓存。如果没有，则新读取 ini 文件内容。
    ///     在文件不存在或读取失败时返回 Nothing。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    private static ConcurrentDictionary<string, string>? IniGetContent(string fileName)
    {
        try
        {
            if (!fileName.Contains(@":\"))
                fileName = $@"{ExePath}PCL\{fileName}.ini";
            if (IniCache.TryGetValue(fileName, out var value))
                return value;
            if (!File.Exists(fileName))
                return null;
            var ini = new ConcurrentDictionary<string, string>();
            foreach (var line in ReadFile(fileName)
                         .Split([.. "\r\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOfF(":");
                if (index > 0)
                    ini[line[..index]] = line[(index + 1)..];
            }

            IniCache[fileName] = ini;
            return ini;
        }
        catch (Exception ex)
        {
            Log(ex, $"生成 ini 文件缓存失败（{fileName}）", LogLevel.Hint);
            return null;
        }
    }

    /// <summary>
    ///     读取 ini 文件。这可能会使用到缓存。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">没有找到键时返回的默认值。</param>
    public static string ReadIni(
        string fileName,
        string key,
        string defaultValue = "")
    {
        var content = IniGetContent(fileName);
        return content is null || !content.TryGetValue(key, out var ini)
            ? defaultValue
            : ini;
    }

    /// <summary>
    ///     写入 ini 文件，这会更新缓存。
    ///     若 Value 为 Nothing，则删除该键。
    /// </summary>
    /// <param name="fileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    /// <param name="key">键。</param>
    /// <param name="value">值。</param>
    /// <remarks></remarks>
    public static void WriteIni(string fileName, string key, string? value)
    {
        try
        {
            if (key.Contains(':'))
                throw new Exception($"尝试写入 ini 文件 {fileName} 的键名中包含了冒号：{key}");
            key = key
                .Replace("\r", "")
                .Replace("\n", "");
            value = value
                ?.Replace("\r", "")
                .Replace("\n", "");

            lock (WriteIniLock)
            {
                var content = IniGetContent(fileName) ?? new ConcurrentDictionary<string, string>();
                if (value is null)
                {
                    if (!content.ContainsKey(key))
                        return;
                    content.Remove(key, out _);
                }
                else
                {
                    if (content.TryGetValue(key, out var value1)
                        && value1 == value)
                        return;
                    content[key] = value;
                }

                var fileContent = new StringBuilder();
                foreach (var pair in content)
                {
                    fileContent.Append(pair.Key);
                    fileContent.Append(":");
                    fileContent.Append(pair.Value);
                    fileContent.Append("\r\n");
                }

                if (!fileName.Contains(@":\"))
                    fileName = $@"{ExePath}PCL\{fileName}.ini";
                WriteFile(fileName, fileContent.ToString());
            }
        }
        catch (Exception ex)
        {
            Log(ex, $"写入文件失败（{fileName} → {key}:{value}）", LogLevel.Hint);
        }
    }

    /// <summary>
    ///     从文件路径或者 Url 获取不包含文件名的路径，或获取文件夹的父文件夹路径。
    ///     取决于原路径格式，路径以 / 或 \ 结尾。
    ///     不包含路径将会抛出异常。
    /// </summary>
    public static string GetPathFromFullPath(string filePath)
    {
        string? getPathFromFullPathRet;
        if (!(filePath.Contains('\\') || filePath.Contains('/')))
            throw new Exception("不包含路径：" + filePath);
        if (filePath.EndsWithF(@"\")
            || filePath.EndsWithF("/"))
        {
            var isRight = filePath.EndsWithF(@"\");
            filePath = filePath[..^1];
            getPathFromFullPathRet = filePath[..filePath.LastIndexOfAny(['\\', '/'])]
                + (isRight ? @"\" : "/");
        }
        else
        {
            getPathFromFullPathRet = filePath[..(filePath.LastIndexOfAny(['\\', '/']) + 1)];
            if (string.IsNullOrEmpty(getPathFromFullPathRet))
                throw new Exception("不包含路径：" + filePath);
        }

        return getPathFromFullPathRet;
    }

    /// <summary>
    ///     从文件路径或者 Url 获取不包含路径的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameFromPath(string filePath)
    {
        filePath = filePath.Replace("/", @"\");
        if (filePath.EndsWithF(@"\"))
            throw new Exception("不包含文件名：" + filePath);
        if (filePath.Contains('?'))
            filePath = filePath[..filePath.IndexOfF("?")];
        if (filePath.Contains('\\'))
            filePath = filePath[(filePath.LastIndexOfF(@"\") + 1)..];
        var length = filePath.Length;
        return length switch
        {
            0 => throw new Exception("不包含文件名：" + filePath),
            > 250 => throw new PathTooLongException("文件名过长：" + filePath),
            _ => filePath
        };
    }

    /// <summary>
    ///     从文件夹路径获取文件夹名。
    /// </summary>
    public static string GetFolderNameFromPath(string folderPath)
    {
        if (folderPath.EndsWithF(@":\")
            || folderPath.EndsWithF(@":\\"))
            return folderPath[..1];
        if (folderPath.EndsWithF(@"\")
            || folderPath.EndsWithF("/"))
            folderPath = folderPath[..^1];
        return GetFileNameFromPath(folderPath);
    }

    /// <summary>
    ///     复制文件。会自动创建文件夹、会覆盖已有的文件。
    /// </summary>
    public static void CopyFile(string fromPath, string toPath)
    {
        try
        {
            if (!fromPath.Contains(@":\"))
                fromPath = ExePath + fromPath;
            if (!toPath.Contains(@":\"))
                toPath = ExePath + toPath;
            if (fromPath == toPath)
                return;
            Directory.CreateDirectory(GetPathFromFullPath(toPath));
            File.Copy(fromPath, toPath, true);
        }
        catch (Exception ex)
        {
            throw new Exception($"复制文件出错：{fromPath} → {toPath}", ex);
        }
    }

    /// <summary>
    ///     读取文件，如果失败则返回空数组。
    /// </summary>
    public static byte[] ReadFileBytes(string filePath, Encoding? encoding = null)
    {
        try
        {
            if (!filePath.Contains(@":\"))
                filePath = ExePath + filePath;
            if (File.Exists(filePath))
            {
                using var readStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var ms = new MemoryStream();
                readStream.CopyTo(ms);
                return ms.ToArray();
            }

            Log($"[System] 欲读取的文件不存在，已返回空内容：{filePath}");
            return [];
        }
        catch (Exception ex)
        {
            Log(ex, $"读取文件出错：{filePath}");
            return [];
        }
    }

    /// <summary>
    ///     读取文件，如果失败则返回空字符串。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static string ReadFile(string filePath, Encoding? encoding = null)
    {
        string? readFileRet = default;
        var fileBytes = ReadFileBytes(filePath);
        readFileRet = encoding is null
            ? DecodeBytes(fileBytes)
            : encoding.GetString(fileBytes);
        return readFileRet;
    }

    /// <summary>
    ///     读取流中的所有文本。
    /// </summary>
    public static string ReadFile(Stream stream, Encoding? encoding = null)
    {
        try
        {
            var readedContent = new MemoryStream();
            stream.CopyTo(readedContent);
            var bts = readedContent.ToArray();
            return (encoding ?? EncodingDetector.DetectEncoding(bts)).GetString(bts);
        }
        catch (Exception ex)
        {
            Log(ex, "读取流出错");
            return "";
        }
    }

    /// <summary>
    ///     写入文件。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    /// <param name="text">文件内容。</param>
    /// <param name="append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
    public static void WriteFile(
        string filePath,
        string text,
        bool append = false,
        Encoding? encoding = null)
    {
        if (!filePath.Contains(@":\"))
            filePath = ExePath + filePath;
        Directory.CreateDirectory(GetPathFromFullPath(filePath));
        if (append)
        {
            using var writer = new StreamWriter(
                filePath,
                true,
                encoding ?? EncodingDetector.DetectEncoding(ReadFileBytes(filePath)));
            writer.Write(text);
        }
        else
        {
            var bytes = encoding is null
                ? new UTF8Encoding(false).GetBytes(text)
                : encoding.GetBytes(text);
            var tempPath = $"{filePath}.pcltmp.{Guid.NewGuid():N}";
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, filePath, true);
        }
    }

    /// <summary>
    ///     写入文件。
    ///     如果 CanThrow 设置为 False，返回是否写入成功。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    /// <param name="content">文件内容。</param>
    /// <param name="append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
    public static void WriteFile(string filePath, byte[] content, bool append = false)
    {
        if (!filePath.Contains(@":\"))
            filePath = ExePath + filePath;
        Directory.CreateDirectory(GetPathFromFullPath(filePath));
        File.WriteAllBytes(filePath, content);
    }

    /// <summary>
    ///     将流写入文件。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static bool WriteFile(string filePath, Stream stream)
    {
        try
        {
            if (!filePath.Contains(@":\"))
                filePath = ExePath + filePath;
            Directory.CreateDirectory(GetPathFromFullPath(filePath));
            using var fs = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            fs.SetLength(0L);
            stream.CopyTo(fs);

            return true;
        }
        catch (Exception ex)
        {
            Log(ex, "保存流出错");
            return false;
        }
    }

    /// <summary>
    ///     解码 Bytes。
    /// </summary>
    public static string DecodeBytes(byte[] bytes)
    {
        var length = bytes.Length;
        if (length < 3)
            return Encoding.UTF8.GetString(bytes);
        if (bytes[0] >= 0xEF)
        {
            if (bytes[0] == 0xEF
                && bytes[1] == 0xBB)
                return Encoding.UTF8.GetString(bytes, 3, length - 3);

            if (bytes[0] == 0xFE
                && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 3, length - 3);

            if (bytes[0] == 0xFF
                && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 3, length - 3);

            return Encoding.GetEncoding("GB18030").GetString(bytes, 3, length - 3);
        }

        var utf8 = Encoding.UTF8.GetString(bytes);
        var errorChar = Encoding.UTF8.GetString("\ufffd"u8.ToArray()).ToCharArray()[0];
        return utf8.Contains(errorChar)
            ? Encoding.GetEncoding("GB18030").GetString(bytes)
            : utf8;
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var c in bytes.Span)
            sb.Append(c.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    ///     获取文件 MD5，若失败则返回空字符串。
    /// </summary>
    public static string GetFileMd5(string filePath)
    {
        return GetFileHash(
            filePath,
            "MD5",
            stream =>
                (string)GetHexString(MD5Provider.Instance.ComputeHash(stream)));
    }

    /// <summary>
    ///     获取文件 SHA512，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSha512(string filePath)
    {
        return GetFileHash(
            filePath,
            "SHA512",
            stream =>
                (string)GetHexString(SHA512Provider.Instance.ComputeHash(stream)));
    }

    /// <summary>
    ///     获取文件 SHA256，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSha256(string filePath)
    {
        return GetFileHash(
            filePath,
            "SHA256",
            stream =>
                (string)GetHexString(SHA256Provider.Instance.ComputeHash(stream)));
    }

    /// <summary>
    ///     获取文件 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSha1(string filePath)
    {
        return GetFileHash(
            filePath,
            "SHA1",
            stream =>
                (string)GetHexString(SHA1Provider.Instance.ComputeHash(stream)));
    }

    /// <summary>
    ///     计算文件哈希，失败时最多重试一次。
    ///     文件不存在时不重试。
    /// </summary>
    private static string GetFileHash(
        string filePath,
        string algorithm,
        Func<FileStream, string> computeHash)
    {
        for (var attempt = 0; attempt < 2; attempt++)
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                return computeHash(stream);
            }
            catch (Exception ex)
            {
                if (attempt == 0
                    && ex is not FileNotFoundException)
                {
                    Log(ex, $"获取文件 {algorithm} 可重试失败：{filePath}", LogLevel.Normal);
                    Thread.Sleep(RandomUtils.NextInt(200, 500));
                    continue;
                }

                Log(ex, $"获取文件 {algorithm} 失败：{filePath}");
                return "";
            }

        return "";
    }
}