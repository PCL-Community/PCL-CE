using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Validate;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例设置页：启动选项、内存、服务器与高级设置四张卡片，外加跳转全局设置的入口。
///     所有设置读写实例独立配置（<c>Config.Instance.*</c> 与 <c>Config.InstanceAuth.*</c>），与全局设置页共用同一份配置。
/// </summary>
public partial class PageInstanceSetup : MyPageRight
{
    private readonly DispatcherTimer _ramTimer;
    private MyButton _btnAuthLock = null!;
    private MyButton _btnLittleSkin = null!;
    private MyButton _btnNewProfile = null!;
    private MyCheckBox _checkDebugLog4J = null!;
    private MyCheckBox _checkDisableAssets = null!;
    private MyCheckBox _checkDisableJlw = null!;
    private MyCheckBox _checkDisableLf = null!;
    private MyCheckBox _checkDisableLwjglAgent = null!;
    private MyCheckBox _checkDisableModUpdate = null!;
    private MyCheckBox _checkIgnoreJava = null!;
    private MyCheckBox _checkRunWait = null!;
    private MyCheckBox _checkTitleDefault = null!;
    private MyCheckBox _checkUseProxy = null!;
    private MyComboBox _comboIndie = null!;
    private MyComboBox _comboJava = null!;
    private MyComboBox _comboLoginRequire = null!;
    private MyComboBox _comboRenderer = null!;
    private MyComboBox _comboTitle = null!;
    private MyHint _hintHttp = null!;
    private MyHint _hintLoginLock = null!;
    private MyHint _hintRam32 = null!;
    private MyHint _hintRamHigh = null!;
    private MyHint _hintSsl = null!;
    private int _javaRefreshVersion;
    private TextBlock _labAuthName = null!;
    private TextBlock _labAuthRegister = null!;
    private TextBlock _labAuthServer = null!;
    private MyRadioBox _radioRamAuto = null!;
    private MyRadioBox _radioRamCustom = null!;
    private MyRadioBox _radioRamGlobal = null!;
    private MySlider _sliderRamInitial = null!;
    private MySlider _sliderRamMax = null!;
    private bool _syncing;
    private MyTextBox _textAuthName = null!;
    private MyTextBox _textAuthRegister = null!;
    private MyTextBox _textAuthServer = null!;
    private MyTextBox _textClasspath = null!;
    private MyTextBox _textGame = null!;
    private MyTextBox _textInfo = null!;
    private MyTextBox _textJvm = null!;
    private MyTextBox _textPreLaunch = null!;
    private MyTextBox _textServerEnter = null!;

    public PageInstanceSetup()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        BindControls();
        WireEvents();

