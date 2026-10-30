using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

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
        PanLogCard.Inlines.Add(new Run(Lang.Text("LogPage.Title")));
        PanLogCard.Inlines.Add(new Run(" | "));
        labDebug = new Run($"0 {Lang.Text("LogPage.Level.Debug")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"] };
        PanLogCard.Inlines.Add(labDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        labInfo = new Run($"0 {Lang.Text("LogPage.Level.Info")}")
        {
            Foreground =
                (Brush)System.Windows.Application.Current.Resources[
                    ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"]
        };
        PanLogCard.Inlines.Add(labInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        labWarn = new Run($"0 {Lang.Text("LogPage.Level.Warn")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"] };
        PanLogCard.Inlines.Add(labWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        labError = new Run($"0 {Lang.Text("LogPage.Level.Error")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"] };
        PanLogCard.Inlines.Add(labError);
        PanLogCard.Inlines.Add(new Run(" | "));
        labFatal = new Run($"0 {Lang.Text("LogPage.Level.Fatal")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"] };
        PanLogCard.Inlines.Add(labFatal);
    }

    private void PageLogRight_Loaded(object sender, RoutedEventArgs e)
    {
        Animations.AniBeginControl();
        Reload();
        Animations.AniEndControl();
    }

    public void Reload()
    {
        // 初始化
        if (Launcher.frmLogLeft.currentLog is null || Launcher.frmLogLeft.currentUuid <= 0 ||
            Launcher.frmLogLeft.shownLogs.Count == 0)
        {
            Launcher.frmMain.PageChange(Launcher.frmMain.pageCurrent);
            return;
        }

        PanAllBack.Visibility = Visibility.Visible;
        CardOperation.Visibility = Visibility.Visible;
        BtnOperationKill.IsEnabled = !Launcher.frmLogLeft.currentLog.gameProcess.HasExited;
        BtnOperationExportStackDump.IsEnabled =
            !Launcher.frmLogLeft.currentLog.gameProcess.HasExited &&
            !string.IsNullOrWhiteSpace(Launcher.frmLogLeft.currentLog.jStackPath);
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
                _ => Lang.Text("LogPage.MaxLines.Unlimited")
            };
        });
        // 绑定日志输出
        PanLog.Document = Launcher.frmLogLeft.flowDocuments[Launcher.frmLogLeft.currentUuid];
        // 绑定事件
        Launcher.frmLogLeft.currentLog.LogOutput += OnLogOutput;
        Launcher.frmLogLeft.currentLog.GameExit += OnGameExit;
        RefreshLabText();
    }

    private void RefreshLabText()
    {
        // 刷新计数器

        labFatal.Text =
            $"{Launcher.frmLogLeft.currentLog.countFatal} {Lang.Text("LogPage.Level.Fatal")}";
        labError.Text =
            $"{Launcher.frmLogLeft.currentLog.countError} {Lang.Text("LogPage.Level.Error")}";
        labWarn.Text =
            $"{Launcher.frmLogLeft.currentLog.countWarn} {Lang.Text("LogPage.Level.Warn")}";
        labInfo.Text =
            $"{Launcher.frmLogLeft.currentLog.countInfo} {Lang.Text("LogPage.Level.Info")}";
        labDebug.Text =
            $"{Launcher.frmLogLeft.currentLog.countDebug} {Lang.Text("LogPage.Level.Debug")}";
    }

    private void OnLogOutput(GameWatcher.Watcher sender, GameWatcher.LogOutputEventArgs e)
    {
        Common.RunInUi(() =>
        {
            if (Launcher.frmLogLeft.currentLog is not null)
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
        if (Launcher.frmSetupLauncherMisc is null)
            return;
        Launcher.frmSetupLauncherMisc.SliderMaxLog.Value = sender.Value;
    }
    #endregion
    #region 卡片按钮


    private void BtnOperationClear_Click(object sender, Common.RouteEventArgs e)
    {
        Launcher.frmLogLeft.flowDocuments[Launcher.frmLogLeft.currentUuid].Blocks.Clear();
    }

    private void BtnOperationExport_Click(object sender, Common.RouteEventArgs e)
    {
        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.Export.GameLog.FileName",
                Launcher.frmLogLeft.currentLog.version.Name),
            Lang.Text("LogPage.Export.GameLog.Filter"));
        if (savePath.Length < 3)
            return;
        File.WriteAllLines(savePath, Launcher.frmLogLeft.currentLog.fullLog);
        HintService.Hint(Lang.Text("LogPage.Export.Success"), HintType.Success);
        Common.OpenExplorer(savePath);
    }

    private void BtnOperationKill_Click(object sender, Common.RouteEventArgs e)
    {
        if (Launcher.frmLogLeft.currentLog.State <= GameWatcher.Watcher.MinecraftState.Running)
        {
            Launcher.frmLogLeft.currentLog.Kill();
            HintService.Hint(
                Lang.Text("LogPage.Action.GameClosed", Launcher.frmLogLeft.currentLog.version.Name),
                HintType.Success);
        }
    }

    private void BtnOperationExportStackDump_Click(object sender, Common.RouteEventArgs e)
    {
        var formattedDate = DateTime.Now.ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-")
            .Replace(":", ".")
            .Replace(" ", "_");
        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.ExportStack.FileName", formattedDate),
            Lang.Text("LogPage.ExportStack.Filter"));
        if (savePath.Length < 3)
            return;
        HintService.Hint(Lang.Text("LogPage.ExportStack.Progress"));
        BtnOperationExportStackDump.IsEnabled = false;
        Common.RunInNewThread(() =>
        {
            var dump = Launcher.frmLogLeft.currentLog.ExportStackDump();
            File.WriteAllLines(savePath, dump);
            Common.RunInUi(() =>
            {
                HintService.Hint(Lang.Text("LogPage.ExportStack.Success"), HintType.Success);
                BtnOperationExportStackDump.IsEnabled = true;
            });
            Common.OpenExplorer(savePath);
        });
    }

    private void OnGameExit()
    {
        Common.RunInUi(() => BtnOperationKill.IsEnabled = false);
    #endregion
        Common.RunInUi(() => BtnOperationExportStackDump.IsEnabled = false);
    }

}
