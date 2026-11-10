using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class PageInstanceSavesLeft : IRefreshable, IPageView
{
    public static string currentSave;

    private readonly PageInstanceSavesLeftViewModel _vm = new();
    private bool isLoad;

    public FormMain.PageSubType pageID = FormMain.PageSubType.Default;

    public PageInstanceSavesLeft()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += Page_Loaded;
        ItemInfo.Check += PageCheck;
    }

    public void OnNavigatedTo(object? parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }

    public PageInstanceSavesLeftViewModel ViewModel => _vm;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (isLoad)
            return;
        isLoad = true;
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        PageInstanceSavesLeftViewModel.OpenSaveFolder(currentSave);
    }

    private void PageCheck(object sender, Common.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)Common.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id = FormMain.PageSubType.Default)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case FormMain.PageSubType.VersionSavesInfo:
            {
                if (Launcher.frmInstanceSavesInfo is null)
                    Launcher.frmInstanceSavesInfo = new PageInstanceSavesInfo();
                return Launcher.frmInstanceSavesInfo;
            }
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (Launcher.frmInstanceSavesDatapack is null)
                    Launcher.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                return Launcher.frmInstanceSavesDatapack;
            }

            default:
            {
                throw new Exception(PageInstanceSavesLeftViewModel.GetUnknownSubPageMessage(id));
            }
        }
    }

    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        Animations.AniBeginControl();
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                PageInstanceSavesLeftViewModel.GetSwitchFailedMessage(id),
                Common.LogLevel.Feedback,
                userSummary: PageInstanceSavesLeftViewModel.GetSwitchFailedMessage(id));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        Animations.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        Launcher.frmMain.pageRight = target;
        ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnExit();
        Animations.AniStart(new[]
        {
            Animations.AaCode(() =>
            {
                ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnForceExit();
                Launcher.frmMain.PanMainRight.Child = Launcher.frmMain.pageRight;
                Launcher.frmMain.pageRight.Opacity = 0d;
            }, 130),
            Animations.AaCode(() =>
            {
                Launcher.frmMain.pageRight.Opacity = 1d;
                Launcher.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    public void RefreshButton_Click(object sender, EventArgs e)
    {
        Refresh((FormMain.PageSubType)Common.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh()
    {
        Refresh(Launcher.frmMain.PageCurrentSub);
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (Launcher.frmInstanceSavesDatapack is null)
                    Launcher.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                if (ItemDatapack.Checked)
                    Launcher.frmInstanceSavesDatapack.Refresh();
                else
                    ItemDatapack.Checked = true;

                break;
            }
        }

        PageInstanceSavesLeftViewModel.HintRefreshing();
    }
}
