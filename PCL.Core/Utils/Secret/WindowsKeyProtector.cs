using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using PCL.Core.Logging;
using PlainToolkit.CngProtectedData;
using DataProtectionScope = PlainToolkit.CngProtectedData.DataProtectionScope;

namespace PCL.Core.Utils.Secret;

/// <summary>Windows DPAPI 保护：v2 = CNG DPAPI，v1 = 旧版 CAPI DPAPI（只读，解密成功后迁移到 v2）。</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsKeyProtector(byte[] entropy) : IKeyProtector
{
    public uint Version => 2;

    public string Description => "CNG DPAPI";

    public bool TryProtect(byte[] rawKey, out byte[] storeData)
    {
        try
        {
            var blob = CngProtectedData.Protect(rawKey, entropy, DataProtectionScope.CurrentUser);
            var roundTrip = CngProtectedData.Unprotect(
                blob,
                entropy,
                DataProtectionScope.CurrentUser);
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
            LogWrapper.Warn(ex, "Encryption", "CNG DPAPI 自检失败，本机可能无法保存用户密钥");
            storeData = [];
            return false;
        }
    }

    public byte[] Unprotect(uint version, byte[] data)
    {
        return version switch
        {
            2 => CngProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser),
            1 => ProtectedData.Unprotect(
                data,
                entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser),
            _ => throw new NotSupportedException($"Unsupported key version: {version}")
        };
    }
}