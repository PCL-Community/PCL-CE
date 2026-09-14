using System.Windows;
using System.Windows.Input;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     以独立窗口展示一次崩溃分析的错误报告。
///     窗口只消费 <see cref="CrashReportSnapshot" />，不参与崩溃分析流程。
/// </summary>
public partial class CrashReportWindow
{
    private readonly CrashReportSnapshot _report;

    internal CrashReportWindow(CrashReportSnapshot report)
    {
        _report = report;
        InitializeComponent();

        Title = report.Title;
        LabTitle.Text = report.Title;
        TextReport.Text = report.Text;

        BtnOpenLog.Visibility = report.ShowOpenDirectFile ? Visibility.Visible : Visibility.Collapsed;
        BtnModify.Visibility = report.ShowOpenInstanceSettings ? Visibility.Visible : Visibility.Collapsed;
        BtnExport.Visibility = report.CanExportReport ? Visibility.Visible : Visibility.Collapsed;

        // 关闭是本窗口的主操作，使用主题色高亮
        BtnClose.ColorType = MyIconTextButton.ColorState.Highlight;

        TryAttachToMainWindow();
    }

    /// <summary>
    ///     将窗口挂到主窗口上，使其跟随主窗口显示、隐藏，且不影响任务栏。
    /// </summary>
    private void TryAttachToMainWindow()
    {
        try
        {
            if (ModMain.frmMain is { IsLoaded: true, IsVisible: true })
                Owner = ModMain.frmMain;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "设置错误报告窗口的父窗口失败");
        }
    }

    private void PanTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        // 与主窗体一致：仅当鼠标直接位于标题栏空白处时才拖动，避免抢占标题栏按钮的点击
        if (sender is not FrameworkElement { IsMouseDirectlyOver: true })
            return;

        try
        {
            DragMove();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "拖动错误报告窗口失败");
        }
    }

    private void BtnCopy_Click(object sender, ModBase.RouteEventArgs e)
    {
        _report.CopyReport();
    }

    private void BtnOpenLog_Click(object sender, ModBase.RouteEventArgs e)
    {
        _report.OpenDirectFile();
    }

    private void BtnModify_Click(object sender, ModBase.RouteEventArgs e)
    {
        _report.OpenInstanceSettings();
    }

    private void BtnExport_Click(object sender, ModBase.RouteEventArgs e)
    {
        // 打包压缩包可能耗时较久，放到后台线程，避免卡住界面
        ModBase.RunInThread(() => _report.ExportReport());
    }

    private void BtnClose_Click(object sender, ModBase.RouteEventArgs e)
    {
        Close();
    }

    private void BtnTitleClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