        _ramTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1d)
        };
        _ramTimer.Tick += (_, _) => RefreshRam();
        AttachedToVisualTree += (_, _) => _ramTimer.Start();
        DetachedFromVisualTree += (_, _) => _ramTimer.Stop();
        ViewModel.ReloadRequested += Reload;
    }

    public PageInstanceSetupViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        PanBack.ScrollToHome();
        Reload();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        Reload();
        Dispatcher.UIThread.Post(() =>
        {
            if (PanScroll is not { } scroll)
                return;
            scroll.Offset = new Vector(scroll.Offset.X, offset);
        });
    }

    public void Reload()
    {
        _syncing = true;
        try
        {
            ViewModel.Load();
            RefreshJavaCombo();
            ApplyState();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BindControls()
    {
        _btnAuthLock = BtnServerAuthLock;
        _btnLittleSkin = BtnServerAuthLittle;
        _btnNewProfile = BtnServerNewProfile;
        _checkDebugLog4J = CheckUseDebugLog4j2Config;
        _checkDisableAssets = CheckAdvanceAssetsV2;
        _checkDisableJlw = CheckAdvanceDisableJLW;
        _checkDisableLf = CheckAdvanceDisableLF;
        _checkDisableLwjglAgent = CheckAdvanceDisableLwjglUnsafeAgent;
        _checkDisableModUpdate = CheckAdvanceDisableModUpdate;
        _checkIgnoreJava = CheckAdvanceJava;
        _checkRunWait = CheckAdvanceRunWait;
        _checkTitleDefault = CheckArgumentTitleEmpty;
        _checkUseProxy = CheckAdvanceUseProxyV2;
        _comboIndie = ComboArgumentIndieV2;
        _comboJava = ComboArgumentJava;
        _comboLoginRequire = ComboServerLoginRequire;
        _comboRenderer = ComboAdvanceRenderer;
        _comboTitle = ComboArgumentTitle;
        _hintHttp = LabServerAuthServerSecurity;
        _hintLoginLock = HintServerLoginLock;
        _hintRam32 = LabRamWarn;
        _hintRamHigh = HintRamTooHigh;
        _hintSsl = LabServerAuthServerSecurityVerify;
        _labAuthName = LabServerAuthName;
        _labAuthRegister = LabServerAuthRegister;
        _labAuthServer = LabServerAuthServer;
        _radioRamAuto = RadioRamType0;
        _radioRamCustom = RadioRamType1;
        _radioRamGlobal = RadioRamType2;
        _sliderRamInitial = SliderRamInitialCustom;
        _sliderRamMax = SliderRamCustom;
        _textAuthName = TextServerAuthName;
        _textAuthRegister = TextServerAuthRegister;
        _textAuthServer = TextServerAuthServer;
        _textClasspath = TextAdvanceClasspathHead;
        _textGame = TextAdvanceGame;
        _textInfo = TextArgumentInfo;
        _textJvm = TextAdvanceJvm;
        _textPreLaunch = TextAdvanceRun;
        _textServerEnter = TextServerEnter;
    }

    private void WireEvents()
    {
        _comboIndie.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = OnIndieChangedAsync();
        };
        _comboTitle.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetTitle(_comboTitle.Text ?? string.Empty);
            RefreshTitleHint();
        };
        _checkTitleDefault.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetUseGlobalTitle(_checkTitleDefault.Checked == true);
            RefreshTitleHint();
        };
        _textInfo.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetInfo(_textInfo.Text ?? string.Empty);
        };
        _textInfo.ValidateRules.Add(new BlacklistValidator(["\"", "“", "”"]));
        _comboJava.DropDownOpened += (_, _) => RefreshJavaCombo();
        _comboJava.SelectionChanged += (_, _) =>
        {
            if (!_syncing) SaveJavaSelection();
        };

        _radioRamGlobal.Check += (_, _) => SaveMemorySolution(2);
        _radioRamAuto.Check += (_, _) => SaveMemorySolution(0);
        _radioRamCustom.Check += (_, _) => SaveMemorySolution(1);
        _sliderRamMax.Change += (_, user) =>
        {
            if (user && !_syncing) SaveMemoryValues();
        };
        _sliderRamInitial.Change += (_, user) =>
        {
            if (user && !_syncing) SaveMemoryValues();
        };

        _comboLoginRequire.SelectionChanged += (_, _) => OnLoginRequireChanged();
        _textAuthServer.ValidateRules.Add(new HttpValidator());
        _textAuthServer.LostFocus += (_, _) => NormalizeAuthServer();
        _textAuthRegister.ValidateRules.Add(
            new HttpValidator
            {
                AllowsNullOrEmpty = true
            });
        _textAuthRegister.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetAuthRegister(_textAuthRegister.Text ?? string.Empty);
        };
        _textAuthName.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetAuthDisplayName(_textAuthName.Text ?? string.Empty);
        };
        _textServerEnter.ValidateRules.Add(
            new BlacklistValidator(["\"", "“", "”", "http://", "https://"]));
        _textServerEnter.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            var text = (_textServerEnter.Text ?? string.Empty).Replace('：', ':');
            if (text != _textServerEnter.Text)
            {
                _syncing = true;
                _textServerEnter.Text = text;
                _syncing = false;
            }

            ViewModel.SetServerEnter(text);
        };
        _btnLittleSkin.Click += (_, _) => _ = ApplyLittleSkinAsync();
        _btnAuthLock.Click += (_, _) => _ = LockAuthAsync();
        _btnNewProfile.Click += (_, _) => CreateServerProfile();

        _comboRenderer.SelectionChanged += (_, _) => _ = OnRendererChangedAsync();
        _textJvm.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetJvmArgs(_textJvm.Text ?? string.Empty);
        };
        _textGame.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetGameArgs(_textGame.Text ?? string.Empty);
        };
        _textClasspath.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetClasspath(_textClasspath.Text ?? string.Empty);
        };
        _textPreLaunch.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetPreLaunchCommand(_textPreLaunch.Text ?? string.Empty);
            RefreshRunWait();
        };

        _checkRunWait.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetPreLaunchCommandWait(_checkRunWait.Checked == true);
        };
        _checkDisableModUpdate.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetDisableModUpdate(_checkDisableModUpdate.Checked == true);
        };
        _checkIgnoreJava.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetIgnoreJavaCompatibility(_checkIgnoreJava.Checked == true);
        };
        _checkDisableAssets.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetDisableAssetVerify(_checkDisableAssets.Checked == true);
        };
        _checkUseProxy.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetUseProxy(_checkUseProxy.Checked == true);
        };
        _checkDisableJlw.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetDisableJlw(_checkDisableJlw.Checked == true);
        };
        _checkDisableLf.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetDisableLf(_checkDisableLf.Checked == true);
        };
        _checkDisableLwjglAgent.Change += (_, _) =>
        {
            if (_syncing) return;
            ViewModel.SetDisableLwjglUnsafeAgent(_checkDisableLwjglAgent.Checked == true);
        };
        _checkDebugLog4J.Change += (_, _) =>
        {
            if (!_syncing) _ = OnDebugLog4jChangedAsync();
        };
    }

    private void ApplyState()
    {
        if (ViewModel.Instance is null)
        {
            _comboTitle.Text = string.Empty;
            _textInfo.Text = string.Empty;
            _comboIndie.SelectedIndex = 1;
            return;
        }

        _comboIndie.SelectedIndex = ViewModel.IndieEnabled ? 0 : 1;
        _comboTitle.Text = ViewModel.TitleText;
        _checkTitleDefault.Checked = ViewModel.UseGlobalTitle;
        _textInfo.Text = ViewModel.InfoText;
        RefreshTitleHint();

        SetMemorySolution(ViewModel.MemorySolution);
        _sliderRamMax.Value = (int)Math.Round(ViewModel.CustomMemorySize);
        _sliderRamInitial.Value = (int)Math.Round(ViewModel.CustomInitialMemorySize);
        ApplyRamState();

        _textServerEnter.Text = ViewModel.ServerEnterText;
        _comboLoginRequire.SelectedIndex = ViewModel.LoginRequirement;
        _textAuthServer.Text = ViewModel.AuthServerAddress;
        _textAuthName.Text = ViewModel.AuthDisplayName;
        _textAuthRegister.Text = ViewModel.AuthRegisterAddress;
        ApplyServerLoginState();

        _comboRenderer.SelectedIndex = ViewModel.RendererIndex;
        _textClasspath.Text = ViewModel.ClasspathHead;
        _textJvm.Text = ViewModel.JvmArgs;
        _textGame.Text = ViewModel.GameArgs;
        _textPreLaunch.Text = ViewModel.PreLaunchCommand;
        _checkRunWait.Checked = ViewModel.PreLaunchCommandWait;
        _checkDisableLwjglAgent.Checked = ViewModel.DisableLwjglUnsafeAgent;
        _checkDisableModUpdate.Checked = ViewModel.DisableModUpdate;
        _checkDisableAssets.Checked = ViewModel.DisableAssetVerify;
        _checkUseProxy.Checked = ViewModel.UseProxy;
        _checkIgnoreJava.Checked = ViewModel.IgnoreJavaCompatibility;
        _checkDisableJlw.Checked = ViewModel.DisableJlw;
        _checkDisableJlw.IsEnabled = !ViewModel.IsArm64System;
        ToolTip.SetTip(
            _checkDisableJlw,
            ViewModel.IsArm64System
                ? Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64ToolTip")
                : Lang.Text("Setup.Launch.Advanced.DisableJlw.ToolTip"));
        _checkDebugLog4J.Checked = ViewModel.UseDebugLog4j;
        _checkDisableLf.Checked = ViewModel.DisableLf;
        RefreshRunWait();
    }

    public void Reset()
    {
        _syncing = true;
        try
        {
            ViewModel.Reset();
            ApplyState();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ApplyRamState()
    {
        var columns = PanRamDisplay.ColumnDefinitions;
        columns[0].Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamUsedWeight),
            GridUnitType.Star);
        columns[1].Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamGameWeight),
            GridUnitType.Star);
        columns[2].Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamEmptyWeight),
            GridUnitType.Star);
        _sliderRamMax.MaxValue = (int)Math.Max(1d, ViewModel.RamMaxValue);
        _sliderRamInitial.MaxValue = (int)Math.Max(1d, ViewModel.RamMaxValue);
        _hintRam32.IsVisible = ViewModel.Ram32WarningVisible;
        _hintRamHigh.IsVisible = ViewModel.RamHighWarningVisible;
    }

    private void ApplyServerLoginState()
    {
        _labAuthServer.IsVisible = ViewModel.AuthVisible;
        _textAuthServer.IsVisible = ViewModel.AuthVisible;
        _labAuthRegister.IsVisible = ViewModel.AuthVisible;
        _textAuthRegister.IsVisible = ViewModel.AuthVisible;
        _labAuthName.IsVisible = ViewModel.AuthVisible;
        _textAuthName.IsVisible = ViewModel.AuthVisible;
        _btnLittleSkin.IsVisible = ViewModel.AuthVisible;
        _textAuthServer.IsEnabled = ViewModel.AuthEditable;
        _textAuthRegister.IsEnabled = ViewModel.AuthEditable;
        _textAuthName.IsEnabled = ViewModel.AuthEditable;
        _btnLittleSkin.IsEnabled = ViewModel.AuthEditable;
        _comboLoginRequire.IsEnabled = ViewModel.AuthEditable;
        _hintLoginLock.IsVisible = ViewModel.LoginLockVisible;
        _btnAuthLock.IsVisible = ViewModel.AuthLockVisible;
        _btnAuthLock.IsEnabled = ViewModel.AuthLockEnabled && _textAuthServer.IsValidated;
        _btnNewProfile.IsVisible = ViewModel.NewProfileVisible;
        _hintSsl.IsVisible = ViewModel.SslWarningVisible;
        _hintHttp.IsVisible = ViewModel.HttpWarningVisible;
    }

    #region 启动选项逻辑

    private async Task OnIndieChangedAsync()
    {
        var desired = _comboIndie.SelectedIndex == 0;
        _comboIndie.IsEnabled = false;
        try
        {
            if (!await ViewModel.ApplyIndieAsync(desired))
            {
                _syncing = true;
                _comboIndie.SelectedIndex = desired ? 1 : 0;
                _syncing = false;
            }
        }
        finally
        {
            _comboIndie.IsEnabled = true;
        }
    }

    private void RefreshTitleHint()
    {
        _comboTitle.HintText = ViewModel.TitleHintText;
        _checkTitleDefault.IsVisible = ViewModel.TitleDefaultVisible;
    }

    private void RefreshJavaCombo()
    {
        if (_comboJava is null)
            return;

        var version = ++_javaRefreshVersion;
        var wasSyncing = _syncing;
        _syncing = true;
        _comboJava.Items.Clear();
        var loading = new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Options.Java.Loading"),
            IsEnabled = false
        };
        _comboJava.Items.Add(loading);
        _comboJava.SelectedItem = loading;
        _syncing = wasSyncing;
        _ = RefreshJavaComboAsync(version);
    }

    private async Task RefreshJavaComboAsync(int version)
    {
        if (_comboJava is null)
            return;
        IReadOnlyList<JavaOption> options;
        try
        {
            options = await ViewModel.LoadJavaOptionsAsync();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
            return;
        }

        if (version != _javaRefreshVersion || _comboJava is null)
            return;

        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            _comboJava.Items.Clear();
            if (ViewModel.HasJavaLoadError)
            {
                var failed = new MyComboBoxItem
                {
                    Content = Lang.Text("Instance.Setup.Options.Java.LoadFailed"),
                    IsEnabled = false
                };
                _comboJava.Items.Add(failed);
                _comboJava.SelectedItem = failed;
                return;
            }

            MyComboBoxItem? selectedItem = null;
            foreach (var option in options)
            {
                var item = new MyComboBoxItem
                {
                    Content = option.Text,
                    Tag = option,
                    IsEnabled = option.IsEnabled
                };
                if (!string.IsNullOrEmpty(option.ToolTip))
                    ToolTip.SetTip(item, option.ToolTip);
                _comboJava.Items.Add(item);
                if (option.IsSelected)
                    selectedItem = item;
            }

            if (selectedItem is not null)
                _comboJava.SelectedItem = selectedItem;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }

    private void SaveJavaSelection()
    {
        if (_comboJava.SelectedItem is not MyComboBoxItem { Tag: JavaOption option })
            return;
        _ = SaveJavaSelectionAsync(option);
    }

    private async Task SaveJavaSelectionAsync(JavaOption option)
    {
        if (!await ViewModel.SelectJavaAsync(option))
            RefreshJavaCombo();
        RefreshRam();
    }

    #endregion

    #region 内存逻辑

    private void SetMemorySolution(int solution)
    {
        _radioRamAuto.Checked = solution == 0;
        _radioRamCustom.Checked = solution == 1;
        _radioRamGlobal.Checked = solution == 2;
        _sliderRamMax.IsEnabled = solution == 1;
        _sliderRamInitial.IsEnabled = solution == 1 && !Config.Launch.LockMemory;
    }

    private void SaveMemorySolution(int solution)
    {
        if (_syncing)
            return;
        ViewModel.SetMemorySolution(solution);
        SetMemorySolution(solution);
        RefreshRam();
    }

    private void SaveMemoryValues()
    {
        ViewModel.SetMemoryValues(_sliderRamMax.Value, _sliderRamInitial.Value);
        RefreshRam();
    }

    private void RefreshRam()
    {
        if (ViewModel.Instance is null)
            return;
        ViewModel.RefreshRam();
        ApplyRamState();
    }

    #endregion

    #region 服务器逻辑

    private void NormalizeAuthServer()
    {
        if (_syncing)
            return;
        if (ViewModel.NormalizeAuthServer())
        {
            _syncing = true;
            _textAuthServer.Text = ViewModel.AuthServerAddress;
            _syncing = false;
        }

        ViewModel.SetAuthServer(_textAuthServer.Text ?? string.Empty);
        RefreshServerLogin(_comboLoginRequire.SelectedIndex);
    }

    private void OnLoginRequireChanged()
    {
        if (_syncing)
            return;
        if (!ViewModel.AuthEditable)
        {
            _syncing = true;
            _comboLoginRequire.SelectedIndex = ViewModel.LoginRequirement;
            _syncing = false;
            return;
        }

        var index = _comboLoginRequire.SelectedIndex;
        if (index < 0)
            return;
        ViewModel.RefreshServerLogin(index);
        ApplyServerLoginState();
        _btnAuthLock.IsEnabled = _textAuthServer.IsValidated;
        if (index is 2 or 3 && !_textAuthServer.IsValidated)
            return;
        ViewModel.SetLoginRequirement(index);
    }

    private void RefreshServerLogin(int type)
    {
        ViewModel.RefreshServerLogin(type);
        ApplyServerLoginState();
    }

    private async Task ApplyLittleSkinAsync()
    {
        if (!await ViewModel.ApplyLittleSkinAsync())
            return;

        _syncing = true;
        _textAuthServer.Text = ViewModel.AuthServerAddress;
        _textAuthRegister.Text = ViewModel.AuthRegisterAddress;
        _textAuthName.Text = ViewModel.AuthDisplayName;
        _syncing = false;
        RefreshServerLogin(_comboLoginRequire.SelectedIndex);
    }

    private async Task LockAuthAsync()
    {
        _btnAuthLock.IsEnabled = false;
        try
        {
            await ViewModel.LockAuthAsync();
        }
        finally
        {
            _btnAuthLock.IsEnabled = true;
        }
    }

    private void CreateServerProfile()
    {
        ViewModel.CreateServerProfileCommand.Execute(null);
    }

    #endregion

    #region 高级设置逻辑

    private async Task OnRendererChangedAsync()
    {
        if (_syncing)
            return;
        var index = _comboRenderer.SelectedIndex;
        if (index < 0)
            return;
        _comboRenderer.IsEnabled = false;
        try
        {
            await ViewModel.ApplyRendererAsync(index);
            _syncing = true;
            _comboRenderer.SelectedIndex = ViewModel.RendererIndex;
            _syncing = false;
        }
        finally
        {
            _comboRenderer.IsEnabled = true;
        }
    }

    private async Task OnDebugLog4jChangedAsync()
    {
        if (_syncing)
            return;
        _checkDebugLog4J.IsEnabled = false;
        try
        {
            await ViewModel.ApplyDebugLog4jAsync(_checkDebugLog4J.Checked == true);
            _syncing = true;
            _checkDebugLog4J.Checked = ViewModel.UseDebugLog4j;
            _syncing = false;
        }
        finally
        {
            _checkDebugLog4J.IsEnabled = true;
        }
    }

    private void RefreshRunWait()
    {
        ViewModel.RefreshRunWait();
        _checkRunWait.IsVisible = ViewModel.RunWaitVisible;
    }

    #endregion
}