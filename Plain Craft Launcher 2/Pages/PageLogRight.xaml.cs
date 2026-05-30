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
    public Run labDebug;
    public Run labError;
    public Run labFatal;
    public Run labInfo;
    public Run labWarn;

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
        labDebug = new Run("0 Debug")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"] };
        PanLogCard.Inlines.Add(labDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        labInfo = new Run("0 Info")
        {
            Foreground =
                (Brush)System.Windows.Application.Current.Resources[
                    ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"]
        };
        PanLogCard.Inlines.Add(labInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        labWarn = new Run("0 Warn")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"] };
        PanLogCard.Inlines.Add(labWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        labError = new Run("0 Error")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"] };
        PanLogCard.Inlines.Add(labError);
        PanLogCard.Inlines.Add(new Run(" | "));
        labFatal = new Run("0 Fatal")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"] };
        PanLogCard.Inlines.Add(labFatal);
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
        if (ModMain.frmLogLeft.currentLog is null || ModMain.frmLogLeft.currentUuid <= 0 ||
            ModMain.frmLogLeft.shownLogs.Count == 0)
        {
            ModMain.frmMain.PageChange(ModMain.frmMain.pageCurrent);
            return;
        }

        PanAllBack.Visibility = Visibility.Visible;
        CardOperation.Visibility = Visibility.Visible;
        BtnOperationKill.IsEnabled = !ModMain.frmLogLeft.currentLog.gameProcess.HasExited;
        BtnOperationExportStackDump.IsEnabled = !ModMain.frmLogLeft.currentLog.gameProcess.HasExited &&
                                                !string.IsNullOrWhiteSpace(ModMain.frmLogLeft.currentLog.jStackPath);
        SliderMaxLog.Value = Config.System.MaxGameLog;
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.getHintText = new Func<object, object>(v =>
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
        PanLog.Document = ModMain.frmLogLeft.flowDocuments[ModMain.frmLogLeft.currentUuid];
        // 绑定事件
        ModMain.frmLogLeft.currentLog.LogOutput += OnLogOutput;
        ModMain.frmLogLeft.currentLog.GameExit += OnGameExit;
        RefreshLabText();
    }

    private void RefreshLabText()
    {
        // 刷新计数器

        labFatal.Text = $"{ModMain.frmLogLeft.currentLog.countFatal} Fatal";
        labError.Text = $"{ModMain.frmLogLeft.currentLog.countError} Error";
        labWarn.Text = $"{ModMain.frmLogLeft.currentLog.countWarn} Warn";
        labInfo.Text = $"{ModMain.frmLogLeft.currentLog.countInfo} Info";
        labDebug.Text = $"{ModMain.frmLogLeft.currentLog.countDebug} Debug";
    }

    private void OnLogOutput(ModWatcher.Watcher sender, ModWatcher.LogOutputEventArgs e)
    {
        ModBase.RunInUi(() =>
        {
            if (ModMain.frmLogLeft.currentLog is not null)
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
        if (ModMain.frmSetupLauncherMisc is null)
            return;
        ModMain.frmSetupLauncherMisc.SliderMaxLog.Value = sender.Value;
    }

    #endregion

    #region 卡片按钮

    private void BtnOperationClear_Click(object sender, ModBase.RouteEventArgs e)
    {
        ModMain.frmLogLeft.flowDocuments[ModMain.frmLogLeft.currentUuid].Blocks.Clear();
    }

    private void BtnOperationExport_Click(object sender, ModBase.RouteEventArgs e)
    {
        // TODO(i18n): 文本 @ 文件选择弹窗 - 窗口标题 & 类型选择器选项
        var savePath = SystemDialogs.SelectSaveFile("选择导出位置",
            $"游戏日志 - {ModMain.frmLogLeft.currentLog.version.Name}.log", "游戏日志(*.log)|*.log");
        if (savePath.Length < 3)
            return;
        File.WriteAllLines(savePath, ModMain.frmLogLeft.currentLog.fullLog);
        // TODO(i18n): 文本 @ 左下角提示 - 导出成功提示
        ModMain.Hint("日志已导出！", ModMain.HintType.Finish);
        ModBase.OpenExplorer(savePath);
    }

    private void BtnOperationKill_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (ModMain.frmLogLeft.currentLog.State <= ModWatcher.Watcher.MinecraftState.Running)
        {
            ModMain.frmLogLeft.currentLog.Kill();
            // TODO(i18n): 文本 @ 左下角提示 - 客户端关闭提示
            ModMain.Hint($"已关闭游戏 {ModMain.frmLogLeft.currentLog.version.Name}！", ModMain.HintType.Finish);
        }
    }

    private void BtnOperationExportStackDump_Click(object sender, ModBase.RouteEventArgs e)
    {
        var savePath = SystemDialogs.SelectSaveFile("选择导出位置",
            $"游戏运行栈 - {DateTime.Now.ToString("G", CultureInfo.InvariantCulture).Replace("/", "-").Replace(":", ".").Replace(" ", "_")}.log",
            "游戏运行栈(*.log)|*.log");
        if (savePath.Length < 3)
            return;
        // TODO(i18n): 文本 @ 左下角提示 - 导出运行栈提示
        ModMain.Hint("正在导出运行栈，请稍等（可能需要 15 秒 ~ 1 分钟）");
        BtnOperationExportStackDump.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            var dump = ModMain.frmLogLeft.currentLog.ExportStackDump(savePath);
            File.WriteAllLines(savePath, dump);
            ModBase.RunInUi(() =>
            {
                // TODO(i18n): 文本 @ 左下角提示 - 导出运行栈提示
                ModMain.Hint("运行栈已导出！", ModMain.HintType.Finish);
                BtnOperationExportStackDump.IsEnabled = true;
            });
            ModBase.OpenExplorer(savePath);
        });
    }

    private void OnGameExit()
    {
        ModBase.RunInUi(() => BtnOperationKill.IsEnabled = false);
        ModBase.RunInUi(() => BtnOperationExportStackDump.IsEnabled = false);
    }

    #endregion
}
