using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Minecraft.Yggdrasil;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     第三方（Authlib-Injector）登录视图：支持密码与 OAuth（Yggdrasil Connect 设备码）两种模式。
///     验证服务器来自拖入的 authlib 链接；未拖入时保持为空，与 WPF 初始状态一致。
///     密码模式下提供注册与找回密码入口，两者都跳转到实例配置的注册地址。
/// </summary>
public partial class PageLoginAuth : LoginPageBase
{
    private readonly MyButton _btnBack;
    private readonly MyTextButton _btnBackOAuth;
    private readonly MyTextButton _btnLink;
    private readonly MyButton _btnLogin;
    private readonly MyButton _btnOAuth;
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

    private string _authServerUrl = "";
    private bool _hasRegisterLink;
    private bool _isLoggingIn;
    private bool _isOAuthMode;
    private bool _isRegisterMode = true;
    private bool _loginModeInitialized;
    private bool? _oauthSupported;

    public PageLoginAuth()
    {
        InitializeComponent();
        _panOAuth = this.FindControl<Grid>("PanOAuth")!;
        _panPasswordLinks = this.FindControl<Grid>("PanPasswordLinks")!;
        _textName = this.FindControl<MyTextBox>("TextName")!;
        _textNameTitle = this.FindControl<TextBlock>("TextNameTitle")!;
        _textPass = this.FindControl<MyPasswordBox>("TextPass")!;
        _textPassTitle = this.FindControl<TextBlock>("TextPassTitle")!;
        _btnBack = this.FindControl<MyButton>("BtnBack")!;
        _btnBackOAuth = this.FindControl<MyTextButton>("BtnBackOAuth")!;
        _btnLogin = this.FindControl<MyButton>("BtnLogin")!;
        _btnOAuth = this.FindControl<MyButton>("BtnOAuth")!;
        _btnLink = this.FindControl<MyTextButton>("BtnLink")!;
        _btnWebsite = this.FindControl<MyTextButton>("BtnWebsite")!;
        _btnUsePassword = this.FindControl<MyTextButton>("BtnUsePassword")!;
        _btnPasswordWebsite = this.FindControl<MyTextButton>("BtnPasswordWebsite")!;
        _btnUseOAuth = this.FindControl<MyTextButton>("BtnUseOAuth")!;

        _btnBack.Click += (_, _) => GoBack();
        _btnBackOAuth.Click += (_, _) => GoBack();
        _btnWebsite.Click += (_, _) => OpenServerWebsite();
        _btnPasswordWebsite.Click += (_, _) => OpenServerWebsite();
        _btnUsePassword.Click += (_, _) => SetLoginMode(false);
        _btnUseOAuth.Click += (_, _) => SetLoginMode(true);
        _btnLink.Click += (_, _) => OpenRegisterAddress();
        _btnLogin.Click += (_, _) => _ = LoginAsync();
        _btnOAuth.Click += (_, _) => _ = LoginAsync();
        _textName.TextChanged += (_, _) => RefreshRegisterButton();

        SetLoginMode(false, false);
        RefreshRegisterButton();

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree +=
            (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    public static string? PendingAuthServer { get; set; }

    public static bool? PendingAuthServerOAuthSupported { get; set; }

    private void OnLanguageChanged()
    {
        RefreshRegisterButton();
        if (!_isLoggingIn)
            _btnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
    }

    public override void Reload()
    {
        if (PendingAuthServer is { } pending)
        {
            PendingAuthServer = null;
            _authServerUrl = pending;
        }

        if (PendingAuthServerOAuthSupported is { } knownSupport)
        {
            PendingAuthServerOAuthSupported = null;
            _oauthSupported = knownSupport;
            UpdateOAuthEntryVisibility();
            SetLoginMode(knownSupport);
        }
        else if (_oauthSupported is null)
        {
            var server = _authServerUrl;
            _ = Task.Run(async () =>
            {
                var supported = await IsOAuthSupportedAsync(server).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!string.Equals(_authServerUrl, server, StringComparison.OrdinalIgnoreCase))
                        return;
                    _oauthSupported = supported;
                    UpdateOAuthEntryVisibility();
                    SetLoginMode(supported);
                });
            });
        }

