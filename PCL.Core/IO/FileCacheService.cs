using System.IO;
using PCL.Core.App;

namespace PCL.Core.IO;

[LifecycleService(LifecycleState.Loaded, Priority = 1919820)]
public class FileCacheService : GeneralService
{
    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    private FileCacheService() : base("cache", "文件缓存") { _context = ServiceContext; }

    public override void Start()
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
