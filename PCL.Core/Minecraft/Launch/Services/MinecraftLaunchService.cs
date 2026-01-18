using System;
using System.Diagnostics;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Launch.Services;

public class MinecraftLaunchService(IMcInstance instance, JavaInfo selectedJava, string launchArg) {
    public void LaunchMinecraft() {
        var noJavaw = Config.Launch.NoJavaw;

        // 启动信息
        var gameProcess = new Process();
        var startInfo = new ProcessStartInfo(noJavaw ? selectedJava.JavaExePath : selectedJava.JavawExePath);

        // 设置环境变量
        var pathEnv = startInfo.EnvironmentVariables["PATH"];
        var paths = pathEnv != null ? pathEnv.Split(';').ToList() : [];
        paths.Add(selectedJava.JavaFolder);
        startInfo.EnvironmentVariables["Path"] = string.Join(";", paths.Distinct().ToList());
        startInfo.EnvironmentVariables["appdata"] = instance.Folder.Path;

        // 设置其他参数
        startInfo.WorkingDirectory = instance.IsolatedPath;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = noJavaw;
        startInfo.Arguments = launchArg;
        gameProcess.StartInfo = startInfo;

        // 开始进程
        gameProcess.Start();
        McLaunchUtils.Log("已启动游戏进程：" + selectedJava.JavawExePath);
        // TODO: 有待考量
        /*
        if (Loader.IsAborted) {
            McLaunchLog("由于取消启动，已强制结束游戏进程"); // #1631
            gameProcess.Kill();
            return;
        }
        */
        // Loader.Output = gameProcess;
        // McLaunchProcess = gameProcess;

        // 进程优先级处理
        try {
            gameProcess.PriorityBoostEnabled = true;
            switch (Config.Launch.ProcessPriority) {
                case 0: // 高
                    gameProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case 2: // 低
                    gameProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "设置进程优先级失败");
        }
    }
}
