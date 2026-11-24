using PCL.Core.Platform;
using PCL.Core.Utils.Secret;

namespace PCL.Platform;

/// <summary>Windows DPAPI 密钥保护器工厂。</summary>
public sealed class WindowsKeyProtectorFactory : IKeyProtectorFactory
{
    public IKeyProtector Create(byte[] key)
    {
        return new WindowsKeyProtector(key);
    }
}