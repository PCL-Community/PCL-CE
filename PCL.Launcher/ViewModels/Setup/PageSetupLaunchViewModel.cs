using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.OS;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「启动」页的视图模型。</summary>
public partial class PageSetupLaunchViewModel : ViewModelBase
{
    [ObservableProperty] private bool _checkAdvanceRunWaitVisible;

    [ObservableProperty]
    private int _customInitialMemorySize = Config.Launch.CustomInitialMemorySize;

    [ObservableProperty] private int _customMemorySize = Config.Launch.CustomMemorySize;

    [ObservableProperty] private bool _disableCrashAnalysis = Config.Launch.DisableCrashAnalysis;

    [ObservableProperty]
    private bool _disableJlw = SystemInfo.IsArm64System || Config.Launch.DisableJlw;

    [ObservableProperty] private bool _disableJlwEnabled = !SystemInfo.IsArm64System;

    [ObservableProperty] private bool _disableLF = Config.Launch.DisableLF;

    [ObservableProperty]
    private bool _disableLwjglUnsafeAgent = Config.Launch.DisableLwjglUnsafeAgent;

    [ObservableProperty] private string _gameArgsText = Config.Launch.GameArgs;

    [ObservableProperty] private int _indieSolutionIndex = Math.Max(
        0,
        Config.Launch.IndieSolutionV2);

    [ObservableProperty] private string _jvmArgsText = Config.Launch.JvmArgs;

    [ObservableProperty] private bool _jvmResetVisible;

    [ObservableProperty] private bool _lockMemory = Config.Launch.LockMemory;

    [ObservableProperty] private int _memoryAllocationMode = Math.Clamp(
        Config.Launch.MemoryAllocationMode,
        0,
        1);

    [ObservableProperty] private int _msAuthTypeIndex = Math.Max(0, Config.Launch.LoginMsAuthType);

    [ObservableProperty] private bool _noJavaw = Config.Launch.NoJavaw;

    [ObservableProperty] private bool _preLaunchCommandWait = Config.Launch.PreLaunchCommandWait;

    [ObservableProperty] private string _preLaunchText = Config.Launch.PreLaunchCommand;

    [ObservableProperty] private int _preferredIpStackIndex = Math.Max(
        0,
        (int)Config.Launch.PreferredIpStack);

    [ObservableProperty]
    private int _priorityIndex = PriorityToIndex((int)Config.Launch.ProcessPriority);

    [ObservableProperty] private bool _ramCustomEnabled;

    [ObservableProperty] private string _ramGameText = "2.5 GiB";

    [ObservableProperty] private bool _ramInitialCustomEnabled;

    [ObservableProperty] private bool _ramTooHighVisible;

    [ObservableProperty] private string _ramTotalText = " / 7.9 GiB";

    [ObservableProperty] private string _ramUsedText = "4.7 GiB";

    [ObservableProperty] private bool _ramWarnVisible;

    private bool _reloading;

    [ObservableProperty] private int _rendererIndex = Math.Max(0, Config.Launch.Renderer);

    [ObservableProperty] private bool _setGpuPreference = Config.Launch.SetGpuPreference;

    [ObservableProperty] private bool _showSwitchButton;

    [ObservableProperty] private bool _showWindowSize;

    [ObservableProperty] private string _typeInfoText = Config.Launch.TypeInfo;

    [ObservableProperty] private int _visibilityIndex = Math.Max(
        0,
        (int)Config.Launch.LauncherVisibility);

    [ObservableProperty]
    private string _windowHeightText = Config.Launch.GameWindowHeight.ToString();

    [ObservableProperty] private string _windowTitleText = Config.Launch.Title;

    [ObservableProperty] private int _windowTypeIndex = Math.Max(
        0,
        (int)Config.Launch.GameWindowMode);

    [ObservableProperty] private string _windowWidthText = Config.Launch.GameWindowWidth.ToString();

