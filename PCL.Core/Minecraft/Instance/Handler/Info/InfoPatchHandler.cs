using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Handler.Info;

public static class InfoPatchHandler {
    /// <summary>
    /// 将 Patch 类型 JSON 转化为对应的 InstanceInfo
    /// </summary>
    public static IMcInstance RefreshPatchInstanceInfo(IMcInstance instance, JsonObject versionJson, JsonObject libraries) {
        var clonedInstance = InstanceFactory.CloneInstance(instance);
        var instanceInfo = new PatchInstanceInfo();
        try {
            foreach (var patch in versionJson["patches"]!.AsArray()) {
                var patcherInfo = patch.Deserialize<PatchInfo>(Files.PrettierJsonOptions);
                if (patcherInfo != null) {
                    instanceInfo.Patches.Add(patcherInfo);
                }
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "识别 Patches 字段时出错");
            clonedInstance.Desc = $"无法识别：{ex.Message}";
        }
        
        clonedInstance.InstanceInfo = instanceInfo;
        return clonedInstance;
    }
}
