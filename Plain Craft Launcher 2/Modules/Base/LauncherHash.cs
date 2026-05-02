using System.IO;
using System.Text;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.Utils;
using PCL.Core.Utils.Hash;

namespace PCL;

/// <summary>
/// Owns launcher hash and checksum helpers, reusing PCL.Core hash providers where applicable.
/// </summary>
public static class LauncherHash
{
    public static string GetAuthSHA1(Stream inputStream)
    {
        try
        {
            return Conversions.ToString(GetHexString(SHA1Provider.Instance.ComputeHash(inputStream)));
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "获取流 SHA1 失败");
            return "";
        }
    }

    public static ulong GetHash(string str)
    {
        var hash = 5381UL;
        for (int i = 0, loopTo = str.Length - 1; i <= loopTo; i++)
            hash = (hash << 5) ^ hash ^ (ulong)Strings.AscW(str[i]);
        return hash ^ 0xA98F501BC684032FUL;
    }

    public static string GetStringMD5(string str)
    {
        return Conversions.ToString(GetHexString(MD5Provider.Instance.ComputeHash(str)));
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var c in bytes.Span)
            sb.Append(c.ToString("x2"));
        return sb.ToString();
    }

    public static string GetFileMD5(string filePath)
    {
        return GetFileHash(filePath, "MD5", stream => MD5Provider.Instance.ComputeHash(stream));
    }

    public static string GetFileSHA512(string filePath)
    {
        return GetFileHash(filePath, "SHA512", stream => SHA512Provider.Instance.ComputeHash(stream));
    }

    public static string GetFileSHA256(string filePath)
    {
        return GetFileHash(filePath, "SHA256", stream => SHA256Provider.Instance.ComputeHash(stream));
    }

    public static string GetFileSHA1(string filePath)
    {
        return GetFileHash(filePath, "SHA1", stream => SHA1Provider.Instance.ComputeHash(stream));
    }

    private static string GetFileHash(string filePath, string hashName, Func<Stream, Memory<byte>> computeHash)
    {
        var retry = false;
        while (true)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return Conversions.ToString(GetHexString(computeHash(fs)));
            }
            catch (Exception ex)
            {
                if (retry || ex is FileNotFoundException)
                {
                    LauncherLogger.Log(ex, $"获取文件 {hashName} 失败：" + filePath);
                    return "";
                }

                retry = true;
                LauncherLogger.Log(ex, $"获取文件 {hashName} 可重试失败：" + filePath, LauncherLogger.LogLevel.Normal);
                Thread.Sleep(RandomUtils.NextInt(200, 500));
            }
        }
    }
}