    public void Reload()
    {
        _reloading = true;
        try
        {
            TypeInfoText = Config.Launch.TypeInfo;
            WindowTitleText = Config.Launch.Title;
            IndieSolutionIndex = Math.Clamp(Config.Launch.IndieSolutionV2, 0, 4);
            VisibilityIndex = Math.Clamp((int)Config.Launch.LauncherVisibility, 0, 5);
            PriorityIndex = PriorityToIndex((int)Config.Launch.ProcessPriority);
            WindowTypeIndex = Math.Clamp((int)Config.Launch.GameWindowMode, 0, 4);
            WindowWidthText = Config.Launch.GameWindowWidth.ToString();
            WindowHeightText = Config.Launch.GameWindowHeight.ToString();
            MsAuthTypeIndex = Math.Clamp(Config.Launch.LoginMsAuthType, 0, 1);
            PreferredIpStackIndex = Math.Clamp((int)Config.Launch.PreferredIpStack, 0, 2);
            RendererIndex = Math.Clamp(Config.Launch.Renderer, 0, 3);
            JvmArgsText = Config.Launch.JvmArgs;
            GameArgsText = Config.Launch.GameArgs;
            PreLaunchText = Config.Launch.PreLaunchCommand;
            PreLaunchCommandWait = Config.Launch.PreLaunchCommandWait;
            MemoryAllocationMode = Math.Clamp(Config.Launch.MemoryAllocationMode, 0, 1);
            CustomMemorySize = Config.Launch.CustomMemorySize;
            CustomInitialMemorySize = Config.Launch.CustomInitialMemorySize;
            LockMemory = Config.Launch.LockMemory;
            DisableJlw = SystemInfo.IsArm64System || Config.Launch.DisableJlw;
            DisableLF = Config.Launch.DisableLF;
            SetGpuPreference = Config.Launch.SetGpuPreference;
            NoJavaw = Config.Launch.NoJavaw;
            DisableLwjglUnsafeAgent = Config.Launch.DisableLwjglUnsafeAgent;
            DisableCrashAnalysis = Config.Launch.DisableCrashAnalysis;
            DisableJlwEnabled = !SystemInfo.IsArm64System;
            ShowWindowSize = (int)Config.Launch.GameWindowMode == 3;
            CheckAdvanceRunWaitVisible = !string.IsNullOrEmpty(PreLaunchText);
            RamType(MemoryAllocationMode);
            RefreshJvmResetVisibility();
            RefreshInstanceState();
            // 强制重发索引属性变更，确保下拉在重新进入页面时按配置重新选中
            OnPropertyChanged(nameof(IndieSolutionIndex));
            OnPropertyChanged(nameof(VisibilityIndex));
            OnPropertyChanged(nameof(WindowTypeIndex));
            OnPropertyChanged(nameof(MsAuthTypeIndex));
            OnPropertyChanged(nameof(PreferredIpStackIndex));
            OnPropertyChanged(nameof(RendererIndex));
            OnPropertyChanged(nameof(PriorityIndex));
        }
        finally
        {
            _reloading = false;
        }
    }

    /// <summary>按内存分配模式刷新内存滑块与初始内存滑块的可用状态。</summary>
    public void RamType(int type)
    {
        RamCustomEnabled = type == 1;
        RamInitialCustomEnabled = type == 1 && !Config.Launch.LockMemory;
    }

    public void RefreshInstanceState()
    {
        ShowSwitchButton = InstanceList.McMcInstanceSelected is not null;
    }

