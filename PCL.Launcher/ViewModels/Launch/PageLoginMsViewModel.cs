using System.Security.Authentication;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Launch;

namespace PCL;

/// <summary>微软登录页面的视图模型：设备码登录状态与登录命令。</summary>
public partial class PageLoginMsViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool IsLoginEnabled { get; set; } = true;

    [ObservableProperty] public partial bool IsBackVisible { get; set; } = true;

    [ObservableProperty]
    public partial string LoginText { get; set; } = Lang.Text("Launch.Account.Microsoft.Start");

    public bool IsLoggingIn { get; private set; }

    /// <summary>请求返回上一层登录页。</summary>
    public event Action? BackRequested;

    /// <summary>登录成功后请求启动页刷新。</summary>
    public event Action? RefreshRequested;

    public void ResetLoginState()
    {
        if (IsLoggingIn)
            return;
        IsLoginEnabled = true;
        IsBackVisible = true;
        LoginText = Lang.Text("Launch.Account.Microsoft.Start");
    }

    [RelayCommand]
    private void Back()
    {
        ProfileService.IsCreatingProfile = false;
        BackRequested?.Invoke();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoggingIn)
            return;
        IsLoggingIn = true;
        IsLoginEnabled = false;
        IsBackVisible = false;

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
                        LauncherCommon.RunInUi(() => LoginText = Lang.Number(progress, "P0"));
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
            else if (ex.Message.StartsWith('$'))
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
            IsLoggingIn = false;
            IsLoginEnabled = true;
            IsBackVisible = true;
            LoginText = Lang.Text("Launch.Account.Login");
            if (loginSucceeded)
                RefreshRequested?.Invoke();
            else
                ResetLoginState();
        }
    }
}