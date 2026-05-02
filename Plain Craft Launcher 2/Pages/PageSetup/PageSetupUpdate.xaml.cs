using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL;

public partial class PageSetupUpdate
{
    public VersionDataModel UpdateInfo;

    public PageSetupUpdate()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        ModAnimation.AniControlEnabled += 1;
        TextMirrorCDK.Password = Config.Update.MirrorChyanKey;

        ComboSystemUpdateChannel.SelectedIndex = (int)Config.Update.UpdateChannel;
        ComboSystemUpdateMode.SelectedIndex = (int)Config.Update.UpdateMode;

        TextCurrentVersion.Text = "PCL CE " + VersionNameFormat(ModBase.VersionBaseName);
        ModAnimation.AniControlEnabled -= 1;
        CheckUpdate();
    }

    private async Task<UpdateStatus> IsLatestAsync()
    {
        try
        {
            // 修复：使用 dynamic 绕过命名空间重名导致的编译期类型冲突，
            // 或者你可以尝试替换为 PCL.Core.App.SemVer.Parse(ModBase.VersionBaseName)
            if (await UpdateManager.RemoteServer.IsLatestAsync(
                    UpdateManager.IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(ModBase.VersionBaseName),
                    ModBase.VersionCode))
            {
                ModBase.Log("[Update] 已是最新版本");
                return UpdateStatus.Latest;
            }

            ModBase.Log("[Update] 有可用的新版本");
            return UpdateStatus.Available;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "无法获取最新版本信息，请检查网络连接", ModBase.LogLevel.Hint);
            return UpdateStatus.Error;
        }
    }

    public async void CheckUpdate()
    {
        ModBase.Log("[Update] 开始检查更新");
        CardUpdate.Visibility = Visibility.Collapsed;
        CardCheck.Visibility = Visibility.Visible;
        TextCurrentDesc.Text = "正在检查更新...";
        BtnCheckAgain.IsEnabled = false;
        switch (await IsLatestAsync())
        {
            case UpdateStatus.Available:
            {
                Exception checkUpdateEx = null;
                try
                {
                    UpdateInfo = UpdateManager.RemoteServer.GetLatestVersion(
                        UpdateManager.IsCurrentVersionBeta
                            ? UpdateChannel.beta
                            : UpdateChannel.stable, ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
                    TextUpdateName.Text = "PCL CE " + VersionNameFormat(UpdateInfo.VersionName);
                    var summary = UpdateInfo.Changelog.Between("<summary>", "</summary>");
                    if (!UpdateInfo.Changelog.Contains("<summary>") || string.IsNullOrWhiteSpace(summary.Trim()))
                        TextChangelog.Text = "开发者似乎忘记提供更新摘要了...也许你可以点击下方看看完整更新日志？";
                    else
                        TextChangelog.Text = summary;
                }
                catch (Exception ex)
                {
                    checkUpdateEx = ex;
                }

                BtnCheckAgain.IsEnabled = true;
                if (UpdateInfo is null)
                {
                    TextCurrentDesc.Text = "检查更新时出错";
                    if (checkUpdateEx is not null)
                        ModBase.Log(checkUpdateEx, "[Update] 检查更新失败", ModBase.LogLevel.Msgbox);
                    else
                        ModBase.Log("[Update] 检查更新失败", ModBase.LogLevel.Msgbox);
                    return;
                }

                if (UpdateManager.UpdateLoader is not null && UpdateManager.UpdateLoader.State == ModBase.LoadState.Loading)
                {
                    BtnUpdate_Timer();
                    BtnUpdate.IsEnabled = false;
                }
                else if (UpdateManager.IsUpdateWaitingRestart)
                {
                    BtnUpdate.Text = "重启安装";
                    BtnUpdate.IsEnabled = true;
                }
                else
                {
                    BtnUpdate.Text = "下载并安装";
                    BtnUpdate.IsEnabled = true;
                }

                CardUpdate.Visibility = Visibility.Visible;
                CardCheck.Visibility = Visibility.Collapsed;
                break;
            }
            case UpdateStatus.Latest:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = "已是最新版本";
                break;
            }
            case UpdateStatus.Error:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = "检查更新时出错";
                break;
            }
        }
    }

    public void BtnUpdate_Timer()
    {
        while (UpdateManager.UpdateLoader is not null && UpdateManager.UpdateLoader.State == ModBase.LoadState.Loading)
        {
            ModBase.RunInUi(() => BtnUpdate.Text = $"{Math.Round(UpdateManager.UpdateLoader.Progress, 2)}%");
            Thread.Sleep(200);
        }
    }

    private void BtnUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        // 检查 .NET 版本
        if (!UpdateInfo.VersionName.StartsWithF("2.13.") && !ModBase
                .ShellAndGetOutput("cmd", "/c dotnet --list-runtimes")
                .ContainsF("Microsoft.WindowsDesktop.App 8.0.", true))
        {
            ModMain.MyMsgBox(
                $"发现了启动器更新（版本 {UpdateInfo.VersionName}），但是新版本要求你的电脑安装 .NET 8 才可以运行。{"\r\n"}你需要先安装 .NET 8 才可以继续更新。{"\r\n"}{"\r\n"}点击下方按钮打开网页，然后选择 ⌈.NET 桌面运行时⌋ 中的 {(ModBase.IsArm64System ? "Arm64" : "x64")} 选项下载。",
                "启动器更新 - 缺少运行环境", "下载 .NET 8 运行时", "取消",
                Button1Action: () => ModBase.OpenWebsite("https://get.dot.net/8"), ForceWait: true);
            return;
        }

        if (UpdateManager.IsUpdateWaitingRestart) UpdateManager.UpdateRestart(true);
        // 开始更新流程
        UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);
    }

    private void BtnChangelogDetail_Click(object sender, EventArgs e)
    {
        if (UpdateInfo is null)
            ModMain.MyMsgBox("没有可用的更新日志...", "关于此更新");
        else
            ModMain.MyMsgBoxMarkdown(UpdateInfo.Changelog, "关于此更新");
    }

    private void ComboSystemUpdateMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)ComboSystemUpdateMode.SelectedIndex;
    }

    private void ComboSystemUpdateBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        var IsCancelled = false;
        switch (ComboSystemUpdateChannel.SelectedIndex)
        {
            case 0:
            {
                break;
            }
            case 1:
            {
                const string warningMsg = """
                                          你正在切换启动器更新通道到测试版。
                                          测试版可以提供下个版本更新内容的预览，但可能会包含未经充分测试的功能，稳定性欠佳。

                                          在升级到测试版后，你需要等待下一个正式版发布，或是手动重新下载启动器来切换到正式版。
                                          该选项仅推荐具有一定基础知识和能力的用户选择。如果你正在制作整合包，请使用正式版！
                                          """;

                if (ModMain.MyMsgBox(warningMsg, "继续之前...", "我已知晓", "取消", IsWarn: true) == 2)
                    IsCancelled = true;
                else
                    CheckUpdate();
                break;
            }
            case 2:
            {
                const string devWarning = """
                                          你正在切换启动器更新通道到开发版。
                                          该通道可第一时间获取基于最新代码构建的开发版本，但可能极不稳定，甚至直接无法启动。

                                          在升级到开发版后，只能手动重新下载启动器来切换回正式版或测试版。
                                          该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！
                                          """;

                if (ModMain.MyMsgBox(devWarning, "继续之前...", "我已知晓", "取消", IsWarn: true) == 2)
                {
                    IsCancelled = true;
                    break;
                }

                const string confirmText = "我确认切换到此分支并已知晓风险";
                const string finalConfirmPrompt = $"""
                                                   你确定要切换到开发版通道吗？
                                                   开发版可能存在严重问题，甚至无法启动！

                                                   在升级到开发版后，将无法切换回其他任何更新通道，只能手动重新下载启动器来切换回正式版或测试版。

                                                   该选项仅推荐高级用户选择。如果你正在制作整合包，请使用正式版！
                                                   请输入 '{confirmText}' 以确认。
                                                   """;

                var ret = ModMain.MyMsgBoxInput("最终确认", finalConfirmPrompt, Button1: "提交", Button2: "取消", IsWarn: true);
    
                if (ret == confirmText)
                {
                    CheckUpdate();
                }
                else
                {
                    ModMain.Hint("你输入了错误的内容...");
                    IsCancelled = true;
                }
                break;
            }
        }

        if (IsCancelled)
        {
            ModAnimation.AniControlEnabled += 1;
            ComboSystemUpdateChannel.SelectedItem = e.RemovedItems[0];
            ModAnimation.AniControlEnabled -= 1;
        }
        else
        {
            Config.Update.UpdateChannel = (Core.App.UpdateChannel)ComboSystemUpdateChannel.SelectedIndex;
        }
    }

    private void TextMirrorCDK_PasswordChanged(object sender, EventArgs e)
    {
        Config.Update.MirrorChyanKey = TextMirrorCDK.Password;
    }

    private void BtnGetMirrorCDK_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://mirrorchyan.com/");
    }

    private void BtnChangelog_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/v" + ModBase.VersionBaseName);
    }

    public string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains("-"))
            return str;
        var add = str.AfterLast("-");
        str = str.BeforeLast("-");
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }

    private void BtnCheckAgain_OnClick(object sender, MouseButtonEventArgs e)
    {
        CheckUpdate();
    }

    private enum UpdateStatus
    {
        Checking = 0,
        Available = 1,
        Error = 2,
        Latest = 3
    }
}