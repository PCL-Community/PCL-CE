using System;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.Link.Scaffolding;

/// <summary>
///     联机大厅码生成与解析。
///     大厅码格式：4 段 × 4 位十六进制，如 "A3F2-91BC-440E-7D5A"。
///     用于 P2P 信令服务器中的房间标识和配对。
/// </summary>
public static class LobbyCodeGenerator
{
    /// <summary>
    ///     生成一个随机大厅码。
    /// </summary>
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);

        var code = new StringBuilder(19);
        for (var i = 0; i < 8; i++)
        {
            if (i > 0 && i % 2 == 0)
                code.Append('-');
            code.Append(bytes[i].ToString("X2"));
        }

        return code.ToString();
    }

    /// <summary>
    ///     尝试解析大厅码字符串。规范化后返回，若无效则返回 null。
    /// </summary>
    public static string? TryParse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // 去除空格和连字符
        var cleaned = new StringBuilder(16);
        foreach (var c in input)
        {
            if (c is >= '0' and <= '9')
                cleaned.Append(c);
            else if (c is >= 'A' and <= 'F')
                cleaned.Append(c);
            else if (c is >= 'a' and <= 'f')
                cleaned.Append(char.ToUpperInvariant(c));
            else if (c is '-' or ' ')
                continue;
            else
                return null; // 包含非法字符
        }

        if (cleaned.Length != 16)
            return null;

        // 重新格式化为 "XXXX-XXXX-XXXX-XXXX"
        return
            $"{cleaned.ToString(0, 4)}-{cleaned.ToString(4, 4)}-{cleaned.ToString(8, 4)}-{cleaned.ToString(12, 4)}";
    }

    /// <summary>
    ///     从大厅码解析出房间 ID（原始字节）。
    /// </summary>
    public static byte[]? GetRoomId(string code)
    {
        var parsed = TryParse(code);
        if (parsed is null) return null;

        var hex = parsed.Replace("-", "");
        var bytes = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                return null;
        }

        return bytes;
    }

    /// <summary>
    ///     从大厅码生成短链接码（8 位，用于手动输入）。
    /// </summary>
    public static string ToShortCode(string fullCode)
    {
        var parsed = TryParse(fullCode);
        if (parsed is null) return "";

        var hex = parsed.Replace("-", "");
        // 取前 8 位作为短码
        return hex[..8];
    }
}
