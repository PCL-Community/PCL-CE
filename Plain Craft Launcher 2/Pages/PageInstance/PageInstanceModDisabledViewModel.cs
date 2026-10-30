using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例 Mod 不可用提示页的视图模型。</summary>
public partial class PageInstanceModDisabledViewModel : ViewModelBase
{
    [ObservableProperty] private Visibility _btnDownloadVisibility = Visibility.Visible;

    public bool Refresh()
    {
        var newVisibility =
            (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow) ||
            (ModMain.frmSelectRight is not null && ModMain.frmSelectRight.showHidden)
                ? Visibility.Collapsed
                : Visibility.Visible;
        if (BtnDownloadVisibility == newVisibility)
            return false;

        BtnDownloadVisibility = newVisibility;
        return true;
    }
}
