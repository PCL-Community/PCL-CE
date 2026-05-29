using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.UI;
using System.Globalization;

namespace PCL;

public partial class PageLogRight
{
    public Run LabDebug;
    public Run LabError;
    public Run LabFatal;
    public Run LabInfo;
    public Run LabWarn;

    public PageLogRight()
    {
        Initialized += (_, _) => Init();
        Loaded += PageLogRight_Loaded;
        InitializeComponent();
    }

    public void Init()
    {
        PanLogCard.Inlines.Clear();
        // TODO(i18n): 文本 @ 标题栏 - 实时日志卡片标题
        PanLogCard.Inlines.Add(new Run("实时日志"));
        PanLogCard.Inlines.Add(new Run(" | "));
        LabDebug = new Run("0 Debug")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"] };
        PanLogCard.Inlines.Add(LabDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        LabInfo = new Run("0 Info")
        {
            Foreground =
                (Brush)System.Windows.Application.Current.Resources[
                    ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"]
        };
        PanLogCard.Inlines.Add(LabInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        LabWarn = new Run("0 Warn")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"] };
        PanLogCard.Inlines.Add(LabWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        LabError = new Run("0 Error")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"] };
        PanLogCard.Inlines.Add(LabError);
        PanLogCard.Inlines.Add(new Run(" | "));
        LabFatal = new Run("0 Fatal")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"] };
        PanLogCard.Inlines.Add(LabFatal);
    }

    private void PageLogRight_Loaded(object sender, RoutedEventArgs e)
    {
        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        // 初始化
        if (ModMain.FrmLogLeft.CurrentLog is null || ModMain.FrmLogLeft.CurrentUuid <= 0 ||
            ModMain.FrmLogLeft.ShownLogs.Count == 0)
        {
            ModMain.FrmMain.PageChange(ModMain.FrmMain.PageCurrent);
            return;
        }

        PanAllBack.Visibility = Visibility.Visible;
        CardOperation.Visibility = Visibility.Visible;
        BtnOperationKill.IsEnabled = !ModMain.FrmLogLeft.CurrentLog.GameProcess.HasExited;
        BtnOperationExportStackDump.IsEnabled = !ModMain.FrmLogLeft.CurrentLog.GameProcess.HasExited &&
                                                !string.IsNullOrWhiteSpace(ModMain.FrmLogLeft.CurrentLog.JStackPath);
        SliderMaxLog.Value = Config.System.MaxGameLog;
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.GetHintText = new Func<object, object>(v =>
        {
            return v switch
            {
                _ when (int)v <= 5 => ((int)v * 10 + 50).ToString(),
                _ when (int)v <= 13 => ((int)v * 50 - 150).ToString(),
                _ when (int)v <= 28 => ((int)v * 100 - 800).ToString(),
                _ => "无限制"
            };
        });
        // 绑定日志输出
        PanLog.Document = ModMain.FrmLogLeft.FlowDocuments[ModMain.FrmLogLeft.CurrentUuid];
        // 绑定事件
        ModMain.FrmLogLeft.CurrentLog.LogOutput += OnLogOutput;
        ModMain.FrmLogLeft.CurrentLog.GameExit += OnGameExit;
        RefreshLabText();
    }

    private void RefreshLabText()
    {
        // 刷新计数器

        LabFatal.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountFatal} Fatal";
        LabError.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountError} Error";
        LabWarn.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountWarn} Warn";
        LabInfo.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountInfo} Info";
        LabDebug.Text = $"{ModMain.FrmLogLeft.CurrentLog.CountDebug} Debug";
    }

    private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
    {
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmLogLeft.CurrentLog is not null)
            {
                if (CheckAutoScroll.Checked == true) PanBack.ScrollToBottom();
                RefreshLabText();
            }
        });
    }

    #region 滑动条

    private void SliderMaxLog_ValueChanged(object o, bool user)
    {
        var sender = (MySlider)o;
        Config.System.MaxGameLog = sender.Value;
        if (ModMain.FrmSetupLauncherMisc is null)
            return;
        ModMain.FrmSetupLauncherMisc.SliderMaxLog.Value = sender.Value;
    }

    #endregion

    #region 卡片按钮

    private void BtnOperationClear_Click(object sender, ModBase.RouteEventArgs e)
    {
        ModMain.FrmLogLeft.FlowDocuments[ModMain.FrmLogLeft.CurrentUuid].Blocks.Clear();
    }

    private void BtnOperationExport_Click(object sender, ModBase.RouteEventArgs e)
    {
        // TODO(i18n): 文本 @ 文件选择弹窗 - 窗口标题 & 类型选择器选项
        var SavePath = SystemDialogs.SelectSaveFile("选择导出位置",
            $"游戏日志 - {ModMain.FrmLogLeft.CurrentLog.Version.Name}.log", "游戏日志(*.log)|*.log");
        if (SavePath.Length < 3)
            return;
        File.WriteAllLines(SavePath, ModMain.FrmLogLeft.CurrentLog.FullLog);
        // TODO(i18n): 文本 @ 左下角提示 - 导出成功提示
        ModMain.Hint("日志已导出！", ModMain.HintType.Finish);
        ModBase.OpenExplorer(SavePath);
    }

    private void BtnOperationKill_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (ModMain.FrmLogLeft.CurrentLog.State <= ModWatcher.Watcher.MinecraftState.Running)
        {
            ModMain.FrmLogLeft.CurrentLog.Kill();
            // TODO(i18n): 文本 @ 左下角提示 - 客户端关闭提示
            ModMain.Hint($"已关闭游戏 {ModMain.FrmLogLeft.CurrentLog.Version.Name}！", ModMain.HintType.Finish);
        }
    }

    private void BtnOperationExportStackDump_Click(object sender, ModBase.RouteEventArgs e)
    {
        var SavePath = SystemDialogs.SelectSaveFile("选择导出位置",
            $"游戏运行栈 - {DateTime.Now.ToString("G", CultureInfo.InvariantCulture).Replace("/", "-").Replace(":", ".").Replace(" ", "_")}.log",
            "游戏运行栈(*.log)|*.log");
        if (SavePath.Length < 3)
            return;
        // TODO(i18n): 文本 @ 左下角提示 - 导出运行栈提示
        ModMain.Hint("正在导出运行栈，请稍等（可能需要 15 秒 ~ 1 分钟）");
        BtnOperationExportStackDump.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            var Dump = ModMain.FrmLogLeft.CurrentLog.ExportStackDump(SavePath);
            File.WriteAllLines(SavePath, Dump);
            ModBase.RunInUi(() =>
            {
                // TODO(i18n): 文本 @ 左下角提示 - 导出运行栈提示
                ModMain.Hint("运行栈已导出！", ModMain.HintType.Finish);
                BtnOperationExportStackDump.IsEnabled = true;
            });
            ModBase.OpenExplorer(SavePath);
        });
    }

    private void OnGameExit()
    {
        ModBase.RunInUi(() => BtnOperationKill.IsEnabled = false);
        ModBase.RunInUi(() => BtnOperationExportStackDump.IsEnabled = false);
    }

    #endregion
}
