using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.App.Configuration.Impl;
using PCL.Core.App.Configuration.NTraffic;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Configuration;

/// <summary>
/// 全局配置服务。
/// </summary>
[LifecycleService(LifecycleState.Loading, Priority = 1919810)]
[LifecycleScope("config", "配置")]
public sealed partial class ConfigService
{
    private static readonly Dictionary<string, ConfigItem> _Items = [];

    private static readonly HashSet<string> _KeySet = [];

    /// <summary>
    /// 配置键的集合。
    /// </summary>
    public static IReadOnlySet<string> KeySet => _KeySet;

    /// <summary>
    /// 全局配置文件的版本号。
    /// </summary>
    [ConfigItem<int>("FileVersion", 1)] public static partial int SharedVersion { get; set; }

    /// <summary>
    /// 本地配置文件的版本号。
    /// </summary>
    [ConfigItem<int>("LocalFileVersion", 1, ConfigSource.Local)] public static partial int LocalVersion { get; set; }

    /// <summary>
    /// 全局共享配置文件路径。
    /// </summary>
    public static string SharedConfigPath { get; } = Path.Combine(FileService.SharedDataPath, "config.v1.json");

    /// <summary>
    /// 本地配置文件路径。
    /// </summary>
    public static string LocalConfigPath { get; } = Path.Combine(FileService.DataPath, "config.v1.yml");

    #region Getters & Setters

    /// <summary>
    /// 尝试获取配置项的可观察对象。
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="item">返回可观察对象</param>
    /// <returns>若配置键存在，则为 <c>true</c>，否则为 <c>false</c></returns>
    public static bool TryGetConfigItemNoType(string key, [NotNullWhen(true)] out ConfigItem? item)
        => _Items.TryGetValue(key, out item);

    /// <summary>
    /// 尝试获取配置项。
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="item">返回配置项，若类型不匹配则为 <c>null</c></param>
    /// <typeparam name="TValue">配置项的值类型</typeparam>
    /// <returns>若配置键存在，则为 <c>true</c>，否则为 <c>false</c></returns>
    /// <exception cref="InvalidOperationException">配置项尚未初始化完成</exception>
    public static bool TryGetConfigItem<TValue>(string key, out ConfigItem<TValue>? item)
    {
        if (!_isConfigItemsInitialized) throw new InvalidOperationException("Not initialized");
        var result = TryGetConfigItemNoType(key, out var value);
        item = result ? (value as ConfigItem<TValue>) : null;
        return result;
    }

    /// <summary>
    /// 获取配置项。
    /// </summary>
    /// <param name="key">配置键</param>
    /// <typeparam name="TValue">配置项的值类型</typeparam>
    /// <returns>配置项实例</returns>
    /// <exception cref="InvalidOperationException">配置项尚未初始化完成</exception>
    /// <exception cref="KeyNotFoundException">配置键不存在</exception>
    /// <exception cref="InvalidCastException">值类型参数与实际类型不匹配</exception>
    public static ConfigItem<TValue> GetConfigItem<TValue>(string key)
    {
        var result = TryGetConfigItem<TValue>(key, out var item);
        if (!result) throw new KeyNotFoundException($"Config key not found: '{key}'");
        return item ?? throw new InvalidCastException($"Type of '{key}' is incompatible with {typeof(TValue).FullName}");
    }

    /// <summary>
    /// 向指定作用域批量注册事件观察器。
    /// </summary>
    /// <param name="scope"><see cref="IConfigScope"/> 实例</param>
    /// <param name="observer">观察器实例</param>
    public static void RegisterObserver(IConfigScope scope, ConfigObserver observer)
    {
        var itemKeys = scope.CheckScope(KeySet);
        foreach (var key in itemKeys)
        {
            var item = _Items[key];
            item.Observe(observer);
        }
    }

    #endregion

    #region Providers

    private static TrafficCenter? _sharedConfigProvider;
    private static TrafficCenter? _sharedEncryptedConfigProvider;
    private static TrafficCenter? _localConfigProvider;
    private static TrafficCenter? _instanceConfigProvider;

