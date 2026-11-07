using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     第三方（Authlib-Injector）登录视图：支持密码与 OAuth（Yggdrasil Connect 设备码）两种模式。
///     登录、OAuth 探测与注册链接由 <see cref="PageLoginAuthViewModel" /> 负责；页面只保留模式切换动画。
/// </summary>
public partial class PageLoginAuth : LoginPageBase
{
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
        _btnBackOAuth = this.FindControl<MyTextButton>("BtnBackOAuth")!;
        _btnLogin = this.FindControl<MyButton>("BtnLogin")!;
        _btnLink = this.FindControl<MyTextButton>("BtnLink")!;
        _btnWebsite = this.FindControl<MyTextButton>("BtnWebsite")!;
        _btnUsePassword = this.FindControl<MyTextButton>("BtnUsePassword")!;
        _btnPasswordWebsite = this.FindControl<MyTextButton>("BtnPasswordWebsite")!;
        _btnUseOAuth = this.FindControl<MyTextButton>("BtnUseOAuth")!;

        ViewModel.LoginModeRequested += mode => SetLoginMode(mode);
        ViewModel.BackRequested += RequestBack;
        ViewModel.RefreshRequested += RequestRefresh;

        _btnUsePassword.Click += (_, _) => SetLoginMode(false);
        _btnUseOAuth.Click += (_, _) => SetLoginMode(true);

        SetLoginMode(false, false);

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree +=
            (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    public static string? PendingAuthServer { get; set; }

    public static bool? PendingAuthServerOAuthSupported { get; set; }

    public PageLoginAuthViewModel ViewModel { get; } = new();

    internal static Task<bool> IsOAuthSupportedAsync(string authServerUrl)
    {
        return PageLoginAuthViewModel.IsOAuthSupportedAsync(authServerUrl);
    }

    private void OnLanguageChanged()
    {
        ViewModel.RefreshLocalization();
        ApplyModeVisibility(ViewModel.IsOAuthMode);
        UpdateOAuthEntryVisibility();
    }

    public override void Reload()
    {
        var pendingServer = PendingAuthServer;
        var pendingOAuthSupport = PendingAuthServerOAuthSupported;
        PendingAuthServer = null;
        PendingAuthServerOAuthSupported = null;
        ViewModel.Reload(pendingServer, pendingOAuthSupport);
    }

    #region 模式切换

    private void SetLoginMode(bool useOAuth, bool animate = true)
    {
        var shouldAnimate = ViewModel.SetLoginMode(useOAuth);
        UpdateOAuthEntryVisibility();
        if (!animate || !shouldAnimate)
        {
            ApplyModeVisibility(useOAuth);
            return;
        }

        var oldControls = GetModeControls(!useOAuth);
        var animations = (
            from control in oldControls
            where control.IsVisible
            select AaOpacity(control, -control.Opacity, 90)).ToList();
        animations.Add(AaCode(() => ApplyModeVisibility(useOAuth), after: true));
        var newControls = GetModeControls(useOAuth);
        foreach (var control in newControls)
        {
            control.Opacity = 0d;
            var targetOpacity = GetModeTargetOpacity(control);
            animations.Add(AaOpacity(control, targetOpacity, 130, 20, new AniEaseInFluent()));
        }

        AniStart(animations, "Profile Authentication Mode");
    }

    private List<Control> GetModeControls(bool oauthMode)
    {
        return oauthMode
            ? [_panOAuth, _btnUsePassword, _btnWebsite, _btnBackOAuth]
            :
            [
                _textName, _textNameTitle, _panPasswordLinks, _btnLink, _btnLogin, _textPass,
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
        var available = ViewModel.UseOAuthVisible;
        Grid.SetColumnSpan(_btnPasswordWebsite, available ? 1 : 3);
    }

    #endregion
}