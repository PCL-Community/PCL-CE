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
        CheckDownloadAutoSelectVersion.Checked = (bool?)Config.Download.AutoSelectInstance;
        CheckFixAuthlib.Checked = (bool?)Config.Download.FixAuthLib;

        // Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = Config.Download.Comp.NameFormatV2;
        ComboDownloadMod.SelectedIndex = Config.Download.Comp.CompSourceSolution;
        ComboModLocalNameStyle.SelectedIndex = Config.Download.Comp.UiCompNameSolution;
        CheckDownloadIgnoreQuilt.Checked = (bool?)Config.Download.Comp.IgnoreQuilt;
        CheckDownloadClipboard.Checked = (bool?)Config.Download.Comp.ReadClipboard;

        // Minecraft 更新提示
        CheckUpdateRelease.Checked = (bool?)Config.Tool.ReleaseNotification;
        CheckUpdateSnapshot.Checked = (bool?)Config.Tool.SnapshotNotification;

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
            ModMain.Hint("已初始化其他页设置！", ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化其他页设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Checked);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.SelectedIndex);
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
                    return "无限制";
            }
        });
    }

    private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
    {
        if (SliderDownloadThread.Value < 100)
            return;
        if (!(States.Hint.LargeDownloadThread as bool? ?? false))
        {
            States.Hint.LargeDownloadThread = true;
            ModMain.MyMsgBox(
                """
                如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。
                一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！
                """,
                "警告", "我知道了", IsWarn: true);
        }
    }
}
