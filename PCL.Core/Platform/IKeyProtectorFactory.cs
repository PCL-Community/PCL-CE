using PCL.Core.Utils.Secret;

namespace PCL.Core.Platform;

/// <summary>创建本机用户密钥保护器。</summary>
public interface IKeyProtectorFactory
{
    IKeyProtector Create(byte[] key);
}