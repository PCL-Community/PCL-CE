using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.UI.Bridges;

/// <summary>实例内存设置：按实例级或全局内存方案换算分配值与初始堆，32 位 Java 下上限 1 GB。</summary>
public sealed class MemorySettingsBridge : IMemorySettings
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    public double GetRam(McInstance? version, bool? is32BitJava = null)
    {
        var instancePath = version?.PathInstance;
        double ramGive;
        if (string.IsNullOrEmpty(instancePath))
        {
            LogWrapper.Debug("UI", "内存设置缺少实例上下文，使用全局内存配置");
            ramGive = Config.Launch.MemoryAllocationMode == 0
                ? _GetAutoRam(null)
                : _GetRamFromTick(Config.Launch.CustomMemorySize);
        }
        else
        {
            ramGive = Config.Instance.MemorySolution[instancePath] switch
            {
                2 => _GetGlobalRam(version),
                1 => _GetRamFromTick(Config.Instance.CustomMemorySize[instancePath]),
                _ => _GetAutoRam(version)
            };
        }

        var is32Bit = is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(version);
        if (is32Bit) ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    public double? GetInitialRam(McInstance? version, bool? is32BitJava = null)
    {
        if (Config.Launch.LockMemory) return null;
        if (_HasCustomXms(version)) return null;

        var instancePath = version?.PathInstance;
        if (string.IsNullOrEmpty(instancePath))
        {
            LogWrapper.Debug("UI", "初始内存设置缺少实例上下文，使用全局内存配置");
            return _GetGlobalInitialRam(null, is32BitJava);
        }

        return Config.Instance.MemorySolution[instancePath] switch
        {
            2 => _GetGlobalInitialRam(version, is32BitJava),
            1 => _GetInstanceInitialRam(version, is32BitJava),
            _ => null
        };
    }

    /// <summary>全局内存方案（MemorySolution == 2）的初始堆：使用全局设置，仅在手动分配模式下生效。</summary>
    private double? _GetGlobalInitialRam(McInstance? version, bool? is32BitJava)
    {
        if (Config.Launch.MemoryAllocationMode != 1) return null;
        var initialTick = Config.Launch.CustomInitialMemorySize;
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    private double? _GetInstanceInitialRam(McInstance? version, bool? is32BitJava)
    {
        var instancePath = version?.PathInstance;
        if (string.IsNullOrEmpty(instancePath)) return null;
        var initialTick = Config.Instance.CustomInitialMemorySize[instancePath];
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    /// <summary>判断 JVM 参数是否自带 -Xms：实例级参数优先，为空时取全局参数。</summary>
    private static bool _HasCustomXms(McInstance? version)
    {
        var instancePath = version?.PathInstance;
        var instanceArgs = string.IsNullOrEmpty(instancePath)
            ? ""
            : Config.Instance.JvmArgs[instancePath];
        var effectiveArgs =
            string.IsNullOrEmpty(instanceArgs) ? Config.Launch.JvmArgs : instanceArgs;
        return !string.IsNullOrEmpty(effectiveArgs)
            && effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
    }

    private static double _GetGlobalRam(McInstance? version)
    {
        return Config.Launch.MemoryAllocationMode == 0
            ? _GetAutoRam(version)
            : _GetRamFromTick(Config.Launch.CustomMemorySize);
    }

    private static double _GetAutoRam(McInstance? version)
    {
        var ramAvailable = Math.Round(
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerGigabyte * 10)
            / 10;
        double ramMinimum;
        double ramTarget1;
        double ramTarget2;
        double ramTarget3;
        if (version is not null && !version.IsLoaded) version.Load();
        if (version is not null && version.Modable)
        {
            var modDirectory = new DirectoryInfo(Path.Combine(version.PathIndie, "mods"));
            var modCount = modDirectory.Exists ? modDirectory.GetFiles().Length : 0;
            ramMinimum = 0.5d + modCount / 150d;
            ramTarget1 = 1.5d + modCount / 90d;
            ramTarget2 = 2.7d + modCount / 50d;
            ramTarget3 = 4.5d + modCount / 25d;
        }
        else if (version is not null && version.Info.HasOptiFine)
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 3d;
            ramTarget3 = 5d;
        }
        else
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 2.5d;
            ramTarget3 = 4d;
        }

        var ramGive = 0d;
        var stages = new[]
        {
            (Delta: ramTarget1, Ratio: 1d),
            (Delta: ramTarget2 - ramTarget1, Ratio: 0.7d),
            (Delta: ramTarget3 - ramTarget2, Ratio: 0.4d),
            (Delta: ramTarget3, Ratio: 0.15d)
        };
        foreach (var (delta, ratio) in stages)
        {
            ramGive += Math.Min(ramAvailable * ratio, delta);
            ramAvailable -= delta / ratio;
            if (ramAvailable < 0.1d) break;
        }

        return Math.Round(Math.Max(ramGive, ramMinimum), 1);
    }

    private static double _GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => (value - 25) * 1 + 8,
            _ => (value - 33) * 2 + 16
        };
    }
}