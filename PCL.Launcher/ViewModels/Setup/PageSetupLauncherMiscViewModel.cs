using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「启动器杂项」页的视图模型。</summary>
public partial class PageSetupLauncherMiscViewModel : ViewModelBase
{
    [ObservableProperty] private int _activityIndex = Math.Clamp(
        States.System.AnnounceSolution,
        0,
        2);

    [ObservableProperty]
    private string _httpProxyPassword = Config.Network.HttpProxy.CustomPassword;

    [ObservableProperty] private string _httpProxyText = Config.Network.HttpProxy.CustomAddress;

    [ObservableProperty]
    private string _httpProxyUsername = Config.Network.HttpProxy.CustomUsername;

    [ObservableProperty] private bool _telemetryEnabled = !TelemetryService.IsBuildUntrusted;

    public bool IsBuildUntrusted => TelemetryService.IsBuildUntrusted;

    public void Reload()
    {
        ActivityIndex = Math.Clamp(States.System.AnnounceSolution, 0, 2);
        HttpProxyText = Config.Network.HttpProxy.CustomAddress;
        HttpProxyUsername = Config.Network.HttpProxy.CustomUsername;
        HttpProxyPassword = Config.Network.HttpProxy.CustomPassword;
        TelemetryEnabled = !TelemetryService.IsBuildUntrusted;
    }

    public void ApplyHttpProxy()
    {
        Config.Network.HttpProxy.CustomAddress = HttpProxyText ?? string.Empty;
        Config.Network.HttpProxy.CustomUsername = HttpProxyUsername ?? string.Empty;
        Config.Network.HttpProxy.CustomPassword = HttpProxyPassword ?? string.Empty;
        HintService.Show(Hints.Success(Lang.Text("Setup.Misc.Network.Proxy.Apply")));
    }
}