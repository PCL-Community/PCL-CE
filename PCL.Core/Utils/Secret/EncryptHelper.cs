using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Exts;
using PlainToolkit.CngProtectedData;
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DataProtectionScope = PlainToolkit.CngProtectedData.DataProtectionScope;


namespace PCL.Core.Utils.Secret;

public static class EncryptHelper
{
    private const string ModelName = "EncryptHelper";

    private static readonly byte[] _Key = "PCL CE Encryption Key"u8.ToArray();
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
#pragma warning disable CS0612,CS0618 // Type or member is obsolete
                var decryptedData = AesCbcProvider.Instance.Decrypt(rawData, Encoding.UTF8.GetBytes(IdentifyOld.EncryptKey));
#pragma warning restore CS0612,CS0618 // Type or member is obsolete
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

    private static readonly object _KeyLock = new();
    private static byte[]? _cachedKey;

    /// <summary>
    /// 用户密钥。只在成功获取后缓存——失败不会被记住，下次访问会重新尝试。<br/>
    /// 注意：密钥文件的 version（1 = 旧版 CAPI DPAPI，2 = CNG DPAPI）与
    /// <see cref="SecretEncrypt"/> 的数据算法 version（0/1/2）语义无关。
    /// </summary>
    internal static byte[] EncryptionKey
    {
        get { lock (_KeyLock) return _cachedKey ??= _LoadOrCreateKey(); }
    }

    //internal static string KeyFilePath => Path.Combine(Paths.SharedData, "UserKey.bin");

    // NOTE: only used for unit test
    // NOTE: do not touch on production env
    private static string? _keyFileverride;
    internal static void OverrideKeyFile(string? path) => _keyFileverride = path;

    internal static string KeyFilePath => _keyFileverride ?? Path.Combine(Paths.SharedData, "UserKey.bin");

    internal static void ResetKeyCache() { lock (_KeyLock) _cachedKey = null; }

    private static byte[] _LoadOrCreateKey()
    {
        var keyFile = KeyFilePath;
        try
        {
            if (File.Exists(keyFile)) return _ReadKey(keyFile);
        }
        // IOException / UnauthorizedAccessException 属瞬时故障（占用、杀软扫描），故意不在此列：
        // 照常抛出、下次访问重试，绝不能因此换掉一把还能用的密钥。
        catch (Exception ex) when (_IsKeyUnusable(ex))
        {
            LogWrapper.Error(ex, "Encryption",
                $"用户密钥无法解密，将重建（HRESULT=0x{ex.HResult:X8}，file={keyFile}）");
        }
        return _CreateKey(keyFile);
    }

    /// <summary>读取密钥；v1 密钥会在解密成功后惰性迁移为 v2。内容不可用时抛出。</summary>
    private static byte[] _ReadKey(string keyFile)
    {
        var data = EncryptionData.FromBytes(File.ReadAllBytes(keyFile));
        switch (data.Version)
        {
            case 2:
                return CngProtectedData.Unprotect(data.Data, _Key, DataProtectionScope.CurrentUser);
            case 1:
                var legacyKey = ProtectedData.Unprotect(data.Data, _Key, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                if (_TryProtect(legacyKey, out var migrated) && _WriteKeyFile(keyFile, migrated))
                    LogWrapper.Info("Encryption", $"用户密钥已迁移至 version 2（CNG DPAPI）：{keyFile}");
                return legacyKey;
            default:
                throw new NotSupportedException($"Unsupported key version: {data.Version}");
        }
    }

    /// <summary>生成并落盘一把新密钥。本机 DPAPI 不可用时抛出（不伪造密钥，避免每次启动换钥匙）。</summary>
    private static byte[] _CreateKey(string keyFile)
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        if (!_TryProtect(key, out var storeData))
            throw new InvalidOperationException("本机 DPAPI 不可用，无法创建用户密钥");
        _QuarantineBrokenKeyFile(keyFile); // 已确认能重建，才动原文件
        if (!_WriteKeyFile(keyFile, storeData))
            LogWrapper.Error("Encryption", $"用户密钥写入失败，本次运行仅使用内存密钥：{keyFile}");
        else
            LogWrapper.Warn("Encryption", "用户密钥已重建（version 2）。旧密钥保护的数据无法恢复，需要重新登录。");
        return key;
    }

    /// <summary>
    /// 用 CNG DPAPI 保护密钥，并确认这份密文能读回原密钥后，才序列化为可落盘的字节。<br/>
    /// 读不回说明本机 DPAPI 不可用或该 provider 不接受当前参数——此时宁可失败也不落盘，
    /// 否则下次启动照样解不开，会退化成「每次启动换一把钥匙」。
    /// </summary>
    private static bool _TryProtect(byte[] rawKey, out byte[] storeData)
    {
        try
        {
            var blob = CngProtectedData.Protect(rawKey, _Key, DataProtectionScope.CurrentUser);
            var roundTrip = CngProtectedData.Unprotect(blob, _Key, DataProtectionScope.CurrentUser);
            storeData = EncryptionData.ToBytes(new EncryptionData { Version = 2, Data = blob });
            return rawKey.AsSpan().SequenceEqual(roundTrip);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Encryption", "CNG DPAPI 自检失败，本机可能无法保存用户密钥");
            storeData = [];
            return false;
        }
    }

    /// <summary>原子写入密钥文件（同目录临时文件 + 覆盖式移动）。</summary>
    private static bool _WriteKeyFile(string keyFile, byte[] storeData)
    {
        var tmpFile = $"{keyFile}.tmp{RandomUtils.NextInt(10000, 99999)}";
        try
        {
            using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Write(storeData);
                fs.Flush(true);
            }
            File.Move(tmpFile, keyFile, true);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Encryption", $"写入用户密钥失败：{keyFile}");
            try { File.Delete(tmpFile); } catch { /* 清理失败可忽略 */ }
            return false;
        }
    }

    /// <summary>把无法解密的密钥文件改名留存，便于取证。</summary>
    private static void _QuarantineBrokenKeyFile(string keyFile)
    {
        try
        {
            if (File.Exists(keyFile))
                File.Move(keyFile, $"{keyFile}.broken-{DateTime.Now:yyyyMMddHHmmss}", true);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Encryption", $"留存无法解密的用户密钥失败：{keyFile}");
        }
    }

    /// <summary>只有「内容确实不可用」才重建；I/O 类异常视为瞬时故障。</summary>
    private static bool _IsKeyUnusable(Exception ex) =>
        ex is CryptographicException or NotSupportedException or ArgumentException
            or FormatException or Win32Exception;


    #endregion
}
