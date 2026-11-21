using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Configuration;

/// <summary>全局配置服务。</summary>
[LifecycleService(LifecycleState.Loading, Priority = 1919810)]
[LifecycleScope("config", "配置")]
public sealed partial class ConfigService
{
    private static readonly Dictionary<string, ConfigItem> _Items = [];

    private static readonly HashSet<string> _KeySet = [];

    /// <summary>配置键的集合。</summary>
    public static IReadOnlySet<string> KeySet => _KeySet;

    /// <summary>全局配置文件的版本号。</summary>
    [ConfigItem<int>("FileVersion", 1)]
    public static partial int SharedVersion { get; set; }

    /// <summary>本地配置文件的版本号。</summary>
    [ConfigItem<int>("LocalFileVersion", 1, ConfigSource.Local)]
    public static partial int LocalVersion { get; set; }

    /// <summary>全局共享配置文件路径。</summary>
    public static string SharedConfigPath { get; } =
        Path.Combine(Paths.SharedData, "config.v1.json");

    /// <summary>本地配置文件路径。</summary>
    public static string LocalConfigPath { get; } = Path.Combine(Paths.Data, "config.v1.yml");

    #region Getters & Setters

    /// <summary>尝试获取无泛型的配置项。</summary>
    /// <param name="key">配置键</param>
    /// <param name="item">返回可观察对象</param>
    /// <returns>若配置键存在，则为 <c>true</c>，否则为 <c>false</c></returns>
    public static bool TryGetConfigItemNoType(string key, [NotNullWhen(true)] out ConfigItem? item)
    {
        return _Items.TryGetValue(key, out item);
    }

    /// <summary>尝试获取配置项。</summary>
    /// <param name="key">配置键</param>
    /// <param name="item">返回配置项，若类型不匹配则为 <c>null</c></param>
    /// <typeparam name="TValue">配置项的值类型</typeparam>
    /// <returns>若配置键存在，则为 <c>true</c>，否则为 <c>false</c></returns>
    /// <exception cref="InvalidOperationException">配置项尚未初始化完成</exception>
    public static bool TryGetConfigItem<TValue>(string key, out ConfigItem<TValue>? item)
    {
        if (!_isConfigItemsInitialized) throw new InvalidOperationException("Not initialized");
        var result = TryGetConfigItemNoType(key, out var value);
        item = result ? value as ConfigItem<TValue> : null;
        return result;
    }

