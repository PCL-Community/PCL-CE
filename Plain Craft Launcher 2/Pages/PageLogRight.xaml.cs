using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
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
        PanLogCard.Inlines.Add(new Run(Lang.Text("LogPage.Title")));
        PanLogCard.Inlines.Add(new Run(" | "));
        labDebug = new Run($"0 {Lang.Text("LogPage.Level.Debug")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushDebug"] };
        labDebug.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.DebugCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        labInfo = new Run($"0 {Lang.Text("LogPage.Level.Info")}")
        {
            Foreground =
                (Brush)System.Windows.Application.Current.Resources[
                    ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"]
        };
        labInfo.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.InfoCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        labWarn = new Run($"0 {Lang.Text("LogPage.Level.Warn")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushWarn"] };
        labWarn.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.WarnCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        labError = new Run($"0 {Lang.Text("LogPage.Level.Error")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushError"] };
        labError.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.ErrorCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labError);
        PanLogCard.Inlines.Add(new Run(" | "));
        labFatal = new Run($"0 {Lang.Text("LogPage.Level.Fatal")}")
            { Foreground = (Brush)System.Windows.Application.Current.Resources["ColorBrushFatal"] };
        labFatal.SetBinding(Run.TextProperty,
            new Binding(nameof(PageLogRightViewModel.FatalCountText)) { Source = _vm });
        PanLogCard.Inlines.Add(labFatal);
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

        _vm.RefreshCounters();
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
        HintService.Hint(Lang.Text("LogPage.Export.Success"), HintType.Success);
        Common.OpenExplorer(savePath);
    }

    private void BtnOperationKill_Click(object sender, Common.RouteEventArgs e)
    {
        if (Launcher.frmLogLeft.currentLog.State <= GameWatcher.Watcher.MinecraftState.Running)
        {
            Launcher.frmLogLeft.currentLog.Kill();
            HintService.Hint(Lang.Text("LogPage.Action.GameClosed", Launcher.frmLogLeft.currentLog.version.Name),
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
        _vm.IsExportStackDumpEnabled = false;
        Common.RunInNewThread(() =>
        {
            var dump = Launcher.frmLogLeft.currentLog.ExportStackDump();
            File.WriteAllLines(savePath, dump);
            Common.RunInUi(() =>
            {
                HintService.Hint(Lang.Text("LogPage.ExportStack.Success"), HintType.Success);
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
