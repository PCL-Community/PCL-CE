using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.Utils.OS;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「启动」页的视图模型。</summary>
public partial class PageSetupLaunchViewModel : ViewModelBase
{
    [ObservableProperty] private bool _disableJlwEnabled = !SystemInfo.IsArm64System;

    [ObservableProperty] private string _gameArgsText = Config.Launch.GameArgs;

    [ObservableProperty] private int _indieSolutionIndex = Math.Max(0, Config.Launch.IndieSolutionV2);

    [ObservableProperty] private string _jvmArgsText = Config.Launch.JvmArgs;

    [ObservableProperty] private int _msAuthTypeIndex = Math.Max(0, Config.Launch.LoginMsAuthType);

    [ObservableProperty] private int _preferredIpStackIndex = Math.Max(0, (int)Config.Launch.PreferredIpStack);

    [ObservableProperty] private string _preLaunchText = Config.Launch.PreLaunchCommand;

    [ObservableProperty] private string _priorityValue = ((int)Config.Launch.ProcessPriority).ToString();

    [ObservableProperty] private int _rendererIndex = Math.Max(0, Config.Launch.Renderer);

    [ObservableProperty] private bool _showSwitchButton = true;

    [ObservableProperty] private bool _showWindowSize;

    [ObservableProperty] private string _typeInfoText = Config.Launch.TypeInfo;

    [ObservableProperty] private int _visibilityIndex = Math.Max(0, (int)Config.Launch.LauncherVisibility);

    [ObservableProperty] private string _windowHeightText = Config.Launch.GameWindowHeight.ToString();

    [ObservableProperty] private int _windowTypeIndex = Math.Max(0, (int)Config.Launch.GameWindowMode);

    [ObservableProperty] private string _windowWidthText = Config.Launch.GameWindowWidth.ToString();

    public void Reload()
    {
        TypeInfoText = Config.Launch.TypeInfo;
        IndieSolutionIndex = Math.Max(0, Config.Launch.IndieSolutionV2);
        VisibilityIndex = Math.Max(0, (int)Config.Launch.LauncherVisibility);
        PriorityValue = ((int)Config.Launch.ProcessPriority).ToString();
        WindowTypeIndex = Math.Max(0, (int)Config.Launch.GameWindowMode);
        WindowWidthText = Config.Launch.GameWindowWidth.ToString();
        WindowHeightText = Config.Launch.GameWindowHeight.ToString();
        MsAuthTypeIndex = Math.Max(0, Config.Launch.LoginMsAuthType);
        PreferredIpStackIndex = Math.Max(0, (int)Config.Launch.PreferredIpStack);
        RendererIndex = Math.Max(0, Config.Launch.Renderer);
        JvmArgsText = Config.Launch.JvmArgs;
        GameArgsText = Config.Launch.GameArgs;
        PreLaunchText = Config.Launch.PreLaunchCommand;
        DisableJlwEnabled = !SystemInfo.IsArm64System;
        ShowWindowSize = (int)Config.Launch.GameWindowMode == 3;
        NotifyIndexChanged();
    }

    // 强制重发索引属性变更，确保下拉在重新进入页面时按配置重新选中
    private void NotifyIndexChanged()
    {
        OnPropertyChanged(nameof(IndieSolutionIndex));
        OnPropertyChanged(nameof(VisibilityIndex));
        OnPropertyChanged(nameof(WindowTypeIndex));
        OnPropertyChanged(nameof(MsAuthTypeIndex));
        OnPropertyChanged(nameof(PreferredIpStackIndex));
        OnPropertyChanged(nameof(RendererIndex));
    }
}