    /// <summary>
    /// 获取配置提供方。
    /// </summary>
    /// <param name="source">来源定义</param>
    /// <returns>提供方实例</returns>
    /// <exception cref="InvalidOperationException">配置提供方尚未初始化完成</exception>
    /// <exception cref="ArgumentException">来源定义无效</exception>
    public static IConfigProvider GetProvider(ConfigSource source)
    {
        if (!_isProvidersInitialized) throw new InvalidOperationException("Not initialized");
        return source switch
        {
            ConfigSource.Shared => _sharedConfigProvider!,
            ConfigSource.SharedEncrypt => _sharedEncryptedConfigProvider!,
            ConfigSource.Local => _localConfigProvider!,
            ConfigSource.GameInstance => _instanceConfigProvider!,
            _ => throw new ArgumentException($"Invalid source: {source}")
        };
    }

    private static void _InitializeProviders()
    {
        Action[] inits = [
            () => // shared config file
            {
                // try migrate
                if (!File.Exists(SharedConfigPath))
                {
                    string[] oldPaths = [
                        Path.Combine(FileService.OldSharedDataPath, "Config.json"),
                        Path.Combine(FileService.SharedDataPath, "config.json")
                    ];
                    _TryMigrate(SharedConfigPath, oldPaths.Select(path =>
                        new ConfigMigration { From = path, To = SharedConfigPath, OnMigration = SharedJsonMigration }));
                }
                // load
                var fileProvider = new JsonFileProvider(SharedConfigPath);
                var trafficCenter = new FileTrafficCenter(fileProvider);
                _sharedConfigProvider = trafficCenter;
                _sharedEncryptedConfigProvider = new EncryptedFileTrafficCenter(trafficCenter);
            },
            () => // local config file
            {
                // try migrate
                if (!File.Exists(LocalConfigPath)) _TryMigrate(LocalConfigPath, [
                    new ConfigMigration
                    {
                        From = Path.Combine(FileService.DataPath, "setup.ini"),
                        To = LocalConfigPath,
                        OnMigration = CatIniMigration
                    }
                ]);
                // load
                var fileProvider = new YamlFileProvider(LocalConfigPath);
                _localConfigProvider = new FileTrafficCenter(fileProvider);
            },
            () => // instance config file(s)
            {
                _instanceConfigProvider = new DynamicCacheTrafficCenter
                {
                    TrafficCenterFactory = argument =>
                    {
                        var dir = Path.GetFullPath(argument.ToString()!);
                        var configPath = Path.Combine(dir, "PCL", "config.v1.yml");
                        if (!File.Exists(dir)) _TryMigrate(dir, [
                            new ConfigMigration
                            {
                                From = Path.Combine(dir, "PCL", "setup.ini"),
                                To = configPath,
                                OnMigration = CatIniMigration
                            }
                        ]);
                        var fileProvider = new YamlFileProvider(configPath);
                        var trafficCenter = new FileTrafficCenter(fileProvider);
                        return trafficCenter;
                    }
                };
            }
        ];
        try { Task.WaitAll(inits.Select(Task.Run).ToArray()); }
        catch (AggregateException ex) { throw ex.GetBaseException(); }

        return;
        void SharedJsonMigration(string from, string to)
        {
            File.Copy(from, to);
        }
        void CatIniMigration(string from, string to)
        {
            var lines = File.ReadAllLines(from);
            var yamlProvider = new YamlFileProvider(to);
            foreach (var line in lines)
            {
                if (line.IsNullOrWhiteSpace()) continue;
                var kv = line.Split(':', 2);
                if (kv.Length != 2) continue;
                yamlProvider.Set(kv[0], kv[1]);
            }
            yamlProvider.Sync();
        }
    }

    private static void _TryMigrate(string target, IEnumerable<ConfigMigration> migrations)
    {
        Context.Info($"Try migrating config: {target}");
        try
        {
            var result = ConfigMigration.Migrate(target, migrations);
            if (!result) Context.Info("No migration solution available");
        }
        catch (Exception ex)
        {
            Context.Warn("Migration failed", ex);
        }
    }

    #endregion

