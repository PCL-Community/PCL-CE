using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentValidation;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例设置页：启动选项、内存、服务器与高级设置四张卡片，外加跳转全局设置的入口。
///     所有设置读写实例独立配置（<c>Config.Instance.*</c> 与 <c>Config.InstanceAuth.*</c>），与全局设置页共用同一份配置。
/// </summary>
public class PageInstanceSetup : MyPageRight
{
    private readonly StackPanel _panMain;
    private readonly DispatcherTimer _ramTimer;
    private MyButton _btnAuthLock = null!;
    private MyButton _btnLittleSkin = null!;
    private MyButton _btnNewProfile = null!;
    private MyCheckBox _checkDebugLog4j = null!;
    private MyCheckBox _checkDisableAssets = null!;
    private MyCheckBox _checkDisableJlw = null!;
    private MyCheckBox _checkDisableLF = null!;
    private MyCheckBox _checkDisableLwjglAgent = null!;
    private MyCheckBox _checkDisableModUpdate = null!;
    private MyCheckBox _checkIgnoreJava = null!;
    private MyCheckBox _checkRunWait = null!;
    private MyCheckBox _checkTitleDefault = null!;
    private MyCheckBox _checkUseProxy = null!;
    private ColumnDefinition _columnRamEmpty = null!;
    private ColumnDefinition _columnRamGame = null!;
    private ColumnDefinition _columnRamUsed = null!;

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
    private TextBlock _labRamGame = null!;
    private TextBlock _labRamTotal = null!;
    private TextBlock _labRamUsed = null!;
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
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 25d, 25d, 25d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        Child = scroll;
        BuildUi();

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
        Reload();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        BuildUi();
        Reload();
        Dispatcher.UIThread.Post(() =>
        {
            PanScroll?.Offset = new Vector(PanScroll.Offset.X, offset);
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
        _checkDebugLog4j.Checked = ViewModel.UseDebugLog4j;
        _checkDisableLF.Checked = ViewModel.DisableLf;
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
        _columnRamUsed.Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamUsedWeight),
            GridUnitType.Star);
        _columnRamGame.Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamGameWeight),
            GridUnitType.Star);
        _columnRamEmpty.Width = new GridLength(
            Math.Max(0.01d, ViewModel.RamEmptyWeight),
            GridUnitType.Star);
        _sliderRamMax.MaxValue = (int)Math.Max(1d, ViewModel.RamMaxValue);
        _sliderRamInitial.MaxValue = (int)Math.Max(1d, ViewModel.RamMaxValue);
        _labRamUsed.Text = ViewModel.RamUsedText;
        _labRamTotal.Text = ViewModel.RamTotalText;
        _labRamGame.Text = ViewModel.RamGameText;
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
        _hintLoginLock.IsVisible = ViewModel.LoginLockVisible;
        _btnAuthLock.IsVisible = ViewModel.AuthLockVisible;
        _btnAuthLock.IsEnabled = ViewModel.AuthLockEnabled && _textAuthServer.IsValidated;
        _btnNewProfile.IsVisible = ViewModel.NewProfileVisible;
        _hintSsl.IsVisible = ViewModel.SslWarningVisible;
        _hintHttp.IsVisible = ViewModel.HttpWarningVisible;
    }

    #region 界面构建

    private void BuildUi()
    {
        _panMain.Children.Clear();
        _panMain.Children.Add(
            new MyHint
            {
                Text = Lang.Text("Instance.Setup.Hint"),
                Theme = MyHint.Themes.Blue,
                CanClose = true,
                RelativeSetup = "HintIndieSetup",
                Margin = new Thickness(0d, 0d, 0d, 15d)
            });

        BuildArgumentCard();
        BuildMemoryCard();
        BuildServerCard();
        BuildAdvancedCard();

        var globalButton = new MyExtraTextButton
        {
            Text = Lang.Text("Instance.Setup.OpenGlobalSettings"),
            SvgIcon = "lucide/arrow-right",
            LogoScale = 0.9d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, -5d, 0d, 0d)
        };
        globalButton.Click += (_, _) => PageRouter.Navigate(PageType.Setup);
        _panMain.Children.Add(globalButton);
    }

    private void BuildArgumentCard()
    {
        var card = NewCard("Setup.Launch.Options.Title");
        var body = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 25d, 21d)
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,60"),
            RowDefinitions = new RowDefinitions("28,9,28,9,Auto,Auto,28,9,28")
        };

        AddRowLabel(grid, "Instance.Setup.Options.InstanceIsolation", 0);
        _comboIndie = new MyComboBox();
        Grid.SetRow(_comboIndie, 0);
        Grid.SetColumn(_comboIndie, 1);
        Grid.SetColumnSpan(_comboIndie, 2);
        var indieEnabled = new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Options.InstanceIsolation.Enabled")
        };
        ToolTip.SetTip(
            indieEnabled,
            Lang.Text("Instance.Setup.Options.InstanceIsolation.Enabled.ToolTip"));
        _comboIndie.Items.Add(indieEnabled);
        var indieDisabled = new MyComboBoxItem
        {
            Content = Lang.Text("Common.Action.Close")
        };
        ToolTip.SetTip(
            indieDisabled,
            Lang.Text("Instance.Setup.Options.InstanceIsolation.Disabled.ToolTip"));
        _comboIndie.Items.Add(indieDisabled);
        _comboIndie.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = OnIndieChangedAsync();
        };
        grid.Children.Add(_comboIndie);

        AddRowLabel(grid, "Setup.Launch.Options.WindowTitle.Label", 2);
        _comboTitle = new MyComboBox
        {
            IsEditable = true,
            HintText = Lang.Text("Instance.Setup.FollowGlobal")
        };
        ToolTip.SetTip(_comboTitle, Lang.Text("Instance.Setup.Options.WindowTitle.ToolTip"));
        Grid.SetRow(_comboTitle, 2);
        Grid.SetColumn(_comboTitle, 1);
        Grid.SetColumnSpan(_comboTitle, 2);
        _comboTitle.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Launch.Options.WindowTitle.SampleFormat")
            });
        _comboTitle.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetTitle(_comboTitle.Text ?? string.Empty);
            RefreshTitleHint();
        };
        grid.Children.Add(_comboTitle);

        _checkTitleDefault = new MyCheckBox
        {
            Text = Lang.Text("Instance.Setup.Options.WindowTitle.Default")
        };
        ToolTip.SetTip(
            _checkTitleDefault,
            Lang.Text("Instance.Setup.Options.WindowTitle.Default.ToolTip"));
        Grid.SetRow(_checkTitleDefault, 4);
        Grid.SetColumnSpan(_checkTitleDefault, 3);
        _checkTitleDefault.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetUseGlobalTitle(_checkTitleDefault.Checked == true);
            RefreshTitleHint();
        };
        grid.Children.Add(_checkTitleDefault);

        AddRowLabel(grid, "Setup.Launch.Options.CustomInfo.Label", 6);
        _textInfo = new MyTextBox
        {
            HintText = Lang.Text("Instance.Setup.FollowGlobal"),
            ValidateRules = [new BlacklistValidator(["\"", "“", "”"])]
        };
        ToolTip.SetTip(_textInfo, Lang.Text("Instance.Setup.Options.CustomInfo.ToolTip"));
        Grid.SetRow(_textInfo, 6);
        Grid.SetColumn(_textInfo, 1);
        Grid.SetColumnSpan(_textInfo, 2);
        _textInfo.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetInfo(_textInfo.Text ?? string.Empty);
        };
        grid.Children.Add(_textInfo);

        AddRowLabel(grid, "Instance.Setup.Options.Java", 8);
        _comboJava = new MyComboBox
        {
            MaxDropDownHeight = 240d
        };
        ToolTip.SetTip(_comboJava, Lang.Text("Instance.Setup.Options.Java.ToolTip"));
        Grid.SetRow(_comboJava, 8);
        Grid.SetColumn(_comboJava, 1);
        Grid.SetColumnSpan(_comboJava, 2);
        _comboJava.DropDownOpened += (_, _) => RefreshJavaCombo();
        _comboJava.SelectionChanged += (_, _) =>
        {
            if (!_syncing) SaveJavaSelection();
        };
        grid.Children.Add(_comboJava);

        body.Children.Add(grid);
        card.BorderChild = body;
        _panMain.Children.Add(card);
    }

    private void BuildMemoryCard()
    {
        var card = NewCard("Setup.Launch.Memory.Title");
        var body = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 25d, 15d)
        };

        _hintRam32 = new MyHint
        {
            Text = Lang.Text("Setup.Launch.Memory.Warning.Java32Bit"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 0d, 0d, 12d),
            IsVisible = false
        };
        body.Children.Add(_hintRam32);
        _hintRamHigh = new MyHint
        {
            Text = Lang.Text("Setup.Launch.Memory.Warning.TooMuch"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 0d, 0d, 12d),
            IsVisible = false
        };
        body.Children.Add(_hintRamHigh);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0d, 0d, 0d, 3d)
        };

        _radioRamGlobal = new MyRadioBox
        {
            Text = Lang.Text("Instance.Setup.FollowGlobal"),
            Margin = new Thickness(0d, 0d, 20d, 9d)
        };
        Grid.SetRow(_radioRamGlobal, 0);
        Grid.SetColumnSpan(_radioRamGlobal, 2);
        _radioRamGlobal.Check += (_, _) => SaveMemorySolution(2);
        grid.Children.Add(_radioRamGlobal);

        _radioRamAuto = new MyRadioBox
        {
            Text = Lang.Text("Setup.Launch.Memory.Auto"),
            Width = 110d,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 20d, 9d)
        };
        ToolTip.SetTip(_radioRamAuto, Lang.Text("Setup.Launch.Memory.Auto.ToolTip"));
        Grid.SetRow(_radioRamAuto, 1);
        _radioRamAuto.Check += (_, _) => SaveMemorySolution(0);
        grid.Children.Add(_radioRamAuto);

        _radioRamCustom = new MyRadioBox
        {
            Text = Lang.Text("Common.Option.Customize"),
            Width = 110d,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 20d, 9d)
        };
        Grid.SetRow(_radioRamCustom, 2);
        _radioRamCustom.Check += (_, _) => SaveMemorySolution(1);
        grid.Children.Add(_radioRamCustom);

        var maxLabel = new TextBlock
        {
            Text = Lang.Text("Setup.Launch.Memory.Max"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 20d, 9d)
        };
        ToolTip.SetTip(maxLabel, Lang.Text("Setup.Launch.Memory.Max.ToolTip"));
        Grid.SetRow(maxLabel, 3);
        grid.Children.Add(maxLabel);
        _sliderRamMax = new MySlider
        {
            MaxValue = 49,
            Value = 13,
            Margin = new Thickness(0d, 0d, 12d, 9d)
        };
        Grid.SetRow(_sliderRamMax, 3);
        Grid.SetColumn(_sliderRamMax, 1);
        _sliderRamMax.Change += (_, user) =>
        {
            if (user && !_syncing) SaveMemoryValues();
        };
        grid.Children.Add(_sliderRamMax);

        var initialLabel = new TextBlock
        {
            Text = Lang.Text("Setup.Launch.Memory.Initial"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        ToolTip.SetTip(initialLabel, Lang.Text("Setup.Launch.Memory.Initial.ToolTip"));
        Grid.SetRow(initialLabel, 4);
        grid.Children.Add(initialLabel);
        _sliderRamInitial = new MySlider
        {
            MaxValue = 49,
            Value = 0,
            Margin = new Thickness(0d, 0d, 12d, 0d)
        };
        Grid.SetRow(_sliderRamInitial, 4);
        Grid.SetColumn(_sliderRamInitial, 1);
        ToolTip.SetTip(_sliderRamInitial, Lang.Text("Setup.Launch.Memory.Initial.ToolTip"));
        _sliderRamInitial.Change += (_, user) =>
        {
            if (user && !_syncing) SaveMemoryValues();
        };
        grid.Children.Add(_sliderRamInitial);

        body.Children.Add(grid);
        body.Children.Add(BuildRamDisplay());
        card.BorderChild = body;
        _panMain.Children.Add(card);
    }

    private Grid BuildRamDisplay()
    {
        var grid = new Grid
        {
            Margin = new Thickness(0d, 11d, 2d, 0d),
            RowDefinitions = new RowDefinitions("*,4,*")
        };
        _columnRamUsed = new ColumnDefinition(new GridLength(4.7d, GridUnitType.Star));
        _columnRamGame = new ColumnDefinition(new GridLength(2.5d, GridUnitType.Star));
        _columnRamEmpty = new ColumnDefinition(new GridLength(0.7d, GridUnitType.Star));
        grid.ColumnDefinitions.Add(_columnRamUsed);
        grid.ColumnDefinitions.Add(_columnRamGame);
        grid.ColumnDefinitions.Add(_columnRamEmpty);

        var rectUsed = new Rectangle();
        Grid.SetRow(rectUsed, 1);
        ContainerTheme.BindDynamic(rectUsed, Shape.FillProperty, "ColorObject3");
        grid.Children.Add(rectUsed);
        var rectGame = new Rectangle
        {
            Opacity = 0.5d
        };
        Grid.SetRow(rectGame, 1);
        Grid.SetColumn(rectGame, 1);
        ContainerTheme.BindDynamic(rectGame, Shape.FillProperty, "ColorBrush3");
        grid.Children.Add(rectGame);
        var rectEmpty = new Rectangle
        {
            Opacity = 0.7d
        };
        Grid.SetRow(rectEmpty, 1);
        Grid.SetColumn(rectEmpty, 2);
        ContainerTheme.BindDynamic(rectEmpty, Shape.FillProperty, "ColorBrush6");
        grid.Children.Add(rectEmpty);

        // 两段说明文字分别贴左/贴右，使用各自列而不在同一单元格叠加，避免窄窗口下文字重叠。
        var usedTitle = new TextBlock
        {
            Text = Lang.Text("Instance.Setup.Memory.Used"),
            Opacity = 0.7d,
            FontSize = 11d,
            Margin = new Thickness(2d, 0d, 0d, 5d),
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumnSpan(usedTitle, 1);
        grid.Children.Add(usedTitle);

        var gameTitle = new TextBlock
        {
            Text = Lang.Text("Setup.Launch.Memory.GameAllocation"),
            Opacity = 0.7d,
            FontSize = 11d,
            Margin = new Thickness(0d, 0d, 2d, 5d),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(gameTitle, 1);
        Grid.SetColumnSpan(gameTitle, 2);
        grid.Children.Add(gameTitle);

        var usedPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2d, 3d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(usedPanel, 2);
        Grid.SetColumnSpan(usedPanel, 1);
        _labRamUsed = new TextBlock
        {
            FontSize = 16d
        };
        ContainerTheme.BindDynamic(_labRamUsed, TextBlock.ForegroundProperty, "ColorBrushMemory");
        usedPanel.Children.Add(_labRamUsed);
        _labRamTotal = new TextBlock
        {
            FontSize = 16d
        };
        ContainerTheme.BindDynamic(_labRamTotal, TextBlock.ForegroundProperty, "ColorBrushMemory");
        usedPanel.Children.Add(_labRamTotal);
        grid.Children.Add(usedPanel);

        _labRamGame = new TextBlock
        {
            FontSize = 16d,
            Margin = new Thickness(0d, 3d, 2d, 0d),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(_labRamGame, 2);
        Grid.SetColumn(_labRamGame, 1);
        Grid.SetColumnSpan(_labRamGame, 2);
        ContainerTheme.BindDynamic(_labRamGame, TextBlock.ForegroundProperty, "ColorBrushMemory");
        grid.Children.Add(_labRamGame);
        return grid;
    }

    private void BuildServerCard()
    {
        var card = NewCard("Instance.Setup.Server.Title");
        var body = new StackPanel
        {
            Margin = new Thickness(25d, 42d, 25d, 18d)
        };

        _hintLoginLock = new MyHint
        {
            Text = Lang.Text("Instance.Setup.Server.LoginLockHint"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 0d, 0d, 13d),
            IsVisible = false
        };
        body.Children.Add(_hintLoginLock);
        _hintSsl = new MyHint
        {
            Text = Lang.Text("Instance.Setup.Server.SslWarning"),
            Margin = new Thickness(0d, 0d, 0d, 13d),
            IsVisible = false
        };
        body.Children.Add(_hintSsl);
        _hintHttp = new MyHint
        {
            Text = Lang.Text("Instance.Setup.Server.HttpWarning"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 0d, 0d, 13d),
            IsVisible = false
        };
        body.Children.Add(_hintHttp);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("28,Auto,Auto,Auto,9,28,3")
        };
        AddRowLabel(grid, "Instance.Setup.Server.RestrictLogin", 0);
        _comboLoginRequire = new MyComboBox();
        Grid.SetRow(_comboLoginRequire, 0);
        Grid.SetColumn(_comboLoginRequire, 1);
        _comboLoginRequire.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Server.LoginMethod.Unlimited")
            });
        _comboLoginRequire.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Server.LoginMethod.MicrosoftOnly")
            });
        _comboLoginRequire.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Server.LoginMethod.AuthOnly")
            });
        _comboLoginRequire.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Server.LoginMethod.MicrosoftOrAuth")
            });
        _comboLoginRequire.SelectionChanged += (_, _) => OnLoginRequireChanged();
        grid.Children.Add(_comboLoginRequire);

        _labAuthServer = AddFieldLabel(grid, "Instance.Setup.Server.AuthServer", 1);
        _textAuthServer = AddFieldBox(
            grid,
            1,
            "Instance.Setup.Server.AuthServer.Hint",
            [new HttpValidator()]);
        _textAuthServer.LostFocus += (_, _) =>
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
        };

        _labAuthRegister = AddFieldLabel(grid, "Instance.Setup.Server.Register", 2);
        _textAuthRegister = AddFieldBox(
            grid,
            2,
            "Instance.Setup.Server.Register.Hint",
            [
                new HttpValidator
                {
                    AllowsNullOrEmpty = true
                }
            ]);
        _textAuthRegister.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetAuthRegister(_textAuthRegister.Text ?? string.Empty);
        };

        _labAuthName = AddFieldLabel(grid, "Instance.Setup.Server.Name", 3);
        _textAuthName = AddFieldBox(grid, 3, "Instance.Setup.Server.Name.Hint", []);
        _textAuthName.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetAuthDisplayName(_textAuthName.Text ?? string.Empty);
        };

        AddRowLabel(grid, "Instance.Setup.Server.AutoJoin", 5);
        _textServerEnter = new MyTextBox
        {
            ValidateRules = [new BlacklistValidator(["\"", "“", "”", "http://", "https://"])]
        };
        ToolTip.SetTip(_textServerEnter, Lang.Text("Instance.Setup.Server.AutoJoin.ToolTip"));
        Grid.SetRow(_textServerEnter, 5);
        Grid.SetColumn(_textServerEnter, 1);
        _textServerEnter.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            var text = (_textServerEnter.Text ?? string.Empty).Replace('：', ':');
            if (text != _textServerEnter.Text)
            {
                _syncing = true;
                _textServerEnter.Text = text;
                _syncing = false;
            }

            ViewModel.SetServerEnter(text);
        };
        grid.Children.Add(_textServerEnter);
        body.Children.Add(grid);

        _btnLittleSkin = new MyButton
        {
            Text = Lang.Text("Instance.Setup.Server.SetLittleSkin"),
            Height = 35d,
            MinWidth = 150d,
            Margin = new Thickness(0d, 12d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = MyButton.ColorState.Highlight
        };
        _btnLittleSkin.Click += (_, _) => _ = ApplyLittleSkinAsync();
        body.Children.Add(_btnLittleSkin);

        _btnAuthLock = new MyButton
        {
            Text = Lang.Text("Instance.Setup.Server.LockLoginMethod"),
            Height = 35d,
            MinWidth = 150d,
            Margin = new Thickness(180d, -35d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _btnAuthLock.Click += (_, _) => _ = LockAuthAsync();
        body.Children.Add(_btnAuthLock);

        _btnNewProfile = new MyButton
        {
            Text = Lang.Text("Instance.Setup.Server.CreateProfile"),
            Height = 35d,
            MinWidth = 150d,
            Margin = new Thickness(360d, -35d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _btnNewProfile.Click += (_, _) => CreateServerProfile();
        body.Children.Add(_btnNewProfile);

        card.BorderChild = body;
        _panMain.Children.Add(card);
    }

    private void BuildAdvancedCard()
    {
        var card = NewCard("Setup.Launch.Advanced.Title");
        var grid = new Grid
        {
            Margin = new Thickness(25d, 40d, 25d, 15d),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("28,9,Auto,9,28,9,28,9,31,Auto,Auto")
        };

        AddRowLabel(grid, "Setup.Launch.Advanced.Renderer.Label", 0);
        _comboRenderer = new MyComboBox();
        Grid.SetRow(_comboRenderer, 0);
        Grid.SetColumn(_comboRenderer, 1);
        _comboRenderer.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.FollowGlobal")
            });
        _comboRenderer.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Launch.Advanced.Renderer.GameDefault")
            });
        _comboRenderer.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Launch.Advanced.Renderer.Software")
            });
        _comboRenderer.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Launch.Advanced.Renderer.DirectX12")
            });
        _comboRenderer.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Launch.Advanced.Renderer.Vulkan")
            });
        _comboRenderer.SelectionChanged += (_, _) => _ = OnRendererChangedAsync();
        grid.Children.Add(_comboRenderer);

        AddRowLabel(grid, "Setup.Launch.Advanced.JvmHead", 2);
        _textJvm = new MyTextBox
        {
            MaxLength = 4000,
            Height = 100d,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0d, 5d),
            HintText = Lang.Text("Instance.Setup.FollowGlobal")
        };
        ToolTip.SetTip(_textJvm, Lang.Text("Instance.Setup.Advanced.JvmArgs.ToolTip"));
        Grid.SetRow(_textJvm, 2);
        Grid.SetColumn(_textJvm, 1);
        _textJvm.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetJvmArgs(_textJvm.Text ?? string.Empty);
        };
        grid.Children.Add(_textJvm);

        AddRowLabel(grid, "Setup.Launch.Advanced.GameTail", 4);
        _textGame = new MyTextBox
        {
            HintText = Lang.Text("Instance.Setup.FollowGlobal")
        };
        ToolTip.SetTip(_textGame, Lang.Text("Instance.Setup.Advanced.GameArgs.ToolTip"));
        Grid.SetRow(_textGame, 4);
        Grid.SetColumn(_textGame, 1);
        _textGame.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetGameArgs(_textGame.Text ?? string.Empty);
        };
        grid.Children.Add(_textGame);

        AddRowLabel(grid, "Instance.Setup.Advanced.Classpath", 6);
        _textClasspath = new MyTextBox();
        ToolTip.SetTip(_textClasspath, Lang.Text("Instance.Setup.Advanced.Classpath.ToolTip"));
        Grid.SetRow(_textClasspath, 6);
        Grid.SetColumn(_textClasspath, 1);
        _textClasspath.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetClasspath(_textClasspath.Text ?? string.Empty);
        };
        grid.Children.Add(_textClasspath);

        AddRowLabel(grid, "Setup.Launch.Advanced.PreLaunchCommand", 8);
        _textPreLaunch = new MyTextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 3d)
        };
        ToolTip.SetTip(
            _textPreLaunch,
            Lang.Text("Instance.Setup.Advanced.PreLaunchCommand.ToolTip"));
        Grid.SetRow(_textPreLaunch, 8);
        Grid.SetColumn(_textPreLaunch, 1);
        _textPreLaunch.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetPreLaunchCommand(_textPreLaunch.Text ?? string.Empty);
            RefreshRunWait();
        };
        grid.Children.Add(_textPreLaunch);

        var checks = new StackPanel
        {
            Margin = new Thickness(0d, 8d, 0d, 4d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(checks, 9);
        Grid.SetColumnSpan(checks, 2);

        _checkRunWait = NewCheck(
            "Setup.Launch.Advanced.PreLaunchCommand.Wait",
            value => ViewModel.SetPreLaunchCommandWait(value));
        checks.Children.Add(_checkRunWait);

        _checkDisableModUpdate = NewCheck(
            "Instance.Setup.Advanced.DisableModUpdate",
            value => ViewModel.SetDisableModUpdate(value));
        ToolTip.SetTip(
            _checkDisableModUpdate,
            Lang.Text("Setup.Ui.FeatureHide.Item.ModUpdate.ToolTip"));
        checks.Children.Add(_checkDisableModUpdate);

        _checkIgnoreJava = NewCheck(
            "Instance.Setup.Advanced.IgnoreJavaCompatibility",
            value => ViewModel.SetIgnoreJavaCompatibility(value));
        ToolTip.SetTip(
            _checkIgnoreJava,
            Lang.Text("Instance.Setup.Advanced.IgnoreJavaCompatibility.ToolTip"));
        checks.Children.Add(_checkIgnoreJava);

        _checkDisableAssets = NewCheck(
            "Instance.Setup.Advanced.DisableFileVerification",
            value => ViewModel.SetDisableAssetVerify(value));
        ToolTip.SetTip(
            _checkDisableAssets,
            Lang.Text("Instance.Setup.Advanced.DisableFileVerification.ToolTip"));
        _checkDisableAssets.Margin = new Thickness(0d, 0d, 60d, 0d);
        checks.Children.Add(_checkDisableAssets);

        _checkUseProxy = NewCheck(
            "Instance.Setup.Advanced.UseGlobalProxy",
            value => ViewModel.SetUseProxy(value));
        ToolTip.SetTip(_checkUseProxy, Lang.Text("Instance.Setup.Advanced.UseGlobalProxy.ToolTip"));
        checks.Children.Add(_checkUseProxy);

        _checkDisableJlw = NewCheck(
            "Setup.Launch.Advanced.DisableJlw",
            value => ViewModel.SetDisableJlw(value));
        ToolTip.SetTip(_checkDisableJlw, Lang.Text("Setup.Launch.Advanced.DisableJlw.ToolTip"));
        checks.Children.Add(_checkDisableJlw);

        _checkDisableLF = NewCheck(
            "Setup.Launch.Advanced.DisableLF",
            value => ViewModel.SetDisableLf(value));
        ToolTip.SetTip(_checkDisableLF, Lang.Text("Setup.Launch.Advanced.DisableLF.ToolTip"));
        checks.Children.Add(_checkDisableLF);

        _checkDebugLog4j = new MyCheckBox
        {
            Text = Lang.Text("Instance.Setup.Advanced.UseDebugLog4j"),
            Height = 28d
        };
        _checkDebugLog4j.Change += (_, _) =>
        {
            if (!_syncing)
                _ = OnDebugLog4jChangedAsync();
        };
        checks.Children.Add(_checkDebugLog4j);

        _checkDisableLwjglAgent = NewCheck(
            "Setup.Launch.Advanced.DisableLwjglUnsafeAgent",
            value => ViewModel.SetDisableLwjglUnsafeAgent(value));
        ToolTip.SetTip(
            _checkDisableLwjglAgent,
            Lang.Text("Setup.Launch.Advanced.DisableLwjglUnsafeAgent.ToolTip"));
        checks.Children.Add(_checkDisableLwjglAgent);

        grid.Children.Add(checks);
        card.Children.Add(grid);
        card.SwapControl = grid;
        card.IsSwapped = true;
        _panMain.Children.Add(card);
    }

    private MyCheckBox NewCheck(string textKey, Action<bool> onChanged)
    {
        var check = new MyCheckBox
        {
            Text = Lang.Text(textKey),
            Height = 28d
        };
        check.Change += (_, _) =>
        {
            if (_syncing)
                return;
            onChanged(check.Checked == true);
        };
        return check;
    }

    private static void AddRowLabel(Grid grid, string textKey, int row)
    {
        var label = new TextBlock
        {
            Text = Lang.Text(textKey),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 25d, 0d)
        };
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private static TextBlock AddFieldLabel(Grid grid, string textKey, int row)
    {
        var label = new TextBlock
        {
            Text = Lang.Text(textKey),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 8d, 25d, 0d)
        };
        Grid.SetRow(label, row);
        grid.Children.Add(label);
        return label;
    }

    private static MyTextBox AddFieldBox(
        Grid grid,
        int row,
        string hintKey,
        IReadOnlyList<IValidator<string>> rules)
    {
        var box = new MyTextBox
        {
            ShowValidateResult = false,
            HintText = Lang.Text(hintKey),
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        foreach (var rule in rules)
            box.ValidateRules.Add(rule);
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        return box;
    }

    private static MyCard NewCard(string titleKey)
    {
        return new MyCard
        {
            Title = Lang.Text(titleKey),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
    }

    #endregion

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
        if (_labRamUsed is null || ViewModel.Instance is null)
            return;
        ViewModel.RefreshRam();
        ApplyRamState();
    }

    #endregion

    #region 服务器逻辑

    private void OnLoginRequireChanged()
    {
        if (_syncing)
            return;
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
        _checkDebugLog4j.IsEnabled = false;
        try
        {
            await ViewModel.ApplyDebugLog4jAsync(_checkDebugLog4j.Checked == true);
            _syncing = true;
            _checkDebugLog4j.Checked = ViewModel.UseDebugLog4j;
            _syncing = false;
        }
        finally
        {
            _checkDebugLog4j.IsEnabled = true;
        }
    }

    private void RefreshRunWait()
    {
        ViewModel.RefreshRunWait();
        _checkRunWait.IsVisible = ViewModel.RunWaitVisible;
    }

    #endregion
}