    public void RefreshJvmResetVisibility()
    {
        try
        {
            var defaultArgs = ConfigService.GetConfigItem<string>("LaunchAdvanceJvm").DefaultValue;
            JvmResetVisible = !string.Equals(JvmArgsText, defaultArgs, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            JvmResetVisible = false;
        }
    }

    /// <summary>计算内存显示文本与 32 位 Java / 内存过高警告；返回供视图绘制宽度条的数据。</summary>
    public RamVisualState? BuildRamVisual()
    {
        try
        {
            var instance = InstanceList.McMcInstanceSelected;
            var ramGame = Math.Round(MemorySettings.Current?.GetRam(instance!) ?? 0d, 5);
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();
            var ramTotal = Math.Round(phyRam.Total / 1024d / 1024d / 1024d, 1);
            var ramAvailable = Math.Round(phyRam.Available / 1024d / 1024d / 1024d, 1);
            var ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
            var ramUsed = Math.Round(ramTotal - ramAvailable, 5);
            var ramEmpty = Math.Round(Math.Clamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);

            var maxTick = ramTotal switch
            {
                <= 1.5d => (int)Math.Round(Math.Max(Math.Floor((ramTotal - 0.3d) / 0.1d), 1d)),
                <= 8d => (int)Math.Round(Math.Floor((ramTotal - 1.5d) / 0.5d) + 12d),
                <= 16d => (int)Math.Round(Math.Floor((ramTotal - 8d) / 1d) + 25d),
                _ => (int)Math.Round(Math.Floor((ramTotal - 16d) / 2d) + 33d)
            };

            var ramInitial = MemorySettings.Current?.GetInitialRam(instance!);
            string suffixText;
            if (ramInitial.HasValue && ramGame != ramGameActual)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixBoth",
                    Lang.Number(ramInitial.Value, "N1"),
                    Lang.Number(ramGameActual, "N1"));
            else if (ramInitial.HasValue)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixInitial",
                    Lang.Number(ramInitial.Value, "N1"));
            else if (ramGame != ramGameActual)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixAvailable",
                    Lang.Number(ramGameActual, "N1"));
            else
                suffixText = string.Empty;

            RamGameText =
                $"{Lang.Number(ramGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : string.Empty)}";
            RamUsedText = $"{Lang.Number(ramUsed, "N1")} GiB";
            RamTotalText = $" / {Lang.Number(ramTotal, "N1")} GiB";
            RamWarnVisible = ramGame == 1d
                && !JavaRuntime.IsGameSet64BitJava(instance)
                && !SystemInfo.Is32BitSystem
                && JavaRuntime.Javas.ExistAnyJava();
            RamTooHighVisible = ramGame / ramTotal > 0.75d;

