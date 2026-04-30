using PCL.Core.Utils.Hash;
using System;
using System.Text;

namespace PCL.Core.Utils;

public static class TextUtils
{
    /// <summary>
    /// 为字符串进行 XML 转义。
    /// </summary>
    public static string EscapeXml(string str)
    {
        if (str.StartsWith('{'))
            str = "{}" + str; // #4187

        return str.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("\r\n", "&#xa;");
    }

    /// <summary>
    /// 为字符串进行 Like 关键字转义。
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
            switch (c)
            {
                case '[':
                case ']':
                case '*':
                case '?':
                case '#':
                    {
                        sb.Append($"[{c}]");
                        break;
                    }

                default:
                    {
                        sb.Append(c);
                        break;
                    }
            }

        return sb.ToString();
    }

    /// <summary>
    /// 获取字符串哈希值。
    /// </summary>
    public static ulong GetHash(string str)
    {
        var getHashRet = 5381UL;
        for (int i = 0, loopTo = str.Length - 1; i <= loopTo; i++)
            getHashRet = (getHashRet << 5) ^ getHashRet ^ str[i];
        return getHashRet ^ 0xA98F501BC684032FUL;
    }



    /// <summary>
    /// 获取字符串 MD5。
    /// </summary>
    public static string GetStringMD5(string str)
    {
        return GetHexString(MD5Provider.Instance.ComputeHash(str));
    }


    public static string GetHexString(Memory<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var c in bytes.Span)
            sb.Append(c.ToString("x2"));

        return sb.ToString();
    }
}