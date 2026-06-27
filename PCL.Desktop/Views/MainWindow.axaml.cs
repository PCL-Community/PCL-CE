// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Application.Accounts;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Views.Launch;
using PCL.Platform.Paths;

namespace PCL.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Stopwatch _showAnimationClock = new();
    private Control? _showAnimationRoot;
    private RotateTransform? _showAnimationRotate;
    private TranslateTransform? _showAnimationTranslate;
    private DispatcherTimer? _showAnimationTimer;
    private bool _showAnimationStarted;
    private bool _isNavExpanded;
    private DispatcherTimer? _navAnimTimer;
    private readonly Stopwatch _pageChangeClock = new();
    private DispatcherTimer? _pageChangeTimer;
    private double _navExpandedWidth = 200d;
    private double _navAnimStart;
    private double _navAnimTarget;
    private int _navAnimElapsed;
    private int _currentNavPage;
    private int _pendingNavPage;
    private bool _isPageContentSwapped;
    private bool _isMainWindowOpened;
    private PageLaunchLeft? _launchLeft;
    private PageLaunchRight? _launchRight;
    private PageLoginProfile? _loginProfilePage;
    private PageLoginProfileSkin? _loginProfileSkinPage;
    private PageLoginMs? _loginMsPage;
    private PageLoginAuth? _loginAuthPage;
    private PageLoginOffline? _loginOfflinePage;
    private readonly List<LoginProfileInfo> _loginProfiles = [];

    private const double NavCollapsedWidth = 50d;
    private const int NavAnimDuration = 200;
    private const double PageFadeOutDuration = 110d;
    private const double PageFadeInDuration = 170d;

    private static readonly Dictionary<int, string> NavPageTitles = new()
    {
        [0] = "启动",
        [1] = "下载",
        [2] = "社区",
        [3] = "设置",
        [4] = "在线"
    };

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Opacity = 0d;
        CanResize = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        SetWindowIcon();
        CaptureShowAnimationTransforms();
        Opened += (_, _) =>
        {
            _isMainWindowOpened = true;
            StartShowAnimation();
        };
        SyncTitleOverlayWidth();
        _ = LoadProfilesAsync();
        SelectNavPage(0, animate: false);
    }

    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
    }

    private void FormMain_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            e.GetPosition(this).Y <= 48)
        {
            BeginMoveDrag(e);
        }
    }

    private void FormMain_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncMainSize();
        SyncTitleOverlayWidth();
    }

    private void FormMain_Closing(object? sender, WindowClosingEventArgs e)
    {
    }

    private void FormMain_Activated(object? sender, EventArgs e)
    {
    }

    private void FrmMain_Drop(object? sender, DragEventArgs e)
    {
    }

    private void FormMain_MouseMove(object? sender, PointerEventArgs e)
    {
    }

    private void VideoEnded(object? sender, EventArgs e)
    {
    }

    private void PanTitle_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncTitleOverlayWidth();
    }

    private void BtnTitleClose_Click(object? sender, EventArgs e) => Close();

    private void BtnTitleMin_Click(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnTitleHelp_Click(object? sender, EventArgs e)
    {
    }

    private void BtnTitleInner_Click(object? sender, EventArgs e)
    {
    }

    private void BtnNavItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem item || !TryGetNavPage(item, out int page))
            return;

        SelectNavPage(page, animate: _isMainWindowOpened);
        e.Handled = true;
    }

    private void BtnNavToggle_Click(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanNavLayer") is not { } navLayer)
            return;

        _isNavExpanded = !_isNavExpanded;
        if (_isNavExpanded)
            _navExpandedWidth = MeasureNavExpandedWidth(navLayer);

        _navAnimStart = GetCurrentNavWidth(navLayer);
        _navAnimTarget = _isNavExpanded ? _navExpandedWidth : NavCollapsedWidth;
        _navAnimElapsed = 0;
        _navAnimTimer?.Stop();
        _navAnimTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _navAnimTimer.Tick += NavAnimTimer_Tick;
        _navAnimTimer.Start();
    }

    private void PanMainLeft_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
    }

    private void BtnExtraUpdateRestart_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraBack_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraDownload_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraApril_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraShutdown_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraLog_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_RightClick(object? sender, PointerReleasedEventArgs e)
    {
    }

    private void FormDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void SyncTitleOverlayWidth()
    {
        Control? panTitle = this.FindControl<Control>("PanTitle");
        Control? panTitleMain = this.FindControl<Control>("PanTitleMain");
        Control? panTitleInner = this.FindControl<Control>("PanTitleInner");
        if (panTitle is null)
            return;

        double width = panTitle.Bounds.Width;
        if (width <= 0)
            width = Width;
        if (panTitleMain is not null)
            panTitleMain.Width = width;
        if (panTitleInner is not null)
            panTitleInner.Width = width;
    }

    public void ActivateExistingInstance()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        ForceWindowsForeground();
    }

    private void SetWindowIcon()
    {
        try
        {
            using Stream iconStream = Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://PCL.Desktop/Assets/icon.ico", UriKind.Absolute));
            Icon = new WindowIcon(iconStream);
        }
        catch (IOException)
        {
        }
    }

    private void ForceWindowsForeground()
    {
        if (!OperatingSystem.IsWindows())
            return;

        nint handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
            return;

        WindowsForegroundApi.ShowWindow(handle, 9);
        WindowsForegroundApi.SetForegroundWindow(handle);
    }

    private void SyncMainSize(double? navWidth = null)
    {
        Control? panBack = this.FindControl<Control>("PanBack");
        Control? panForm = this.FindControl<Control>("PanForm");
        Control? panTitle = this.FindControl<Control>("PanTitle");
        Control? panMain = this.FindControl<Control>("PanMain");
        Control? navLayer = this.FindControl<Control>("PanNavLayer");
        Control? videoBack = this.FindControl<Control>("VideoBack");
        if (panBack is null)
            return;

        double formWidth = panBack.Bounds.Width;
        double formHeight = panBack.Bounds.Height;
        if (formWidth <= 0d)
            formWidth = Math.Max(0d, Width - 20d);
        if (formHeight <= 0d)
            formHeight = Math.Max(0d, Height - 20d);

        if (panForm is not null)
        {
            panForm.Width = formWidth;
            panForm.Height = formHeight;
        }

        if (panMain is not null)
        {
            double currentNavWidth = navWidth ?? GetCurrentNavWidth(navLayer);
            panMain.Width = Math.Max(0d, formWidth - currentNavWidth);
            panMain.Height = Math.Max(0d, formHeight - (panTitle?.Bounds.Height ?? 0d));
        }

        if (videoBack is not null)
        {
            videoBack.Width = formWidth;
            videoBack.Height = formHeight;
        }
    }

    private void SetNavWidth(Control navLayer, double width)
    {
        navLayer.Width = width;
        SyncMainSize(width);
    }

    private double MeasureNavExpandedWidth(Control navLayer)
    {
        double originalWidth = navLayer.Width;
        navLayer.Width = double.NaN;
        navLayer.InvalidateMeasure();
        navLayer.Measure(new Size(double.PositiveInfinity, Math.Max(0d, Bounds.Height)));

        double measuredWidth = navLayer.DesiredSize.Width;
        foreach (MyListItem item in GetNavItems())
        {
            item.Measure(new Size(double.PositiveInfinity, item.Bounds.Height > 0d ? item.Bounds.Height : 42d));
            measuredWidth = Math.Max(measuredWidth, item.DesiredSize.Width + 2d);
        }

        navLayer.Width = originalWidth;
        navLayer.InvalidateMeasure();

        if (double.IsNaN(measuredWidth) || double.IsInfinity(measuredWidth) || measuredWidth <= 0d)
            measuredWidth = _navExpandedWidth;
        return Math.Max(measuredWidth, NavCollapsedWidth + 1d) + 10d;
    }

    private static double GetCurrentNavWidth(Control? navLayer)
    {
        if (navLayer is null)
            return NavCollapsedWidth;
        if (!double.IsNaN(navLayer.Width) && navLayer.Width > 0d)
            return navLayer.Width;
        return navLayer.Bounds.Width > 0d ? navLayer.Bounds.Width : NavCollapsedWidth;
    }

    private void NavAnimTimer_Tick(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanNavLayer") is not { } navLayer)
        {
            _navAnimTimer?.Stop();
            _navAnimTimer = null;
            return;
        }

        _navAnimElapsed += 16;
        double progress = Math.Min(1d, (double)_navAnimElapsed / NavAnimDuration);
        double current = _navAnimStart + (_navAnimTarget - _navAnimStart) * EaseOutCubic(progress);
        SetNavWidth(navLayer, current);
        if (progress < 1d)
            return;

        _navAnimTimer?.Stop();
        _navAnimTimer = null;
        SetNavWidth(navLayer, _navAnimTarget);
    }

    private void SelectNavPage(int page, bool animate)
    {
        if (!NavPageTitles.ContainsKey(page))
            page = 0;

        MyListItem? selected = null;
        foreach (MyListItem item in GetNavItems())
        {
            if (TryGetNavPage(item, out int itemPage) && itemPage == page)
            {
                selected = item;
                break;
            }
        }

        if (selected is null)
            return;

        selected.Checked = true;
        foreach (MyListItem item in GetNavItems())
        {
            if (!ReferenceEquals(item, selected))
                item.Checked = false;
        }

        if (!animate || page == _currentNavPage)
        {
            ApplyPagePlaceholder(page);
            return;
        }

        BeginPageChangeAnimation(page);
    }

    private void ApplyPagePlaceholder(int page)
    {
        _currentNavPage = page;
        if (page == 0)
            ApplyLaunchPage();
        else
            ApplyPlaceholderPage(page);

        if (this.FindControl<Control>("PanMainRight") is { } right)
            right.Opacity = 1d;
    }

    private void ApplyLaunchPage()
    {
        if (this.FindControl<Border>("PanMainLeft") is not { } leftHost ||
            this.FindControl<Border>("PanMainRight") is not { } rightHost)
        {
            return;
        }

        _launchLeft ??= CreateLaunchLeftPage();
        _launchRight ??= new PageLaunchRight();

        leftHost.Child = _launchLeft;
        rightHost.Child = _launchRight;
        _launchLeft.TriggerShowAnimation();
        _launchRight.PageOnEnter();
    }

    private PageLaunchLeft CreateLaunchLeftPage()
    {
        PageLaunchLeft page = new();
        page.DownloadRequested += (_, _) => SelectNavPage(1, animate: true);
        page.InstanceSelectRequested += (_, _) => _launchRight?.AppendLog("正在重新检查本地游戏版本。");
        page.InstanceSettingsRequested += (_, _) => _launchRight?.AppendLog("版本设置页面正在迁移中。");
        page.CancelLaunchRequested += (_, _) => _launchRight?.AppendLog("已取消启动。");
        page.StatusMessage += (_, message) => _launchRight?.AppendLog(message);
        page.LoginPageRequested += (_, type) => ApplyLaunchLoginPage(page, type);
        page.LaunchRequested += (_, instance) =>
        {
            _launchRight?.AppendLog($"已请求启动 {instance.Name}。");
            page.UpdateLaunchingStatus("正在准备启动参数", 0.18d, "等待账户档案");
        };
        return page;
    }

    private void ApplyLaunchLoginPage(PageLaunchLeft launchPage, PageLaunchLeft.LaunchLoginPageType type)
    {
        switch (type)
        {
            case PageLaunchLeft.LaunchLoginPageType.ProfileSkin:
                if (_loginProfiles.Count == 0)
                {
                    launchPage.SetSelectedProfilePresent(false);
                    ApplyLaunchLoginPage(launchPage, PageLaunchLeft.LaunchLoginPageType.Profile);
                    return;
                }

                LoginProfileInfo selectedProfile = _loginProfiles[0];
                _loginProfileSkinPage ??= CreateProfileSkinPage(launchPage);
                _loginProfileSkinPage.SetProfile(selectedProfile);
                launchPage.SetLoginPage(_loginProfileSkinPage, animate: true, PageLaunchLeft.LaunchLoginPageType.ProfileSkin);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Profile:
                _loginProfilePage ??= CreateProfilePage(launchPage);
                _loginProfilePage.SetProfiles(_loginProfiles);
                launchPage.SetLoginPage(_loginProfilePage, animate: true, PageLaunchLeft.LaunchLoginPageType.Profile);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Ms:
                _loginMsPage ??= CreateMicrosoftLoginPage(launchPage);
                launchPage.SetLoginPage(_loginMsPage, animate: true, PageLaunchLeft.LaunchLoginPageType.Ms);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Auth:
                _loginAuthPage ??= CreateAuthLoginPage(launchPage);
                launchPage.SetLoginPage(_loginAuthPage, animate: true, PageLaunchLeft.LaunchLoginPageType.Auth);
                break;
            case PageLaunchLeft.LaunchLoginPageType.Offline:
                _loginOfflinePage ??= CreateOfflineLoginPage(launchPage);
                _loginOfflinePage.SetSkinSources(_loginProfiles);
                launchPage.SetLoginPage(_loginOfflinePage, animate: true, PageLaunchLeft.LaunchLoginPageType.Offline);
                break;
            default:
                launchPage.SetLoginPage(
                    CreateLoginPlaceholder(type),
                    animate: true,
                    type);
                break;
        }
    }

    private PageLoginProfile CreateProfilePage(PageLaunchLeft launchPage)
    {
        PageLoginProfile page = new();
        page.ProfileSelected += (_, profile) =>
        {
            _loginProfiles.Remove(profile);
            _loginProfiles.Insert(0, profile);
            launchPage.SetSelectedProfilePresent(true);
            launchPage.RefreshPage(anim: true);
            SaveProfilesInBackground("保存账户档案选择");
            _launchRight?.AppendLog($"已选择账户档案 {profile.Username}。");
        };
        page.CreateProfileRequested += (_, _) =>
        {
            ShowProfileTypeSelector(launchPage);
        };
        page.ImportExportRequested += (_, _) => _launchRight?.AppendLog("档案导入与导出入口正在迁移中。");
        return page;
    }

    private PageLoginProfileSkin CreateProfileSkinPage(PageLaunchLeft launchPage)
    {
        PageLoginProfileSkin page = new();
        page.ChangeProfileRequested += (_, _) =>
        {
            launchPage.SetSelectedProfilePresent(false);
            launchPage.RefreshPage(anim: true);
        };
        page.ChangeSkinRequested += (_, _) => _launchRight?.AppendLog("皮肤更换入口正在迁移中。");
        page.SaveSkinRequested += (_, _) => _launchRight?.AppendLog("皮肤保存入口正在迁移中。");
        page.RefreshSkinRequested += (_, _) => _launchRight?.AppendLog("皮肤刷新入口正在迁移中。");
        page.ChangeCapeRequested += (_, _) => _launchRight?.AppendLog("披风更换入口正在迁移中。");
        page.EditPasswordRequested += (_, _) => _launchRight?.AppendLog("密码修改入口正在迁移中。");
        page.EditNameRequested += (_, _) => _launchRight?.AppendLog("用户名修改入口正在迁移中。");
        return page;
    }

    private void ShowProfileTypeSelector(PageLaunchLeft launchPage)
    {
        MyMsgSelect dialog = new();
        dialog.Configure(
            "选择账户类型",
            [
                CreateProfileTypeItem(
                    "Microsoft 登录",
                    "使用正版 Microsoft 账户登录，适合已购买 Minecraft 的玩家。",
                    "lucide/shield-check"),
                CreateProfileTypeItem(
                    "第三方登录",
                    "使用 Authlib-Injector 兼容认证服务器登录。",
                    "lucide/network"),
                CreateProfileTypeItem(
                    "离线登录",
                    "创建本地离线档案。联机服务器可能不会接受此档案。",
                    "lucide/link-2-off")
            ]);
        ShowSelectionDialog(dialog, selectedIndex =>
        {
            if (selectedIndex is not int index)
                return;

            PageLaunchLeft.LaunchLoginPageType? target = index switch
            {
                0 => PageLaunchLeft.LaunchLoginPageType.Ms,
                1 => PageLaunchLeft.LaunchLoginPageType.Auth,
                2 => PageLaunchLeft.LaunchLoginPageType.Offline,
                _ => null
            };
            if (target is null)
                return;

            launchPage.RefreshPage(anim: true, target.Value);
            _launchRight?.AppendLog($"正在创建{dialog.Items[index].Title}档案。");
        });
    }

    private static MyListItem CreateProfileTypeItem(string title, string info, string icon) =>
        new()
        {
            Title = title,
            Info = info,
            SvgIcon = icon,
            LogoScale = 0.82d,
            MinHeight = 42d,
            Margin = new Thickness(0d, 2d)
        };

    private void ShowSelectionDialog(MyMsgSelect dialog, Action<int?> closed)
    {
        if (this.FindControl<BlurBorder>("PanMsgBackground") is not { } background ||
            this.FindControl<Grid>("PanMsg") is not { } host)
        {
            closed(null);
            return;
        }

        host.Children.Clear();
        background.IsVisible = true;
        background.Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
        dialog.Closed += (_, args) =>
        {
            host.Children.Remove(dialog);
            if (host.Children.Count == 0)
            {
                background.Background = Brushes.Transparent;
                background.IsVisible = false;
            }
            closed(args.SelectedIndex);
        };
        host.Children.Add(dialog);
    }

    private PageLoginMs CreateMicrosoftLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginMs page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.PurchaseRequested += (_, _) => OpenExternalUrl(
            "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        page.WebsiteRequested += (_, _) => OpenExternalUrl("https://www.minecraft.net/zh-hans");
        page.LoginRequested += (_, _) =>
        {
            _launchRight?.AppendLog("Microsoft 登录正在接入跨平台账户服务。");
            page.UpdateProgress(0.05d);
            page.FinishLogin();
        };
        return page;
    }

    private PageLoginAuth CreateAuthLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginAuth page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.ValidationFailed += (_, message) => _launchRight?.AppendLog(message);
        page.RegisterLinkRequested += (_, isRegisterMode) =>
            _launchRight?.AppendLog(isRegisterMode ? "请先填写认证服务器后再注册账户。" : "请在认证服务网站中找回密码。");
        page.LoginRequested += (_, request) =>
        {
            _launchRight?.AppendLog($"正在准备连接第三方认证服务器：{request.Server}");
            page.FinishLogin();
        };
        return page;
    }

    private PageLoginOffline CreateOfflineLoginPage(PageLaunchLeft launchPage)
    {
        PageLoginOffline page = new();
        page.BackRequested += (_, _) => launchPage.RefreshPage(anim: true);
        page.ValidationFailed += (_, message) => _launchRight?.AppendLog(message);
        page.ProfileCreateRequested += (_, request) =>
        {
            string info = string.IsNullOrWhiteSpace(request.SkinSourceUuid)
                ? "离线登录"
                : $"离线登录 · 借用 {request.SkinSourceName}";
            LoginProfileInfo profile = new(
                request.Username,
                info,
                LaunchLoginProfileKind.Offline,
                Uuid: request.Uuid,
                SvgIcon: "lucide/user");

            _loginProfiles.RemoveAll(existing =>
                existing.Kind == LaunchLoginProfileKind.Offline &&
                string.Equals(existing.Uuid, profile.Uuid, StringComparison.OrdinalIgnoreCase));
            _loginProfiles.Insert(0, profile);
            _loginProfilePage?.SetProfiles(_loginProfiles, profile);
            launchPage.SetSelectedProfilePresent(true);
            launchPage.RefreshPage(anim: true);
            SaveProfilesInBackground("保存离线账户档案");
            _launchRight?.AppendLog($"已创建并选中离线档案 {profile.Username}。");
        };
        return page;
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            using LaunchProfileStore store = CreateLaunchProfileStore();
            LaunchProfileLoadResult result = await store.LoadAsync().ConfigureAwait(false);
            List<LoginProfileInfo> profiles = result.Profiles.Profiles
                .Select(ToLoginProfileInfo)
                .ToList();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _loginProfiles.Clear();
                _loginProfiles.AddRange(profiles);
                _loginProfilePage?.SetProfiles(_loginProfiles);
                _launchLeft?.SetSelectedProfilePresent(_loginProfiles.Count > 0);
                if (result.WasRecovered)
                    _launchRight?.AppendLog($"账户档案配置已重置，损坏文件已备份到：{result.BackupPath}");
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _launchRight?.AppendLog("读取账户档案失败：" + ex.Message));
        }
    }

    private void SaveProfilesInBackground(string action)
    {
        LaunchProfileSet snapshot = new()
        {
            Profiles = _loginProfiles.Select(ToLaunchProfile).ToArray()
        };
        _ = Task.Run(async () =>
        {
            try
            {
                using LaunchProfileStore store = CreateLaunchProfileStore();
                await store.SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _launchRight?.AppendLog(action + "失败：" + ex.Message));
            }
        });
    }

    private static LaunchProfileStore CreateLaunchProfileStore() =>
        new(CreateLaunchProfilePath());

    private static string CreateLaunchProfilePath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("PCLN_LAUNCH_PROFILES_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "launch-profiles.json");
    }

    private static LoginProfileInfo ToLoginProfileInfo(LaunchProfile profile) =>
        new(
            profile.Username,
            profile.Info,
            profile.Kind switch
            {
                LaunchProfileKind.Microsoft => LaunchLoginProfileKind.Microsoft,
                LaunchProfileKind.ThirdParty => LaunchLoginProfileKind.ThirdParty,
                _ => LaunchLoginProfileKind.Offline
            },
            profile.Uuid,
            profile.Logo,
            profile.SvgIcon,
            profile.SkinAddress,
            profile.AuthServer);

    private static LaunchProfile ToLaunchProfile(LoginProfileInfo profile) =>
        new()
        {
            Username = profile.Username,
            Info = profile.Info,
            Kind = profile.Kind switch
            {
                LaunchLoginProfileKind.Microsoft => LaunchProfileKind.Microsoft,
                LaunchLoginProfileKind.ThirdParty => LaunchProfileKind.ThirdParty,
                _ => LaunchProfileKind.Offline
            },
            Uuid = profile.Uuid,
            Logo = profile.Logo,
            SvgIcon = profile.SvgIcon,
            SkinAddress = profile.SkinAddress,
            AuthServer = profile.AuthServer
        };

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _launchRight?.AppendLog("无法打开浏览器：" + ex.Message);
        }
    }

    private static Grid CreateLoginPlaceholder(PageLaunchLeft.LaunchLoginPageType type) =>
        new()
        {
            Children =
            {
                new MyCard
                {
                    Title = type switch
                    {
                        PageLaunchLeft.LaunchLoginPageType.Ms => "Microsoft 登录",
                        PageLaunchLeft.LaunchLoginPageType.Auth => "第三方登录",
                        PageLaunchLeft.LaunchLoginPageType.Offline => "离线档案",
                        _ => "账户"
                    },
                    Children =
                    {
                        new TextBlock
                        {
                            Margin = new Thickness(25d, 38d, 23d, 16d),
                            FontSize = 13.5d,
                            TextWrapping = TextWrapping.Wrap,
                            Text = "该登录分页正在迁移中，入口与状态切换已按 WPF 启动页保留。"
                        }
                    }
                }
            }
        };

    private void ApplyPlaceholderPage(int page)
    {
        if (this.FindControl<Border>("PanMainLeft") is { } leftHost)
        {
            if (leftHost.Child is MyPageLeft oldLeft)
                oldLeft.TriggerHideAnimation();
            leftHost.Child = null;
        }

        if (this.FindControl<Border>("PanMainRight") is { } rightHost)
        {
            if (rightHost.Child is MyPageRight oldRight)
                oldRight.PageOnExit();

            rightHost.Child = CreateLoadingPlaceholder(NavPageTitles[page]);
        }
    }

    private static Grid CreateLoadingPlaceholder(string pageTitle) =>
        new()
        {
            Children =
            {
                new MyLoading
                {
                    Name = "LoadMain",
                    Width = 220d,
                    Height = 120d,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Text = $"正在加载{pageTitle}页面"
                }
            }
        };

    private void BeginPageChangeAnimation(int page)
    {
        _pendingNavPage = page;
        _isPageContentSwapped = false;
        _pageChangeClock.Restart();
        _pageChangeTimer?.Stop();
        _pageChangeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _pageChangeTimer.Tick += PageChangeTimer_Tick;
        _pageChangeTimer.Start();
        PageChangeTimer_Tick(this, EventArgs.Empty);
    }

    private void PageChangeTimer_Tick(object? sender, EventArgs e)
    {
        if (this.FindControl<Control>("PanMainRight") is not { } right)
        {
            _pageChangeTimer?.Stop();
            _pageChangeTimer = null;
            ApplyPagePlaceholder(_pendingNavPage);
            return;
        }

        double elapsed = _pageChangeClock.Elapsed.TotalMilliseconds;
        if (elapsed <= PageFadeOutDuration)
        {
            right.Opacity = 1d - EaseOutCubic(Normalize(elapsed, PageFadeOutDuration));
            return;
        }

        if (!_isPageContentSwapped)
        {
            _isPageContentSwapped = true;
            ApplyPagePlaceholder(_pendingNavPage);
            right.Opacity = 0d;
        }

        double fadeInElapsed = elapsed - PageFadeOutDuration;
        right.Opacity = EaseOutCubic(Normalize(fadeInElapsed, PageFadeInDuration));
        if (fadeInElapsed < PageFadeInDuration)
            return;

        _pageChangeTimer?.Stop();
        _pageChangeTimer = null;
        right.Opacity = 1d;
    }

    private IEnumerable<MyListItem> GetNavItems()
    {
        if (this.FindControl<Panel>("PanTitleSelect") is not { } panel)
            yield break;

        foreach (Control child in panel.Children)
        {
            if (child is MyListItem item)
                yield return item;
        }
    }

    private static bool TryGetNavPage(MyListItem item, out int page)
    {
        page = 0;
        return item.Tag switch
        {
            int value => SetPage(value, out page),
            string text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out page),
            _ => false
        };
    }

    private static bool SetPage(int value, out int page)
    {
        page = value;
        return true;
    }

    private void CaptureShowAnimationTransforms()
    {
        if (Content is not Control root)
            return;

        _showAnimationRoot = root;
        if (root.RenderTransform is not TransformGroup group)
            return;

        foreach (ITransform transform in group.Children)
        {
            _showAnimationRotate ??= transform as RotateTransform;
            _showAnimationTranslate ??= transform as TranslateTransform;
        }
    }

    private void StartShowAnimation()
    {
        if (_showAnimationStarted)
            return;

        _showAnimationStarted = true;
        _showAnimationClock.Restart();
        _showAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _showAnimationTimer.Tick += ShowAnimationTimer_Tick;
        _showAnimationTimer.Start();
        ShowAnimationTimer_Tick(this, EventArgs.Empty);
    }

    private void ShowAnimationTimer_Tick(object? sender, EventArgs e)
    {
        double elapsed = _showAnimationClock.Elapsed.TotalMilliseconds;
        double delayed = Math.Max(0d, elapsed - 100d);

        Opacity = EaseOutCubic(Normalize(delayed, 250d));

        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 60d * (1d - EaseOutBack(Normalize(delayed, 600d)));
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = -4d * (1d - EaseOutBack(Normalize(delayed, 500d)));

        if (elapsed < 720d)
            return;

        _showAnimationTimer?.Stop();
        _showAnimationTimer = null;
        Opacity = 1d;
        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 0d;
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = 0d;
        if (_showAnimationRoot is not null)
            _showAnimationRoot.RenderTransform = null;
    }

    private static double Normalize(double elapsedMilliseconds, double durationMilliseconds)
    {
        return Math.Clamp(elapsedMilliseconds / durationMilliseconds, 0d, 1d);
    }

    private static double EaseOutCubic(double progress)
    {
        double inverse = 1d - progress;
        return 1d - inverse * inverse * inverse;
    }

    private static double EaseOutBack(double progress)
    {
        const double overshoot = 1.15d;
        double shifted = progress - 1d;
        return 1d + shifted * shifted * ((overshoot + 1d) * shifted + overshoot);
    }

    private static class WindowsForegroundApi
    {
        private static readonly Lazy<Api?> ApiInstance = new(LoadApi);

        public static void ShowWindow(nint hWnd, int nCmdShow)
        {
            _ = ApiInstance.Value?.ShowWindow(hWnd, nCmdShow);
        }

        public static void SetForegroundWindow(nint hWnd)
        {
            _ = ApiInstance.Value?.SetForegroundWindow(hWnd);
        }

        private static Api? LoadApi()
        {
            if (!NativeLibrary.TryLoad("user32.dll", out nint library))
                return null;

            if (!NativeLibrary.TryGetExport(library, "ShowWindow", out nint showWindow) ||
                !NativeLibrary.TryGetExport(library, "SetForegroundWindow", out nint setForegroundWindow))
            {
                NativeLibrary.Free(library);
                return null;
            }

            return new Api(
                library,
                Marshal.GetDelegateForFunctionPointer<ShowWindowDelegate>(showWindow),
                Marshal.GetDelegateForFunctionPointer<SetForegroundWindowDelegate>(setForegroundWindow));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool ShowWindowDelegate(nint hWnd, int nCmdShow);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool SetForegroundWindowDelegate(nint hWnd);

        private sealed class Api(nint library, ShowWindowDelegate showWindow, SetForegroundWindowDelegate setForegroundWindow)
        {
            private readonly nint _library = library;

            public bool ShowWindow(nint hWnd, int nCmdShow) => showWindow(hWnd, nCmdShow);

            public bool SetForegroundWindow(nint hWnd) => setForegroundWindow(hWnd);

            ~Api()
            {
                if (_library != 0)
                    NativeLibrary.Free(_library);
            }
        }
    }
}
