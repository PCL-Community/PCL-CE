using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.IO;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Secret;

public static class EncryptHelper
{
    public static (IEncryptionProvider Provider, uint Version) DefaultProvider => _DefaultProvider.Value;
    private static readonly Lazy<(IEncryptionProvider Provider, uint Version)> _DefaultProvider = new(_SelectBestEncryption);

    private static (IEncryptionProvider Provider, uint Version) _SelectBestEncryption()
    {
        var aesHardwareSupport = System.Runtime.Intrinsics.X86.Aes.IsSupported ||
                                 System.Runtime.Intrinsics.Arm.Aes.IsSupported;
        if (aesHardwareSupport && AesGcmProvider.Instance.IsSupported) return (AesGcmProvider.Instance, 2);
        if (ChaCha20Poly1305Provider.Instance.IsSupported) return (ChaCha20Poly1305Provider.Instance, 1);
        return (ChaCha20SoftwareProvider.Instance, 0);
    }

    public static string SecretEncrypt(string? data)
    {
        if (data.IsNullOrEmpty()) return string.Empty;
        var rawData = Encoding.UTF8.GetBytes(data);

        return Convert.ToBase64String(EncryptionData.ToBytes(new EncryptionData
            { Version = DefaultProvider.Version, Data = DefaultProvider.Provider.Encrypt(rawData, EncryptionKey) }));
    }

    public static string SecretDecrypt(string? data)
    {
        if (data.IsNullOrEmpty()) return string.Empty;
        var rawData = Convert.FromBase64String(data);
        Exception? decryptError;
        if (EncryptionData.IsValid(rawData))
        {
            try
            {
                var encryptionData = EncryptionData.FromBytes(rawData);
                IEncryptionProvider provider = encryptionData.Version switch
                {
                    0 => ChaCha20SoftwareProvider.Instance,
                    1 => ChaCha20Poly1305Provider.Instance,
                    2 => AesGcmProvider.Instance,
                    _ => throw new NotSupportedException("Unsupported encryption version")
                };
                var decryptedData = provider.Decrypt(encryptionData.Data, EncryptionKey);
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex) { decryptError = ex; }
        }
        else
        {
            try
            {
                var decryptedData = AesCbcProvider.Instance.Decrypt(rawData, Encoding.UTF8.GetBytes(IdentifyOld.EncryptKey));
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex) { decryptError = ex; }
        }

        throw new Exception($"Unknown Encryption data, the data may broken", decryptError);
    }

    #region "加密存储信息数据"


    public struct EncryptionData
    {
        public uint Version;
        public byte[] Data;

        private const uint MagicNumber = 0x454E4321;

        public static EncryptionData FromBase64(string base64)
        {
            return FromBytes(Convert.FromBase64String(base64));
        }

        public static EncryptionData FromBytes(ReadOnlySpan<byte> bytes)
        {
            // 0 - 4  MagicNumber |  4 - 8 version || 8 - 12 bytes rData length | n bytes rData
            if (bytes.Length < 12)
                throw new ArgumentException("No enough data for EncryptionData", nameof(bytes));

            if (BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]) != MagicNumber)
                throw new ArgumentException("Unknown data for EncryptionData", nameof(bytes));

            var dataLength = BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]);
            if (dataLength > bytes.Length - 12)
                throw new ArgumentException("No enough data for EncryptionData", nameof(bytes));
            if (dataLength < 0)
                throw new ArgumentException("Invalid data length for EncryptionData", nameof(bytes));

            var rData = bytes[12..(12 + dataLength)];

            return new EncryptionData
            {
                Version = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..8]),
                Data = rData.ToArray()
            };
        }

        public static byte[] ToBytes(EncryptionData encryptionData)
        {
            var length = 12 + encryptionData.Data.Length;
            var bytes = new byte[length];
            var bytesSpan = bytes.AsSpan();
            BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[..4], MagicNumber);
            BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[4..8], encryptionData.Version);
            BinaryPrimitives.WriteInt32BigEndian(bytesSpan[8..12], encryptionData.Data.Length);
            encryptionData.Data.CopyTo(bytesSpan[12..]);

            return bytes;
        }

        public static bool IsValid(ReadOnlySpan<byte> data)
        {
            try
            {
                return data.Length >= 12 && BinaryPrimitives.ReadUInt32BigEndian(data[..4]) == MagicNumber;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region "密钥存储和获取"

    private static readonly byte[] _IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");
    internal static byte[] EncryptionKey { get => _EncryptionKey.Value; }
    private static readonly Lazy<byte[]> _EncryptionKey = new(_GetKey);

    private static byte[] _GetKey()
    {
        var keyFile = Path.Combine(FileService.SharedDataPath, "UserKey.bin");
        if (File.Exists(keyFile))
        {
            var buf = File.ReadAllBytes(keyFile);
            var data = EncryptionData.FromBytes(buf);
            return data.Version switch
            {
                1 => ProtectedData.Unprotect(data.Data, _IdentifyEntropy, DataProtectionScope.CurrentUser),
                _ => throw new NotSupportedException("Unsupported encryption version")
            };
        }
        else
        {
            var randomKey = new byte[32];
            RandomNumberGenerator.Fill(randomKey);
            var storeData = EncryptionData.ToBytes(new EncryptionData
            {
                Version = 1,
                Data = ProtectedData.Protect(randomKey, _IdentifyEntropy, DataProtectionScope.CurrentUser)
            });

            var tmpFile = $"{keyFile}.tmp{RandomUtils.NextInt(10000, 99999)}";
            using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Write(storeData);
                fs.Flush(true);
            }

            File.Move(tmpFile, keyFile, true);

            return randomKey;
        }
    }

    #endregion
}