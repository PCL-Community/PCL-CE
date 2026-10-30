using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public partial class PageInstanceSetup : IPageView
{
    private readonly PageInstanceSetupViewModel _vm = new();

    private new bool isLoaded;
    private bool _refreshed;

    public PageInstanceSetup()
    {     
        Loaded += PageSetupSystem_Loaded;
        InitializeComponent();
        DataContext = _vm;
        Unloaded += (_, _) => _refreshed = false;

        ComboArgumentIndieV2.SelectionChanged += ComboArgumentIndieV2_SelectionChanged;
        TextArgumentTitle.TextChanged += TextArgumentTitle_TextChanged;
        TextArgumentInfo.TextChanged += TextBoxChange;
        ComboArgumentJava.SelectionChanged += JavaSelectionUpdate;

        RadioRamType2.Check += RadioBoxChange;
        RadioRamType0.Check += RadioBoxChange;
        RadioRamType1.Check += RadioBoxChange;
        SliderRamCustom.Change += SliderChange;
        SliderRamInitialCustom.Change += SliderChange;

        ComboServerLoginRequire.SelectionChanged += ComboServerLogin_Changed;
        TextServerAuthServer.TextChanged += TextBoxChange;
        TextServerAuthServer.LostFocus += TextServerAuthServer_MouseLeave;
        TextServerAuthRegister.TextChanged += TextBoxChange;
        TextServerAuthName.TextChanged += TextBoxChange;
        TextServerEnter.TextChanged += TextBoxChange;
        BtnServerAuthLittle.Click += BtnServerAuthLittle_Click;
        BtnServerAuthLock.Click += BtnServerAuthLock_Click;
        BtnServerNewProfile.Click += BtnServerNewProfile_Click;

        ComboAdvanceRenderer.SelectionChanged += ComboAdvanceRenderer_SelectionChanged;
        TextAdvanceJvm.TextChanged += TextBoxChange;
        TextAdvanceGame.TextChanged += TextBoxChange;
        TextAdvanceClasspathHead.TextChanged += TextBoxChange;
        TextAdvanceRun.TextChanged += TextAdvanceRun_TextChanged;
        CheckAdvanceRunWait.Change += CheckBoxChange;
        CheckAdvanceDisableModUpdate.Change += CheckBoxChange;
        CheckAdvanceJava.Change += CheckBoxChange;
        CheckAdvanceAssetsV2.Change += CheckBoxChange;
        CheckAdvanceUseProxyV2.Change += CheckBoxChange;
        CheckAdvanceDisableJLW.Change += CheckBoxChange;
        CheckAdvanceDisableLF.Change += CheckBoxChange;
        CheckUseDebugLog4j2Config.Change += CheckUseDebugLog4j2Config_CheckChanged;
        CheckAdvanceDisableLwjglUnsafeAgent.Change += CheckBoxChange;

        BtnSwitch.Click += BtnSwitch_Click;
        
        TextServerEnter.TextChanged += TextServerEnter_Change;
        ComboArgumentJava.DropDownOpened += ComboArgumentJava_DropDownOpened;
        CheckArgumentTitleEmpty.Change += CheckArgumentTitleEmpty_Change;
    }

    public PageInstanceSetupViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;

        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam();

        // 由于各个实例不同，每次都需要重新加载
        ModBase.BeginUiUpdate();
        Reload();
        ModBase.EndUiUpdate();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        // 内存自动刷新
        var timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 1) };
        timer.Tick += (_, _) => RefreshRam();
        timer.Start();
        RectRamGame.SizeChanged += (s, e) => RefreshRamText();
    }

    public void Reload()
    {
        try
        {
            // 启动参数
            TextArgumentTitle.Text = Config.Instance.Title[PageInstanceLeft.McInstance.PathInstance];
            CheckArgumentTitleEmpty.Checked = Config.Instance.UseGlobalTitle[PageInstanceLeft.McInstance.PathInstance];
            TextArgumentInfo.Text = Config.Instance.TypeInfo[PageInstanceLeft.McInstance.PathInstance];
            var _unused = PageInstanceLeft.McInstance.PathIndie; // 触发自动判定
            ComboArgumentIndieV2.SelectedIndex = Config.Instance.IndieV2[PageInstanceLeft.McInstance.PathInstance] ? 0 : 1;
            _vm.RefreshTitleEmptyHint(TextArgumentTitle.Text);
            _vm.RefreshTitleHint(CheckArgumentTitleEmpty.Checked == true);
            RefreshJavaComboBox();

            // 游戏内存
            var ramType = Config.Instance.MemorySolution[PageInstanceLeft.McInstance.PathInstance];
            ((MyRadioBox)FindName("RadioRamType" + ramType)).Checked = true;
            SliderRamCustom.Value = Config.Instance.CustomMemorySize[PageInstanceLeft.McInstance.PathInstance];
            SliderRamInitialCustom.Value = Config.Instance.CustomInitialMemorySize[PageInstanceLeft.McInstance.PathInstance];
            RamType(ramType);

            // 服务器
            TextServerEnter.Text = Config.Instance.ServerToEnter[PageInstanceLeft.McInstance.PathInstance];
            ComboServerLoginRequire.SelectedIndex = Config.InstanceAuth.LoginRequirementSolution[PageInstanceLeft.McInstance.PathInstance];
            comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
            TextServerAuthServer.Text = Config.InstanceAuth.AuthServerAddress[PageInstanceLeft.McInstance.PathInstance];
            TextServerAuthName.Text = Config.InstanceAuth.AuthServerDisplayName[PageInstanceLeft.McInstance.PathInstance];
            TextServerAuthRegister.Text = Config.InstanceAuth.AuthRegisterAddress[PageInstanceLeft.McInstance.PathInstance];
            ServerLogin(ComboServerLoginRequire.SelectedIndex);

            // 高级设置
            ComboAdvanceRenderer.SelectedIndex = Config.Instance.Renderer[PageInstanceLeft.McInstance.PathInstance];
            TextAdvanceClasspathHead.Text = Config.Instance.ClasspathHead[PageInstanceLeft.McInstance.PathInstance];
            TextAdvanceJvm.Text = Config.Instance.JvmArgs[PageInstanceLeft.McInstance.PathInstance];
            TextAdvanceGame.Text = Config.Instance.GameArgs[PageInstanceLeft.McInstance.PathInstance];
            TextAdvanceRun.Text = Config.Instance.PreLaunchCommand[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceRunWait.Checked = Config.Instance.PreLaunchCommandWait[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = Config.Instance.DisableLwjglUnsafeAgent[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceDisableModUpdate.Checked = Config.Instance.DisableModUpdate[PageInstanceLeft.McInstance.PathInstance];
            if (Config.Instance.AssetVerifySolutionV1[PageInstanceLeft.McInstance.PathInstance] == 2)
            {
                ModBase.Log("[Setup] 已迁移老版本的关闭文件校验设置");
                Config.Instance.AssetVerifySolutionV1Config.Reset(PageInstanceLeft.McInstance.PathInstance);
                Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.McInstance.PathInstance] = true;
            }

            CheckAdvanceAssetsV2.Checked = Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceUseProxyV2.Checked = Config.Instance.UseProxy[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceJava.Checked = Config.Instance.IgnoreJavaCompatibility[PageInstanceLeft.McInstance.PathInstance];
            if (SystemInfo.IsArm64System)
            {
                CheckAdvanceDisableJLW.Checked = true;
                _vm.DisableJlwEnabled = false;
                CheckAdvanceDisableJLW.ToolTip = Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64ToolTip");
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = Config.Instance.DisableJlw[PageInstanceLeft.McInstance.PathInstance];
            }
            CheckUseDebugLog4j2Config.Checked = Config.Instance.UseDebugLof4j2Config[PageInstanceLeft.McInstance.PathInstance];
            CheckAdvanceDisableLF.Checked = Config.Instance.DisableLF[PageInstanceLeft.McInstance.PathInstance];
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "重载实例独立设置时出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            _vm.ResetSettings();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "初始化实例独立设置失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private static void SetByTag(string tag, object value)
        => ConfigService.TrySetValue(tag, value, PageInstanceLeft.McInstance.PathInstance);

    private void RadioBoxChange(object o, ModBase.RouteEventArgs routeEventArgs)
    {
        if (ModBase.IsUiUpdating)
            return;
        if (o is not MyRadioBox { Tag: string tag }) return;

        var slash = tag.IndexOf('/');
        if (slash < 0) return;

        SetByTag(tag[..slash], int.Parse(tag[(slash + 1)..]));
    }

    private void TextBoxChange(object o, TextChangedEventArgs textChangedEventArgs)
    {
        if (ModBase.IsUiUpdating)
            return;
        if (o is not MyTextBox textBox) return;

        SetByTag(textBox.Tag?.ToString(), textBox.Text);
    }

    private void SliderChange(object o, bool user)
    {
        if (ModBase.IsUiUpdating)
            return;
        if (o is not MySlider slider) return;

        SetByTag(slider.Tag?.ToString(), slider.Value);
    }

    private void ComboChange(MyComboBox sender, object e)
    {
        if (ModBase.IsUiUpdating || sender.SelectedIndex < 0)
            return;

        SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object sender, bool user)
    {
        if (ModBase.IsUiUpdating)
            return;
        if (sender is not MyCheckBox checkBox) return;

        SetByTag(checkBox.Tag?.ToString(), checkBox.Checked.GetValueOrDefault());
    }

    // 切换到全局设置
    private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Setup);
    }

    #region 游戏内存
    public void RamType(int type)
    {
        if (SliderRamCustom is null)
            return;
        _vm.RamType(type);
    }

    /// <summary>
    /// 刷新 UI 上的 RAM 显示。
    /// </summary>
    public void RefreshRam()
    {
        if (LabRamGame is null || LabRamUsed is null ||
            ModMain.frmMain.pageCurrent != FormMain.PageType.InstanceSetup ||
            ModMain.frmInstanceLeft.pageID != FormMain.PageSubType.VersionSetup)
            return;
        _vm.RefreshRam(PageInstanceLeft.McInstance);
        if (Math.Abs(SliderRamCustom.MaxValue - _vm.RamMaxValue) > 0.001d)
        {
            SliderRamCustom.MaxValue = _vm.RamMaxValue;
            _vm.RefreshRam(PageInstanceLeft.McInstance);
        }
        var ramUsed = _vm.RamUsed;
        var ramGameActual = _vm.RamGameActual;
        var ramEmpty = _vm.RamEmpty;
        ColumnRamUsed.Width = new GridLength(ramUsed, GridUnitType.Star);
        ColumnRamGame.Width = new GridLength(ramGameActual, GridUnitType.Star);
        ColumnRamEmpty.Width = new GridLength(ramEmpty, GridUnitType.Star);
    }

    private int ramTextLeft = 2;

    /// <summary>
    /// 刷新 UI 上的文本位置。
    /// </summary>
    private void RefreshRamText()
    {
        // 获取宽度信息
        var rectUsedWidth = RectRamUsed.ActualWidth;
        var totalWidth = PanRamDisplay.ActualWidth;
        var labGameWidth = LabRamGame.ActualWidth;
        var labUsedWidth = LabRamUsed.ActualWidth;
        var labTotalWidth = LabRamTotal.ActualWidth;
        var labGameTitleWidth = LabRamGameTitle.ActualWidth;
        var labUsedTitleWidth = LabRamUsedTitle.ActualWidth;
        // 左侧
        int left;
        if (rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth)
            // 全写不下了
            left = 0;
        else if (rectUsedWidth - 25d < labUsedWidth + labTotalWidth)
            // 显示不下完整数据
            left = 1;
        else
            // 正常
            left = 2;
        if (ramTextLeft != left)
        {
            ramTextLeft = left;
            switch (left)
            {
                case 0:
                {
                    LabRamUsed.Opacity = 0d;
                    LabRamTotal.Opacity = 0d;
                    LabRamUsedTitle.Opacity = 0d;
                    break;
                }
                case 1:
                {
                    LabRamUsed.Opacity = 1d;
                    LabRamTotal.Opacity = 0d;
                    LabRamUsedTitle.Opacity = 0.7d;
                    break;
                }
                case 2:
                {
                    LabRamUsed.Opacity = 1d;
                    LabRamTotal.Opacity = 1d;
                    LabRamUsedTitle.Opacity = 0.7d;
                    break;
                }
            }
        }

        // 右侧
        int right;
        if (totalWidth < labGameWidth + 2d + rectUsedWidth || totalWidth < labGameTitleWidth + 2d + rectUsedWidth)
            // 挤到最右边
            right = 0;
        else
            // 正常情况
            right = 1;
        if (right == 0)
        {
            LabRamGame.Margin = new Thickness(totalWidth - labGameWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(totalWidth - labGameTitleWidth, 0d, 0d, 5d);
        }
        else
        {
            LabRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

    }

    /// <summary>
    /// 获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(McInstance version, bool? is32BitJava = default)
    {
        return PageInstanceSetupViewModel.GetRam(version, is32BitJava);
    }

    /// <summary>
    /// 获取当前设置的初始堆大小（-Xms）。单位为 GB；未启用自定义初始大小时返回 null。
    /// </summary>
    public static double? GetInitialRam(McInstance version, bool? is32BitJava = default)
    {
        return PageInstanceSetupViewModel.GetInitialRam(version, is32BitJava);
    }

    #endregion

    #region 服务器

    // 全局
    private int comboServerLoginLast;

    private void ComboServerLogin_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ModBase.IsUiUpdating || ComboServerLoginRequire.SelectedIndex < 0)
            return;
        ServerLogin(ComboServerLoginRequire.SelectedIndex);
        if (TextServerAuthServer.IsValidated)
            BtnServerAuthLock.IsEnabled = true;
        else
            BtnServerAuthLock.IsEnabled = false;
        if ((ComboServerLoginRequire.SelectedIndex == 2 || ComboServerLoginRequire.SelectedIndex == 3) &&
            !TextServerAuthServer.IsValidated)
            return;
        if (comboServerLoginLast == ComboServerLoginRequire.SelectedIndex)
            return;
        comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        Config.InstanceAuth.LoginRequirementSolution[PageInstanceLeft.McInstance.PathInstance] = ComboServerLoginRequire.SelectedIndex;
    }

    private void TextServerAuthServer_MouseLeave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextServerAuthServer.Text))
            return;
        if (!(TextServerAuthServer.Text.EndsWithF("/api/yggdrasil/") ||
              TextServerAuthServer.Text.EndsWithF("/api/yggdrasil")))
        {
            if (TextServerAuthServer.Text.EndsWithF("/"))
            {
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}api/yggdrasil";
                HintService.Hint(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted"));
            }
            else
            {
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}/api/yggdrasil";
                HintService.Hint(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted"));
            }
        }

        if (TextServerAuthServer.Text.EndsWithF("/api/yggdrasil/"))
        {
            TextServerAuthServer.Text = TextServerAuthServer.Text.BeforeLast("/");
            HintService.Hint(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted"));
        }

        comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        ComboChange(ComboServerLoginRequire, null);
    }

    public void ServerLogin(int type)
    {
        _vm.RefreshServerLogin(type, TextServerAuthServer.Text);
        CardServer.TriggerForceResize();
    }

    // LittleSkin
    private async void BtnServerAuthLittle_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextServerAuthServer.Text) &&
                TextServerAuthServer.Text != "https://littleskin.cn/api/yggdrasil" && await ModMain.MyMsgBoxAsync(
                    Lang.Text("Instance.Setup.Server.LittleSkin.Override.Message"),
                    Lang.Text("Instance.Setup.Server.LittleSkin.Override.Title"), Lang.Text("Instance.Setup.Server.LittleSkin.Override.Continue"), Lang.Text("Common.Action.Cancel")) == 2)
                return;
            TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil";
            TextServerAuthRegister.Text = "https://littleskin.cn/auth/register";
            TextServerAuthName.Text = Lang.Text("Instance.Setup.Server.LittleSkin.Name");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "应用 LittleSkin 登录设置失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
    }

    // 锁定设置
    private async void BtnServerAuthLock_Click(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (await ModMain.MyMsgBoxAsync(
                    Lang.Text("Instance.Setup.Server.LockLoginMethod.Message"),
                    Lang.Text("Instance.Setup.Server.LockLoginMethod.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
            {
                Config.InstanceAuth.AuthLocked[PageInstanceLeft.McInstance.PathInstance] = true;
                Reload();
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "锁定登录方式失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    // 跳转新建档案
    private void BtnServerNewProfile_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ProfileUi.CanCreateOtherProfile())
            return;

        ModMain.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
        PageLoginAuth.draggedAuthServer = TextServerAuthServer.Text;
        ModBase.RunInNewThread(() =>
        {
            Thread.Sleep(150);
            ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(ModLaunch.McLoginType.Auth));
        });
    }

    private static void TextServerEnter_Change(object sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox textBox) textBox.Text = textBox.Text.Replace("：", ":");
    }

    #endregion

    #region Java 选择

    // 刷新 Java 下拉框显示
    public void RefreshJavaComboBox()
    {
        if (ComboArgumentJava is null)
            return;

        // 获取实例的 Java 偏好（已兼容新旧格式）
        var preference = ModJava.GetInstanceJavaPreference(PageInstanceLeft.McInstance);

        // === 1. 初始化固定选项（使用类型安全的 Tag） ===
        ComboArgumentJava.Items.Clear();

        // 选项 0: 跟随全局设置
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.FollowGlobal"),
            Tag = new UseGlobalPreference()
        });

        // 选项 1: 自动选择
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Options.Java.AutoSelect"),
            Tag = new AutoSelect() // Nothing 表示自动选择
        });

        // 选项 2: 相对路径选项
        MyComboBoxItem relativePathItem;
        if (preference is UseRelativePath)
        {
            var relPref = (UseRelativePath)preference;
            var absPath = Path.GetFullPath(Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath));
            var javaEntry = ModJava.Javas.Get(absPath);

            if (Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory) && javaEntry is not null &&
                javaEntry.IsEnabled)
                // 有效路径：显示具体 Java 信息
                relativePathItem = new MyComboBoxItem
                {
                    Content = Lang.Text("Instance.Setup.Options.Java.SelectRelative.WithJava", javaEntry.ToString()),
                    Tag = new UseRelativePath(relPref.RelativePath),
                    ToolTip = Lang.Text("Instance.Setup.Options.Java.RelativePathToolTip", relPref.RelativePath, absPath)
                };
            else
                // 无效路径：提示用户重新选择
                relativePathItem = new MyComboBoxItem
                {
                    Content = Lang.Text("Instance.Setup.Options.Java.SelectRelative.Invalid"),
                    Tag = new UseRelativePath(relPref.RelativePath),
                    ToolTip = Lang.Text("Instance.Setup.Options.Java.InvalidPathToolTip", absPath)
                };
        }
        else
        {
            // 未配置相对路径：使用默认模板
            relativePathItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.SelectRelative"),
                Tag = new UseRelativePath(@"jre\bin\java.exe"),
                ToolTip = Lang.Text("Instance.Setup.Options.Java.SelectRelativeToolTip")
            };
        }

        ComboArgumentJava.Items.Add(relativePathItem);

        // === 2. 添加所有可用 Java 运行时 ===
        MyComboBoxItem selectedItem = null;
        try
        {
            foreach (var curJava in ModJava.Javas.GetSortedJavaList())
            {
                var item = new MyComboBoxItem
                {
                    Content = curJava.ToString(),
                    ToolTip =
                        Lang.Text("Instance.Setup.Options.Java.Details.ToolTip", curJava.Installation.JavaExePath, curJava.Installation.Version, curJava.Source),
                    Tag = curJava
                };
                ToolTipService.SetInitialShowDelay(item, 300);
                ToolTipService.SetBetweenShowDelay(item, 100);
                ComboArgumentJava.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Config.Instance.SelectedJava[PageInstanceLeft.McInstance.PathInstance] = "使用全局设置";
            ModBase.Log(
                ex,
                "更新实例设置 Java 下拉框失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
            ComboArgumentJava.Items.Clear();
            ComboArgumentJava.Items.Add(new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.LoadFailed"),
                IsEnabled = false
            });
            ComboArgumentJava.SelectedIndex = 0;
            RefreshRam();
            return;
        }

        // === 3. 根据当前偏好设置选中项（优先使用新格式 preference） ===
        if (preference is null)
        {
            // 自动选择
            selectedItem = ComboArgumentJava.Items[1] as MyComboBoxItem;
        }
        else if (preference is UseGlobalPreference)
        {
            selectedItem = ComboArgumentJava.Items[0] as MyComboBoxItem;
        }
        else if (preference is UseRelativePath)
        {
            selectedItem = ComboArgumentJava.Items[2] as MyComboBoxItem;
        }
        else if (preference is ExistingJava)
        {
            var existPref = (ExistingJava)preference;
            // 在 Java 列表中查找匹配项（从索引 3 开始）
            for (int i = 3, loopTo = ComboArgumentJava.Items.Count - 1; i <= loopTo; i++)
            {
                var item = ComboArgumentJava.Items[i] as MyComboBoxItem;
                if (item is not null && item.Tag is JavaEntry)
                {
                    var javaEntry = (JavaEntry)item.Tag;
                    if (string.Equals(javaEntry.Installation.JavaExePath, existPref.JavaExePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selectedItem = item;
                        break;
                    }
                }
            }
        }

        // 降级处理：无匹配项时回退到自动选择
        if (selectedItem is null && ComboArgumentJava.Items.Count > 1)
            selectedItem = ComboArgumentJava.Items[1] as MyComboBoxItem;

        // 设置选中项
        if (selectedItem is not null) ComboArgumentJava.SelectedItem = selectedItem;

        // === 4. 无可用 Java 时的降级处理 ===
        if (!ModJava.Javas.ExistAnyJava() && ComboArgumentJava.Items.Count <= 3)
        {
            ComboArgumentJava.Items.Clear();
            var noJavaItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.NoRuntime"),
                ToolTip = Lang.Text("Instance.Setup.Options.Java.NoRuntime.ToolTip"),
                IsEnabled = false
            };
            ComboArgumentJava.Items.Add(noJavaItem);
            ComboArgumentJava.SelectedItem = noJavaItem;
        }

        // === 5. 刷新关联控件 ===
        RefreshRam();
    }

    // 阻止在无效状态下展开下拉框
    private void ComboArgumentJava_DropDownOpened(object? sender, EventArgs e)
    {
        if (ComboArgumentJava.SelectedItem is null)
        {
            ComboArgumentJava.IsDropDownOpen = false;
            return;
        }

        var firstItem = ComboArgumentJava.Items[0] as MyComboBoxItem;
        if (firstItem is not null &&
        ((string)firstItem.Content == Lang.Text("Instance.Setup.Options.Java.NoRuntime") ||
        (string)firstItem.Content == Lang.Text("Instance.Setup.Options.Java.LoadFailed")))
            ComboArgumentJava.IsDropDownOpen = false;
    }

    // 下拉框选择更改处理（保存新格式配置）
    private void JavaSelectionUpdate(object sender, SelectionChangedEventArgs e)
    {
        if (ModBase.IsUiUpdating)
            return;
        if (ComboArgumentJava.SelectedItem is null)
            return;

        var selectedItem = ComboArgumentJava.SelectedItem as MyComboBoxItem;
        if (selectedItem is null || (selectedItem.Tag is null &&
        (string)selectedItem.Content != Lang.Text("Instance.Setup.Options.Java.AutoSelect")))
            return;

        JavaPreference preference = default;
        var logMessage = "";

        // 根据 Tag 类型生成偏好对象
        if (selectedItem.Tag is null or AutoSelect)
        {
            // 自动选择：存储空字符串
            preference = new AutoSelect();
            logMessage = "[Java] 修改实例 Java 选择设置：自动选择";
        }
        else if (selectedItem.Tag is UseGlobalPreference)
        {
            preference = new UseGlobalPreference();
            logMessage = "[Java] 修改实例 Java 选择设置：跟随全局设置";
        }
        else if (selectedItem.Tag is UseRelativePath)
        {
            // 相对路径：需要用户选择实际文件
            var ret = SystemDialogs.SelectFile(Lang.Text("Setup.Java.SelectFile.Filter"), Lang.Text("Setup.Java.SelectFile.Title"), Basics.ExecutableDirectory);
            if (string.IsNullOrWhiteSpace(ret))
                // 用户取消，不保存配置，保持原选择
                return;

            ret = Path.GetFullPath(ret);
            var relativePath = Path.GetRelativePath(Basics.ExecutableDirectory, ret);

            // 验证路径是否在启动器目录内
            if (!Files.IsPathWithinDirectory(relativePath, Basics.ExecutableDirectory))
            {
                HintService.Hint(Lang.Text("Instance.Setup.Options.Java.PathOutOfRange"), HintType.Error);
                return;
            }

            preference = new UseRelativePath(relativePath);
            logMessage = $"[Java] 修改实例 Java 选择设置：相对路径 | {relativePath}";
        }
        else if (selectedItem.Tag is JavaEntry)
        {
            var javaEntry = (JavaEntry)selectedItem.Tag;
            preference = new ExistingJava(javaEntry.Installation.JavaExePath);
            logMessage = $"[Java] 修改实例 Java 选择设置：{javaEntry}";
        }

        // 保存配置
        var json = JsonSerializer.Serialize(preference, JsonCompat.SerializerOptions);
        Config.Instance.SelectedJava[PageInstanceLeft.McInstance.PathInstance] = json;


        ModBase.Log(logMessage);
        RefreshRam();
    }

    #endregion

    #region 其他设置

    // 版本隔离警告
    private bool isReverting;

    private async void ComboArgumentIndieV2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (ModBase.IsUiUpdating)
                return;
            if (isReverting)
                return;
            if (await ModMain.MyMsgBoxAsync(
                    Lang.Text("Instance.Setup.Options.InstanceIsolation.Message"),
                    Lang.Text("Common.Dialog.Warning"), Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
            {
                isReverting = true;
                ComboArgumentIndieV2.SelectedItem = e.RemovedItems[0];
                isReverting = false;
            }
            else
            {
                bool newValue = ComboArgumentIndieV2.SelectedIndex == 0;
                Config.Instance.IndieV2[PageInstanceLeft.McInstance.PathInstance] = newValue;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "修改版本隔离设置失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    // 游戏窗口
    private void CheckArgumentTitleEmpty_Change(object sender, bool e)
    {
        _vm.RefreshTitleHint(CheckArgumentTitleEmpty.Checked == true);
        CheckBoxChange(sender,e);
    }

    private void TextArgumentTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.RefreshTitleEmptyHint(TextArgumentTitle.Text);
        TextBoxChange(sender,e);
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.RefreshRunWait(TextAdvanceRun.Text);
        TextBoxChange(sender,e);
    }

    private async void ComboAdvanceRenderer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (ModBase.IsUiUpdating)
                return;

            var args = e; // 转换事件参数

            if (!States.Hint.Renderer && ComboAdvanceRenderer.SelectedIndex != 0)
            {
                if (await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                        Lang.Text("Common.Dialog.Warning"),
                        Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                {
                    ComboAdvanceRenderer.SelectedItem = args.RemovedItems[0];
                }
                else
                {
                    ComboChange(ComboAdvanceRenderer, e);
                    States.Hint.Renderer = true;
                }
            }
            else
            {
                ComboChange(ComboAdvanceRenderer, e);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "修改渲染器设置失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    private async void CheckUseDebugLog4j2Config_CheckChanged(object sender, bool e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (ModBase.IsUiUpdating)
                return;
            var checkBox = sender as MyCheckBox;
            if (checkBox is null) return;

            if (checkBox.Checked.GetValueOrDefault() && !States.Hint.DebugLog4j2Config)
            {
                if (await ModMain.MyMsgBoxAsync(
                        Lang.Text("Instance.Setup.Advanced.UseDebugLog4j.Message"),
                        Lang.Text("Common.Dialog.Warning"), Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                {
                    checkBox.Checked = false;
                }
                else
                {
                    CheckBoxChange(sender, e);
                    States.Hint.DebugLog4j2Config = true;
                }
            }
            else
            {
                CheckBoxChange(sender, e);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "修改 Debug Log4j2 设置失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    #endregion
}
