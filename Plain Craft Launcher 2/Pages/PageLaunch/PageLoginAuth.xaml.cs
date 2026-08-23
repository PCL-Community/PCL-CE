using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Yggdrasil;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class PageLoginAuth
{
    public static string draggedAuthServer;
    public static bool? draggedAuthServerOAuthSupported;

    // 预设服务器
    internal const string DefaultAuthServer = "https://littleskin.cn/api/yggdrasil";

    internal static readonly IReadOnlyDictionary<string, string> PredefinedAuthServers = new Dictionary<string, string>
    {
        { Lang.Text("Launch.Account.Auth.Preset.LittleSkin"), DefaultAuthServer },
        { Lang.Text("Common.Option.Customize"), "" }
    };

    private bool _isRegisterMode = true;
    private bool _isOAuthMode;
    private bool _hasRegisterLink;
    private bool _loginModeInitialized;
    private string _authServerUrl = "";

    public PageLoginAuth()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        Loaded += (_, _) => ReloadRegisterButton();
        // Handles
        BtnBack.Click += BtnBack_Click;
        BtnBackOAuth.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
        BtnOAuth.Click += BtnLogin_Click;
        BtnUsePassword.Click += (_, _) => _SetLoginMode(false);
        BtnUseOAuth.Click += (_, _) => _SetLoginMode(true);
        BtnLink.Click += Btn_Click;
    }

    private void Reload()
    {
        _authServerUrl = draggedAuthServer ?? "";
        TextServerName.Text = string.IsNullOrWhiteSpace(_authServerUrl)
            ? ""
            : Lang.Text("Launch.Account.Auth.ServerLabel", _authServerUrl);
        TextServerName.Visibility = string.IsNullOrWhiteSpace(_authServerUrl) ? Visibility.Hidden : Visibility.Visible;
        var knownOAuthSupport = draggedAuthServerOAuthSupported;
        draggedAuthServer = null;
        draggedAuthServerOAuthSupported = null;
        _SetLoginMode(knownOAuthSupport == true);
        if (knownOAuthSupport is null && !string.IsNullOrWhiteSpace(_authServerUrl))
        {
            var server = _authServerUrl;
            Dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                var supported = await IsOAuthSupportedAsync(server).ConfigureAwait(true);
                if (string.Equals(_authServerUrl, server, StringComparison.OrdinalIgnoreCase))
                    _SetLoginMode(supported);
            }));
        }
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ProfileService.IsCreatingProfile = false;
        _authServerUrl = "";
        TextServerName.Text = "";
        TextName.Text = null;
        TextPass.Password = null;
        ModMain.frmLaunchLeft.RefreshPage(true);
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_authServerUrl))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
            return;
        }

        if (!_authServerUrl.IsMatch(RegexPatterns.HttpUri))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnOAuth.IsEnabled = false;
        BtnBack.IsEnabled = false;
        BtnBackOAuth.IsEnabled = false;
        BtnUsePassword.IsEnabled = false;
        BtnUseOAuth.IsEnabled = false;
        var loginData = new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
        {
            BaseUrl = _authServerUrl.EndsWithF("/") ? $"{_authServerUrl}authserver" : $"{_authServerUrl}/authserver",
            UserName = TextName.Text, Password = TextPass.Password, Description = "Authlib-Injector",
            LoginType = ModLaunch.McLoginType.Auth
        };
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            var keepControlsDisabled = false;
            try
            {
                ProfileService.IsCreatingProfile = true;
                if (_isOAuthMode)
                {
                    if (await _TryStartYggdrasilConnectAsync().ConfigureAwait(true))
                    {
                        keepControlsDisabled = true;
                        return;
                    }
                    _SetLoginMode(false);
                    HintService.Hint(Lang.Text("Launch.Account.Auth.OAuthUnsupported"), HintType.Info);
                }
                if (string.IsNullOrWhiteSpace(TextName.Text) || string.IsNullOrWhiteSpace(TextPass.Password))
                {
                    HintService.Hint(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
                    return;
                }
                ModLaunch.mcLoginAuthLoader.Start(loginData, true);
                while (ModLaunch.mcLoginAuthLoader.State == ModBase.LoadState.Loading)
                {
                    BtnLogin.Text = Lang.Number(ModLaunch.mcLoginAuthLoader.Progress, "P0");
                    await Task.Delay(50);
                }

                switch (ModLaunch.mcLoginAuthLoader.State)
                {
                    case ModBase.LoadState.Finished:
                        ModMain.frmLaunchLeft.RefreshPage(true);
                        break;
                    case ModBase.LoadState.Aborted:
                        HintService.Hint(Lang.Text("Launch.Account.Auth.Cancelled"));
                        break;
                    case ModBase.LoadState.Waiting:
                    case ModBase.LoadState.Loading:
                    case ModBase.LoadState.Failed:
                    default:
                    {
                        if (ModLaunch.mcLoginAuthLoader.Error is null)
                            throw new Exception(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                        throw new Exception(ModLaunch.mcLoginAuthLoader.Error.Message,
                            ModLaunch.mcLoginAuthLoader.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "$$")
                {
                }
                else if (ex.Message.StartsWith("$"))
                {
                    HintService.Hint(
                        Lang.Text("Launch.Account.Auth.LoginFailed.WithDetail",ex.Message.TrimStart('$')),
                        HintType.Error);
                }
                else
                {
                    ModBase.Log(
                        ex,
                        Lang.Text("Launch.Account.Auth.LoginFailed"),
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Auth.LoginFailed"));
                }
            }
            finally
            {
                if (!keepControlsDisabled) _FinishLoginAttempt();
            }
        }));
    }

    private void _FinishLoginAttempt()
    {
        ProfileService.IsCreatingProfile = false;
        BtnLogin.IsEnabled = true;
        BtnOAuth.IsEnabled = true;
        BtnBack.IsEnabled = true;
        BtnBackOAuth.IsEnabled = true;
        BtnUsePassword.IsEnabled = true;
        BtnUseOAuth.IsEnabled = true;
        BtnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
    }

    private void _SetLoginMode(bool useOAuth)
    {
        if (_loginModeInitialized && _isOAuthMode == useOAuth)
            return;

        var wasOAuth = _loginModeInitialized && _isOAuthMode;
        _isOAuthMode = useOAuth;
        _loginModeInitialized = true;

        var oldElements = wasOAuth
            ? new FrameworkElement[] { PanOAuth, BtnUsePassword, BtnBackOAuth }
            : new FrameworkElement[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, BtnUseOAuth, BtnBack };
        var newElements = useOAuth
            ? new FrameworkElement[] { PanOAuth, BtnUsePassword, BtnBackOAuth }
            : new FrameworkElement[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, BtnUseOAuth, BtnBack };

        var animations = new List<ModAnimation.AniData>();
        if (wasOAuth || !_loginModeInitialized)
        {
            foreach (var element in oldElements)
            {
                if (element.Visibility == Visibility.Visible)
                    animations.Add(ModAnimation.AaOpacity(element, -element.Opacity, 90));
            }
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (var element in oldElements)
            {
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 1d;
            }

            foreach (var element in newElements)
            {
                element.Visibility = ReferenceEquals(element, BtnLink) && !_hasRegisterLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                element.Opacity = 0d;
            }
        }, after: true));

        foreach (var element in newElements)
        {
            var targetOpacity = ReferenceEquals(element, BtnBackOAuth) ? 0.3d : 1d;
            animations.Add(ModAnimation.AaOpacity(element, targetOpacity, 130, 20,
                new ModAnimation.AniEaseInFluent()));
        }

        ModAnimation.AniStart(animations, "Profile Authentication Mode");
    }

    private async Task<bool> _TryStartYggdrasilConnectAsync()
    {
        var server = await ApiLocation.TryRequestAsync(_authServerUrl).ConfigureAwait(true);
        using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(true);
        if (!response.IsSuccess) return false;
        var metadata = (JsonObject)ModBase.GetJson(await response.AsStringAsync().ConfigureAwait(true));
        var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                        ?? metadata["feature.openid_configuration_url"]?.ToString();
        if (string.IsNullOrWhiteSpace(discovery)) return false;

        using var discoveryResponse = await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(true);
        if (!discoveryResponse.IsSuccess) return false;
        var discoveryMetadata = (JsonObject)ModBase.GetJson(await discoveryResponse.AsStringAsync().ConfigureAwait(true));
        var clientId = _GetClientId(server, discoveryMetadata);
        if (string.IsNullOrWhiteSpace(clientId)) return false;

        var provider = new YggdrasilConnectProvider(discovery, clientId,
            yggdrasilServer: server);
        ModBase.Log($"[Profile] Yggdrasil Connect client id resolved for {new Uri(server).Host}", ModBase.LogLevel.Debug);
        await provider.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var device = await provider.GetCodePairAsync(CancellationToken.None).ConfigureAwait(true)
                     ?? throw new InvalidOperationException("Yggdrasil Connect 未返回设备授权码。");
        if (device.IsError)
            throw new InvalidOperationException(device.ErrorDescription ?? device.Error ?? "Yggdrasil Connect 设备授权失败。");

        var converter = new ModMain.MyMsgBoxConverter
        {
            Content = JsonSerializer.SerializeToNode(device, JsonCompat.SerializerOptions)!.AsObject(),
            ForceWait = true,
            Type = ModMain.MyMsgBoxType.Login,
            DeviceCodePoll = (content, token) =>
            {
                var code = content.ToObject<DeviceCodeData>() ?? throw new InvalidOperationException("设备授权数据无效。");
                return provider.PollDeviceCodeAsync(code, token);
            },
            LoginResultHandler = async (oauth, token) =>
            {
                var result = await provider.AuthenticateAsync(new AuthenticationRequest
                {
                    OAuthResult = oauth,
                    ProfileSelector = (candidates, _) => Task.FromResult(_SelectYggdrasilProfile(candidates))
                }, token)
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var profile = ProfileService.ApplyAuthenticationResult(result);
                ModBase.RunInUi(() =>
                {
                    ModMain.frmLaunchLeft.RefreshPage(true);
                    HintService.Hint(Lang.Text("Launch.Account.Profile.Created"), HintType.Success);
                });
                ProfileUi.ProfileLog("Yggdrasil Connect 登录成功：" + profile.UserName);
            },
            CompletionHandler = result => ModBase.RunInUi(() =>
            {
                _FinishLoginAttempt();
                if (result is ThreadInterruptedException)
                    HintService.Hint(Lang.Text("Launch.Account.Auth.Cancelled"));
                else if (result is Exception ex)
                    ModBase.Log(ex, Lang.Text("Launch.Account.Auth.LoginFailed"), ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Auth.LoginFailed"));
            })
        };
        ModMain.WaitingMyMsgBox.Add(converter);
        return true;
    }

    private static AuthenticationCandidate? _SelectYggdrasilProfile(IReadOnlyList<AuthenticationCandidate> candidates)
    {
        if (candidates.Count == 0) return null;
        var selected = candidates[0];
        ModBase.RunInUiWait(() =>
        {
            if (candidates.Count == 1) return;
            var controls = candidates.Select(candidate => (IMyRadio)new MyRadioBox { Text = candidate.Name }).ToList();
            var index = ModMain.MyMsgBoxSelect(controls, Lang.Text("Launch.Account.Auth.ChangeProfile"));
            if (index is >= 0 and < 100000) selected = candidates[index.Value];
        });
        return selected;
    }

    internal static async Task<bool> IsOAuthSupportedAsync(string authServerUrl)
    {
        try
        {
            var server = await ApiLocation.TryRequestAsync(authServerUrl).ConfigureAwait(false);
            using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(false);
            if (!response.IsSuccess) return false;
            var metadata = (JsonObject)ModBase.GetJson(await response.AsStringAsync().ConfigureAwait(false));
            var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                            ?? metadata["feature.openid_configuration_url"]?.ToString();
            if (string.IsNullOrWhiteSpace(discovery)) return false;

            using var discoveryResponse = await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(false);
            if (!discoveryResponse.IsSuccess) return false;
            var discoveryMetadata = (JsonObject)ModBase.GetJson(await discoveryResponse.AsStringAsync().ConfigureAwait(false));
            var clientId = _GetClientId(server, discoveryMetadata);
            var deviceEndpoint = discoveryMetadata["device_authorization_endpoint"]?.ToString();
            var tokenEndpoint = discoveryMetadata["token_endpoint"]?.ToString();
            return !string.IsNullOrWhiteSpace(clientId) &&
                   !string.IsNullOrWhiteSpace(deviceEndpoint) &&
                   !string.IsNullOrWhiteSpace(tokenEndpoint);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检测 Yggdrasil Connect 支持失败", ModBase.LogLevel.Debug);
            return false;
        }
    }

    private static string? _GetClientId(string authServerUrl, JsonObject discoveryMetadata)
    {
        if (!Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri)) return null;
        if (YggdrasilConnectProvider.TryGetBuiltInClientId(serverUri.Host, out var builtInClientId))
            return builtInClientId;
        return discoveryMetadata["shared_client_id"]?.ToString();
    }

    // 链接处理
    private void ComboName_TextChanged(object sender, TextChangedEventArgs e)
    {
        _isRegisterMode = string.IsNullOrEmpty(TextName.Text);
        BtnLink.Content = _isRegisterMode
            ? Lang.Text("Launch.Account.Auth.Register")
            : Lang.Text("Launch.Account.Auth.ForgotPassword");
    }

    private void Btn_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite(_isRegisterMode
            ? Config.InstanceAuth.AuthRegisterAddress.ToString()
            : Config.InstanceAuth.AuthRegisterAddress.ToString().Replace("/auth/register", "/auth/forgot"));
    }

    // 切换注册按钮可见性
    private void ReloadRegisterButton()
    {
        var address = Config.InstanceAuth.AuthRegisterAddress.ToString();
        _hasRegisterLink = new HttpValidator().Validate(address).IsValid;
        BtnLink.Visibility = !_isOAuthMode && _hasRegisterLink ? Visibility.Visible : Visibility.Collapsed;
    }

}
