using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class PageCrashLeft : IRefreshable
{
    public FormMain.PageSubType pageID = FormMain.PageSubType.CrashOverview;

    public PageCrashLeft()
    {
        InitializeComponent();
        ItemOverview.Check += PageCheck;
        ItemDiagnoses.Check += PageCheck;
        ItemSuggestions.Check += PageCheck;
        ItemEvidence.Check += PageCheck;
        ItemLogs.Check += PageCheck;
        ItemEnvironment.Check += PageCheck;
        BtnExportMarkdown.Click += BtnExportMarkdown_Click;
        BtnExportReport.Click += BtnExportReport_Click;
    }

    public void Refresh()
    {
        if (PageGet(pageID) is IRefreshable refreshable) refreshable.Refresh();
    }

    private static void BtnExportMarkdown_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        MinecraftCrashReportExportService.ExportCurrentMarkdown();
    }

    private static void BtnExportReport_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        MinecraftCrashReportExportService.ExportCurrent();
    }


    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: not null } item)
            PageChange((FormMain.PageSubType)ModBase.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id = FormMain.PageSubType.Default)
    {
        if ((int)id == -1) id = pageID;
        return id switch
        {
            FormMain.PageSubType.CrashOverview => ModMain.frmCrashOverview ??= new PageCrashOverview(),
            FormMain.PageSubType.CrashDiagnoses => ModMain.frmCrashDiagnoses ??= new PageCrashDiagnoses(),
            FormMain.PageSubType.CrashSuggestions => ModMain.frmCrashSuggestions ??= new PageCrashSuggestions(),
            FormMain.PageSubType.CrashEvidence => ModMain.frmCrashEvidence ??= new PageCrashEvidence(),
            FormMain.PageSubType.CrashLogs => ModMain.frmCrashLogs ??= new PageCrashLogs(),
            FormMain.PageSubType.CrashEnvironment => ModMain.frmCrashEnvironment ??= new PageCrashEnvironment(),
            _ => throw new Exception(MinecraftCrashUi.Text("Crash.Left.UnknownSubPage",
                new Dictionary<string, string> { ["0"] = ((int)id).ToString() }))
        };
    }

    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id) return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex,
                MinecraftCrashUi.Text("Crash.Left.SwitchFailed",
                    new Dictionary<string, string> { ["0"] = ((int)id).ToString() }), ModBase.LogLevel.Feedback);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);

        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}