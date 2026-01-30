// This code is from terracota project.
// Thanks for Burning_TNT's contribution!

using PCL.Core.Link.Scaffolding.Client.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PCL.Core.Link.Scaffolding;

public static class LobbyCodeGenerator
{
    private const string Chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string FullCodePrefix = "U/";
    private const string NetworkNamePrefix = "scaffolding-mc-";
    private const int BaseVal = 34;

    private const int DataLength = 16; // NNNN NNNN SSSS SSSS (16 chars)
    private const int HyphenCount = 3;
    private const int PayloadLength = DataLength + HyphenCount; // 19
    private const int CodeLength = PayloadLength + 2; // 21 ("U/")

    private static readonly UInt128 _EncodingMaxValue;

    private static readonly Dictionary<char, byte> _CharToValueMap;

    static LobbyCodeGenerator()
    {
        _EncodingMaxValue = _CalculatePower(BaseVal, DataLength);


        _CharToValueMap = new Dictionary<char, byte>(36);
        for (byte i = 0; i < Chars.Length; i++)
        {
            _CharToValueMap[Chars[i]] = i;
        }

        _CharToValueMap['I'] = 1;
        _CharToValueMap['O'] = 0;
    }

    public static LobbyInfo Generate()
    {
        var randomValue = _GetSecureRandomUInt128();
        var valueInRange = randomValue % _EncodingMaxValue;
        var remainder = valueInRange % 7;
        var validValue = randomValue - remainder;

        return _Encode(validValue);
    }

    public static bool TryParse(string input, [NotNullWhen(true)] out LobbyInfo? roomInfo)
    {
        roomInfo = null;

        if (string.IsNullOrWhiteSpace(input) ||
            !input.StartsWith(FullCodePrefix, StringComparison.Ordinal) ||
            input.Length != 21)
        {
            return false;
        }

        Span<byte> values = stackalloc byte[DataLength];
        var valueIndex = 0;
        var payloadSpan = input.AsSpan(FullCodePrefix.Length);

        for (var i = 0; i < payloadSpan.Length; i++)
        {
            var ch = payloadSpan[i];
            if (ch == '-')
            {
                if (i != 4 && i != 9 && i != 14)
                {
                    return false;
                }

                continue;
            }

            if (valueIndex >= DataLength ||
                !_CharToValueMap.TryGetValue(char.ToUpperInvariant(ch), out var charValue))
            {
                return false;
            }

            values[valueIndex++] = charValue;
        }

        if (valueIndex != DataLength)
        {
            return false;
        }

        UInt128 value = 0;
        for (var i = DataLength - 1; i >= 0; i--)
        {
            value = value * BaseVal + values[i];
        }

        if (value % 7 != 0)
        {
            return false;
        }

        var networkNamePayload = payloadSpan[..9];
        var networkSecretPayload = payloadSpan[10..];

        roomInfo = new LobbyInfo(
            string.Concat(FullCodePrefix, payloadSpan).ToUpperInvariant(),
            string.Concat(NetworkNamePrefix, networkNamePayload),
            networkSecretPayload.ToString());

        return true;
    }

    private static LobbyInfo _Encode(UInt128 value)
    {
        var codePayload = string.Create(PayloadLength, value, (span, val) =>
        {
            Span<char> tempChars = stackalloc char[DataLength];
            for (var i = 0; i < DataLength; i++)
            {
                tempChars[i] = Chars[(int)(val % BaseVal)];
                val /= BaseVal;
            }

            tempChars[..4].CopyTo(span[..4]);
            span[4] = '-';
            tempChars[4..8].CopyTo(span[5..9]);
            span[9] = '-';
            tempChars[8..12].CopyTo(span[10..14]);
            span[14] = '-';
            tempChars[12..16].CopyTo(span[15..]);
        });

        var networkNamePayload = codePayload.AsSpan(0, 9);
        var networkSecretPayload = codePayload.AsSpan(10);

        return new LobbyInfo(
            string.Concat(FullCodePrefix, codePayload),
            string.Concat(NetworkNamePrefix, networkNamePayload),
            networkSecretPayload.ToString());
    }

    private static UInt128 _GetSecureRandomUInt128()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        var lower = MemoryMarshal.Read<ulong>(bytes);
        var upper = MemoryMarshal.Read<ulong>(bytes[8..]);

        return new UInt128(lower, upper);
    }

    private static UInt128 _CalculatePower(uint baseVal, int exp)
    {
        UInt128 result = 1;
        for (var i = 0; i < exp; i++)
        {
            result *= baseVal;
        }

        return result;
    }
}