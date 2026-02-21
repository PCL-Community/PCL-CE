using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class PageSetupLauncherMisc
{
    private bool IsFirstLoad = true;

    private new bool IsLoaded;

    public PageSetupLauncherMisc()
    {
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, __) => Reload();
    }

    private void PageSetupLink_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

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
            ModMain.Hint("已初始化杂项页设置！", ModMain.HintType.Finish, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化启动器-杂项页设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private static void ComboChange(MyComboBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.SelectedIndex);
    }

    private static void RadioBoxChange(MyRadioBox sender, object e)
    {
        var gotCfg = sender.Tag.ToString().Split("/");
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void CheckBoxChange(MyCheckBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Checked);
    }

    private static void SliderChange(MySlider sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(Conversions.ToString(sender.Tag), sender.Value);
    }

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
        SliderDebugAnim.GetHintText = new Func<object, object>(v =>
            Conversions.ToBoolean(Operators.ConditionalCompareObjectGreater(v, 29, false))
                ? "关闭"
                : Operators.ConcatenateObject( Math.Round(Convert.ToDouble(Operators.AddObject(Operators.DivideObject(v, 10), 0.1d)), 1),
                    "x"));
        SliderAniFPS.GetHintText = new Func<object, string>(v => $"{Operators.AddObject(v, 1)} FPS");
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.GetHintText = new Func<object, object>(v =>
        {
            switch (v)
            {
                case var @case when Operators.ConditionalCompareObjectLessEqual(@case, 5, false):
                {
                    return Operators.AddObject(Operators.MultiplyObject(v, 10), 50);
                }
                case var case1 when Operators.ConditionalCompareObjectLessEqual(case1, 13, false):
                {
                    return Operators.SubtractObject(Operators.MultiplyObject(v, 50), 150);
                }
                case var case2 when Operators.ConditionalCompareObjectLessEqual(case2, 28, false):
                {
                    return Operators.SubtractObject(Operators.MultiplyObject(v, 100), 800);
                }
                default:
                {
                    return "无限制";
                }
            }
        });
    }

    // 硬件加速
    private void Check_DisableHardwareAcceleration(object sender, bool user)
    {
        ModMain.Hint("此项变更将在重启 PCL 后生效");
    }

    // 调试模式
    private void CheckDebugMode_Change()
    {
        if (ModAnimation.AniControlEnabled == 0)
            ModMain.Hint("部分调试信息将在刷新或启动器重启后切换显示！", Log: false);
    }

    // 自动更新
    private void ComboSystemActivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboSystemActivity.SelectedIndex != 2)
            return;
        if (ModMain.MyMsgBox(
                "若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" + Constants.vbCrLf + "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" +
                Constants.vbCrLf + Constants.vbCrLf + "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" + Constants.vbCrLf +
                "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn: true) ==
            2) ComboSystemActivity.SelectedItem = e.RemovedItems[0];
    }

    #region 导出 / 导入设置

    private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
    {
        string savePath =
            SystemDialogs.SelectSaveFile("选择保存位置", "PCL 全局配置.json", "PCL 配置文件(*.json)|*.json", ModBase.ExePath);
        if (string.IsNullOrWhiteSpace(savePath))
            return;
        File.Copy(ConfigService.SharedConfigPath, savePath, true);
        ModMain.Hint("配置导出成功！", ModMain.HintType.Finish);
        ModBase.OpenExplorer(savePath);
    }

    private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
    {
        string sourcePath = SystemDialogs.SelectFile("PCL 配置文件(*.json)|*.json", "选择配置文件");
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;
        File.Copy(sourcePath, ConfigService.SharedConfigPath, true);
        ModMain.MyMsgBox("配置导入成功！请重启 PCL 以应用配置……", Button1: "重启", ForceWait: true);
        Process.Start(new ProcessStartInfo(ModBase.ExePathWithName));
        FormMain.EndProgramForce();
    }

    #endregion
}