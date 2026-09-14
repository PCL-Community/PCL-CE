using System.Windows;
using System.Windows.Input;

namespace PCL;

/// <summary>
///     错误报告收起后在主界面右下角显示的入口。
///     只负责展示入口与转发操作，报告数据由 <see cref="CrashReportSession" /> 持有。
/// </summary>
public partial class CrashReportDock
{
    public CrashReportDock()
    {
        InitializeComponent();
        Loaded += CrashReportDock_Loaded;
        Unloaded += CrashReportDock_Unloaded;
    }

    private void CrashReportDock_Loaded(object sender, RoutedEventArgs e)
    {
        CrashReportSession.Changed += RefreshState;
        RefreshState();
    }

    private void CrashReportDock_Unloaded(object sender, RoutedEventArgs e)
    {
        CrashReportSession.Changed -= RefreshState;
    }

    private void RefreshState()
    {
        var hasReport = CrashReportSession.HasReport;
        Visibility = hasReport ? Visibility.Visible : Visibility.Collapsed;
        if (!hasReport)
            return;

        // 入口名称形如「1.20.1-Forge-14:32」，便于分辨是哪个实例、哪次崩溃
        LabText.Text = CrashReportSession.EntryLabel;
        ToolTip = CrashReportSession.Title;
    }

    private void PanBack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CrashReportSession.OpenWindow();
    }

    private void BtnOpen_Click(object sender, EventArgs e)
    {
        CrashReportSession.OpenWindow();
    }

    private void BtnDiscard_Click(object sender, EventArgs e)
    {
        CrashReportSession.Discard();
    }
}
