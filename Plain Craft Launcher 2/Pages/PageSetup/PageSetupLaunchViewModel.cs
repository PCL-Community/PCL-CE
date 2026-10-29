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

    [ObservableProperty] private int _indieSolutionIndex = -1;

    [ObservableProperty] private string _jvmArgsText = Config.Launch.JvmArgs;

    [ObservableProperty] private int _msAuthTypeIndex = -1;

    [ObservableProperty] private int _preferredIpStackIndex = -1;

    [ObservableProperty] private string _preLaunchText = Config.Launch.PreLaunchCommand;

    [ObservableProperty] private string _priorityValue = ((int)Config.Launch.ProcessPriority).ToString();

    [ObservableProperty] private int _rendererIndex = -1;

    [ObservableProperty] private bool _showSwitchButton = true;

    [ObservableProperty] private bool _showWindowSize;

    [ObservableProperty] private string _typeInfoText = Config.Launch.TypeInfo;

    [ObservableProperty] private int _visibilityIndex = -1;

    [ObservableProperty] private string _windowHeightText = Config.Launch.GameWindowHeight.ToString();

    [ObservableProperty] private int _windowTypeIndex = -1;

    [ObservableProperty] private string _windowWidthText = Config.Launch.GameWindowWidth.ToString();

    public void Reload()
    {
        TypeInfoText = Config.Launch.TypeInfo;
        IndieSolutionIndex = Config.Launch.IndieSolutionV2;
        VisibilityIndex = (int)Config.Launch.LauncherVisibility;
        PriorityValue = ((int)Config.Launch.ProcessPriority).ToString();
        WindowTypeIndex = (int)Config.Launch.GameWindowMode;
        WindowWidthText = Config.Launch.GameWindowWidth.ToString();
        WindowHeightText = Config.Launch.GameWindowHeight.ToString();
        MsAuthTypeIndex = Config.Launch.LoginMsAuthType;
        PreferredIpStackIndex = (int)Config.Launch.PreferredIpStack;
        RendererIndex = Config.Launch.Renderer;
        JvmArgsText = Config.Launch.JvmArgs;
        GameArgsText = Config.Launch.GameArgs;
        PreLaunchText = Config.Launch.PreLaunchCommand;
        DisableJlwEnabled = !SystemInfo.IsArm64System;
        ShowWindowSize = (int)Config.Launch.GameWindowMode == 3;
    }
}
