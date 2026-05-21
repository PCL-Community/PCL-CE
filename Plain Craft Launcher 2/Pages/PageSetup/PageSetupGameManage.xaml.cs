using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupGameManage
{
    private new bool IsLoaded;

    public PageSetupGameManage()
    {
        InitializeComponent();
        Loaded += PageSetupSystem_Loaded;
    }

    private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        SliderLoad();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        // 下载
        SliderDownloadThread.Value = Config.Download.ThreadLimit;
        SliderDownloadSpeed.Value = Config.Download.SpeedLimit;
        ComboDownloadSource.SelectedIndex = Config.Download.FileSource;
        ComboDownloadVersion.SelectedIndex = Config.Download.VersionListSource;
        CheckDownloadAutoSelectVersion.Checked = Config.Download.AutoSelectInstance;
        CheckFixAuthlib.Checked = Config.Download.FixAuthLib;

        // Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = Config.Download.Comp.NameFormatV2;
        ComboDownloadMod.SelectedIndex = Config.Download.Comp.CompSourceSolution;
        ComboModLocalNameStyle.SelectedIndex = Config.Download.Comp.UiCompNameSolution;
        CheckDownloadIgnoreQuilt.Checked = Config.Download.Comp.IgnoreQuilt;
        CheckDownloadClipboard.Checked = Config.Download.Comp.ReadClipboard;

        // Minecraft 更新提示
        CheckUpdateRelease.Checked = Config.Tool.ReleaseNotification;
        CheckUpdateSnapshot.Checked = Config.Tool.SnapshotNotification;

        // 辅助设置
        CheckHelpLauncherLanguage.Checked = Config.Tool.AutoChangeLanguage;
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Download.Reset();
            Config.Tool.Reset();
            ModBase.Log("[Setup] 已初始化其他页设置");
            ModMain.Hint(Lang.Text("Setup.GameManage.Initialized"), ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.GameManage.Error.InitFailed"), ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetGameManageByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetGameManageByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetGameManageByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private static void SetGameManageByTag(string tag, object value)
    {
        switch (tag)
        {
            case "ToolDownloadThread": Config.Download.ThreadLimit = (int)value; break;
            case "ToolDownloadSpeed": Config.Download.SpeedLimit = (int)value; break;
            case "ToolDownloadSource": Config.Download.FileSource = (int)value; break;
            case "ToolDownloadVersion": Config.Download.VersionListSource = (int)value; break;
            case "ToolDownloadAutoSelectVersion": Config.Download.AutoSelectInstance = (bool)value; break;
            case "ToolFixAuthlib": Config.Download.FixAuthLib = (bool)value; break;
            case "ToolDownloadTranslateV2": Config.Download.Comp.NameFormatV2 = (int)value; break;
            case "ToolDownloadMod": Config.Download.Comp.CompSourceSolution = (int)value; break;
            case "ToolModLocalNameStyle": Config.Download.Comp.UiCompNameSolution = (int)value; break;
            case "ToolDownloadIgnoreQuilt": Config.Download.Comp.IgnoreQuilt = (bool)value; break;
            case "ToolDownloadClipboard": Config.Download.Comp.ReadClipboard = (bool)value; break;
            case "ToolUpdateRelease": Config.Tool.ReleaseNotification = (bool)value; break;
            case "ToolUpdateSnapshot": Config.Tool.SnapshotNotification = (bool)value; break;
            case "ToolHelpChinese": Config.Tool.AutoChangeLanguage = (bool)value; break;
        }
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderDownloadThread.GetHintText = new Func<object, object>(v => (int)v + 1);
        SliderDownloadSpeed.GetHintText = new Func<object, object>(v =>
        {
            int value = (int)v;
            switch (value)
            {
                case <= 14:
                    return Lang.Number((value + 1) * 0.1d, "N1") + " M/s";
                case <= 31:
                    return Lang.Number((value - 11) * 0.5d, "N1") + " M/s";
                case <= 41:
                    return Lang.Number(value - 21, "N0") + " M/s";
                default:
                    return Lang.Text("Setup.GameManage.Download.Unlimited");
            }
        });
    }

    private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
    {
        if (SliderDownloadThread.Value < 100)
            return;
        if (!States.Hint.LargeDownloadThread)
        {
            States.Hint.LargeDownloadThread = true;
            ModMain.MyMsgBox(
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Confirm"), IsWarn: true);
        }
    }
}
