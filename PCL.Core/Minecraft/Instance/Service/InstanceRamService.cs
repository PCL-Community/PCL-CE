using System;
using System.IO;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Instance.Service;

/// <summary>
/// Minecraft 实例内存分配器
/// </summary>
public static class InstanceRamService {
    // ReSharper disable InconsistentNaming
    
    // 内存分配策略常量
    private const int MEMORY_STRATEGY_AUTO = 0;
    private const int MEMORY_STRATEGY_MANUAL = 1;
    private const int MEMORY_STRATEGY_GLOBAL = 2;

    // 32位Java内存限制
    private const double JAVA_32BIT_MAX_MEMORY_GB = 1.0;

    // 手动内存配置的分段阈值
    private const int MANUAL_TIER_1_THRESHOLD = 12;
    private const int MANUAL_TIER_2_THRESHOLD = 25;
    private const int MANUAL_TIER_3_THRESHOLD = 33;

    // 手动内存配置的计算系数
    private const double MANUAL_TIER_1_MULTIPLIER = 0.1;
    private const double MANUAL_TIER_1_BASE = 0.3;
    private const double MANUAL_TIER_2_MULTIPLIER = 0.5;
    private const double MANUAL_TIER_2_BASE = 1.5;
    private const double MANUAL_TIER_3_MULTIPLIER = 1.0;
    private const double MANUAL_TIER_3_BASE = 8.0;
    private const double MANUAL_TIER_4_MULTIPLIER = 2.0;
    private const double MANUAL_TIER_4_BASE = 16.0;

    /// <summary>
    /// 获取当前 Minecraft 实例的 RAM 设置值，单位为 GB
    /// </summary>
    /// <param name="instance">Minecraft 实例配置</param>
    /// <param name="is32BitJava">是否使用 32 位 Java，null 表示自动检测</param>
    /// <returns>分配的 RAM 大小（GB）</returns>
    public static double GetInstanceMemoryAllocation(IMcInstance instance, bool? is32BitJava = null) {
        var memoryStrategy = Config.Instance.MemorySolution[instance.Path];

        // 如果设置为跟随全局配置
        if (memoryStrategy == MEMORY_STRATEGY_GLOBAL) {
            return GetGlobalMemoryAllocation(instance, is32BitJava);
        }

        var allocatedMemory = CalculateMemoryByStrategy(instance, memoryStrategy);

        return ApplyJavaArchitectureLimit(allocatedMemory, instance, is32BitJava);
    }

    /// <summary>
    /// 获取全局内存分配设置，单位为 GB
    /// </summary>
    /// <param name="instance">Minecraft 实例配置，可能为 null</param>
    /// <param name="is32BitJava">是否使用 32 位 Java，null 表示自动检测</param>
    /// <returns>分配的 RAM 大小（GB）</returns>
    /// <remarks>修改此方法时，需同步更新 PageInstanceSetup</remarks>
    public static double GetGlobalMemoryAllocation(IMcInstance instance, bool? is32BitJava = null) {
        var allocatedMemory = Config.Launch.MemoryAllocationMode == MEMORY_STRATEGY_AUTO
            ? CalculateAutoMemoryAllocation(instance)
            : CalculateManualMemoryAllocation(Config.Launch.CustomMemorySize);

        return ApplyJavaArchitectureLimit(allocatedMemory, instance, is32BitJava);
    }

    /// <summary>
    /// 根据策略计算内存分配
    /// </summary>
    private static double CalculateMemoryByStrategy(IMcInstance instance, int memoryStrategy) {
        return memoryStrategy == MEMORY_STRATEGY_AUTO
            ? CalculateAutoMemoryAllocation(instance)
            : CalculateManualMemoryAllocation(memoryStrategy);
    }

    /// <summary>
    /// 计算手动内存配置的分配量
    /// </summary>
    private static double CalculateManualMemoryAllocation(int configValue) {
        return configValue switch {
            <= MANUAL_TIER_1_THRESHOLD => configValue * MANUAL_TIER_1_MULTIPLIER + MANUAL_TIER_1_BASE,
            <= MANUAL_TIER_2_THRESHOLD => (configValue - MANUAL_TIER_1_THRESHOLD) * MANUAL_TIER_2_MULTIPLIER + MANUAL_TIER_2_BASE,
            <= MANUAL_TIER_3_THRESHOLD => (configValue - MANUAL_TIER_2_THRESHOLD) * MANUAL_TIER_3_MULTIPLIER + MANUAL_TIER_3_BASE,
            _ => (configValue - MANUAL_TIER_3_THRESHOLD) * MANUAL_TIER_4_MULTIPLIER + MANUAL_TIER_4_BASE
        };
    }

    /// <summary>
    /// 自动计算内存分配量
    /// </summary>
    private static double CalculateAutoMemoryAllocation(IMcInstance instance) {
        var availableMemoryGb = GetAvailableSystemMemoryGb();
        var memoryRequirements = CalculateInstanceMemoryRequirements(instance);

        return AllocateMemoryByStages(availableMemoryGb, memoryRequirements);
    }

    /// <summary>
    /// 获取系统可用内存（GB）
    /// </summary>
    private static double GetAvailableSystemMemoryGb() {
        var availableBytes = KernelInterop.GetAvailablePhysicalMemoryBytes();
        return Math.Round(availableBytes / 1024.0 / 1024 / 1024 * 10) / 10;
    }

