using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLauncherMisc
{
    private bool isFirstLoad = true;

    private new bool isLoaded;

    public PageSetupLauncherMisc()
    {
        InitializeComponent();
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, _) => Reload();
    }

    private void PageSetupLink_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        SliderLoad();
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        // 系统设置
        ComboSystemActivity.SelectedIndex = States.System.AnnounceSolution;
        CheckSystemDisableHardwareAcceleration.Checked = Config.System.DisableHardwareAcceleration;
        SliderAniFPS.Value = Config.System.AnimationFpsLimit;
        SliderMaxLog.Value = Config.System.MaxGameLog;
        CheckSystemTelemetry.Checked = Config.System.Telemetry;

        // 网络
        TextSystemHttpProxy.Text = Config.Network.HttpProxy.CustomAddress;
        TextSystemHttpProxyCustomUsername.Text = Config.Network.HttpProxy.CustomUsername;
        TextSystemHttpProxyCustomPassword.Text = Config.Network.HttpProxy.CustomPassword;
        ((MyRadioBox)FindName($"RadioHttpProxyType{Config.Network.HttpProxy.Type}")).SetChecked(true, false);
        CheckNetDohEnable.Checked = Config.Network.EnableDoH;

        // 调试选项
        CheckDebugMode.Checked = Config.Debug.Enabled;
        SliderDebugAnim.Value = Config.Debug.AnimationSpeed;
        CheckDebugDelay.Checked = Config.Debug.DontCopy;
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Network.Reset();
            Config.Debug.Reset();
            Config.System.Reset();
            ModBase.Log("[Setup] 已初始化启动器-杂项页设置");
            HintService.Hint(Lang.Text("Setup.Misc.Initialized"), HintType.Success, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Error.InitFailed"), ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private static void SetByTag(string tag, object value)
        => ConfigService.TrySetValue(tag, value);

    // 网络
    private void ApplyHttpProxyBtn_OnClicked(object sender, MouseButtonEventArgs e)
    {
        Config.Network.HttpProxy.CustomAddress = TextSystemHttpProxy.Text;
        Config.Network.HttpProxy.CustomUsername = TextSystemHttpProxyCustomUsername.Text;
        Config.Network.HttpProxy.CustomPassword = TextSystemHttpProxyCustomPassword.Text;
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderDebugAnim.getHintText = new Func<object, object>(v =>
            (int)v > 29
                ? Lang.Text("Common.Action.Close")
                : Lang.Number(Math.Round(Convert.ToDouble(v) / 10 + 0.1d, 1), "N1") + "x");
        SliderAniFPS.getHintText = new Func<object, string>(v => Lang.Number(Convert.ToInt32(v) + 1, "N0") + " FPS");
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.getHintText = new Func<object, object>(v =>
        {
            var val = Convert.ToInt32(v);
            return val switch
            {
                <= 5 => val * 10 + 50,
                <= 13 => val * 50 - 150,
                <= 28 => val * 100 - 800,
                _ => Lang.Text("Setup.Misc.Unlimited")
            };
        });
    }

    // 硬件加速
    private void Check_DisableHardwareAcceleration(object _, bool __)
    {
        HintService.Hint(Lang.Text("Setup.Misc.HardwareAcceleration.RestartNotice"));
    }

    // 调试模式
    private void CheckDebugMode_Change(object _, bool __)
    {
        if (ModAnimation.AniControlEnabled == 0)
            HintService.Hint(Lang.Text("Setup.Misc.Debug.Mode.Hint"), log: false);
    }

    // 自动更新
    private void ComboSystemActivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboSystemActivity.SelectedIndex != 2)
            return;
        if (ModMain.MyMsgBox(
                Lang.Text("Setup.Misc.System.Announcement.Disabled.Warning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.Misc.System.Announcement.Disabled.Warning.Confirm"),
                Lang.Text("Common.Action.Cancel"), isWarn: true) ==
            2) ComboSystemActivity.SelectedItem = e.RemovedItems[0];
    }

    private void CheckDebugMode_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckDebugMode_Change(sender, user);
    }

    private void CheckSystemDisableHardwareAcceleration_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        Check_DisableHardwareAcceleration(sender, user);
    }

    private void ComboSystemActivity_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        ComboSystemActivity_SelectionChanged(sender, e);
    }

    #region 导出 / 导入设置

    private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
    {
        var savePath =
            SystemDialogs.SelectSaveFile(Lang.Text("Setup.Misc.Export.SaveTitle"), "PCL 全局配置.json", Lang.Text("Setup.Misc.Export.Filter"), ModBase.exePath);
        if (string.IsNullOrWhiteSpace(savePath))
            return;
        File.Copy(ConfigService.SharedConfigPath, savePath, true);
        HintService.Hint(Lang.Text("Setup.Misc.Export.Success"), HintType.Success);
        ModBase.OpenExplorer(savePath);
    }

    private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
    {
        var sourcePath = SystemDialogs.SelectFile(Lang.Text("Setup.Misc.Export.Filter"), Lang.Text("Setup.Misc.Import.SelectTitle"));
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;
        File.Copy(sourcePath, ConfigService.SharedConfigPath, true);
        ModMain.MyMsgBox(Lang.Text("Setup.Misc.Import.Success.Message"), button1: Lang.Text("Setup.Misc.Import.Success.Restart"), forceWait: true);
        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
        FormMain.EndProgramForce();
    }

    #endregion
}
