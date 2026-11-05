using System.Security.Authentication;
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     正版（微软）登录视图：开始设备码登录，显示登录进度，成功后由启动页左栏切换到账号视图。
///     设备码弹窗由 <see cref="LoginProfile.ShowDeviceCodeLoginAsync" /> 的界面实现提供。
/// </summary>
public partial class PageLoginMs : StackPanel, ILoginPage, ILoginPage_Bindable
{
    private readonly MyTextButton _btnBack;
    private readonly MyButton _btnLogin;
    private bool _isLoggingIn;

    public PageLoginMs()
    {
        InitializeComponent();
        _btnLogin = this.FindControl<MyButton>("BtnLogin")!;
        _btnBack = this.FindControl<MyTextButton>("BtnBack")!;
        this.FindControl<MyTextButton>("BtnPurchase")!.Click += (_, _) =>
            LauncherUi.Interaction?.OpenWebsite(
                "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        this.FindControl<MyTextButton>("BtnWebsite")!.Click += (_, _) =>
            LauncherUi.Interaction?.OpenWebsite("https://www.minecraft.net/zh-hans");
        _btnBack.Click += (_, _) =>
        {
            ProfileService.IsCreatingProfile = false;
            RequestBack();
        };
        _btnLogin.Click += (_, _) => _ = LoginAsync();
        ResetLoginState();

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree +=
            (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    public void Reload()
    {
        ResetLoginState();
    }

    public event Action? BackRequested;

    public event Action<GameLaunch.McLoginType>? LoginTypeRequested;

    public event Action? RefreshRequested;

    private void OnLanguageChanged()
    {
        if (!_isLoggingIn)
            ResetLoginState();
    }

    private void RequestBack()
    {
        BackRequested?.Invoke();
    }

    private void RequestRefresh()
    {
        RefreshRequested?.Invoke();
    }

    private void ResetLoginState()
    {
        if (_isLoggingIn)
            return;
        _btnLogin.IsEnabled = true;
        _btnBack.IsVisible = true;
        _btnLogin.Text = Lang.Text("Launch.Account.Microsoft.Start");
    }

    private async Task LoginAsync()
    {
        if (_isLoggingIn)
            return;
        _isLoggingIn = true;
        _btnLogin.IsEnabled = false;
        _btnBack.IsVisible = false;

        var previousProfile = ProfileService.Current;
        var loginSucceeded = false;
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    ProfileService.Select(null);
                    GameLaunch.mcLoginMsLoader.Start(
                        LaunchLoginHelper.GetLoginData(GameLaunch.McLoginType.Ms),
                        true);
                    while (GameLaunch.mcLoginMsLoader.State == LoadState.Loading)
                    {
                        var progress = GameLaunch.mcLoginMsLoader.Progress;
                        LauncherCommon.RunInUi(() => _btnLogin.Text = Lang.Number(progress, "P0"));
                        Thread.Sleep(50);
                    }

                    if (GameLaunch.mcLoginMsLoader.State == LoadState.Finished)
                    {
                        loginSucceeded = true;
                        return;
                    }

                    if (GameLaunch.mcLoginMsLoader.State == LoadState.Aborted)
                        throw new ThreadInterruptedException();
                    if (GameLaunch.mcLoginMsLoader.Error is null)
                        throw new InvalidOperationException(
                            Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                    throw GameLaunch.mcLoginMsLoader.Error;
                }
                finally
                {
                    ProfileService.IsCreatingProfile = false;
                }
            });
        }
        catch (ThreadInterruptedException)
        {
            if (!loginSucceeded && previousProfile is not null)
                ProfileService.Select(previousProfile);
            HintService.Show(Hints.Info(Lang.Text("Launch.Account.LoginCancelled")));
        }
        catch (Exception ex)
        {
            if (!loginSucceeded && previousProfile is not null)
                ProfileService.Select(previousProfile);
            if (ex.Message == "$$")
            {
            }
            else if (ex.Message.StartsWith("$"))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Launch.Account.Microsoft.LoginFailed.WithDetail",
                            ex.Message.TrimStart('$'))));
            }
            else if (ex is AuthenticationException && ex.Message.Contains("SSL/TLS"))
            {
                LauncherCommon.Log(
                    ex,
                    $"{Lang.Text("Launch.Account.Microsoft.LoginFailed.Message")}\r\n{ex.Message}",
                    LauncherLogLevel.Msgbox,
                    userSummary: Lang.Text("Launch.Account.Microsoft.Error.OperationFailed"));
            }
            else
            {
                LauncherCommon.Log(
                    ex,
                    Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"),
                    LauncherLogLevel.Msgbox,
                    userSummary: Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"));
            }
        }
        finally
        {
            _isLoggingIn = false;
            _btnLogin.IsEnabled = true;
            _btnBack.IsVisible = true;
            _btnLogin.Text = Lang.Text("Launch.Account.Login");
            if (loginSucceeded)
                RequestRefresh();
            else
                ResetLoginState();
        }
    }
}