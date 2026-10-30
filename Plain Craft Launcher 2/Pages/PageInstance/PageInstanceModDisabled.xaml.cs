using System.Windows;

namespace PCL;

public partial class PageInstanceModDisabled
{
    private readonly PageInstanceModDisabledViewModel _vm = new();
    private bool _refreshed;

    public PageInstanceModDisabled()
    {
        InitializeComponent();
        DataContext = _vm;
        BtnDownload.Click += BtnDownload_Click;
        BtnVersion.Click += BtnVersion_Click;
        BtnDownload.Loaded += BtnDownload_Loaded;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageInstanceModDisabledViewModel ViewModel => _vm;

    private void BtnDownload_Click(object sender, EventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
    }

    private void BtnVersion_Click(object sender, EventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType
            .Launch); // 在实例选择页面选定实例的时候只会返回一层，因此如果不先锚定 Launch，在选择实例后会回退到实例设置的这个页面
        ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect);
    }

    public void BtnDownload_Loaded(object? sender = null, RoutedEventArgs? e = null)
    {
        RefreshDownloadButton();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        RefreshDownloadButton();
    }

    private void RefreshDownloadButton()
    {
        if (_vm.Refresh())
            PanMain.TriggerForceResize();
    }
}