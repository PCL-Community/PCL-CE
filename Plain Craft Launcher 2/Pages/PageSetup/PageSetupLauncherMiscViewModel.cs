using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「启动器」页的视图模型。</summary>
public partial class PageSetupLauncherMiscViewModel : ViewModelBase
{
    [ObservableProperty] private int _activityIndex = -1;

    [ObservableProperty] private string _httpProxyPassword = Config.Network.HttpProxy.CustomPassword;

    [ObservableProperty] private string _httpProxyText = Config.Network.HttpProxy.CustomAddress;

    [ObservableProperty] private string _httpProxyUsername = Config.Network.HttpProxy.CustomUsername;

    [ObservableProperty] private bool _telemetryEnabled = !TelemetryService.IsBuildUntrusted;

    public void Reload()
    {
        ActivityIndex = States.System.AnnounceSolution;
        HttpProxyText = Config.Network.HttpProxy.CustomAddress;
        HttpProxyUsername = Config.Network.HttpProxy.CustomUsername;
        HttpProxyPassword = Config.Network.HttpProxy.CustomPassword;
        TelemetryEnabled = !TelemetryService.IsBuildUntrusted;
    }
}
