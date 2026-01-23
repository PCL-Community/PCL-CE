using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Core.App.RemoteInfo.Sources;
using PCL.Core.UI;

namespace PCL.Core.App.RemoteInfo;

[LifecycleService(LifecycleState.Running)]
[LifecycleScope("remote-info", "远程信息")]
public partial class RemoteInfoService
{
    /// <summary>
    /// 最新版本信息。
    /// </summary>
    public static VersionData? LatestVersion { get; private set; }

    /// <summary>
    /// 是否有等待安装的更新包。
    /// </summary>
    public static bool IsUpdateWaitingInstall { get; private set; }

    /// <summary>
    /// 是否正在安装更新，防止重复安装。
    /// </summary>
    private static bool IsInstallingUpdate { get; set; }
    
    /// <summary>
    /// 更新源控制器实例。
    /// </summary>
    public static readonly SourceController SourceController = new([
        new UpdateMirrorChyanSource(),
        new UpdateMinioSource("https://staticassets.naids.com/resources/pclce/", "Naids"),
        new UpdateMinioSource("https://s3.pysio.online/pcl2-ce/", "Pysio")
    ]);
    
    /// <summary>
    /// 显示公告事件。
    /// </summary>
    public static event EventHandler<AnnouncementContentModel>? ShowAnnouncement;
    
    /// <summary>
    /// 提示安装更新事件。
    /// </summary>
    public static event EventHandler? PromptInstall;
    
    [LifecycleStart]
    private async Task _StartAsync()
    {
        await _ShowAnnouncementsAsync().ConfigureAwait(false);
        await _CheckUpdateFlow().ConfigureAwait(false);
    }
    
    [LifecycleStop]
    private static void _Stop()
    {
        if (IsInstallingUpdate || !IsUpdateWaitingInstall) return;
        
        Context.Info("程序关闭时检测到有等待安装的更新，准备安装");
        InstallUpdate(false);
    }

    #region Private Methods

    private async Task _ShowAnnouncementsAsync()
    {
        try
        {
            if (Config.System.AnnounceSolution > 1)
            {
                Context.Info("公告显示被禁用，跳过显示");
                return;
            }
            
            var showedAnnouncementIds = 
                Config.System.ShowedAnnouncement.Split("|", StringSplitOptions.RemoveEmptyEntries);
            var announcementsList = await SourceController
                .GetAnnouncementListAsync()
                .ConfigureAwait(false);
            if (announcementsList == null)
            {
                Context.Info("未获取到公告列表，跳过显示");
                return;
            }

            var allAnnouncements = announcementsList.Contents;
            var newAnnouncements = allAnnouncements.Where(
                announcement => !showedAnnouncementIds.Contains(announcement.Id)).ToArray();
            
            if (newAnnouncements.Length == 0)
            {
                Context.Info("无新公告，跳过显示");
                return;
            }
            
            Context.Info($"获取到 {newAnnouncements.Length} 条新公告，准备显示");
            foreach (var ann in newAnnouncements)
            {
                Context.Info($"显示公告: {ann.Id} - {ann.Title}");
                ShowAnnouncement?.Invoke(this, ann);
            }
            
            Context.Info("所有新公告显示完毕，更新已显示记录");
            Config.System.ShowedAnnouncement = string.Join("|",
                allAnnouncements.Select(announcement => announcement.Id));
        }
        catch (Exception ex)
        {
            Context.Warn("获取公告时发生未知异常", ex);
        }
    }

