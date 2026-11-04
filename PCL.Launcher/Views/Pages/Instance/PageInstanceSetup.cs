using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Launch;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例设置页：启动选项、内存、服务器与高级设置四张卡片，外加跳转全局设置的入口。
///     所有设置读写实例独立配置（<c>Config.Instance.*</c> 与 <c>Config.InstanceAuth.*</c>），与全局设置页共用同一份配置。
/// </summary>
public class PageInstanceSetup : MyPageRight
{
    private const string DefaultAuthServer = "https://littleskin.cn/api/yggdrasil";

    private readonly StackPanel _panMain;
    private McInstance? _instance;
    private bool _syncing;

    private MyComboBox _comboIndie = null!;
    private MyComboBox _comboTitle = null!;
    private MyCheckBox _checkTitleDefault = null!;
    private MyTextBox _textInfo = null!;
    private MyComboBox _comboJava = null!;

    private MyHint _hintRam32 = null!;
    private MyHint _hintRamHigh = null!;
    private MyRadioBox _radioRamGlobal = null!;
    private MyRadioBox _radioRamAuto = null!;
    private MyRadioBox _radioRamCustom = null!;
    private MySlider _sliderRamMax = null!;
    private MySlider _sliderRamInitial = null!;
    private ColumnDefinition _columnRamUsed = null!;
    private ColumnDefinition _columnRamGame = null!;
    private ColumnDefinition _columnRamEmpty = null!;
    private TextBlock _labRamUsed = null!;
    private TextBlock _labRamTotal = null!;
    private TextBlock _labRamGame = null!;

    private MyHint _hintLoginLock = null!;
    private MyHint _hintSsl = null!;
    private MyHint _hintHttp = null!;
    private MyComboBox _comboLoginRequire = null!;
    private TextBlock _labAuthServer = null!;
    private MyTextBox _textAuthServer = null!;
    private TextBlock _labAuthRegister = null!;
    private MyTextBox _textAuthRegister = null!;
    private TextBlock _labAuthName = null!;
    private MyTextBox _textAuthName = null!;
    private MyTextBox _textServerEnter = null!;
    private MyButton _btnLittleSkin = null!;
    private MyButton _btnAuthLock = null!;
    private MyButton _btnNewProfile = null!;

