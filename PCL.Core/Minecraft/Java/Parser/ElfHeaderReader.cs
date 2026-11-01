using System;
using System.Buffers.Binary;
using System.IO;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

/// <summary>
///     ELF 文件头读取器：Windows 以外的 Java 可执行文件是 ELF 格式，用 e_machine 判断架构。
/// </summary>
internal static class ElfHeaderReader
{
    private const ushort MachineX86 = 3;
    private const ushort MachineArm = 40;
    private const ushort MachineX8664 = 62;
    private const ushort MachineAarch64 = 183;

    private const int DataEncodingOffset = 5;
    private const int MachineOffset = 18;

    /// <summary>读取 ELF 头；不是 ELF 文件或架构不受支持时返回 null。</summary>
    public static (MachineType Architecture, bool Is64Bit)? TryRead(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var header = new byte[MachineOffset + 2];
            if (stream.Read(header, 0, header.Length) < header.Length)
                return null;
            if (header[0] != 0x7F
                || header[1] != (byte)'E'
                || header[2] != (byte)'L'
                || header[3] != (byte)'F')
                return null;

            var span = header.AsSpan(MachineOffset, 2);
            var littleEndian = header[DataEncodingOffset] == 1;
            var machine = littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(span)
                : BinaryPrimitives.ReadUInt16BigEndian(span);

            return machine switch
            {
                MachineX86 => (MachineType.I386, false),
                MachineX8664 => (MachineType.AMD64, true),
                MachineArm => (MachineType.ARM, false),
                MachineAarch64 => (MachineType.ARM64, true),
                _ => null
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}