    /// <summary>
    /// 计算实例内存需求
    /// </summary>
    private static MemoryRequirements CalculateInstanceMemoryRequirements(IMcInstance instance) {
        if (instance.InstanceInfo.IsModded) {
            return CalculateModdedInstanceRequirements(instance);
        }

        if (instance.InstanceInfo.HasPatch("optifine")) {
            return new MemoryRequirements {
                Minimum = 0.5,
                Target1 = 1.5,
                Target2 = 3.0,
                Target3 = 5.0
            };
        }

        // 普通实例
        return new MemoryRequirements {
            Minimum = 0.5,
            Target1 = 1.5,
            Target2 = 2.5,
            Target3 = 4.0
        };
    }

    /// <summary>
    /// 计算带Mod实例的内存需求
    /// </summary>
    private static MemoryRequirements CalculateModdedInstanceRequirements(IMcInstance instance) {
        var modCount = GetModCount(instance);

        return new MemoryRequirements {
            Minimum = 0.5 + modCount / 150.0,
            Target1 = 1.5 + modCount / 90.0,
            Target2 = 2.7 + modCount / 50.0,
            Target3 = 4.5 + modCount / 25.0
        };
    }

    /// <summary>
    /// 获取Mod数量
    /// </summary>
    private static int GetModCount(IMcInstance instance) {
        var modDirectory = new DirectoryInfo(Path.Combine(instance.IsolatedPath, "mods"));
        return modDirectory.Exists ? modDirectory.GetFiles().Length : 0;
    }

    /// <summary>
    /// 分阶段分配内存
    /// </summary>
    private static double AllocateMemoryByStages(double availableMemory, MemoryRequirements requirements) {
        var allocatedMemory = 0.0;
        var remainingMemory = availableMemory;

        var allocationStages = new[] {
            new { Delta = requirements.Target1, Percentage = 1.0 },
            new { Delta = requirements.Target2 - requirements.Target1, Percentage = 0.7 },
            new { Delta = requirements.Target3 - requirements.Target2, Percentage = 0.4 },
            new { Delta = requirements.Target3, Percentage = 0.15 }
        };

        foreach (var stage in allocationStages) {
            if (remainingMemory < 0.1) break;

            var stageAllocation = Math.Min(remainingMemory * stage.Percentage, stage.Delta);
            allocatedMemory += stageAllocation;
            remainingMemory -= stage.Delta / stage.Percentage;
        }

        return Math.Round(Math.Max(allocatedMemory, requirements.Minimum), 1);
    }

    /// <summary>
    /// 应用Java架构限制
    /// </summary>
    private static double ApplyJavaArchitectureLimit(double memoryAllocation, IMcInstance instance, bool? is32BitJava) {
        var is32Bit = is32BitJava ?? !IsUsing64BitJava(instance);

        return is32Bit
            ? Math.Min(JAVA_32BIT_MAX_MEMORY_GB, memoryAllocation)
            : memoryAllocation;
    }

    /// <summary>
    /// 检查是否使用64位Java
    /// </summary>
    private static bool IsUsing64BitJava(IMcInstance instance) {
        try {
            // 检查实例特定的Java设置
            if (TryGetInstanceJavaInfo(instance, out var instanceJavaInfo)) {
                return instanceJavaInfo!.Is64Bit;
            }

            // 检查全局Java设置
            if (TryGetGlobalJavaInfo(out var globalJavaInfo)) {
                return globalJavaInfo!.Is64Bit;
            }

            // 检查系统中是否有任何64位Java
            return JavaService.JavaManager.JavaList.Any(java => java.Is64Bit);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "检查 Java 架构时出错，重置为默认设置");
            ResetJavaSettings(instance);
            return true; // 默认假设为64位
        }
    }

    /// <summary>
    /// 尝试获取实例特定的Java信息
    /// </summary>
    private static bool TryGetInstanceJavaInfo(IMcInstance instance, out JavaInfo? javaInfo) {
        javaInfo = null;
        var instanceJavaPath = Config.Instance.SelectedJava[instance.Path];

        if (instanceJavaPath == "使用全局设置") {
            return false;
        }

        if (!File.Exists(instanceJavaPath)) {
            Config.Instance.SelectedJava[instance.Path] = "使用全局设置";
            return false;
        }

        javaInfo = JavaInfo.Parse(instanceJavaPath);
        return true;
    }

    /// <summary>
    /// 尝试获取全局Java信息
    /// </summary>
    private static bool TryGetGlobalJavaInfo(out JavaInfo? javaInfo) {
        javaInfo = null;
        var globalJavaPath = Config.Launch.SelectedJava;

        if (string.IsNullOrEmpty(globalJavaPath)) {
            return false;
        }

        if (!File.Exists(globalJavaPath)) {
            Config.Launch.SelectedJava = string.Empty;
            return false;
        }

        javaInfo = JavaInfo.Parse(globalJavaPath);
        return javaInfo != null;
    }

    /// <summary>
    /// 重置Java设置为默认值
    /// </summary>
    private static void ResetJavaSettings(IMcInstance instance) {
        Config.Instance.SelectedJava[instance.Path] = "使用全局设置";
        Config.Launch.SelectedJava = string.Empty;
    }

    /// <summary>
    /// 内存需求配置
    /// </summary>
    private class MemoryRequirements {
        public double Minimum { get; set; }
        public double Target1 { get; set; }
        public double Target2 { get; set; }
        public double Target3 { get; set; }
    }
}
