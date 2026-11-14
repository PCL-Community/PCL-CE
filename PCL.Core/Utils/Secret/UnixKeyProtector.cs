using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Utils.Encryption;

namespace PCL.Core.Utils.Secret;

/// <summary>
///     非 Windows 平台的密钥保护：以「机器标识 + 当前用户 + 应用常量」经 HKDF 派生 AES-GCM 密钥。
///     机器标识优先取 <c>/etc/machine-id</c>，缺失时使用 ApplicationData 下持久化的随机标识。
///     密钥文件被复制到其他机器/用户后无法解密，交由上层隔离并重建。
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class UnixKeyProtector : IKeyProtector
{
    private const int KeySize = 32;
    private const string FallbackMachineIdFileName = "machine-id";

    private static readonly byte[] _HkdfInfo = [.. "PCL CE UserKey v3"u8];

    private static readonly string[] _MachineIdFiles =
        ["/etc/machine-id", "/var/lib/dbus/machine-id"];

    private readonly Lazy<byte[]> _derivedKey;

    private readonly byte[] _entropy;

    public UnixKeyProtector(byte[] entropy)
    {
        _entropy = entropy;
        _derivedKey = new Lazy<byte[]>(_DeriveKey);
    }

    public uint Version => 3;

    public string Description => "AES-GCM";

    public bool TryProtect(byte[] rawKey, out byte[] storeData)
    {
        try
        {
            var key = _derivedKey.Value;
            var blob = AesGcmProvider.Instance.Encrypt(rawKey, key);
            var roundTrip = AesGcmProvider.Instance.Decrypt(blob, key);
            storeData = EncryptHelper.EncryptionData.ToBytes(
                new EncryptHelper.EncryptionData
                {
                    Version = Version,
                    Data = blob
                });
            return rawKey.AsSpan().SequenceEqual(roundTrip);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Encryption", "AES-GCM 自检失败，本机可能无法保存用户密钥");
            storeData = [];
            return false;
        }
    }

    public byte[] Unprotect(uint version, byte[] data)
    {
        return version switch
        {
            3 => AesGcmProvider.Instance.Decrypt(data, _derivedKey.Value),
            1 or 2 => throw new NotSupportedException(
                $"密钥由 Windows DPAPI 保护（version {version}），当前平台无法解密"),
            _ => throw new NotSupportedException($"Unsupported key version: {version}")
        };
    }

    private byte[] _DeriveKey()
    {
        var ikm = Encoding.UTF8.GetBytes($"{_GetMachineId()}\n{Environment.UserName}");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeySize, _entropy, _HkdfInfo);
    }

    private static string _GetMachineId()
    {
        foreach (var file in _MachineIdFiles)
            try
            {
                var machineId = File.ReadAllText(file).Trim();
                if (machineId.Length > 0) return machineId;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 继续尝试下一个来源
            }

        return _GetOrCreateFallbackMachineId();
    }

    private static string _GetOrCreateFallbackMachineId()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appDataDir))
            throw new NotSupportedException("无法确定机器标识的持久化目录");
        var dir = Path.Combine(appDataDir, "PCLCE");
        var path = Path.Combine(dir, FallbackMachineIdFileName);

        try
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length > 0) return existing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 尚未创建，走下面的创建流程
        }

        Directory.CreateDirectory(dir);
        var machineId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        try
        {
            using var fs = new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                });
            fs.Write(Encoding.UTF8.GetBytes(machineId));
        }
        catch (IOException)
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length > 0) return existing;
            throw;
        }

        return machineId;
    }
}