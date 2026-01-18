using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Launch.Services;

public class PostLaunchService(IMcInstance instance) {
    public void LaunchPostRun() {
        McLaunchUtils.Log("开始启动结束处理");

        // 暂停或开始音乐播放
        if (Config.UI.Music.StopInGame) {
            // TODO: 等待音乐模块迁移
            /*
            RunInUi(() => {
                if (MusicPause()) LogWrapper.Info("Music", "已根据设置，在启动后暂停音乐播放");
            });
            */
        } else if (Config.UI.Music.StartInGame) {
            /*
            RunInUi(() => {
                if (MusicResume()) LogWrapper.Info("Music", "已根据设置，在启动后开始音乐播放");
            });
            */
        }

        // TODO: 等待视频模块迁移
        /*
        // 暂停视频背景播放
        ModVideoBack.IsGaming = true;
        VideoPause();
        */

        // 启动器可见性
        McLaunchUtils.Log($"启动器可见性：{Config.Launch.LauncherVisibility}");
        switch (Config.Launch.LauncherVisibility) {
            case 0:
                // 直接关闭
                McLaunchUtils.Log("已根据设置，在启动后关闭启动器");
                // RunInUi(() => FrmMain.EndProgram(false));
                break;
            case 2:
            case 3:
                // 隐藏
                McLaunchUtils.Log("已根据设置，在启动后隐藏启动器");
                // RunInUi(() => FrmMain.Hidden = true);
                break;
            case 4:
                // 最小化
                McLaunchUtils.Log("已根据设置，在启动后最小化启动器");
                // RunInUi(() => FrmMain.WindowState = WindowState.Minimized);
                break;
            case 5:
                // 啥都不干
                break;
        }

        // 启动计数
        Config.System.LaunchCount += 1;
        Config.Instance.LaunchCount[instance.Path] += 1;
    }
}