    private MyComboBox _comboRenderer = null!;
    private MyTextBox _textJvm = null!;
    private MyTextBox _textGame = null!;
    private MyTextBox _textClasspath = null!;
    private MyTextBox _textPreLaunch = null!;
    private MyCheckBox _checkRunWait = null!;
    private MyCheckBox _checkDisableModUpdate = null!;
    private MyCheckBox _checkIgnoreJava = null!;
    private MyCheckBox _checkDisableAssets = null!;
    private MyCheckBox _checkUseProxy = null!;
    private MyCheckBox _checkDisableJlw = null!;
    private MyCheckBox _checkDisableLF = null!;
    private MyCheckBox _checkDebugLog4j = null!;
    private MyCheckBox _checkDisableLwjglAgent = null!;

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
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        Reload();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        BuildUi();
        Reload();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (PanScroll is not null)
                    PanScroll.Offset = new Vector(PanScroll.Offset.X, offset);
            });
    }

    public void Reload()
    {
        _syncing = true;
        try
        {
            RefreshJavaCombo();
            if (_instance is null)
            {
                _comboTitle.Text = string.Empty;
                _textInfo.Text = string.Empty;
                _comboIndie.SelectedIndex = 1;
                return;
            }

            var path = _instance.PathInstance;
            _comboIndie.SelectedIndex = Config.Instance.IndieV2[path] ? 0 : 1;
            _comboTitle.Text = Config.Instance.Title[path];
            _checkTitleDefault.Checked = Config.Instance.UseGlobalTitle[path];
            _textInfo.Text = Config.Instance.TypeInfo[path];
            RefreshTitleHint();

            var ramType = Config.Instance.MemorySolution[path];
            SetMemorySolution(ramType);
            _sliderRamMax.Value = Math.Clamp(Config.Instance.CustomMemorySize[path], 1, 49);
            _sliderRamInitial.Value = Math.Clamp(Config.Instance.CustomInitialMemorySize[path], 0, 49);
            RefreshRam();

            _textServerEnter.Text = Config.Instance.ServerToEnter[path];
            _comboLoginRequire.SelectedIndex = Math.Clamp(
                Config.InstanceAuth.LoginRequirementSolution[path], 0, 3);
            _textAuthServer.Text = Config.InstanceAuth.AuthServerAddress[path];
            _textAuthName.Text = Config.InstanceAuth.AuthServerDisplayName[path];
            _textAuthRegister.Text = Config.InstanceAuth.AuthRegisterAddress[path];
            RefreshServerLogin(_comboLoginRequire.SelectedIndex);

            _comboRenderer.SelectedIndex = Math.Clamp(Config.Instance.Renderer[path], 0, 4);
            _textClasspath.Text = Config.Instance.ClasspathHead[path];
            _textJvm.Text = Config.Instance.JvmArgs[path];
            _textGame.Text = Config.Instance.GameArgs[path];
            _textPreLaunch.Text = Config.Instance.PreLaunchCommand[path];
            _checkRunWait.Checked = Config.Instance.PreLaunchCommandWait[path];
            _checkDisableLwjglAgent.Checked = Config.Instance.DisableLwjglUnsafeAgent[path];
            _checkDisableModUpdate.Checked = Config.Instance.DisableModUpdate[path];
            _checkDisableAssets.Checked = Config.Instance.DisableAssetVerifyV2[path];
            _checkUseProxy.Checked = Config.Instance.UseProxy[path];
            _checkIgnoreJava.Checked = Config.Instance.IgnoreJavaCompatibility[path];
            if (SystemInfo.IsArm64System)
            {
                _checkDisableJlw.Checked = true;
                _checkDisableJlw.IsEnabled = false;
                ToolTip.SetTip(_checkDisableJlw, Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64ToolTip"));
            }
            else
            {
                _checkDisableJlw.Checked = Config.Instance.DisableJlw[path];
            }

            _checkDebugLog4j.Checked = Config.Instance.UseDebugLof4j2Config[path];
            _checkDisableLF.Checked = Config.Instance.DisableLF[path];
            RefreshRunWait();
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

    public void Reset()
    {
        if (_instance is null)
            return;
        try
        {
            if (!Config.InstanceAuth.AuthLocked[_instance.PathInstance])
                Config.InstanceAuth.Reset(_instance.PathInstance);
            Config.Instance.Reset(_instance.PathInstance);
            HintService.Show(Hints.Success(Lang.Text("Instance.Setup.Initialize.Success")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }

        Reload();
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
        globalButton.Click += (_, _) => PageRouter.Navigate(PageType.Setup, PageSubType.SetupLaunch);
        _panMain.Children.Add(globalButton);
    }

    private void BuildArgumentCard()
    {
        var card = NewCard("Setup.Launch.Options.Title");
        var body = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 21d) };
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
        ToolTip.SetTip(indieEnabled, Lang.Text("Instance.Setup.Options.InstanceIsolation.Enabled.ToolTip"));
        _comboIndie.Items.Add(indieEnabled);
        var indieDisabled = new MyComboBoxItem { Content = Lang.Text("Common.Action.Close") };
        ToolTip.SetTip(indieDisabled, Lang.Text("Instance.Setup.Options.InstanceIsolation.Disabled.ToolTip"));
        _comboIndie.Items.Add(indieDisabled);
        _comboIndie.SelectionChanged += (_, _) => { if (!_syncing) _ = OnIndieChangedAsync(); };
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
        _comboTitle.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Launch.Options.WindowTitle.SampleFormat")
        });
        _comboTitle.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            RefreshTitleHint();
            if (_instance is null)
                return;
            Config.Instance.Title[_instance.PathInstance] = _comboTitle.Text ?? string.Empty;
        };
        grid.Children.Add(_comboTitle);

        _checkTitleDefault = new MyCheckBox
        {
            Text = Lang.Text("Instance.Setup.Options.WindowTitle.Default")
        };
        ToolTip.SetTip(_checkTitleDefault, Lang.Text("Instance.Setup.Options.WindowTitle.Default.ToolTip"));
        Grid.SetRow(_checkTitleDefault, 4);
        Grid.SetColumnSpan(_checkTitleDefault, 3);
        _checkTitleDefault.Change += (_, _) =>
        {
            RefreshTitleHint();
            if (_syncing || _instance is null)
                return;
            Config.Instance.UseGlobalTitle[_instance.PathInstance] = _checkTitleDefault.Checked == true;
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
            if (_syncing || _instance is null)
                return;
            Config.Instance.TypeInfo[_instance.PathInstance] = _textInfo.Text ?? string.Empty;
        };
        grid.Children.Add(_textInfo);

        AddRowLabel(grid, "Instance.Setup.Options.Java", 8);
        _comboJava = new MyComboBox { MaxDropDownHeight = 240d };
        ToolTip.SetTip(_comboJava, Lang.Text("Instance.Setup.Options.Java.ToolTip"));
        Grid.SetRow(_comboJava, 8);
        Grid.SetColumn(_comboJava, 1);
        Grid.SetColumnSpan(_comboJava, 2);
        _comboJava.DropDownOpened += (_, _) => RefreshJavaCombo();
        _comboJava.SelectionChanged += (_, _) => { if (!_syncing) SaveJavaSelection(); };
        grid.Children.Add(_comboJava);

        body.Children.Add(grid);
        card.BorderChild = body;
        _panMain.Children.Add(card);
    }

    private void BuildMemoryCard()
    {
        var card = NewCard("Setup.Launch.Memory.Title");
        var body = new StackPanel { Margin = new Thickness(25d, 40d, 25d, 15d) };

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
        _sliderRamMax = new MySlider { MaxValue = 49, Value = 13, Margin = new Thickness(0d, 0d, 12d, 9d) };
        Grid.SetRow(_sliderRamMax, 3);
        Grid.SetColumn(_sliderRamMax, 1);
        _sliderRamMax.Change += (_, user) => { if (user && !_syncing) SaveMemoryValues(); };
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
        _sliderRamInitial = new MySlider { MaxValue = 49, Value = 0, Margin = new Thickness(0d, 0d, 12d, 0d) };
        Grid.SetRow(_sliderRamInitial, 4);
        Grid.SetColumn(_sliderRamInitial, 1);
        ToolTip.SetTip(_sliderRamInitial, Lang.Text("Setup.Launch.Memory.Initial.ToolTip"));
        _sliderRamInitial.Change += (_, user) => { if (user && !_syncing) SaveMemoryValues(); };
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

        var rectUsed = new Avalonia.Controls.Shapes.Rectangle();
        Grid.SetRow(rectUsed, 1);
        ContainerTheme.BindDynamic(rectUsed, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorObject3");
        grid.Children.Add(rectUsed);
        var rectGame = new Avalonia.Controls.Shapes.Rectangle { Opacity = 0.5d };
        Grid.SetRow(rectGame, 1);
        Grid.SetColumn(rectGame, 1);
        ContainerTheme.BindDynamic(rectGame, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorBrush3");
        grid.Children.Add(rectGame);
        var rectEmpty = new Avalonia.Controls.Shapes.Rectangle { Opacity = 0.7d };
        Grid.SetRow(rectEmpty, 1);
        Grid.SetColumn(rectEmpty, 2);
        ContainerTheme.BindDynamic(rectEmpty, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorBrush6");
        grid.Children.Add(rectEmpty);

        var usedTitle = new TextBlock
        {
            Text = Lang.Text("Instance.Setup.Memory.Used"),
            Opacity = 0.7d,
            FontSize = 11d,
            Margin = new Thickness(2d, 0d, 0d, 5d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumnSpan(usedTitle, 3);
        grid.Children.Add(usedTitle);

        var gameTitle = new TextBlock
        {
            Text = Lang.Text("Setup.Launch.Memory.GameAllocation"),
            Opacity = 0.7d,
            FontSize = 11d,
            Margin = new Thickness(2d, 0d, 0d, 5d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumnSpan(gameTitle, 3);
        grid.Children.Add(gameTitle);

        var usedPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2d, 3d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(usedPanel, 2);
        Grid.SetColumnSpan(usedPanel, 3);
        _labRamUsed = new TextBlock { FontSize = 16d };
        ContainerTheme.BindDynamic(_labRamUsed, TextBlock.ForegroundProperty, "ColorBrushMemory");
        usedPanel.Children.Add(_labRamUsed);
        _labRamTotal = new TextBlock { FontSize = 16d };
        ContainerTheme.BindDynamic(_labRamTotal, TextBlock.ForegroundProperty, "ColorBrushMemory");
        usedPanel.Children.Add(_labRamTotal);
        grid.Children.Add(usedPanel);

        _labRamGame = new TextBlock
        {
            FontSize = 16d,
            Margin = new Thickness(2d, 3d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(_labRamGame, 2);
        Grid.SetColumnSpan(_labRamGame, 3);
        ContainerTheme.BindDynamic(_labRamGame, TextBlock.ForegroundProperty, "ColorBrushMemory");
        grid.Children.Add(_labRamGame);
        return grid;
    }

    private void BuildServerCard()
    {
        var card = NewCard("Instance.Setup.Server.Title");
        var body = new StackPanel { Margin = new Thickness(25d, 42d, 25d, 18d) };

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
        _comboLoginRequire.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Server.LoginMethod.Unlimited")
        });
        _comboLoginRequire.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Server.LoginMethod.MicrosoftOnly")
        });
        _comboLoginRequire.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Server.LoginMethod.AuthOnly")
        });
        _comboLoginRequire.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Server.LoginMethod.MicrosoftOrAuth")
        });
        _comboLoginRequire.SelectionChanged += (_, _) => OnLoginRequireChanged();
        grid.Children.Add(_comboLoginRequire);

        _labAuthServer = AddFieldLabel(grid, "Instance.Setup.Server.AuthServer", 1);
        _textAuthServer = AddFieldBox(grid, 1, "Instance.Setup.Server.AuthServer.Hint", [new HttpValidator()]);
        _textAuthServer.LostFocus += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            NormalizeAuthServer();
            Config.InstanceAuth.AuthServerAddress[_instance.PathInstance] = _textAuthServer.Text ?? string.Empty;
            RefreshServerLogin(_comboLoginRequire.SelectedIndex);
        };

        _labAuthRegister = AddFieldLabel(grid, "Instance.Setup.Server.Register", 2);
        _textAuthRegister = AddFieldBox(
            grid,
            2,
            "Instance.Setup.Server.Register.Hint",
            [new HttpValidator { AllowsNullOrEmpty = true }]);
        _textAuthRegister.TextChanged += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            Config.InstanceAuth.AuthRegisterAddress[_instance.PathInstance] = _textAuthRegister.Text ?? string.Empty;
        };

        _labAuthName = AddFieldLabel(grid, "Instance.Setup.Server.Name", 3);
        _textAuthName = AddFieldBox(grid, 3, "Instance.Setup.Server.Name.Hint", []);
        _textAuthName.TextChanged += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            Config.InstanceAuth.AuthServerDisplayName[_instance.PathInstance] = _textAuthName.Text ?? string.Empty;
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
            if (_syncing || _instance is null)
                return;
            var text = (_textServerEnter.Text ?? string.Empty).Replace('：', ':');
            if (text != _textServerEnter.Text)
            {
                _syncing = true;
                _textServerEnter.Text = text;
                _syncing = false;
            }

            Config.Instance.ServerToEnter[_instance.PathInstance] = text;
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
        _comboRenderer.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Setup.FollowGlobal") });
        _comboRenderer.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Launch.Advanced.Renderer.GameDefault")
        });
        _comboRenderer.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Launch.Advanced.Renderer.Software")
        });
        _comboRenderer.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Launch.Advanced.Renderer.DirectX12")
        });
        _comboRenderer.Items.Add(new MyComboBoxItem
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
            if (_syncing || _instance is null)
                return;
            Config.Instance.JvmArgs[_instance.PathInstance] = _textJvm.Text ?? string.Empty;
        };
        grid.Children.Add(_textJvm);

        AddRowLabel(grid, "Setup.Launch.Advanced.GameTail", 4);
        _textGame = new MyTextBox { HintText = Lang.Text("Instance.Setup.FollowGlobal") };
        ToolTip.SetTip(_textGame, Lang.Text("Instance.Setup.Advanced.GameArgs.ToolTip"));
        Grid.SetRow(_textGame, 4);
        Grid.SetColumn(_textGame, 1);
        _textGame.TextChanged += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            Config.Instance.GameArgs[_instance.PathInstance] = _textGame.Text ?? string.Empty;
        };
        grid.Children.Add(_textGame);

        AddRowLabel(grid, "Instance.Setup.Advanced.Classpath", 6);
        _textClasspath = new MyTextBox();
        ToolTip.SetTip(_textClasspath, Lang.Text("Instance.Setup.Advanced.Classpath.ToolTip"));
        Grid.SetRow(_textClasspath, 6);
        Grid.SetColumn(_textClasspath, 1);
        _textClasspath.TextChanged += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            Config.Instance.ClasspathHead[_instance.PathInstance] = _textClasspath.Text ?? string.Empty;
        };
        grid.Children.Add(_textClasspath);

        AddRowLabel(grid, "Setup.Launch.Advanced.PreLaunchCommand", 8);
        _textPreLaunch = new MyTextBox { Margin = new Thickness(0d, 0d, 0d, 3d) };
        ToolTip.SetTip(_textPreLaunch, Lang.Text("Instance.Setup.Advanced.PreLaunchCommand.ToolTip"));
        Grid.SetRow(_textPreLaunch, 8);
        Grid.SetColumn(_textPreLaunch, 1);
        _textPreLaunch.TextChanged += (_, _) =>
        {
            if (_syncing || _instance is null)
                return;
            Config.Instance.PreLaunchCommand[_instance.PathInstance] = _textPreLaunch.Text ?? string.Empty;
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
            value => SetInstanceConfig(instance => Config.Instance.PreLaunchCommandWait[instance.PathInstance] = value));
        checks.Children.Add(_checkRunWait);

        _checkDisableModUpdate = NewCheck(
            "Instance.Setup.Advanced.DisableModUpdate",
            value => SetInstanceConfig(instance => Config.Instance.DisableModUpdate[instance.PathInstance] = value));
        ToolTip.SetTip(_checkDisableModUpdate, Lang.Text("Setup.Ui.FeatureHide.Item.ModUpdate.ToolTip"));
        checks.Children.Add(_checkDisableModUpdate);

        _checkIgnoreJava = NewCheck(
            "Instance.Setup.Advanced.IgnoreJavaCompatibility",
            value => SetInstanceConfig(instance => Config.Instance.IgnoreJavaCompatibility[instance.PathInstance] = value));
        ToolTip.SetTip(_checkIgnoreJava, Lang.Text("Instance.Setup.Advanced.IgnoreJavaCompatibility.ToolTip"));
        checks.Children.Add(_checkIgnoreJava);

        _checkDisableAssets = NewCheck(
            "Instance.Setup.Advanced.DisableFileVerification",
            value => SetInstanceConfig(instance => Config.Instance.DisableAssetVerifyV2[instance.PathInstance] = value));
        ToolTip.SetTip(_checkDisableAssets, Lang.Text("Instance.Setup.Advanced.DisableFileVerification.ToolTip"));
        _checkDisableAssets.Margin = new Thickness(0d, 0d, 60d, 0d);
        checks.Children.Add(_checkDisableAssets);

        _checkUseProxy = NewCheck(
            "Instance.Setup.Advanced.UseGlobalProxy",
            value => SetInstanceConfig(instance => Config.Instance.UseProxy[instance.PathInstance] = value));
        ToolTip.SetTip(_checkUseProxy, Lang.Text("Instance.Setup.Advanced.UseGlobalProxy.ToolTip"));
        checks.Children.Add(_checkUseProxy);

        _checkDisableJlw = NewCheck(
            "Setup.Launch.Advanced.DisableJlw",
            value => SetInstanceConfig(instance => Config.Instance.DisableJlw[instance.PathInstance] = value));
        ToolTip.SetTip(_checkDisableJlw, Lang.Text("Setup.Launch.Advanced.DisableJlw.ToolTip"));
        checks.Children.Add(_checkDisableJlw);

        _checkDisableLF = NewCheck(
            "Setup.Launch.Advanced.DisableLF",
            value => SetInstanceConfig(instance => Config.Instance.DisableLF[instance.PathInstance] = value));
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
            value => SetInstanceConfig(
                instance => Config.Instance.DisableLwjglUnsafeAgent[instance.PathInstance] = value));
        ToolTip.SetTip(_checkDisableLwjglAgent, Lang.Text("Setup.Launch.Advanced.DisableLwjglUnsafeAgent.ToolTip"));
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
        IReadOnlyList<FluentValidation.IValidator<string>> rules)
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
        if (_instance is null)
            return;
        var desired = _comboIndie.SelectedIndex == 0;
        _comboIndie.IsEnabled = false;
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Instance.Setup.Options.InstanceIsolation.Message"),
                    Buttons = DialogButtons.Create(
                        "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
            {
                _syncing = true;
                _comboIndie.SelectedIndex = desired ? 1 : 0;
                return;
            }

            Config.Instance.IndieV2[_instance.PathInstance] = desired;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = true;
            _comboIndie.IsEnabled = true;
            _syncing = false;
        }
    }

    private void RefreshTitleHint()
    {
        var useDefault = _checkTitleDefault.Checked == true;
        _comboTitle.HintText = useDefault
            ? Lang.Text("Common.Option.Default")
            : Lang.Text("Instance.Setup.FollowGlobal");
        _checkTitleDefault.IsVisible = string.IsNullOrEmpty(_comboTitle.Text);
    }

    private void RefreshJavaCombo()
    {
        if (_comboJava is null)
            return;
        var path = _instance?.PathInstance;
        var preference = _instance is null
            ? new UseGlobalPreference()
            : JavaRuntime.GetInstanceJavaPreference(_instance);
        var items = new List<MyComboBoxItem>();

        _syncing = true;
        try
        {
            _comboJava.Items.Clear();
            var globalItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.FollowGlobal"),
                Tag = new UseGlobalPreference()
            };
            items.Add(globalItem);
            var autoItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.AutoSelect"),
                Tag = new AutoSelect()
            };
            items.Add(autoItem);
            var relativeItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.SelectRelative"),
                Tag = new UseRelativePath(Path.Combine("jre", "bin", "java.exe"))
            };
            var relativeTag = (UseRelativePath)relativeItem.Tag;
            var absolutePath = Path.GetFullPath(Path.Combine(Basics.ExecutableDirectory, relativeTag.RelativePath));
            ToolTip.SetTip(
                relativeItem,
                Lang.Text("Instance.Setup.Options.Java.RelativePathToolTip", relativeTag.RelativePath, absolutePath));
            items.Add(relativeItem);

            try
            {
                foreach (var java in JavaRuntime.Javas.GetSortedJavaList())
                {
                    var item = new MyComboBoxItem
                    {
                        Content = java.ToString(),
                        Tag = java
                    };
                    ToolTip.SetTip(
                        item,
                        Lang.Text(
                            "Instance.Setup.Options.Java.Details.ToolTip",
                            java.Installation.JavaExePath,
                            java.Installation.Version,
                            java.Source));
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    "更新实例设置 Java 下拉框失败",
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
            }

            foreach (var item in items)
                _comboJava.Items.Add(item);

            MyComboBoxItem? selectedItem = null;
            switch (preference)
            {
                case UseGlobalPreference:
                    selectedItem = globalItem;
                    break;
                case UseRelativePath:
                    selectedItem = relativeItem;
                    break;
                case ExistingJava existing:
                    foreach (var raw in items)
                    {
                        if (raw.Tag is not JavaEntry entry)
                            continue;
                        if (!string.Equals(
                                entry.Installation.JavaExePath,
                                existing.JavaExePath,
                                StringComparison.OrdinalIgnoreCase))
                            continue;
                        selectedItem = raw;
                        break;
                    }

                    break;
            }

            if (selectedItem is null && items.Count > 1)
                selectedItem = autoItem;
            if (selectedItem is not null)
                _comboJava.SelectedItem = selectedItem;

            if (!JavaRuntime.Javas.ExistAnyJava())
            {
                _comboJava.Items.Clear();
                var noJava = new MyComboBoxItem
                {
                    Content = Lang.Text("Instance.Setup.Options.Java.NoRuntime"),
                    IsEnabled = false
                };
                ToolTip.SetTip(noJava, Lang.Text("Instance.Setup.Options.Java.NoRuntime.ToolTip"));
                _comboJava.Items.Add(noJava);
                _comboJava.SelectedItem = noJava;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SaveJavaSelection()
    {
        if (_instance is null)
            return;
        if (_comboJava.SelectedItem is not MyComboBoxItem selected || selected.Tag is null)
            return;

        JavaPreference preference;
        switch (selected.Tag)
        {
            case UseGlobalPreference:
                preference = new UseGlobalPreference();
                break;
            case AutoSelect:
                preference = new AutoSelect();
                break;
            case UseRelativePath:
            {
                var file = SystemDialogs.SelectFile(
                    Lang.Text("Setup.Java.SelectFile.Filter"),
                    Lang.Text("Setup.Java.SelectFile.Title"),
                    Basics.ExecutableDirectory);
                if (string.IsNullOrWhiteSpace(file))
                {
                    RefreshJavaCombo();
                    return;
                }

                var absolute = Path.GetFullPath(file);
                if (!absolute.StartsWith(Basics.ExecutableDirectory, StringComparison.Ordinal))
                {
                    HintService.Show(Hints.Error(Lang.Text("Instance.Setup.Options.Java.PathOutOfRange")));
                    RefreshJavaCombo();
                    return;
                }

                preference = new UseRelativePath(Path.GetRelativePath(Basics.ExecutableDirectory, absolute));
                break;
            }
            case JavaEntry java:
                preference = new ExistingJava(java.Installation.JavaExePath);
                break;
            default:
                return;
        }

        Config.Instance.SelectedJava[_instance.PathInstance] =
            JsonSerializer.Serialize(preference, JsonCompat.SerializerOptions);
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
        if (_syncing || _instance is null)
            return;
        Config.Instance.MemorySolution[_instance.PathInstance] = solution;
        SetMemorySolution(solution);
        RefreshRam();
    }

    private void SaveMemoryValues()
    {
        if (_instance is null)
            return;
        var path = _instance.PathInstance;
        Config.Instance.CustomMemorySize[path] = Math.Clamp(_sliderRamMax.Value, 1, 49);
        Config.Instance.CustomInitialMemorySize[path] = Math.Clamp(_sliderRamInitial.Value, 0, 49);
        RefreshRam();
    }

    private void RefreshRam()
    {
        if (_instance is null || _labRamUsed is null)
            return;

        var instance = _instance;
        var ramGame = Math.Round(MemorySettings.GetRam(instance), 5);
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        var total = Math.Round(memory.Total / bytesPerGigabyte, 1);
        var available = Math.Round(memory.Available / bytesPerGigabyte, 1);
        var gameActual = Math.Round(Math.Min(ramGame, available), 5);
        var used = Math.Round(total - available, 5);
        var empty = Math.Round(Math.Clamp(total - used - ramGame, 0d, 1000d), 1);

        var maxValue = total switch
        {
            <= 1.5d => (int)Math.Round(Math.Max(Math.Floor((total - 0.3d) / 0.1d), 1d)),
            <= 8d => (int)Math.Round(Math.Floor((total - 1.5d) / 0.5d) + 12d),
            <= 16d => (int)Math.Round(Math.Floor((total - 8d) / 1d) + 25d),
            _ => (int)Math.Round(Math.Floor((total - 16d) / 2d) + 33d)
        };
        _sliderRamMax.MaxValue = Math.Max(1, maxValue);
        _sliderRamInitial.MaxValue = Math.Max(1, maxValue);

        _columnRamUsed.Width = new GridLength(Math.Max(0.01d, used), GridUnitType.Star);
        _columnRamGame.Width = new GridLength(Math.Max(0.01d, gameActual), GridUnitType.Star);
        _columnRamEmpty.Width = new GridLength(Math.Max(0.01d, empty), GridUnitType.Star);

        var ramInitial = MemorySettings.GetInitialRam(instance);
        string suffixText;
        if (ramInitial.HasValue && ramGame != gameActual)
            suffixText = Lang.Text(
                "Setup.Launch.Memory.SuffixBoth",
                Lang.Number(ramInitial.Value, "N1"),
                Lang.Number(gameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixInitial", Lang.Number(ramInitial.Value, "N1"));
        else if (ramGame != gameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixAvailable", Lang.Number(gameActual, "N1"));
        else
            suffixText = string.Empty;

        _labRamUsed.Text = Lang.Number(used, "N1") + " GiB";
        _labRamTotal.Text = " / " + Lang.Number(total, "N1") + " GiB";
        _labRamGame.Text = Lang.Number(ramGame, "N1") + " GiB"
                           + (suffixText.Length > 0 ? " (" + suffixText + ")" : string.Empty);

        _hintRam32.IsVisible = ramGame == 1d
                               && !JavaRuntime.IsGameSet64BitJava(instance)
                               && !SystemInfo.Is32BitSystem
                               && JavaRuntime.Javas.ExistAnyJava();
        _hintRamHigh.IsVisible = total > 0d && ramGame / total > 0.75d;
    }

    #endregion

    #region 服务器逻辑

    private void OnLoginRequireChanged()
    {
        if (_syncing || _instance is null)
            return;
        var index = _comboLoginRequire.SelectedIndex;
        if (index < 0)
            return;
        if ((index == 2 || index == 3) && !_textAuthServer.IsValidated)
            return;
        Config.InstanceAuth.LoginRequirementSolution[_instance.PathInstance] = index;
        RefreshServerLogin(index);
    }

    private void RefreshServerLogin(int type)
    {
        var authVisible = type == 2 || type == 3;
        var authEditable = _instance is null || !Config.InstanceAuth.AuthLocked[_instance.PathInstance];
        _labAuthServer.IsVisible = authVisible;
        _textAuthServer.IsVisible = authVisible;
        _labAuthRegister.IsVisible = authVisible;
        _textAuthRegister.IsVisible = authVisible;
        _labAuthName.IsVisible = authVisible;
        _textAuthName.IsVisible = authVisible;
        _btnLittleSkin.IsVisible = authVisible;
        _textAuthServer.IsEnabled = authEditable;
        _textAuthRegister.IsEnabled = authEditable;
        _textAuthName.IsEnabled = authEditable;
        _btnLittleSkin.IsEnabled = authEditable;
        _hintLoginLock.IsVisible = !authEditable;
        _btnAuthLock.IsVisible = type != 0 && type != 1;
        _btnNewProfile.IsVisible = authVisible;

        var address = _textAuthServer.Text ?? string.Empty;
        if (!authVisible)
        {
            _hintSsl.IsVisible = false;
            _hintHttp.IsVisible = false;
        }
        else if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _hintSsl.IsVisible = true;
            _hintHttp.IsVisible = false;
        }
        else if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            _hintSsl.IsVisible = false;
            _hintHttp.IsVisible = true;
        }
        else
        {
            _hintSsl.IsVisible = false;
            _hintHttp.IsVisible = false;
        }
    }

    private void NormalizeAuthServer()
    {
        var address = (_textAuthServer.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(address))
            return;
        if (!address.EndsWith("/api/yggdrasil/", StringComparison.OrdinalIgnoreCase)
            && !address.EndsWith("/api/yggdrasil", StringComparison.OrdinalIgnoreCase))
        {
            address = address.EndsWith('/')
                ? address + "api/yggdrasil"
                : address + "/api/yggdrasil";
            HintService.Show(Hints.Info(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted")));
        }

        if (address.EndsWith("/api/yggdrasil/", StringComparison.OrdinalIgnoreCase))
        {
            address = address[..^1];
            HintService.Show(Hints.Info(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted")));
        }

        _syncing = true;
        _textAuthServer.Text = address;
        _syncing = false;
    }

    private async Task ApplyLittleSkinAsync()
    {
        try
        {
            var current = _textAuthServer.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(current) && current != DefaultAuthServer)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Title"),
                        Content = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Message"),
                        Buttons = DialogButtons.Create(
                            "Instance.Setup.Server.LittleSkin.Override.Continue",
                            "Common.Action.Cancel")
                    });
                if (!dialog.IsConfirmed)
                    return;
            }

            _syncing = true;
            _textAuthServer.Text = DefaultAuthServer;
            _textAuthRegister.Text = "https://littleskin.cn/auth/register";
            _textAuthName.Text = Lang.Text("Instance.Setup.Server.LittleSkin.Name");
            _syncing = false;

            if (_instance is null)
                return;
            var path = _instance.PathInstance;
            Config.InstanceAuth.AuthServerAddress[path] = _textAuthServer.Text ?? string.Empty;
            Config.InstanceAuth.AuthRegisterAddress[path] = _textAuthRegister.Text ?? string.Empty;
            Config.InstanceAuth.AuthServerDisplayName[path] = _textAuthName.Text ?? string.Empty;
            RefreshServerLogin(_comboLoginRequire.SelectedIndex);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
    }

    private async Task LockAuthAsync()
    {
        _btnAuthLock.IsEnabled = false;
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Setup.Server.LockLoginMethod.Title"),
                    Content = Lang.Text("Instance.Setup.Server.LockLoginMethod.Message"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed || _instance is null)
                return;
            Config.InstanceAuth.AuthLocked[_instance.PathInstance] = true;
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _btnAuthLock.IsEnabled = true;
        }
    }

    private void CreateServerProfile()
    {
        PageLoginAuth.PendingAuthServer = _textAuthServer.Text ?? string.Empty;
        PageRouter.Navigate(PageType.Launch);
        LauncherCommon.RunInNewThread(
            () =>
            {
                Thread.Sleep(150);
                LauncherCommon.RunInUi(
                    () => PageLaunchLeft.Current?.RefreshPage(true, GameLaunch.McLoginType.Auth));
            },
            "Instance Auth Profile",
            ThreadPriority.BelowNormal);
    }

    #endregion

    #region 高级设置逻辑

    private async Task OnRendererChangedAsync()
    {
        if (_syncing || _instance is null)
            return;
        var index = _comboRenderer.SelectedIndex;
        if (index < 0)
            return;
        var previous = Math.Clamp(Config.Instance.Renderer[_instance.PathInstance], 0, 4);
        _comboRenderer.IsEnabled = false;
        try
        {
            if (!States.Hint.Renderer && index != 0)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Warning"),
                        Content = Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                        Buttons = DialogButtons.Create(
                            "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                            "Common.Action.Cancel"),
                        IsWarn = true
                    });
                if (!dialog.IsConfirmed)
                {
                    _syncing = true;
                    _comboRenderer.SelectedIndex = previous;
                    return;
                }

                States.Hint.Renderer = true;
            }

            Config.Instance.Renderer[_instance.PathInstance] = index;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = true;
            _comboRenderer.IsEnabled = true;
            _syncing = false;
        }
    }

    private async Task OnDebugLog4jChangedAsync()
    {
        if (_syncing || _instance is null)
            return;
        _checkDebugLog4j.IsEnabled = false;
        try
        {
            var enabled = _checkDebugLog4j.Checked == true;
            if (enabled && !States.Hint.DebugLog4j2Config)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Warning"),
                        Content = Lang.Text("Instance.Setup.Advanced.UseDebugLog4j.Message"),
                        Buttons = DialogButtons.Create(
                            "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                            "Common.Action.Cancel"),
                        IsWarn = true
                    });
                if (!dialog.IsConfirmed)
                {
                    _syncing = true;
                    _checkDebugLog4j.Checked = false;
                    return;
                }

                States.Hint.DebugLog4j2Config = true;
            }

            Config.Instance.UseDebugLof4j2Config[_instance.PathInstance] = enabled;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = true;
            _checkDebugLog4j.IsEnabled = true;
            _syncing = false;
        }
    }

    private void RefreshRunWait()
    {
        _checkRunWait.IsVisible = !string.IsNullOrEmpty(_textPreLaunch.Text);
    }

    private void SetInstanceConfig(Action<McInstance> setter)
    {
        if (_syncing || _instance is null)
            return;
        setter(_instance);
    }

    #endregion
}
