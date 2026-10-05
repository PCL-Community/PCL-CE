using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PCL;

/// <summary>
/// versions.txt 的本地缓存。成功生成后原子覆盖；写入失败保留原文件。
/// </summary>
public static class VanillaVersionCache
{
    public static List<string> Parse(string text)
    {
        return text.Split('\r', '\n').Select(raw => raw.Trim()).Where(id => id.Length != 0).ToList();
    }

    public static IReadOnlyList<string>? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Write(string path, IReadOnlyList<string> ids)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using (var writer = new StreamWriter(stream, encoding, bufferSize: 1024, leaveOpen: true))
                {
                    foreach (var t in ids)
                    {
                        writer.Write(t);
                        writer.Write('\n');
                    }
                }

                stream.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        catch
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 临时文件清不掉也不许动目标文件。
            }

            throw;
        }
    }
}
