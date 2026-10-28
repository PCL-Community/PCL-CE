using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例服务器列表页的视图模型。</summary>
public partial class PageInstanceServerViewModel : ViewModelBase
{
    [ObservableProperty] private bool _showContent = true;

    public void RefreshTip(int serverCount)
    {
        if (serverCount == 0)
        {
            ModBase.Log(Lang.Text("Instance.Server.NoServersFound"));
            ShowContent = false;
            return;
        }

        ModBase.Log(Lang.Text("Instance.Server.FoundServers"));
        ShowContent = true;
    }
}
