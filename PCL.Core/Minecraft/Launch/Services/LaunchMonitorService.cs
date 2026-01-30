using System;
using System.IO;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Instance.Service;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Launch.Services;

public class LaunchMonitorService(IMcInstance instance, JavaInfo selectedJava) {
    private readonly IJsonBasedInstance _jsonBasedInstance = (IJsonBasedInstance)instance;
    
    public void MonitorLaunch() {
        // 输出信息
        McLaunchUtils.Log("");
        McLaunchUtils.Log("~ 基础参数 ~");
        // TODO: 部分等待实现
        // McLaunchUtils.Log("PCL 版本：" + VersionBaseName + " (" + VersionCode + ")");
        McLaunchUtils.Log("游戏版本：" + instance.InstanceInfo.McVersion);
        McLaunchUtils.Log("资源版本：" + McLaunchUtils.GetAssetsIndexName(_jsonBasedInstance));
        McLaunchUtils.Log("分配的内存：" + InstanceRamService.GetInstanceMemoryAllocation(instance, !selectedJava.Is64Bit) + " GB (" + Math.Round(InstanceRamService.GetInstanceMemoryAllocation(instance, !selectedJava.Is64Bit) * 1024) + " MB)");
        McLaunchUtils.Log("MC 文件夹：" + instance.Folder.Path);
        McLaunchUtils.Log("实例文件夹：" + instance.Path);
        McLaunchUtils.Log("版本隔离：" + (instance.IsolatedPath == instance.Path));
        McLaunchUtils.Log($"Patches 格式：{instance is PatchInstance}");
        McLaunchUtils.Log($"Java 信息：{selectedJava}");
        // McLaunchUtils.Log("Natives 文件夹：" + GetNativesFolder());
        McLaunchUtils.Log("");
        McLaunchUtils.Log("~ 档案参数 ~");
        /*
        McLaunchUtils.Log("玩家用户名：" + McLoginLoader.Output.Name);
        McLaunchUtils.Log("AccessToken:" + McLoginLoader.Output.AccessToken);
        McLaunchUtils.Log("ClientToken:" + McLoginLoader.Output.ClientToken);
        McLaunchUtils.Log("UUID:" + McLoginLoader.Output.Uuid);
        McLaunchUtils.Log("验证方式：" + McLoginLoader.Output.Type);
        */
        McLaunchUtils.Log("");
        
        var customEnvReplacer = new CustomEnvReplacer(instance, selectedJava);

        // 获取窗口标题
        var windowTitle = Config.Instance.Title[instance.Path];
        if (string.IsNullOrEmpty(windowTitle) && !Config.Instance.UseGlobalTitle[instance.Path])
            windowTitle = Config.Launch.Title;
        windowTitle = customEnvReplacer.ArgumentReplace(windowTitle);

        // JStack 路径
        var jStackPath = System.IO.Path.Combine(selectedJava.JavaFolder, "jstack.exe");

        // 初始化等待
        // TODO: 等待实现
        /*
        Watcher Watcher = new Watcher(Loader, McInstanceCurrent, windowTitle, File.Exists(jStackPath) ? jStackPath : "", CurrentLaunchOptions.Test);
        McLaunchWatcher = Watcher;

        // 显示实时日志
        if (CurrentLaunchOptions.Test) {
            if (FrmLogLeft == null) RunInUiWait(() => FrmLogLeft = new PageLogLeft());
            if (FrmLogRight == null)
                RunInUiWait(() => {
                    AniControlEnabled++;
                    FrmLogRight = new PageLogRight();
                    AniControlEnabled--;
                });
            FrmLogLeft.Add(Watcher);
            McLaunchUtils.Log("已显示游戏实时日志");
        }
        

        // 等待
        while (Watcher.State == Watcher.MinecraftState.Loading) {
            Thread.Sleep(100);
        }
        if (Watcher.State == Watcher.MinecraftState.Crashed) {
            throw new Exception("$$");
        }
        */
    }
}
