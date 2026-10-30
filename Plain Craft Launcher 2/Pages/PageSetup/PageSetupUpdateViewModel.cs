using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.Utils;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「更新」页的视图模型。</summary>
public partial class PageSetupUpdateViewModel : ViewModelBase
{
    [ObservableProperty] private bool _checkAgainEnabled = true;

    [ObservableProperty] private string _currentDescText = string.Empty;

    [ObservableProperty] private string _currentVersionText = string.Empty;

    [ObservableProperty] private bool _showCheckCard;

    [ObservableProperty] private bool _showUpdateCard;

    [ObservableProperty] private string _updateNameText = string.Empty;

    public void Reload()
    {
        CurrentVersionText = "PCL CE " + VersionNameFormat(Common.VersionBaseName);
    }

    public static string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains("-"))
            return str;
        var add = str.AfterLast("-");
        str = str.BeforeLast("-");
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }
}
