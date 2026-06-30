using System;
using System.Text;

namespace PCL.Core.Utils;

/// <summary>
///     Base64 编解码工具。
/// </summary>
public static class Base64Utils
{
    public static string EncodeString(string text, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return Convert.ToBase64String(encoding.GetBytes(text));
    }

    public static string DecodeToString(string? text, Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        encoding ??= Encoding.UTF8;
        return encoding.GetString(DecodeToBytes(text));
    }

    public static string EncodeBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static byte[] DecodeToBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.FromBase64String(_AddPadding(text.Trim()));
    }

    public static string EncodeUrlSafe(byte[] bytes, bool trimPadding = true)
    {
        var encoded = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');
        return trimPadding ? encoded.TrimEnd('=') : encoded;
    }

    public static byte[] DecodeUrlSafeToBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(_AddPadding(text));
    }

    private static string _AddPadding(string text)
    {
        var remainder = text.Length % 4;
        return remainder == 0
            ? text
            : text.PadRight(text.Length + 4 - remainder, '=');
    }
}