    private async Task _CheckUpdateFlow()
    {
        try
        {
            if (Config.System.Update.UpdateMode == 3)
            {
                Context.Info("更新模式为禁用，跳过检查");
                return;
            }

            Context.Info("检查更新中...");
            if (!await TryGetLatestVersionAsync().ConfigureAwait(false) || LatestVersion is null)
            {
                Context.Info("检查更新失败");
                return;
            }
        
            if (!LatestVersion.IsAvailable)
            {
                Context.Info("已经是最新版本，跳过更新");
                return;
            }
        
            Context.Info($"发现新版本: {LatestVersion.Code}, 准备更新");

            if (Config.System.Update.UpdateMode == 2 && MsgBoxWrapper.Show(
                    $"启动器有新版本可用 ({Basics.VersionName} -> {LatestVersion.Name})，是否立即下载并安装？\r\n\r\n" +
                    "你也可以稍后在 设置 -> 检查更新 界面中更新。",
                    "发现新版本", MsgBoxTheme.Info, true, "立刻更新", "以后再说") != 1)
            {
                Context.Info("用户取消更新"); 
                return;
            }

            if (!await TryDownloadAsync().ConfigureAwait(false)) return;

            if (Config.System.Update.UpdateMode == 1)
            {
                PromptInstall?.Invoke(this, EventArgs.Empty); // 显示重启按钮，等待用户点击
            }
        
            // 自动更新模式，将在关闭程序时安装更新
        }
        catch (Exception ex)
        {
            Context.Warn("检查更新流程中发生未知异常", ex);
            HintWrapper.Show("检查更新时发生未知异常，可能是网络问题", HintTheme.Error);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Step 1: 获取版本信息
    /// (仅获取版本信息请使用 <see cref="SourceController.GetLatestVersionAsync"/>)
    /// </summary>
    /// <returns>获取是否成功</returns>
    public static async Task<bool> TryGetLatestVersionAsync()
    {
        try
        {
            var ret = await SourceController.GetLatestVersionAsync().ConfigureAwait(false);
            if (ret != null)
            {
                Context.Info($"检查更新成功，最新版本: {ret.Code}");
                LatestVersion = ret;
                return true;
            }
            Context.Info("检查更新失败，未获取到版本信息");
        }
        catch (Exception ex)
        {
            Context.Warn("检查更新时发生未知异常", ex);
            HintWrapper.Show("检查更新时发生未知异常，可能是网络问题", HintTheme.Error);
        }
        return false;
    }

    /// <summary>
    /// Step 2: 下载更新包
    /// (需要指定路径请使用 <see cref="SourceController.DownloadAsync"/>)
    /// </summary>
    /// <returns>下载是否成功</returns>
    public static async Task<bool> TryDownloadAsync()
    {
        Context.Info("下载更新包中...");
        try
        {
            var outputPath = Path.Combine(
                Basics.ExecutableDirectory,
                "PCL",
                "Plain Craft Launcher Community Edition.exe");
            if (LatestVersion == null) return false;
            var ret = await SourceController.DownloadAsync(outputPath).ConfigureAwait(false);
            if (ret)
            {
                Context.Info("更新包下载成功");
                IsUpdateWaitingInstall = true;
                return true;
            }
            Context.Warn("更新包下载失败");
            return false;
        }
        catch (Exception ex)
        {
            Context.Warn("下载更新包时发生未知异常", ex);
            HintWrapper.Show("下载更新包时发生未知异常，可能是网络问题", HintTheme.Error);
        }
        return false;
    }

    /// <summary>
    /// Step 3: 安装更新包
    /// </summary>
    /// <param name="triggerRestartAndByEnd">是否在启动更新程序后结束当前程序。</param>
    /// <param name="isUpdateRestart">是否为更新重启。</param>
    public static void InstallUpdate(bool triggerRestartAndByEnd, bool isUpdateRestart = false)
    {
        try
        {
            var fileName = Path.Combine(
                Basics.ExecutableDirectory, "PCL",
                "Plain Craft Launcher Community Edition.exe");

            if (!File.Exists(fileName))
            {
                Context.Warn("更新启动器文件不存在，无法启动更新程序");
                return;
            }


            var startInfo = new ProcessStartInfo(fileName)
            {
                ArgumentList =
                {
                    "update",
                    Environment.ProcessId.ToString(),
                    $"{Basics.ExecutablePath}",
                    $"{fileName}",
                    isUpdateRestart ? "true" : "false"
                },
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
            Context.Info("已尝试启动更新程序,参数: " + string.Join(" ", startInfo.ArgumentList));

            if (!triggerRestartAndByEnd) return;

            Context.Info("已由于更新结束程序");
            IsInstallingUpdate = true;
            Lifecycle.Shutdown();
        }
        catch (Exception ex)
        {
            Context.Warn("启动更新程序失败", ex);
        }
    }

    #endregion
}