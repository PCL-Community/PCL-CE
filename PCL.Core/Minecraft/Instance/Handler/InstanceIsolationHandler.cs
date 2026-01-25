using System.IO;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Handler;

public static class InstanceIsolationHandler {
    /// <summary>
    /// 获取实例的隔离路径，根据全局设置和实例特性决定是否使用独立文件夹。
    /// </summary>
    /// <returns>隔离后的路径，以“\”结尾</returns>
    public static string GetIsolatedPath(IMcInstance instance, McFolder folder) {
        if (instance.CardType == McInstanceCardType.Error) {
            return "";
        }
        
        if (Config.Instance.IndieV2[instance.Path]) {
            return Config.Instance.IndieV2[instance.Path] ? instance.Path : folder.Path;
        }

        var shouldBeIndie = ShouldBeIndie(instance);
        Config.Instance.IndieV2[instance.Path] = shouldBeIndie;
        return Config.Instance.IndieV2[instance.Path] ? instance.Path : folder.Path;
    }

    private static bool ShouldBeIndie(IMcInstance instance) {
        // 若存在 mods 或 saves 文件夹，自动开启隔离
        var modFolder = new DirectoryInfo(instance.Path + "mods\\");
        var saveFolder = new DirectoryInfo(instance.Path + "saves\\");
        if (modFolder.Exists && modFolder.EnumerateFiles().Any() ||
            saveFolder.Exists && saveFolder.EnumerateDirectories().Any()) {
            LogWrapper.Info("Isolation", $"版本隔离初始化（{instance.Name}）：存在 mods 或 saves 文件夹，自动开启");
            return true;
        }

        var isModded = instance.InstanceInfo.IsModded;
        var isRelease = instance.InstanceInfo.VersionType == McVersionType.Release;
        LogWrapper.Info("Isolation", $"版本隔离初始化({instance.Name}): 全局设置({Config.Launch.IndieSolutionV2})");
        
        return Config.Launch.IndieSolutionV2 switch {
            0 => false,
            1 => instance.InstanceInfo.HasPatch("labymod") || isModded,
            2 => !isRelease,
            3 => instance.InstanceInfo.HasPatch("labymod") || isModded || !isRelease,
            _ => true
        };
    }
}