            return new RamVisualState(ramUsed, ramGameActual, ramEmpty, maxTick);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static double GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => value - 25 + 8d,
            _ => (value - 33) * 2d + 16d
        };
    }

    public static int PriorityToIndex(int priority)
    {
        return priority switch
        {
            4 => 0,
            3 => 1,
            0 => 2,
            1 => 3,
            2 => 4,
            _ => 3
        };
    }

    public static int IndexToPriority(int index)
    {
        return index switch
        {
            0 => 4,
            1 => 3,
            2 => 0,
            3 => 1,
            4 => 2,
            _ => 1
        };
    }

    #region 确认框

    public async Task<bool> ConfirmInstanceIsolationAsync()
    {
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Title"),
                Content = Lang.Text("Setup.Launch.Options.InstanceIsolation.DefaultPolicyHint"),
                Buttons = DialogButtons.Ok
            });
        return result.IsConfirmed;
    }

    public async Task<bool> ConfirmCloseImmediatelyAsync()
    {
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Title"),
                Content = Lang.Text(
                    "Setup.Launch.Options.Visibility.CloseImmediately.Warning.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(
                        Lang.Text(
                            "Setup.Launch.Options.Visibility.CloseImmediately.Warning.Continue")),
                    DialogButton.Create(Lang.Text("Common.Action.Cancel")))
            });
        return result.Button == 1;
    }

    public async Task<bool> ConfirmRendererAsync()
    {
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(
                        Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm")),
                    DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                IsWarn = true
            });
        return result.Button == 1;
    }

    #endregion

    #region 配置写入

    partial void OnWindowTitleTextChanged(string value)
    {
        if (!_reloading) Config.Launch.Title = value ?? string.Empty;
    }

    partial void OnPreLaunchCommandWaitChanged(bool value)
    {
        if (!_reloading) Config.Launch.PreLaunchCommandWait = value;
    }

    partial void OnMemoryAllocationModeChanged(int value)
    {
        RamType(value);
        if (!_reloading && value >= 0) Config.Launch.MemoryAllocationMode = value;
    }

    partial void OnCustomMemorySizeChanged(int value)
    {
        if (!_reloading) Config.Launch.CustomMemorySize = value;
    }

    partial void OnCustomInitialMemorySizeChanged(int value)
    {
        if (!_reloading) Config.Launch.CustomInitialMemorySize = value;
    }

    partial void OnLockMemoryChanged(bool value)
    {
        RamType(MemoryAllocationMode);
        if (!_reloading) Config.Launch.LockMemory = value;
    }

    partial void OnDisableJlwChanged(bool value)
    {
        if (!_reloading) Config.Launch.DisableJlw = value;
    }

    partial void OnDisableLFChanged(bool value)
    {
        if (!_reloading) Config.Launch.DisableLF = value;
    }

    partial void OnSetGpuPreferenceChanged(bool value)
    {
        if (!_reloading) Config.Launch.SetGpuPreference = value;
    }

    partial void OnNoJavawChanged(bool value)
    {
        if (!_reloading) Config.Launch.NoJavaw = value;
    }

    partial void OnDisableLwjglUnsafeAgentChanged(bool value)
    {
        if (!_reloading) Config.Launch.DisableLwjglUnsafeAgent = value;
    }

    partial void OnDisableCrashAnalysisChanged(bool value)
    {
        if (!_reloading) Config.Launch.DisableCrashAnalysis = value;
    }

    partial void OnGameArgsTextChanged(string value)
    {
        if (!_reloading) Config.Launch.GameArgs = value ?? string.Empty;
    }

    partial void OnJvmArgsTextChanged(string value)
    {
        if (_reloading) return;
        Config.Launch.JvmArgs = value ?? string.Empty;
        RefreshJvmResetVisibility();
    }

    partial void OnPreLaunchTextChanged(string value)
    {
        CheckAdvanceRunWaitVisible = !string.IsNullOrEmpty(value);
        if (!_reloading) Config.Launch.PreLaunchCommand = value ?? string.Empty;
    }

    partial void OnTypeInfoTextChanged(string value)
    {
        if (!_reloading) Config.Launch.TypeInfo = value ?? string.Empty;
    }

    partial void OnWindowTypeIndexChanged(int value)
    {
        ShowWindowSize = value == 3;
        if (!_reloading && value >= 0)
            Config.Launch.GameWindowMode = (GameWindowSizeMode)value;
    }

    partial void OnWindowWidthTextChanged(string value)
    {
        if (_reloading) return;
        if (int.TryParse(value, out var width) && width is > 0 and <= 9999)
            Config.Launch.GameWindowWidth = width;
    }

    partial void OnWindowHeightTextChanged(string value)
    {
        if (_reloading) return;
        if (int.TryParse(value, out var height) && height is > 0 and <= 9999)
            Config.Launch.GameWindowHeight = height;
    }

    partial void OnMsAuthTypeIndexChanged(int value)
    {
        if (!_reloading && value >= 0) Config.Launch.LoginMsAuthType = value;
    }

    partial void OnPreferredIpStackIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Launch.PreferredIpStack = (JvmPreferredIpStack)value;
    }

    partial void OnPriorityIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Launch.ProcessPriority = (GameProcessPriority)IndexToPriority(value);
    }

    partial void OnVisibilityIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Launch.LauncherVisibility = (LauncherVisibility)value;
    }

    partial void OnIndieSolutionIndexChanged(int value)
    {
        if (!_reloading && value >= 0) Config.Launch.IndieSolutionV2 = value;
    }

    partial void OnRendererIndexChanged(int value)
    {
        if (!_reloading && value >= 0) Config.Launch.Renderer = value;
    }

    #endregion
}

/// <summary>内存占用条中三段宽度的计算结果。</summary>
public sealed record RamVisualState(
    double Used,
    double GameActual,
    double Empty,
    int MaxTick);