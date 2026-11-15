using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「更新」页的视图模型。</summary>
public partial class PageSetupUpdateViewModel : ViewModelBase
{
    [ObservableProperty] private bool _channelEnabled = true;

    [ObservableProperty] private bool _checkAgainEnabled = true;

    [ObservableProperty] private bool _changelogDetailEnabled = true;

    [ObservableProperty] private string _changelogText = string.Empty;

    [ObservableProperty] private string _currentDescText = string.Empty;

    [ObservableProperty] private string _currentVersionText = string.Empty;

    [ObservableProperty] private bool _showCheckCard;

    [ObservableProperty] private bool _showUpdateCard;

    [ObservableProperty] private string _updateNameText = string.Empty;

    public void Reload()
    {
        CurrentVersionText = "PCL CE " + VersionNameFormat(Common.VersionBaseName);
        ChangelogText = Lang.Text("Setup.Update.Changelog.Placeholder");
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
