using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft;

[LifecycleService(LifecycleState.Loaded)]
public sealed class JavaService : GeneralService
{
    private static LifecycleContext? _context;
    public static LifecycleContext Context => _context!;

    /// <inheritdoc />
    public JavaService() : base("java", "Java管理")
    {
        _context = Lifecycle.GetContext(this);
    }

    private static JavaManager? _javaManager;
    public static JavaManager JavaManager => _javaManager!;

    public override void Start()
    {
        if (_javaManager is not null) return;

        Context.Info("Initializing Java Manager...");

        _javaManager = new JavaManager();
        _javaManager.ScanJavaAsync().ContinueWith(_ =>
        {
            LoadFromConfig(); // Java 启用情况以及额外手动添加的 Java
            SaveToConfig();

            var logInfo = string.Join("\n\t", _javaManager.JavaList);
            Context.Info($"Finished to scan java: {logInfo}");
        }, TaskScheduler.Default);
    }

    public override void Stop()
    {
        if (_javaManager is null) return;

        SaveToConfig();
        _javaManager = null;
    }

    public static void LoadFromConfig()
    {
        if (_javaManager is null) return;

        var raw = Config.Launch.JavaList;
        if (raw.IsNullOrWhiteSpace()) return;

        Context.Info("Loading java configs...");
        var caches = JsonSerializer.Deserialize<List<JavaLocalCache>>(raw);
        if (caches is null)
        {
            Context.Warn("Reading java configs fail: Failed to deserialize json");
            return;
        }

        foreach (var cache in caches)
        {
            try
            {
                _javaManager.Add(cache.Path);
                var targetInRecord = _javaManager.InternalJavas.FirstOrDefault(x => x.JavaExePath == Path.GetFullPath(cache.Path));
                if (targetInRecord is not null)
                    targetInRecord.IsEnabled = cache.IsEnable;
            }
            catch(Exception e)
            {
                Context.Error("Error in apply java config", e);
                var temp = JavaInfo.Parse(cache.Path);
                if (temp == null)
                    continue;
                temp.IsEnabled = cache.IsEnable;
                _javaManager.InternalJavas.Add(temp);
            }
        }
    }

    public static void SaveToConfig()
    {
        if (_javaManager is null) return;

        var caches = _javaManager.InternalJavas.Select(x => new JavaLocalCache
        {
            IsEnable = x.IsEnabled,
            Path = x.JavaExePath
        }).ToList();

        var jsonContent = JsonSerializer.Serialize(caches);
        if (jsonContent.IsNullOrEmpty()) return;

        Config.Launch.JavaList = jsonContent;
    }

    private class JavaLocalCache
    {
        public required string Path { get; init; }
        public bool IsEnable { get; init; }
    }

}