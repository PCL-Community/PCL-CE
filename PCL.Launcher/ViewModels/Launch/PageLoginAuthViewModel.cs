using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>第三方（Authlib-Injector）登录页面的视图模型。</summary>
public partial class PageLoginAuthViewModel : ViewModelBase
{
    private string _authServerUrl = string.Empty;
    private bool _hasLoginProgress;
    private bool _hasRegisterLink;
    private bool _isLoggingIn;
    private bool _isRegisterMode = true;
    private double _loginProgress;

    [ObservableProperty] public partial bool ControlsEnabled { get; set; } = true;

    [ObservableProperty] public partial bool IsOAuthMode { get; set; }

    public string LoginButtonText =>
        _isLoggingIn && _hasLoginProgress
            ? Lang.Number(_loginProgress, "P0")
            : Lang.Text("Launch.Account.Auth.Login");

    [ObservableProperty] public partial string Password { get; set; } = string.Empty;

    public string RegisterLinkContent =>
        Lang.Text(
            _isRegisterMode
                ? "Launch.Account.Auth.Register"
                : "Launch.Account.Auth.ForgotPassword");

    [ObservableProperty] public partial bool RegisterLinkVisible { get; set; }

    [ObservableProperty] public partial bool UseOAuthVisible { get; set; }

    [ObservableProperty] public partial string UserName { get; set; } = string.Empty;

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        private set
        {
            if (_isLoggingIn == value)
                return;
            _isLoggingIn = value;
            if (!value)
                _hasLoginProgress = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoginButtonText));
        }
    }

    public bool IsLoginModeInitialized { get; private set; }

    /// <summary>请求返回上一层登录页。</summary>
    public event Action? BackRequested;

    /// <summary>登录成功或档案状态变化后请求启动页刷新。</summary>
    public event Action? RefreshRequested;

    partial void OnUserNameChanged(string value)
    {
        RefreshRegisterState();
    }

    /// <summary>内置 OAuth 服务器清单内的主机。不访问网络。</summary>
    internal static bool IsListedOAuthServer(string? server)
    {
        return Uri.TryCreate(server, UriKind.Absolute, out var uri)
            && YggdrasilConnectProvider.TryGetBuiltInClientId(uri.Host, out _);
    }

    /// <summary>处理拖入或选中的认证服务器，并返回是否应直接进入 OAuth。</summary>
    public bool Reload(string? pendingServer)
    {
        if (pendingServer is not null)
            _authServerUrl = pendingServer;

        UseOAuthVisible = IsListedOAuthServer(_authServerUrl);
        if (IsLoggingIn)
            return IsOAuthMode;

        ControlsEnabled = true;
        RefreshRegisterState();
        return UseOAuthVisible;
    }

    /// <summary>切换密码 / OAuth 模式；返回是否需要由页面执行切换动画。</summary>
    public bool SetLoginMode(bool useOAuth)
    {
        if (IsLoginModeInitialized && IsOAuthMode == useOAuth)
            return false;

        var wasInitialized = IsLoginModeInitialized;
        IsOAuthMode = useOAuth;
        IsLoginModeInitialized = true;
        RefreshRegisterState();
        return wasInitialized;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoggingIn)
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

        IsLoggingIn = true;
        ProfileService.IsCreatingProfile = true;
        ControlsEnabled = false;

        if (IsOAuthMode)
        {
            if (!await TryStartYggdrasilConnectAsync().ConfigureAwait(true))
            {
                HintService.Show(Hints.Error(Lang.Text("Launch.Account.Auth.LoginFailed")));
                FinishLoginAttempt();
            }

            return;
        }

        var userName = UserName;
        var password = Password;
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
                    LauncherCommon.RunInUi(() => UpdateLoginProgress(progress));
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
                RefreshRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void Back()
    {
        ProfileService.IsCreatingProfile = false;
        _authServerUrl = "";
        UserName = "";
        Password = "";
        BackRequested?.Invoke();
    }

    [RelayCommand]
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

    [RelayCommand]
    private void OpenRegisterAddress()
    {
        var address = GetRegisterAddress();
        if (string.IsNullOrWhiteSpace(address))
            return;
        Basics.OpenPath(
            _isRegisterMode ? address : address.Replace("/auth/register", "/auth/forgot"));
    }

    private void FinishLoginAttempt()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(FinishLoginAttempt);
            return;
        }

        ProfileService.IsCreatingProfile = false;
        IsLoggingIn = false;
        ControlsEnabled = true;
    }

    private void UpdateLoginProgress(double progress)
    {
        _loginProgress = progress;
        _hasLoginProgress = true;
        OnPropertyChanged(nameof(LoginButtonText));
    }

    private static void ReportLoginError(Exception ex)
    {
        if (ex.Message == "$$")
        {
        }
        else if (ex.Message.StartsWith('$'))
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

    #region Yggdrasil Connect

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
                            SelectYggdrasilProfileAsync(candidates)
                    },
                    null,
                    true,
                    CancellationToken.None)
                .ConfigureAwait(false);
            LogWrapper.Info("Launch", "Yggdrasil Connect 登录成功：" + profile.UserName);
            await Dispatcher.UIThread.InvokeAsync(() => RefreshRequested?.Invoke());
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

    private static async Task<AuthenticationCandidate?> SelectYggdrasilProfileAsync(
        IReadOnlyList<AuthenticationCandidate> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        var result = await DialogService.Instance.ShowAsync(
            new SelectDialogRequest
            {
                Title = Lang.Text("Launch.Account.Auth.ChangeProfile"),
                Options =
                [
                    .. candidates
                        .Select(candidate => new DialogOption
                        {
                            Text = candidate.Name
                        })
                ],
                Buttons = DialogButtons.Ok,
                HighLight = false
            });
        var index = result.SelectedIndex;
        return index is >= 0 && index < candidates.Count ? candidates[index.Value] : null;
    }

    #endregion

    #region 注册与找回密码

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

    private void RefreshRegisterState()
    {
        var address = GetRegisterAddress();
        _hasRegisterLink = Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        _isRegisterMode = string.IsNullOrEmpty(UserName);
        OnPropertyChanged(nameof(RegisterLinkContent));
        RegisterLinkVisible = !IsOAuthMode && _hasRegisterLink;
    }

    #endregion
}