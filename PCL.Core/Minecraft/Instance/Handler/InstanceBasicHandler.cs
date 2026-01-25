using System.IO;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Handler;

public static class InstanceBasicHandler {
    public static string GetName(string path) {
        return string.IsNullOrEmpty(path) ? "" : new DirectoryInfo(path).Name;
    }
    
    public static bool GetIsStarred(string path) {
        return Config.Instance.Starred[path];
    }
    
    /// <summary>
    /// 实例分类
    /// </summary>
    public static McInstanceCardType RefreshInstanceCardType(IMcInstance instance) {
        var savedCardType = (McInstanceCardType)Config.Instance.CardType[instance.Path];

        // 如果配置中已有正确的分类，直接返回
        if (HasCorrectCardType(savedCardType)) {
            return savedCardType;
        }

        // 判断各个可安装模组的实例
        var cardType = RecognizeInstanceCardType(instance.InstanceInfo);

        if (HasCorrectCardType(cardType)) {
            return cardType;
        }

        // 有附加组件但无法识别，归类为未知附加组件
        if (instance.InstanceInfo.Patches.Count > 0) {
            return McInstanceCardType.UnknownPatchers;
        } 
        
        // 没有任何附加组件，按原版分类
        return instance.InstanceInfo.VersionType switch {
            McVersionType.Release => McInstanceCardType.Release,
            McVersionType.Snapshot => McInstanceCardType.Snapshot,
            McVersionType.Fool => McInstanceCardType.Fool,
            McVersionType.Old => McInstanceCardType.Old,
            _ => McInstanceCardType.UnknownPatchers
        };
    }

    /// <summary>
    /// 判断卡片类型是否为正确的非自动分类
    /// </summary>
    public static bool HasCorrectCardType(McInstanceCardType cardType) => cardType != McInstanceCardType.Auto;
    
    /// <summary>
    /// 从实例信息中识别实例的卡片类型
    /// </summary>
    private static McInstanceCardType RecognizeInstanceCardType(PatchInstanceInfo instanceInfo) {
        var cachedCardType = McInstanceCardType.Auto;
        
        if (instanceInfo.HasPatch("NeoForge")) {
            cachedCardType = McInstanceCardType.NeoForge;
        } else if (instanceInfo.HasPatch("Fabric")) {
            cachedCardType = McInstanceCardType.Fabric;
        } else if (instanceInfo.HasPatch("LegacyFabric")) {
            cachedCardType = McInstanceCardType.LegacyFabric;
        } else if (instanceInfo.HasPatch("Quilt")) {
            cachedCardType = McInstanceCardType.Quilt;
        } else if (instanceInfo.HasPatch("Forge")) {
            cachedCardType = McInstanceCardType.Forge;
        } else if (instanceInfo.HasPatch("Cleanroom")) {
            cachedCardType = McInstanceCardType.Cleanroom;
        } else if (instanceInfo.HasPatch("LiteLoader")) {
            cachedCardType = McInstanceCardType.LiteLoader;
        } 
        
        // 判断客户端类型的补丁实例
        else if (instanceInfo.HasPatch("OptiFine")) {
            cachedCardType = McInstanceCardType.OptiFine;
        } else if (instanceInfo.HasPatch("LabyMod")) {
            cachedCardType = McInstanceCardType.LabyMod;
        } else if (instanceInfo.HasPatch("Client")) {
            cachedCardType = McInstanceCardType.Client;
        }

        return cachedCardType;
    }
}
