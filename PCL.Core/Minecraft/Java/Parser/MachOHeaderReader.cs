using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

internal static class MachOHeaderReader
{
    private const uint Magic64 = 0xFEEDFACF;
    private const uint MagicFat = 0xCAFEBABE;
    private const uint CpuTypeX8664 = 0x01000007;
    private const uint CpuTypeArm64 = 0x0100000C;
    private const int MaxFatArchCount = 30;
    private const int FatArchSize = 20;

    public static (MachineType Architecture, bool Is64Bit)? TryRead(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var header = new byte[8];
            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length)
                return null;

            if (BinaryPrimitives.ReadUInt32LittleEndian(header) == Magic64)
            {
                var cpuType = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
                return _MapCpuType(cpuType);
            }

            if (BinaryPrimitives.ReadUInt32BigEndian(header) != MagicFat)
                return null;

            var archCount = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
            if (archCount is 0 or > MaxFatArchCount)
                return null;

            var entries = new byte[(int)archCount * FatArchSize];
            if (stream.ReadAtLeast(entries, entries.Length, throwOnEndOfStream: false) < entries.Length)
                return null;

            var preferredCpuType = _GetProcessCpuType();
            (MachineType Architecture, bool Is64Bit)? fallback = null;
            for (var i = 0; i < (int)archCount; i++)
            {
                var cpuType = BinaryPrimitives.ReadUInt32BigEndian(
                    entries.AsSpan(i * FatArchSize, 4));
                if (_MapCpuType(cpuType) is not { } architecture)
                    continue;
                if (cpuType == preferredCpuType)
                    return architecture;
                fallback ??= architecture;
            }

            return fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (MachineType Architecture, bool Is64Bit)? _MapCpuType(uint cpuType)
    {
        return cpuType switch
        {
            CpuTypeX8664 => (MachineType.AMD64, true),
            CpuTypeArm64 => (MachineType.ARM64, true),
            _ => null
        };
    }

    private static uint _GetProcessCpuType()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => CpuTypeX8664,
            Architecture.Arm64 => CpuTypeArm64,
            _ => 0
        };
    }
}