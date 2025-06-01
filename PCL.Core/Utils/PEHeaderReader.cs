using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Utils.PE
{
    /// <summary>
    /// 通用化 PE 文件头读取器
    /// </summary>
    public class PEHeaderReader
    {
        private const int PE_POINTER_OFFSET = 0x3C;
        private const uint PE_SIGNATURE = 0x00004550; // "PE\0\0"

        /// <summary>
        /// 读取并解析 PE 文件头结构
        /// </summary>
        public static PEStruct ReadPEHeader(string filePath)
        {
            var result = new PEStruct();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                result.ErrorMessage = "文件不存在或路径无效";
                return result;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 验证 DOS 头
                    if (!IsValidDosHeader(fs))
                    {
                        result.ErrorMessage = "无效的DOS头(MZ签名)";
                        return result;
                    }

                    // 获取 NT 头偏移量
                    long peHeaderOffset = GetPEOffset(fs);
                    if (peHeaderOffset <= 0 || peHeaderOffset >= fs.Length - 24)
                    {
                        result.ErrorMessage = "无效的PE头偏移量";
                        return result;
                    }

                    // 定位并验证 PE 签名
                    fs.Seek(peHeaderOffset, SeekOrigin.Begin);
                    if (!IsValidPESignature(fs))
                    {
                        result.ErrorMessage = "无效的PE签名";
                        return result;
                    }

                    // 读取完整的 IMAGE_FILE_HEADER 结构
                    result = ParseImageFileHeader(fs);
                    result.IsValid = true;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"读取失败: {ex.Message}";
            }
            return result;
        }

        private static bool IsValidDosHeader(FileStream fs)
        {
            if (fs.Length < 2) return false;
            fs.Seek(0, SeekOrigin.Begin);
            return fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
        }

        private static long GetPEOffset(FileStream fs)
        {
            fs.Seek(PE_POINTER_OFFSET, SeekOrigin.Begin);
            using (var reader = new BinaryReader(fs, Encoding.Default, true))
            {
                return reader.ReadInt32();
            }
        }

        private static bool IsValidPESignature(FileStream fs)
        {
            using (var reader = new BinaryReader(fs, Encoding.Default, true))
            {
                return reader.ReadUInt32() == PE_SIGNATURE;
            }
        }

        private static PEStruct ParseImageFileHeader(FileStream fs)
        {
            using (var reader = new BinaryReader(fs, Encoding.Default, true))
            {
                return new PEStruct
                {
                    // 将读取的ushort值转换为MachineType枚举
                    Machine = (MachineType)reader.ReadUInt16(),
                    NumberOfSections = reader.ReadUInt16(),
                    TimeDateStamp = reader.ReadUInt32(),
                    PointerToSymbolTable = reader.ReadUInt32(),
                    NumberOfSymbols = reader.ReadUInt32(),
                    SizeOfOptionalHeader = reader.ReadUInt16(),
                    Characteristics = reader.ReadUInt16()
                };
            }
        }

        
    }

    /// <summary>
    /// PE 文件机器架构类型
    /// </summary>
    public enum MachineType : ushort
    {
        Unknown = 0x0,
        I386 = 0x14C,        // x86
        IA64 = 0x200,        // Intel Itanium
        AMD64 = 0x8664,      // x64 (AMD or Intel)
        ARM = 0x1C0,         // ARM little endian
        ARM64 = 0xAA64,      // ARM64 little endian
        ARMNT = 0x1C4,       // ARM Thumb-2 little endian
        EFI_BYTECODE = 0xEBC, // EFI byte code
        M32R = 0x9041,       // Mitsubishi M32R little endian
        MIPS16 = 0x266,      // MIPS16
        MIPSFPU = 0x366,     // MIPS with FPU
        MIPSFPU16 = 0x466,   // MIPS16 with FPU
        POWERPC = 0x1F0,     // Power PC little endian
        POWERPCFP = 0x1F1,   // Power PC with floating point support
        R4000 = 0x166,       // MIPS little endian
        SH3 = 0x1A2,         // Hitachi SH3
        SH3DSP = 0x1A3,      // Hitachi SH3 DSP
        SH4 = 0x1A6,         // Hitachi SH4
        SH5 = 0x1A8,         // Hitachi SH5
        THUMB = 0x1C2,       // Thumb
        WCEMIPSV2 = 0x169,   // MIPS little-endian WCE v2
    }

    public class PEStruct
    {
        public MachineType Machine { get; set; }
        public ushort NumberOfSections { get; set; }
        public uint TimeDateStamp { get; set; }
        public uint PointerToSymbolTable { get; set; }
        public uint NumberOfSymbols { get; set; }
        public ushort SizeOfOptionalHeader { get; set; }
        public ushort Characteristics { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 判断是否为64位架构
        /// </summary>
        public bool Is64Bit => Machine == MachineType.AMD64 || Machine == MachineType.IA64 || Machine == MachineType.ARM64;
    }
}
