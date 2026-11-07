namespace PCL.Core.Utils.Secret;

/// <summary>
///     用户密钥文件的本地保护方案：Windows 使用 DPAPI，其他平台使用机器/用户绑定的 AES-GCM。
/// </summary>
public interface IKeyProtector
{
    /// <summary>新建密钥时写入的存储版本。</summary>
    uint Version { get; }

    /// <summary>写入日志的算法名称。</summary>
    string Description { get; }

    /// <summary>保护并自检密钥；本机保护不可用时返回 <c>false</c>，此时绝不能落盘。</summary>
    bool TryProtect(byte[] rawKey, out byte[] storeData);

    /// <summary>解密密钥文件内容；当前平台不支持该版本或内容损坏时抛出异常。</summary>
    byte[] Unprotect(uint version, byte[] data);
}