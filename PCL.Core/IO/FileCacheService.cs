using System.IO;
using PCL.Core.App;

namespace PCL.Core.IO;

[LifecycleService(LifecycleState.Loaded, Priority = 1919820)]
[LifecycleScope("cache", "文件缓存")]
public partial class FileCacheService
{
    [LifecycleStart]
    private static void _Start()
    {
        _InitializeCache();
    }

    public static string CachePath { get; private set; } = @"PCL\CE\_Cache";

    private static void _InitializeCache()
    {
        CachePath = Path.Combine(FileService.TempPath, "cache");
        Context.Debug($"当前缓存目录: {CachePath}");
        Directory.CreateDirectory(CachePath);
        var cacheInfo = FileService.WaitForResult(PredefinedFileItems.CacheInformation)?.Try<string>();
        Context.Trace(cacheInfo ?? "NUL");
    }
}
