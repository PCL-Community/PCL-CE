using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Tasks;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Instance.Utils;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.Instance;

public class InstanceManager(McFolder folder) {
    /// <summary>
    /// Minecraft 实例列表
    /// </summary>
    public List<IMcInstance> McInstanceList { get; } = [];

    /// <summary>
    /// 用作 UI 显示被排序过的实例字典
    /// </summary>
    public Dictionary<McInstanceCardType, List<IMcInstance>> McInstanceUiDict { get; set; } = [];

    public async Task McInstanceListLoadAsync(CancellationToken cancelToken = default) {
        try {
            // Get version folders
            var versionPath = Path.Combine(folder.Path, "versions");

            await Directories.CheckPermissionWithExceptionAsync(versionPath, cancelToken);
            foreach (var path in Directory.GetDirectories(versionPath)) {
                var mcInstance = await InstanceFactory.CreateInstanceAsync(path, folder);
                if (mcInstance != null) {
                    McInstanceList.Add(mcInstance);
                }
            }

            if (Config.System.Debug.AddRandomDelay) {
                await Task.Delay(Random.Shared.Next(200, 3000), cancelToken);
            }
        } catch (OperationCanceledException) {
            // Handle cancellation
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "加载 Minecraft 实例列表失败");
        }

        SortInstance();

