using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class PageInstanceSavesLeft : IRefreshable
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
        ItemDatapack.Check += PageCheck;
        BtnOpenFolder.Click += BtnOpenFolder_Click;
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

    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id = FormMain.PageSubType.Default)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case FormMain.PageSubType.VersionSavesInfo:
            {
                if (ModMain.frmInstanceSavesInfo is null)
                    ModMain.frmInstanceSavesInfo = new PageInstanceSavesInfo();
                return ModMain.frmInstanceSavesInfo;
            }
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.frmInstanceSavesDatapack is null)
                    ModMain.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                return ModMain.frmInstanceSavesDatapack;
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
        ModAnimation.AniBeginControl();
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                PageInstanceSavesLeftViewModel.GetSwitchFailedMessage(id),
                ModBase.LogLevel.Feedback,
                userSummary: PageInstanceSavesLeftViewModel.GetSwitchFailedMessage(id));
        }
        finally
        {
            ModAnimation.AniEndControl();
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

    public void RefreshButton_Click(object sender, EventArgs e)
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh()
    {
        Refresh(ModMain.frmMain.PageCurrentSub);
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.frmInstanceSavesDatapack is null)
                    ModMain.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                if (ItemDatapack.Checked)
                    ModMain.frmInstanceSavesDatapack.Refresh();
                else
                    ItemDatapack.Checked = true;

                break;
            }
        }

        PageInstanceSavesLeftViewModel.HintRefreshing();
    }
}
