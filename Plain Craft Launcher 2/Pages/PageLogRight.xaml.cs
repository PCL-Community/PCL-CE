using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;
using System.Globalization;

namespace PCL;

public partial class PageLogRight : IPageView
{
    public Run labDebug;
    public Run labError;
    public Run labFatal;
    public Run labInfo;
    public Run labWarn;

    private readonly PageLogRightViewModel _vm = new();

    private bool _refreshed;

    public PageLogRight()
    {
        Initialized += (_, _) => Init();
        Loaded += PageLogRight_Loaded;
        Unloaded += PageLogRight_Unloaded;
        InitializeComponent();
        DataContext = _vm;
    }

    public PageLogRightViewModel ViewModel => _vm;

    public void Init()
    {
        PanLogCard.Inlines.Clear();
        var titleRun = new Run();
        titleRun.TrySetLocalizedValue("Text", "LogPage.Title");
        PanLogCard.Inlines.Add(titleRun);
        PanLogCard.Inlines.Add(new Run(" | "));
        labDebug = new Run
        {
            Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"]
        };
        PanLogCard.Inlines.Add(labDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        labInfo = new Run
        {
            Foreground =
                (Brush)System.Windows.Application.Current.Resources[
                    ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"]
        };
        labInfo.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.InfoCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        labWarn = new Run
        {
            Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"]
        };
        PanLogCard.Inlines.Add(labWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        labError = new Run
        {
            Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"]
        };
        PanLogCard.Inlines.Add(labError);
        PanLogCard.Inlines.Add(new Run(" | "));
        labFatal = new Run
        {
            Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"]
        };
        PanLogCard.Inlines.Add(labFatal);
        RefreshLabText();
    }

    private void PageLogRight_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void PageLogRight_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Animations.AniBeginControl();
        Reload();
        Animations.AniEndControl();
    }

    public void Reload()
    {
        // 初始化
        if (!_vm.CanShow())
        {
            Launcher.frmMain.PageChange(Launcher.frmMain.pageCurrent);
            return;
        }

        PanAllBack.Visibility = Visibility.Visible;
        CardOperation.Visibility = Visibility.Visible;
        _vm.RefreshOperationButtons();
        SliderMaxLog.Value = Config.System.MaxGameLog;
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.getHintText = new Func<object, object>(PageLogRightViewModel.GetMaxLogHintText);
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
        LocalizedText.TryBind(
            labFatal,
            "Text",
            () => $"{GetCount(log => log.countFatal)} {Lang.Text("LogPage.Level.Fatal")}");
        LocalizedText.TryBind(
            labError,
            "Text",
            () => $"{GetCount(log => log.countError)} {Lang.Text("LogPage.Level.Error")}");
        LocalizedText.TryBind(
            labWarn,
            "Text",
            () => $"{GetCount(log => log.countWarn)} {Lang.Text("LogPage.Level.Warn")}");
        LocalizedText.TryBind(
            labInfo,
            "Text",
            () => $"{GetCount(log => log.countInfo)} {Lang.Text("LogPage.Level.Info")}");
        LocalizedText.TryBind(
            labDebug,
            "Text",
            () => $"{GetCount(log => log.countDebug)} {Lang.Text("LogPage.Level.Debug")}");
    }

    private static uint GetCount(Func<GameWatcher.Watcher, uint> selector)
    {
        var log = Launcher.frmLogLeft?.currentLog;
        return log is null ? 0u : selector(log);
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
            Lang.Text("LogPage.Export.GameLog.FileName", Launcher.frmLogLeft.currentLog.version.Name),
            Lang.Text("LogPage.Export.GameLog.Filter"));
        if (savePath.Length < 3)
            return;
        File.WriteAllLines(savePath, Launcher.frmLogLeft.currentLog.fullLog);
        HintService.Show(Hints.Success(Lang.Text("LogPage.Export.Success")));
        Common.OpenExplorer(savePath);
    }

    private void BtnOperationKill_Click(object sender, Common.RouteEventArgs e)
    {
        if (Launcher.frmLogLeft.currentLog.State <= GameWatcher.Watcher.MinecraftState.Running)
        {
            Launcher.frmLogLeft.currentLog.Kill();
            HintService.Show(Hints.Success(Lang.Text("LogPage.Action.GameClosed", Launcher.frmLogLeft.currentLog.version.Name)));
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
        HintService.Show(Hints.Info(Lang.Text("LogPage.ExportStack.Progress")));
        _vm.IsExportStackDumpEnabled = false;
        Common.RunInNewThread(() =>
        {
            var dump = Launcher.frmLogLeft.currentLog.ExportStackDump();
            File.WriteAllLines(savePath, dump);
            Common.RunInUi(() =>
            {
                HintService.Show(Hints.Success(Lang.Text("LogPage.ExportStack.Success")));
                _vm.IsExportStackDumpEnabled = true;
            });
            Common.OpenExplorer(savePath);
        });
    }

    private void OnGameExit()
    {
        Common.RunInUi(() => _vm.IsKillEnabled = false);
        Common.RunInUi(() => _vm.IsExportStackDumpEnabled = false);
    }

    #endregion
}
