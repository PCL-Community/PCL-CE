using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;

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
        SliderDownloadThread.Value = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadThread"));
        SliderDownloadSpeed.Value = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadSpeed"));
        ComboDownloadSource.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadSource"));
        ComboDownloadVersion.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadVersion"));
        CheckDownloadAutoSelectVersion.Checked = (bool?)ModBase.Setup.Get("ToolDownloadAutoSelectVersion");
        CheckFixAuthlib.Checked = (bool?)ModBase.Setup.Get("ToolFixAuthlib");

        // Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadTranslateV2"));
        ComboDownloadMod.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolDownloadMod"));
        ComboModLocalNameStyle.SelectedIndex = Conversions.ToInteger(ModBase.Setup.Get("ToolModLocalNameStyle"));
        CheckDownloadIgnoreQuilt.Checked = (bool?)ModBase.Setup.Get("ToolDownloadIgnoreQuilt");
        CheckDownloadClipboard.Checked = (bool?)ModBase.Setup.Get("ToolDownloadClipboard");

        // Minecraft 更新提示
        CheckUpdateRelease.Checked = (bool?)ModBase.Setup.Get("ToolUpdateRelease");
        CheckUpdateSnapshot.Checked = (bool?)ModBase.Setup.Get("ToolUpdateSnapshot");

        // 辅助设置
        CheckHelpChinese.Checked = (bool?)ModBase.Setup.Get("ToolHelpChinese");
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
        dynamic sender = senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag, sender.Checked);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        dynamic sender = senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag, sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        dynamic sender = senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag, sender.SelectedIndex);
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderDownloadThread.GetHintText = new Func<object, object>(v => Operators.AddObject(v, 1));
        SliderDownloadSpeed.GetHintText = new Func<object, object>(v =>
        {
            switch (v)
            {
                case var @case when Operators.ConditionalCompareObjectLessEqual(@case, 14, false):
                {
                    return $"{Operators.MultiplyObject(Operators.AddObject(v, 1), 0.1d):F1} M/s";
                }
                case var case1 when Operators.ConditionalCompareObjectLessEqual(case1, 31, false):
                {
                    return $"{Operators.MultiplyObject(Operators.SubtractObject(v, 11), 0.5d):F1} M/s";
                }
                case var case2 when Operators.ConditionalCompareObjectLessEqual(case2, 41, false):
                {
                    return Operators.ConcatenateObject(Operators.SubtractObject(v, 21), " M/s");
                }
                default:
                {
                    return "无限制";
                }
            }
        });
    }

    private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
    {
        if (SliderDownloadThread.Value < 100)
            return;
        if (!(ModBase.Setup.Get("HintDownloadThread") as bool? ?? false))
        {
            ModBase.Setup.Set("HintDownloadThread", true);
            ModMain.MyMsgBox(
                "如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" + Constants.vbCrLf + "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！",
                "警告", "我知道了", IsWarn: true);
        }
    }
}