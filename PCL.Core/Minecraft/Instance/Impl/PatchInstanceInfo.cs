using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Handler;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Impl;

/// <summary>
/// 表示一个 Minecraft 实例的版本信息和附加组件信息。
/// </summary>
public class PatchInstanceInfo {
    private static readonly FrozenDictionary<string, string> _PatchersImageMap =
        new Dictionary<string, string> {
            { "neoforge", "Blocks/NeoForge.png" },
            { "fabric", "Blocks/Fabric.png" },
            { "legacyfabric", "Blocks/Fabric.png" },
            { "forge", "Blocks/Forge.png" },
            { "liteloader", "Blocks/Egg.png" },
            { "quilt", "Blocks/Quilt.png" },
            { "cleanroom", "Blocks/Cleanroom.png" },
            { "labymod", "Blocks/LabyMod.png" },
            { "optifine", "Blocks/OptiFine.png" }
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    
    public DateTime ReleaseTime { get; set; } = DateTime.MinValue;

    public string? McVersionStr => Patches.Find(p => p.Id == "game")?.Version;
    
    public DateTime? McReleaseDate => Patches.Find(p => p.Id == "game")?.ReleaseTime;

    public string FormattedVersion => !string.IsNullOrEmpty(McVersionStr) ? InstanceInfoHandler.GetFormattedVersion(McVersionStr!) : string.Empty;

    public bool IsNormalVersion => !string.IsNullOrEmpty(McVersionStr) && InstanceInfoHandler.IsNormalVersion(McVersionStr!);
    
    public McVersionType VersionType { get; set; } = McVersionType.Release;
    
    public Version? McVersion => IsNormalVersion ? Version.Parse(McVersionStr!) : null;
    
    /// <summary>
    /// 原版主版本号，如 12（对于 1.12.2）
    /// </summary>
    public int? McVersionMinor => McVersion?.Minor;

    /// <summary>
    /// 原版次版本号，如 2（对于 1.12.2）
    /// </summary>
    public int? McVersionBuild => McVersion?.Build;

    public List<PatchInfo> Patches { get; } = [];

    public bool IsModded => HasAnyPatch([
        "cleanroom", "liteloader", "forge", "neoforge", "fabric", "legacyfabric", "quilt"
    ]);

    public bool IsClient => HasAnyPatch([
        "labymod", "optifine"
    ]);

    // 检查是否包含特定加载器
    public bool HasPatch(string patcherId) {
        return Patches.Any(p => p.Id.Equals(patcherId, StringComparison.OrdinalIgnoreCase));
    }
    
    public string GetPatchVersion(string id) {
        return Patches.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Version ?? string.Empty;
    }

    // 检查是否包含一组加载器中的任意一个
    public bool HasAnyPatch(IEnumerable<string> patcherIds) {
        return patcherIds.Any(id => Patches.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    public PatchInfo? GetPatch(string patcherId) {
        return Patches.FirstOrDefault(p => p.Id.Equals(patcherId, StringComparison.OrdinalIgnoreCase));
    }

    public string GetLogo() {
        switch (VersionType) {
            case McVersionType.Fool:
                return Basics.GetAppImagePath("Blocks/GoldBlock.png");
            case McVersionType.Old:
                return Basics.GetAppImagePath("Blocks/CobbleStone.png");
            case McVersionType.Snapshot:
                return Basics.GetAppImagePath("Blocks/CommandBlock.png");
            case McVersionType.Release:
                break;
            default:
                return Basics.GetAppImagePath("Blocks/RedstoneBlock.png");
        }

        // 其次判断加载器等
        foreach (var loader in new[] { "neoforge", "fabric", "legacyFabric", "forge", "liteloader", "quilt", "cleanroom", "labymod", "optifine" }) {
            if (Patches.Any(p => p.Id.Equals(loader, StringComparison.OrdinalIgnoreCase))) {
                return Basics.GetAppImagePath(_PatchersImageMap[loader]);
            }
        }

        // 正常版本
        return Basics.GetAppImagePath("Blocks/Grass.png");
    }
}
