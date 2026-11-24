using Avalonia.Controls;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
/// 第三方（Authlib-Injector）登录视图：支持密码与 OAuth（Yggdrasil Connect 设备码）两种模式。 内置清单中的服务器直接进入
/// OAuth，其余固定密码登录。页面只保留手动切换动画。
/// </summary>
public partial class PageLoginAuth : LoginPageBase
{
    private readonly MyButton _btnBack;
    private readonly MyTextButton _btnBackOAuth;
    private readonly MyTextButton _btnLink;
    private readonly MyButton _btnLogin;
    private readonly MyTextButton _btnPasswordWebsite;
    private readonly MyTextButton _btnUseOAuth;
    private readonly MyTextButton _btnUsePassword;
    private readonly MyTextButton _btnWebsite;
    private readonly Grid _panOAuth;
    private readonly Grid _panPasswordLinks;
    private readonly MyTextBox _textName;
    private readonly TextBlock _textNameTitle;
    private readonly MyPasswordBox _textPass;
    private readonly TextBlock _textPassTitle;

    public PageLoginAuth()
    {
        InitializeComponent();
        DataContext = ViewModel;
        _panOAuth = this.FindControl<Grid>("PanOAuth")!;
        _panPasswordLinks = this.FindControl<Grid>("PanPasswordLinks")!;
        _textName = this.FindControl<MyTextBox>("TextName")!;
        _textNameTitle = this.FindControl<TextBlock>("TextNameTitle")!;
        _textPass = this.FindControl<MyPasswordBox>("TextPass")!;
        _textPassTitle = this.FindControl<TextBlock>("TextPassTitle")!;
        _btnBack = this.FindControl<MyButton>("BtnBack")!;
        _btnBackOAuth = this.FindControl<MyTextButton>("BtnBackOAuth")!;
        _btnLogin = this.FindControl<MyButton>("BtnLogin")!;
        _btnLink = this.FindControl<MyTextButton>("BtnLink")!;
        _btnWebsite = this.FindControl<MyTextButton>("BtnWebsite")!;
        _btnUsePassword = this.FindControl<MyTextButton>("BtnUsePassword")!;
        _btnUseOAuth = this.FindControl<MyTextButton>("BtnUseOAuth")!;
        _btnPasswordWebsite = this.FindControl<MyTextButton>("BtnPasswordWebsite")!;

        ViewModel.BackRequested += RequestBack;
        ViewModel.RefreshRequested += RequestRefresh;

        _btnUsePassword.Click += (_, _) => SetLoginMode(false);
        _btnUseOAuth.Click += (_, _) => SetLoginMode(true);

        ApplyListedMode(PendingAuthServer);

        AttachedToVisualTree += (_, _) =>
        {
            if (PendingAuthServer is not null)
                ApplyListedMode(PendingAuthServer);
        };
    }

    public static string? PendingAuthServer { get; set; }

    public PageLoginAuthViewModel ViewModel { get; } = new();

    public override void Reload()
    {
        var pendingServer = PendingAuthServer;
        PendingAuthServer = null;
        SetLoginMode(ViewModel.Reload(pendingServer), false);
    }

    private void ApplyListedMode(string? server)
    {
        ViewModel.UseOAuthVisible = PageLoginAuthViewModel.IsListedOAuthServer(server);
        UpdateOAuthEntryVisibility();
        SetLoginMode(ViewModel.UseOAuthVisible, false);
    }

    #region 模式切换

    private void SetLoginMode(bool useOAuth, bool animate = true)
    {
        var shouldAnimate = ViewModel.SetLoginMode(useOAuth);
        UpdateOAuthEntryVisibility();
        if (!animate || !shouldAnimate)
        {
            if (!animate)
                AnimationService.CancelAnimationByName("Profile Authentication Mode");
            ApplyModeVisibility(useOAuth);
            return;
        }

        var oldControls = GetModeControls(!useOAuth);
        var animations = (
            from control in oldControls
            where control.IsVisible
            select (IAnimation)new OpacityAnimation(control, -control.Opacity, 90)).ToList();
        animations.Add(new ActionAnimation(() => ApplyModeVisibility(useOAuth), true));
        var newControls = GetModeControls(useOAuth);
        foreach (var control in newControls)
        {
            control.Opacity = 0d;
            var targetOpacity = GetModeTargetOpacity(control);
            animations.Add(
                new OpacityAnimation(control, targetOpacity, 130, 20, new FluentEaseIn()));
        }

        AnimationService.Run(animations, "Profile Authentication Mode");
    }

    private List<Control> GetModeControls(bool oauthMode)
    {
        return oauthMode
            ? [_panOAuth, _btnUsePassword, _btnWebsite, _btnBackOAuth]
            :
            [
                _textName, _textNameTitle, _panPasswordLinks, _btnLink, _btnLogin, _btnBack,
                _textPass,
                _textPassTitle
            ];
    }

    private double GetModeTargetOpacity(Control control)
    {
        return ReferenceEquals(control, _btnBackOAuth)
            || ReferenceEquals(control, _panPasswordLinks)
                ? 0.3d
                : 1d;
    }

    private void ApplyModeVisibility(bool oauthMode)
    {
        var oauthControls = GetModeControls(true);
        var passwordControls = GetModeControls(false);
        foreach (var control in oauthControls)
        {
            control.IsVisible = oauthMode;
            control.Opacity = GetModeTargetOpacity(control);
        }

        foreach (var control in passwordControls)
        {
            control.IsVisible = !oauthMode;
            control.Opacity = GetModeTargetOpacity(control);
        }

        _btnLink.IsVisible = !oauthMode && ViewModel.RegisterLinkVisible;
    }

    private void UpdateOAuthEntryVisibility()
    {
        Grid.SetColumnSpan(_btnPasswordWebsite, ViewModel.UseOAuthVisible ? 1 : 3);
    }

    #endregion
}