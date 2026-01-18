using System;
using System.IO;
using System.Threading.Tasks;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Handler;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance;

public static class InstanceFactory {
    private static T CopyCommonProperties<T>(T source, T destination) where T: class, IMcInstance {
        destination.CardType = source.CardType;
        destination.Desc = source.Desc;
        destination.Logo = source.Logo;
        destination.InstanceInfo = source.InstanceInfo;
        return destination;
    }

    public static IMcInstance CloneInstance(IMcInstance original) =>
        original switch {
            MergeInstance merge => CopyCommonProperties(merge, new MergeInstance(merge.Path, merge.Folder)),
            PatchInstance patch => CopyCommonProperties(patch, new PatchInstance(patch.Path, patch.Folder)),
            _ => throw new NotSupportedException("不支持的实例类型")
        };

    public static void UpdateFromClonedInstance(IMcInstance instance, IMcInstance clonedInstance) {
        if (instance.GetType() != clonedInstance.GetType())
            throw new ArgumentException("实例类型必须相同");

        instance.CardType = clonedInstance.CardType;
        instance.Desc = clonedInstance.Desc;
        instance.Logo = clonedInstance.Logo;
        instance.InstanceInfo = clonedInstance.InstanceInfo;
    }

    public static async Task<IMcInstance?> CreateInstanceAsync(string path, McFolder folder) {
        try {
            var instance = await CheckPermissionAsync(path, folder);
            if (instance != null) {
                return instance;
            }
            
            return await CheckJsonAsync(path, folder);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "创建实例类时出错");
            return null;
        }
    }

    /// <summary>
    /// 检查对实例文件夹的访问权限
    /// </summary>
    /// <returns>在有问题时返回 <c>ErrorInstance</c>, 没问题时返回 null。</returns>
    /// <exception cref="DirectoryNotFoundException">在实例文件夹不存在时抛出</exception>
    private static async Task<IMcInstance?> CheckPermissionAsync(string path, McFolder folder) {
        if (!Directory.Exists(path)) {
            throw new DirectoryNotFoundException("实例文件夹不存在");
        }

        try {
            Directory.CreateDirectory(path + "PCL\\");
            await Directories.CheckPermissionWithExceptionAsync(path + "PCL\\");
        } catch (Exception ex) {
            LogWrapper.Warn(ex, $"没有访问实例文件夹的权限：{path}");
            return new ErrorInstance(path, folder, desc: "PCL 没有对该文件夹的访问权限，请以管理员身份运行");
        }
        
        return null;
    }

    /// <summary>
    /// 检查实例 JSON 的可用性并返回对应的实例类型
    /// </summary>
    private static async Task<IMcInstance> CheckJsonAsync(string path, McFolder folder) {
        var versionJson = await InstanceJsonHandler.RefreshVersionJsonAsync(new ErrorInstance(path, folder));
        if (versionJson == null) {
            LogWrapper.Warn($"实例 JSON 可用性检查失败（{path}）");
            return new ErrorInstance(path, folder, desc: "实例 JSON 不存在或无法解析");
        }

        if (versionJson.ContainsKey("patchers")) {
            return new PatchInstance(path, folder, versionJson: versionJson);
        } 
        
        if (versionJson.ContainsKey("libraries")) {
            return new MergeInstance(path, folder, versionJson: versionJson);
        }
        
        LogWrapper.Warn($"实例信息检查失败（{path}）");
        return new ErrorInstance(path, folder, desc: "无法识别实例信息");
    }
}
