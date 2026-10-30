using System;
using System.Security.Authentication;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>微软登录页面的视图模型。</summary>
public partial class PageLoginMsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isLoginEnabled = true;

    [ObservableProperty] private bool _isBackVisible = true;

    [ObservableProperty] private string _loginText = Lang.Text("Launch.Account.Microsoft.Start");

    public void ResetLoginState()
    {
        IsLoginEnabled = true;
        IsBackVisible = true;
        LoginText = Lang.Text("Launch.Account.Microsoft.Start");
    }

    [RelayCommand]
    public void Back()
    {
        ProfileService.IsCreatingProfile = false;
        Common.RunInUi(() => Launcher.frmLaunchLeft?.RefreshPage(true));
    }

    [RelayCommand]
    public void Login()
    {
        IsLoginEnabled = false;
        IsBackVisible = false;
        LoginText = Lang.Number(0d, "P0");
        Common.RunInNewThread(() =>
        {
            var previousProfile = ProfileService.Current;
            var loginSucceeded = false;
            try
            {
                ProfileService.Select(null);
                GameLaunch.mcLoginMsLoader.Start(ProfileUi.GetLoginData(GameLaunch.McLoginType.Ms), true);
                while (GameLaunch.mcLoginMsLoader.State == Common.LoadState.Loading)
                {
                    Common.RunInUi(() => LoginText = Lang.Number(GameLaunch.mcLoginMsLoader.Progress, "P0"));
                    Thread.Sleep(50);
                }

                if (GameLaunch.mcLoginMsLoader.State == Common.LoadState.Finished)
                {
                    loginSucceeded = true;
                    Common.RunInUi(() => Launcher.frmLaunchLeft?.RefreshPage(true));
                }
                else if (GameLaunch.mcLoginMsLoader.State == Common.LoadState.Aborted)
                {
                    throw new ThreadInterruptedException();
                }
                else if (GameLaunch.mcLoginMsLoader.Error is null)
                {
                    throw new InvalidOperationException(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                }
                else
                {
                    throw GameLaunch.mcLoginMsLoader.Error;
                }
            }
            catch (ThreadInterruptedException)
            {
                if (!loginSucceeded && previousProfile is not null) ProfileService.Select(previousProfile);
                HintService.Hint(Lang.Text("Launch.Account.LoginCancelled"));
            }
            catch (Exception ex)
            {
                if (!loginSucceeded && previousProfile is not null) ProfileService.Select(previousProfile);
                if (ex.Message == "$$")
                {
                }
                else if (ex.Message.StartsWith("$"))
                {
                    HintService.Hint(
                        Lang.Text("Launch.Account.Microsoft.LoginFailed.WithDetail", ex.Message.TrimStart('$')),
                        HintType.Error);
                }
                else if (ex is AuthenticationException && ex.Message.ContainsF("SSL/TLS"))
                {
                    Common.Log(
                        ex,
                        $"{Lang.Text("Launch.Account.Microsoft.LoginFailed.Message")}\r\n{ex.Message}",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.Error.OperationFailed"));
                }
                else
                {
                    Common.Log(
                        ex,
                        Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"),
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"));
                }
            }
            finally
            {
                ProfileService.IsCreatingProfile = false;
                Common.RunInUi(() =>
                {
                    IsLoginEnabled = true;
                    IsBackVisible = true;
                    LoginText = Lang.Text("Launch.Account.Login");
                });
            }
        }, "Ms Login");
    }
}