    /// <summary>获取配置项。</summary>
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
        return item
            ?? throw new InvalidCastException(
                $"Type of '{key}' is incompatible with {typeof(TValue).FullName}");
    }

    /// <summary>按键设置配置值，自动处理类型匹配。若键不存在则静默失败。</summary>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    /// <param name="argument">上下文参数（实例路径等）</param>
    public static void TrySetValue(string key, object value, object? argument = null)
    {
        if (!TryGetConfigItemNoType(key, out var item)) return;
        if (item.Type.IsEnum && value is not string)
            item.SetValueNoType(Enum.ToObject(item.Type, value), argument);
        else
            item.SetValueNoType(value, argument);
    }

    /// <summary>向指定作用域批量注册事件观察器。</summary>
    /// <param name="scope"><see cref="IConfigScope" /> 实例</param>
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

    private static ConfigStorage? _sharedConfigProvider;
    private static ConfigStorage? _sharedEncryptedConfigProvider;
    private static ConfigStorage? _localConfigProvider;
    private static ConfigStorage? _instanceConfigProvider;
    private static readonly List<(string Path, Exception Error)> _ProviderInitFailures = [];
    private static readonly Lock _ProviderFailureLock = new();

    /// <summary>获取配置提供方。</summary>
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
        lock (_ProviderFailureLock)
        {
            _ProviderInitFailures.Clear();
        }

        var shared = _CreateProvider(
            () =>
            {
                if (!File.Exists(SharedConfigPath))
                {
                    string[] oldPaths =
                    [
                        Path.Combine(Paths.OldSharedData, "Config.json"),
                        Path.Combine(Paths.SharedData, "config.json")
                    ];
                    _TryMigrate(
                        SharedConfigPath,
                        oldPaths.Select(path =>
                            new ConfigMigration
                            {
                                From = path,
                                To = SharedConfigPath,
                                OnMigration = SharedJsonMigration
                            }));
                }

                return new FileConfigStorage(new JsonFileProvider(SharedConfigPath));
            },
            SharedConfigPath);
        _sharedConfigProvider = shared;
        _sharedEncryptedConfigProvider = new EncryptedFileConfigStorage(shared);
        _localConfigProvider = _CreateProvider(
            () =>
            {
                if (!File.Exists(LocalConfigPath))
                    _TryMigrate(
                        LocalConfigPath,
                        [
                            new ConfigMigration
                            {
                                From = Path.Combine(Paths.Data, "setup.ini"),
                                To = LocalConfigPath,
                                OnMigration = CatIniMigration
                            }
                        ]);
                return new FileConfigStorage(new YamlFileProvider(LocalConfigPath));
            },
            LocalConfigPath);
        _instanceConfigProvider = new DynamicCacheConfigStorage
        {
            StorageFactory = argument =>
            {
                ArgumentNullException.ThrowIfNull(argument);
                var dir = Path.GetFullPath(argument.ToString()!);
                var configPath = Path.Combine(dir, "PCL", "config.v1.yml");
                return _CreateProvider(
                    () =>
                    {
                        if (!File.Exists(dir))
                            _TryMigrate(
                                dir,
                                [
                                    new ConfigMigration
                                    {
                                        From = Path.Combine(dir, "PCL", "setup.ini"),
                                        To = configPath,
                                        OnMigration = CatIniMigration
                                    }
                                ]);
                        return new FileConfigStorage(new YamlFileProvider(configPath));
                    },
                    configPath);
            }
        };

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

    private static ConfigStorage _CreateProvider(Func<ConfigStorage> factory, string path)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 3; attempt++)
            try
            {
                return factory();
            }
            catch (ConfigFileInitException ex)
            {
                lock (_ProviderFailureLock)
                {
                    _ProviderInitFailures.Add((path, ex));
                }

                return new FileConfigStorage(new MemoryFileProvider(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       && attempt < 2)
            {
                lastError = ex;
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }

        lastError ??= new IOException($"Unable to initialize config storage: {path}");
        LogWrapper.Warn(
            lastError,
            "Config",
            Lang.Text("Config.Error.StorageFallback", path, lastError.Message));
        return new FileConfigStorage(new MemoryFileProvider(path));
    }

    private static void _ReportProviderFailures()
    {
        (string Path, Exception Error) failure;
        lock (_ProviderFailureLock)
        {
            failure = _ProviderInitFailures.FirstOrDefault(item =>
                item.Error is ConfigFileInitException);
        }

        if (failure.Error is not ConfigFileInitException ex) return;

        var backupPath = ex.Path + ".failbackup";
        var bakPath = ex.Path + ".bak";
        try
        {
            File.Move(ex.Path, backupPath, true);
            if (File.Exists(bakPath)) File.Copy(bakPath, ex.Path, true);
            Context.Fatal(
                Lang.Text(
                    "Config.Error.InvalidFormat.RecoveryMessage",
                    "PROVIDER",
                    ex.Path,
                    backupPath),
                ex);
        }
        catch (Exception recoveryEx)
        {
            Context.Warn(
                Lang.Text("Config.Error.StorageFallback", ex.Path, recoveryEx.Message),
                recoveryEx);
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

    /// <summary>配置服务是否已加载完成。未加载完成时，调用与配置项相关的方法可能会抛出 <see cref="InvalidOperationException" />。</summary>
    public static bool IsInitialized { get; private set; }

    private static bool _isProvidersInitialized;
    private static bool _isConfigItemsInitialized;

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
            Context.Trace("Initializing config items...");
            _InitializeConfigItems();
            Context.Debug($"Finished initialize {_Items.Count} item(s)");
            _isConfigItemsInitialized = true;
            Context.Trace("Initializing providers...");
            _InitializeProviders();
            _isProvidersInitialized = true;
            _ReportProviderFailures();
            Context.Trace("Initializing observers...");
            _InitializeObservers();
            Context.Info("Invoking init events...");
            foreach (var (_, item) in _Items) item.TriggerEvent(ConfigEvent.Init, null, true, true);
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            var currentSection = _isConfigItemsInitialized ? "OBSERVER" :
                _isProvidersInitialized ? "CONFIG_ITEM" : "PROVIDER";
            string msg;
#if DEBUG
            msg = Lang.Text("Config.Error.LoadFailed.DebugMessage", currentSection);
#else
            if (ex is ConfigFileInitException e)
            {
                var filePath = e.Path;
                var backupPath = e.Path + ".failbackup";
                var bakPath = e.Path + ".bak";
                File.Move(filePath, backupPath, true);
                if (File.Exists(bakPath)) File.Copy(bakPath, filePath, true);
                msg = Lang.Text(
                    "Config.Error.InvalidFormat.RecoveryMessage",
                    currentSection,
                    filePath,
                    backupPath);
            }
            else
            {
                msg = Lang.Text("Config.Error.LoadFailed.Message", currentSection);
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
        if (Lifecycle.GetServiceLastException(Service.Identifier) is { } ex)
            Context.Warn("配置初始化阶段出现异常，正在尝试保存可用配置", ex);

        Context.Info("Saving config...");
        try
        {
            _sharedConfigProvider?.Stop();
        }
        catch (Exception stopEx)
        {
            Context.Warn("停止全局配置存储时出错", stopEx);
        }

        try
        {
            _localConfigProvider?.Stop();
        }
        catch (Exception stopEx)
        {
            Context.Warn("停止本地配置存储时出错", stopEx);
        }

        try
        {
            _instanceConfigProvider?.Stop();
        }
        catch (Exception stopEx)
        {
            Context.Warn("停止实例配置存储时出错", stopEx);
        }
    }

    [RegisterConfigEvent]
    public static ConfigEventRegistry SharedVersionInit =>
        new(
            SharedVersionConfig,
            trigger: ConfigEvent.Init,
            handler: e => _UpdateConfigVersion(SharedVersionConfig, "全局", (int)e.NewValue!)
        );

    [RegisterConfigEvent]
    public static ConfigEventRegistry LocalVersionInit =>
        new(
            LocalVersionConfig,
            trigger: ConfigEvent.Init,
            handler: e => _UpdateConfigVersion(LocalVersionConfig, "本地", (int)e.NewValue!)
        );

    private static void _UpdateConfigVersion(
        ConfigItem<int> versionConfig,
        string name,
        int fileVersion)
    {
        var targetVersion = versionConfig.DefaultValue;
        var isUnset = versionConfig.IsDefault();
        LogWrapper.Info(
            $"{name}配置: 文件版本 {(isUnset ? "UNSET" : fileVersion)}, 目标版本 {targetVersion}");
        if (isUnset || targetVersion != fileVersion) versionConfig.SetValue(targetVersion);
    }

    #endregion
}