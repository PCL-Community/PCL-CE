using System.Windows;
using PCL.Core.App;

namespace PCL;

public partial class PageInstanceModDisabled
{
    public PageInstanceModDisabled()
    {
        InitializeComponent();
        BtnDownload.Click += BtnDownload_Click;
        BtnVersion.Click += BtnVersion_Click;
        BtnDownload.Loaded += BtnDownload_Loaded;
    }

    private void BtnDownload_Click(object sender, EventArgs e)
    {
        ModMain.FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
    }

    private void BtnVersion_Click(object sender, EventArgs e)
    {
        ModMain.FrmMain.PageChange(FormMain.PageType
            .Launch); // 在实例选择页面选定实例的时候只会返回一层，因此如果不先锚定 Launch，在选择实例后会回退到实例设置的这个页面
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSelect);
    }

    public void BtnDownload_Loaded(object? sender = null, RoutedEventArgs? e = null)
    {
        var NewVisibility =
            (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow) ||
            (ModMain.FrmSelectRight is not null && ModMain.FrmSelectRight.ShowHidden)
                ? Visibility.Collapsed
                : Visibility.Visible;
        if (BtnDownload.Visibility != NewVisibility)
        {
            BtnDownload.Visibility = NewVisibility;
            PanMain.TriggerForceResize();
        }
    }
}