    #region Lifecycle & Initialization

    /// <summary>
    /// 配置服务是否已加载完成。未加载完成时，调用与配置项相关的方法可能会抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static bool IsInitialized { get; private set; } = false;

    private static bool _isProvidersInitialized = false;
    private static bool _isConfigItemsInitialized = false;

    [LifecycleStart]
    private static void _Start()
    {
        if (IsInitialized) return;
#if TRACE
        var timer = new Stopwatch();
        timer.Start();
#endif
        Context.Info("Config initialization started");
        try
        {
            Context.Trace("Initializing providers...");
            _InitializeProviders();
            _isProvidersInitialized = true;
            Context.Trace("Initializing config items...");
            _InitializeConfigItems();
            Context.Debug($"Finished initialize {_Items.Count} item(s)");
            _isConfigItemsInitialized = true;
            Context.Trace("Initializing observers...");
            _InitializeObservers();
            Context.Info("Invoking init events...");
            foreach (var (_, item) in _Items)
            {
                item.TriggerEvent(ConfigEvent.Init, null, true, true);
            }
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            var currentSection = _isConfigItemsInitialized ? "OBSERVER" : _isProvidersInitialized ? "CONFIG_ITEM" : "PROVIDER";
            var msg = $"配置初始化失败，当前位于 {currentSection} 阶段。";
#if DEBUG
            msg += "\n\n嘻嘻，连配置系统都搞不明白...真是杂鱼呢~❤️ 快修好故障重新启动吧，杂鱼~❤️杂鱼~❤️";
#else
            if (ex is FileInitException e)
            {
                var filePath = e.FilePath;
                var backupPath = e.FilePath + ".failbackup";
                var bakPath = e.FilePath + ".bak";
                File.Move(filePath, backupPath, true);
                if (File.Exists(bakPath)) File.Copy(bakPath, filePath, true);
                msg += $"\n\n配置文件 {filePath} 的内容出了问题，不出意外的话，它应当已经备份到 {backupPath} 文件中。"
                    + $"\n为尽可能防止重复故障，配置文件已恢复至上一版本，若曾手动更改过配置文件，请修复问题，并替换当前的配置文件。"
                    + $"\n\n如果你不知道发生了什么，无视即可，重新打开启动器后相关配置项可能会恢复到默认值，应不影响正常使用。";
            }
#endif
            Context.Fatal(msg, ex);
        }
#if TRACE
        timer.Stop();
        Context.Info($"Config initialization finished in {timer.ElapsedMilliseconds} ms");
#endif
    }

    [LifecycleStop]
    private static void _Stop()
    {
        // 检测是否初始化出错
        if (Lifecycle.GetServiceLastException(Service.Identifier) is { } ex)
        {
            Context.Fatal("配置系统初始化失败", ex);
            return;
        }
        Context.Info("Saving config...");
        // 停止物流中心并释放资源
        _sharedConfigProvider?.Stop();
        _localConfigProvider?.Stop();
        _instanceConfigProvider?.Stop();
    }

    [RegisterConfigEvent]
    public static ConfigEventRegistry SharedVersionInit => new(
        scope: SharedVersionConfig,
        trigger: ConfigEvent.Init,
        handler: e => _UpdateConfigVersion(SharedVersionConfig, "全局", (int)e.NewValue!)
    );

    [RegisterConfigEvent]
    public static ConfigEventRegistry LocalVersionInit => new(
        scope: LocalVersionConfig,
        trigger: ConfigEvent.Init,
        handler: e => _UpdateConfigVersion(LocalVersionConfig, "本地", (int)e.NewValue!)
    );

    private static void _UpdateConfigVersion(ConfigItem<int> versionConfig, string name, int fileVersion)
    {
        var targetVersion = versionConfig.DefaultValue;
        var isUnset = versionConfig.IsDefault();
        LogWrapper.Info($"{name}配置: 文件版本 {(isUnset ? "UNSET" : fileVersion)}, 目标版本 {targetVersion}");
        if (isUnset || targetVersion != fileVersion) versionConfig.SetValue(targetVersion);
    }

    #endregion
}
