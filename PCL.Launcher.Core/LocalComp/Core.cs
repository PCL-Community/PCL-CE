using PCL.Core.Utils.Hash;

namespace PCL;

public static partial class LocalComp
{
    private const int LocalModCacheVersion = 10;

    private static readonly Lazy<HashCache> HashCache = new(() =>
        new HashCache(Path.Combine(LauncherPaths.Temp, "Cache", "HashCache.db")));
}