        foreach (var instance in McInstanceList) {
            instance.Load();
        }
    }
    
    private void SelectInstanceAsync() {
        var savedSelection = Config.Launch.SelectedInstance;

        if (McInstanceList.Any(instance => instance.CardType != McInstanceCardType.Error)) {
            var selectedInstance = McInstanceList
                .FirstOrDefault(instance => instance.Name == savedSelection && instance.CardType != McInstanceCardType.Error);

            if (selectedInstance != null) {
                FolderService.FolderManager.CurrentInst = selectedInstance;
                LogWrapper.Warn($"选择保存的 Minecraft 实例：{FolderService.FolderManager.CurrentInst.Path}");
            } else {
                selectedInstance = McInstanceList
                    .FirstOrDefault(instance => instance.CardType != McInstanceCardType.Error);

                if (selectedInstance != null) {
                    FolderService.FolderManager.CurrentInst = selectedInstance;
                    Config.Launch.SelectedInstance = FolderService.FolderManager.CurrentInst.Name;
                    LogWrapper.Warn($"自动选择 Minecraft 实例：{FolderService.FolderManager.CurrentInst.Path}");
                } else {
                    FolderService.FolderManager.CurrentInst = null;
                    LogWrapper.Warn("未找到可用的 Minecraft 实例");
                }
            }
        } else {
            FolderService.FolderManager.CurrentInst = null;
            if (savedSelection.IsNullOrEmpty()) {
                Config.Launch.SelectedInstance = string.Empty;
                LogWrapper.Warn("清除失效的 Minecraft 实例选择");
            }
            LogWrapper.Warn("未找到可用的 Minecraft 实例");
        }
    }

    private void SortInstance() {
        var groupedInstances = Config.UI.DetailedInstanceClassification
            ? GroupAndSortWithDetailedClassification()
            : GroupAndSortWithoutDetailedClassification();

        McInstanceUiDict = groupedInstances
            .OrderBy(g => Array.IndexOf(_sortableTypes, g.Key))
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // 需要排序的 McInstanceCardType
    private readonly McInstanceCardType[] _sortableTypes = [
        // 收藏和自定义分类
        McInstanceCardType.Star, McInstanceCardType.Custom,
        // 模组加载器和细分分类
        McInstanceCardType.Modded, McInstanceCardType.NeoForge, McInstanceCardType.Fabric,
        McInstanceCardType.Forge, McInstanceCardType.Quilt, McInstanceCardType.LegacyFabric,
        McInstanceCardType.Cleanroom, McInstanceCardType.LiteLoader,
        // 客户端和细分分类
        McInstanceCardType.Client,
        McInstanceCardType.OptiFine, McInstanceCardType.LabyMod,
        // 原版版本
        McInstanceCardType.Release, McInstanceCardType.Snapshot,
        McInstanceCardType.Fool, McInstanceCardType.Old,
        // 最低优先级
        McInstanceCardType.Hidden, McInstanceCardType.UnknownPatchers, McInstanceCardType.Error
    ];

    // PatcherId 映射
    private readonly Dictionary<McInstanceCardType, string> _patcherIds = new() {
        // Game
        { McInstanceCardType.Release, "game" },
        { McInstanceCardType.Snapshot, "game" },
        { McInstanceCardType.Fool, "game" },
        { McInstanceCardType.Old, "game" },
        // Specific
        { McInstanceCardType.Star, "game" },
        { McInstanceCardType.Custom, "game" },
        { McInstanceCardType.Hidden, "game" },
        { McInstanceCardType.UnknownPatchers, "game" },
        // ModLoaders
        { McInstanceCardType.NeoForge, "NeoForge" },
        { McInstanceCardType.Fabric, "Fabric" },
        { McInstanceCardType.Forge, "Forge" },
        { McInstanceCardType.Quilt, "Quilt" },
        { McInstanceCardType.LegacyFabric, "LegacyFabric" },
        { McInstanceCardType.Cleanroom, "Cleanroom" },
        { McInstanceCardType.LiteLoader, "LiteLoader" },
        // Client
        { McInstanceCardType.OptiFine, "OptiFine" },
        { McInstanceCardType.LabyMod, "LabyMod" }
    };

    private List<IGrouping<McInstanceCardType, IMcInstance>> GroupAndSortWithoutDetailedClassification() {
        var moddedTypes = new[] {
            McInstanceCardType.NeoForge, McInstanceCardType.Fabric, McInstanceCardType.Forge,
            McInstanceCardType.Quilt, McInstanceCardType.LegacyFabric,
            McInstanceCardType.Cleanroom, McInstanceCardType.LiteLoader
        };
        var clientTypes = new[] { McInstanceCardType.OptiFine, McInstanceCardType.LabyMod };

        // 先按类型分组，保留所有 McInstanceCardType
        var groupedInstances = McInstanceList
            .GroupBy(instance => instance.CardType)
            .ToList();

        // 处理每个分组，忽略类型的分组不排序
        var sortedGroups = new List<IGrouping<McInstanceCardType, IMcInstance>>();
        foreach (var type in _sortableTypes) {
            var group = groupedInstances.FirstOrDefault(g => g.Key == type);
            var instances = group?.ToList() ?? [];

            if (instances.Count == 0) {
                continue;
            }
            
            if (!IsIgnoredType(type)) {
                // 对非忽略类型的分组进行排序
                instances = instances
                    .OrderBy(instance => GetSortKey(instance, type),
                        McVersionComparerFactory.PatcherVersionComparer)
                    .ToList();
            }
            sortedGroups.Add(new Grouping(type, instances));
        }

        // 合并 Modded 和 Client
        var moddedGroup = sortedGroups
            .Where(g => moddedTypes.Contains(g.Key))
            .SelectMany(g => g)
            .OrderBy(instance => {
                foreach (var t in moddedTypes) {
                    var patcher = instance.InstanceInfo.GetPatch(_patcherIds[t]);
                    if (patcher != null)
                        return (Array.IndexOf(_sortableTypes, t), patcher.Version);
                }
                return (int.MaxValue, "");
            })
            .ToList();

        var clientGroup = sortedGroups
            .Where(g => clientTypes.Contains(g.Key))
            .SelectMany(g => g)
            .OrderBy(instance => {
                foreach (var t in clientTypes) {
                    var patcher = instance.InstanceInfo.GetPatch(_patcherIds[t]);
                    if (patcher != null)
                        return (Array.IndexOf(_sortableTypes, t), patcher.Version);
                }
                return (int.MaxValue, "");
            })
            .ToList();

        // 过滤掉 Modded 和 Client 相关类型的单独分组
        sortedGroups = sortedGroups
            .Where(g => !moddedTypes.Contains(g.Key) && !clientTypes.Contains(g.Key))
            .ToList();

        // 添加合并后的 Modded 和 Client 分组
        if (moddedGroup.Count > 0)
            sortedGroups.Add(new Grouping(McInstanceCardType.Modded, moddedGroup));
        if (clientGroup.Count > 0)
            sortedGroups.Add(new Grouping(McInstanceCardType.Client, clientGroup));

        return sortedGroups;
    }

    private IEnumerable<IGrouping<McInstanceCardType, IMcInstance>> GroupAndSortWithDetailedClassification() {
        return McInstanceList
            .GroupBy(instance => instance.CardType) // 先分组，保留所有 McInstanceCardType
            .Select(g => {
                var instances = g.ToList(); // 转换为 List 以便操作
                if (!IsIgnoredType(g.Key)) {
                    // 对非忽略类型的分组进行排序
                    instances = instances
                        .OrderBy<IMcInstance, (McInstanceCardType, PatchInfo)>(instance => GetSortKey(instance, g.Key),
                            McVersionComparerFactory.PatcherVersionComparer)
                        .ToList();
                }
                // 返回分组，忽略类型的分组保持原序
                return new Grouping(g.Key, instances);
            });
    }

    private bool IsIgnoredType(McInstanceCardType type) => type == McInstanceCardType.Error;

    private (McInstanceCardType, PatchInfo) GetSortKey(IMcInstance instance, McInstanceCardType type) {
        var patcherId = _patcherIds[type];
        return (type, instance.InstanceInfo.GetPatch(patcherId)!);
    }

    // 辅助类实现 IGrouping
    private class Grouping(McInstanceCardType key, IEnumerable<IMcInstance> elements) : IGrouping<McInstanceCardType, IMcInstance> {
        public McInstanceCardType Key { get; } = key;

        public IEnumerator<IMcInstance> GetEnumerator() => elements.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