        if (_isLoggingIn)
            return;
        _btnLogin.IsEnabled = true;
        _btnOAuth.IsEnabled = true;
        _btnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
        RefreshRegisterButton();
    }

    #region 模式切换

    private void SetLoginMode(bool useOAuth, bool animate = true)
    {
        if (_loginModeInitialized && _isOAuthMode == useOAuth)
            return;

        var wasInitialized = _loginModeInitialized;
        _isOAuthMode = useOAuth;
        _loginModeInitialized = true;

        var oldControls = GetModeControls(!useOAuth);
        if (!animate || !wasInitialized)
        {
            ApplyModeVisibility(useOAuth);
            return;
        }

        var animations = new List<AniData>();
        foreach (var control in oldControls)
            if (control.IsVisible)
                animations.Add(AaOpacity(control, -control.Opacity, 90));
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

        _btnLink.IsVisible = !oauthMode && _hasRegisterLink;
        RefreshRegisterButton();
    }

    private void UpdateOAuthEntryVisibility()
    {
        var available = _oauthSupported == true;
        _btnUseOAuth.IsVisible = available;
        Grid.SetColumnSpan(_btnPasswordWebsite, available ? 1 : 3);
    }

    #endregion

    #region 登录

    private async Task LoginAsync()
    {
        if (_isLoggingIn)
            return;

        var serverAddress = _authServerUrl.Trim();
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.EmptyFields")));
            return;
        }

        if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out _))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.InvalidServer")));
            return;
        }

        _isLoggingIn = true;
        ProfileService.IsCreatingProfile = true;
        SetControlsEnabled(false);

        if (_isOAuthMode)
        {
            if (!await TryStartYggdrasilConnectAsync().ConfigureAwait(true))
            {
                HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.LoginFailed")));
                FinishLoginAttempt();
            }

            return;
        }

        var userName = _textName.Text ?? "";
        var password = _textPass.Text ?? "";
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.EmptyFields")));
            FinishLoginAttempt();
            return;
        }

        var loginSucceeded = false;
        try
        {
            await Task.Run(() =>
            {
                var baseUrl = ApiLocation.TryRequestAsync(serverAddress).GetAwaiter().GetResult();
                var loginData = new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
                {
                    BaseUrl = baseUrl,
                    UserName = userName,
                    Password = password,
                    Description = "Authlib-Injector",
                    LoginType = GameLaunch.McLoginType.Auth
                };
                GameLaunch.mcLoginAuthLoader.Start(loginData, true);
                while (GameLaunch.mcLoginAuthLoader.State == LoadState.Loading)
                {
                    var progress = GameLaunch.mcLoginAuthLoader.Progress;
                    LauncherCommon.RunInUi(() => _btnLogin.Text = Lang.Number(progress, "P0"));
                    Thread.Sleep(50);
                }

                switch (GameLaunch.mcLoginAuthLoader.State)
                {
                    case LoadState.Finished:
                        loginSucceeded = true;
                        return;
                    case LoadState.Aborted:
                        throw new ThreadInterruptedException();
                    case LoadState.Waiting:
                    case LoadState.Loading:
                    case LoadState.Failed:
                    default:
                        if (GameLaunch.mcLoginAuthLoader.Error is null)
                            throw new InvalidOperationException(
                                Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                        throw GameLaunch.mcLoginAuthLoader.Error;
                }
            });
        }
        catch (ThreadInterruptedException)
        {
            HintService.Show(Hints.Info(Lang.Text("Launch.Account.Auth.Cancelled")));
        }
        catch (Exception ex)
        {
            ReportLoginError(ex);
        }
        finally
        {
            ProfileService.IsCreatingProfile = false;
            FinishLoginAttempt();
            if (loginSucceeded)
                RequestRefresh();
        }
    }

    private static void ReportLoginError(Exception ex)
    {
        if (ex.Message == "$$")
        {
        }
        else if (ex.Message.StartsWith("$"))
        {
            HintService.Show(
                Hints.Error(
                    Lang.Text(
                        "Launch.Account.Auth.LoginFailed.WithDetail",
                        ex.Message.TrimStart('$'))));
        }
        else
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Launch.Account.Auth.LoginFailed"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Launch.Account.Auth.LoginFailed"));
        }
    }

    private void FinishLoginAttempt()
    {
        ProfileService.IsCreatingProfile = false;
        _isLoggingIn = false;
        SetControlsEnabled(true);
        _btnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
    }

    private void SetControlsEnabled(bool enabled)
    {
        _btnLogin.IsEnabled = enabled;
        _btnOAuth.IsEnabled = enabled;
        _btnBack.IsEnabled = enabled;
        _btnBackOAuth.IsEnabled = enabled;
        _btnUsePassword.IsEnabled = enabled;
        _btnUseOAuth.IsEnabled = enabled;
        _btnWebsite.IsEnabled = enabled;
        _btnPasswordWebsite.IsEnabled = enabled;
    }

    #endregion

    #region Yggdrasil Connect

    internal static async Task<bool> IsOAuthSupportedAsync(string authServerUrl)
    {
        if (Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri)
            && YggdrasilConnectProvider.TryGetBuiltInClientId(serverUri.Host, out _))
            return true;

        try
        {
            var server = await ApiLocation.TryRequestAsync(authServerUrl).ConfigureAwait(false);
            using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(false);
            if (!response.IsSuccess) return false;
            var metadata = (JsonObject)LauncherCommon.GetJson(
                await response.AsStringAsync().ConfigureAwait(false));
            var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                ?? metadata["feature.openid_configuration_url"]?.ToString();
            if (string.IsNullOrWhiteSpace(discovery)) return false;

            using var discoveryResponse =
                await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(false);
            if (!discoveryResponse.IsSuccess) return false;
            var discoveryMetadata = (JsonObject)LauncherCommon.GetJson(
                await discoveryResponse.AsStringAsync().ConfigureAwait(false));
            var clientId = GetClientId(server, discoveryMetadata);
            var deviceEndpoint = discoveryMetadata["device_authorization_endpoint"]?.ToString();
            var tokenEndpoint = discoveryMetadata["token_endpoint"]?.ToString();
            return !string.IsNullOrWhiteSpace(clientId)
                && !string.IsNullOrWhiteSpace(deviceEndpoint)
                && !string.IsNullOrWhiteSpace(tokenEndpoint);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "检测 Yggdrasil Connect 支持失败");
            return false;
        }
    }

    private static string? GetClientId(string authServerUrl, JsonObject discoveryMetadata)
    {
        if (!Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri)) return null;
        return YggdrasilConnectProvider.TryGetBuiltInClientId(
            serverUri.Host,
            out var builtInClientId)
            ? builtInClientId
            : discoveryMetadata["shared_client_id"]?.ToString();
    }

    private async Task<bool> TryStartYggdrasilConnectAsync()
    {
        try
        {
            var server = await ApiLocation.TryRequestAsync(_authServerUrl).ConfigureAwait(true);
            using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(true);
            if (!response.IsSuccess) return false;
            var metadata = (JsonObject)LauncherCommon.GetJson(
                await response.AsStringAsync().ConfigureAwait(true));
            var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                ?? metadata["feature.openid_configuration_url"]?.ToString();
            if (string.IsNullOrWhiteSpace(discovery)) return false;

            using var discoveryResponse =
                await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(true);
            if (!discoveryResponse.IsSuccess) return false;
            var discoveryMetadata = (JsonObject)LauncherCommon.GetJson(
                await discoveryResponse.AsStringAsync().ConfigureAwait(true));
            var clientId = GetClientId(server, discoveryMetadata);
            if (string.IsNullOrWhiteSpace(clientId)) return false;

            _ = CompleteYggdrasilConnectLoginAsync(server, discovery, clientId);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "启动 Yggdrasil Connect 登录失败");
            return false;
        }
    }

    private async Task CompleteYggdrasilConnectLoginAsync(
        string server,
        string discovery,
        string clientId)
    {
        try
        {
            var profile = await ProfileService
                .AuthenticateAsync(
                    ProfileType.YggdrasilConnect,
                    new AuthenticationRequest
                    {
                        Server = server,
                        DiscoveryAddress = discovery,
                        ClientId = clientId,
                        DeviceCodeHandler = LoginProfile.ShowDeviceCodeLoginAsync,
                        ProfileSelector = (candidates, _) =>
                            Task.FromResult(SelectYggdrasilProfile(candidates))
                    },
                    null,
                    true,
                    CancellationToken.None)
                .ConfigureAwait(false);
            LogWrapper.Info("Launch", "Yggdrasil Connect 登录成功：" + profile.UserName);
            await Dispatcher.UIThread.InvokeAsync(RequestRefresh);
        }
        catch (ThreadInterruptedException)
        {
            HintService.Show(Hints.Info(Lang.Text("Launch.Account.Auth.Cancelled")));
        }
        catch (Exception ex)
        {
            ReportLoginError(ex);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(FinishLoginAttempt);
        }
    }

    private static AuthenticationCandidate? SelectYggdrasilProfile(
        IReadOnlyList<AuthenticationCandidate> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        AuthenticationCandidate? selected = null;
        LauncherCommon.RunInUiWait(() =>
        {
            var result = DialogService.Instance.Show(
                new SelectDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Auth.ChangeProfile"),
                    Options = candidates
                        .Select(candidate => new DialogOption
                        {
                            Text = candidate.Name
                        })
                        .ToList(),
                    Buttons = DialogButtons.Ok,
                    HighLight = false,
                    ForceWait = true
                });
            var index = result.Result?.SelectedIndex;
            if (index is >= 0 && index < candidates.Count)
                selected = candidates[index.Value];
        });
        return selected;
    }

    #endregion

    #region 链接与退出

    private void GoBack()
    {
        ProfileService.IsCreatingProfile = false;
        _authServerUrl = "";
        _textName.Text = "";
        _textPass.Text = "";
        RequestBack();
    }

    private void OpenServerWebsite()
    {
        if (!Uri.TryCreate(_authServerUrl, UriKind.Absolute, out var uri))
            return;
        var root = new UriBuilder(uri)
        {
            Path = "/",
            Query = "",
            Fragment = ""
        }.Uri;
        Basics.OpenPath(root.ToString());
    }

    private void OpenRegisterAddress()
    {
        var address = GetRegisterAddress();
        if (string.IsNullOrWhiteSpace(address))
            return;
        Basics.OpenPath(
            _isRegisterMode ? address : address.Replace("/auth/register", "/auth/forgot"));
    }

    private static string GetRegisterAddress()
    {
        if (InstanceList.McMcInstanceSelected is not { } instance)
            return "";
        try
        {
            return Config.InstanceAuth.AuthRegisterAddress[instance.PathInstance];
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "读取实例注册地址失败");
            return "";
        }
    }

    private void RefreshRegisterButton()
    {
        var address = GetRegisterAddress();
        _hasRegisterLink = Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        _isRegisterMode = string.IsNullOrEmpty(_textName.Text);
        _btnLink.Text = Lang.Text(
            _isRegisterMode
                ? "Launch.Account.Auth.Register"
                : "Launch.Account.Auth.ForgotPassword");
        _btnLink.IsVisible = !_isOAuthMode && _hasRegisterLink;
    }

    #endregion
}