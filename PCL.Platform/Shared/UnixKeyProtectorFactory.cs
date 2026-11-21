using PCL.Core.Platform;
using PCL.Core.Utils.Secret;

namespace PCL.Platform;

/// <summary>Unix 机器绑定密钥保护器工厂。</summary>
public sealed class UnixKeyProtectorFactory : IKeyProtectorFactory
{
    public IKeyProtector Create(byte[] key)
    {
        return new UnixKeyProtector(key);
    }
}