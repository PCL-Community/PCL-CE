// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PCL.Core.Link.Scaffolding;

/// <summary>
/// Generates and parses 64-bit lobby identifiers in XXXX-XXXX-XXXX-XXXX form.
/// </summary>
public static class LobbyCodeGenerator
{
    private const int ByteCount = 8;
    private const int CompactLength = ByteCount * 2;
    private const int FormattedLength = CompactLength + 3;
    private static ReadOnlySpan<char> HexDigits => "0123456789ABCDEF";

    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        RandomNumberGenerator.Fill(bytes);
        Span<char> formatted = stackalloc char[FormattedLength];
        Format(bytes, formatted);
        return new string(formatted);
    }

    public static string? TryParse(string input)
    {
        if (!TryDecode(input, out var value))
            return null;

        Span<byte> bytes = stackalloc byte[ByteCount];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        Span<char> formatted = stackalloc char[FormattedLength];
        Format(bytes, formatted);
        return new string(formatted);
    }

    public static byte[]? GetRoomId(string code)
    {
        if (!TryDecode(code, out var value))
            return null;

        var bytes = GC.AllocateUninitializedArray<byte>(ByteCount);
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    public static string ToShortCode(string fullCode)
    {
        if (!TryDecode(fullCode, out var value))
            return string.Empty;

        value >>= 32;
        Span<char> shortCode = stackalloc char[8];
        for (var index = shortCode.Length - 1; index >= 0; index--)
        {
            shortCode[index] = HexDigits[(int)(value & 0xF)];
            value >>= 4;
        }
        return new string(shortCode);
    }

    private static bool TryDecode(string? input, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var digitCount = 0;
        foreach (var character in input)
        {
            if (character is '-' or ' ')
                continue;

            var digit = HexValue(character);
            if (digit < 0 || digitCount == CompactLength)
                return false;

            value = (value << 4) | (uint)digit;
            digitCount++;
        }

        return digitCount == CompactLength;
    }

    private static int HexValue(char character) =>
        character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'A' and <= 'F' => character - 'A' + 10,
            >= 'a' and <= 'f' => character - 'a' + 10,
            _ => -1
        };

    private static void Format(ReadOnlySpan<byte> bytes, Span<char> destination)
    {
        var destinationIndex = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (index is 2 or 4 or 6)
                destination[destinationIndex++] = '-';
            destination[destinationIndex++] = HexDigits[bytes[index] >> 4];
            destination[destinationIndex++] = HexDigits[bytes[index] & 0xF];
        }
    }
}
