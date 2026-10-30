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
        ModBase.RunInUi(() => ModMain.frmLaunchLeft?.RefreshPage());
    }

    [RelayCommand]
    public void Login()
    {
        IsLoginEnabled = false;
        IsBackVisible = false;
        LoginText = Lang.Number(0d, "P0");
        ModBase.RunInNewThread(() =>
        {
            var previousProfile = ProfileService.Current;
            var loginSucceeded = false;
            try
            {
                ProfileService.Select(null);
                ModLaunch.mcLoginMsLoader.Start(ProfileUi.GetLoginData(ModLaunch.McLoginType.Ms), true);
                while (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Loading)
                {
                    ModBase.RunInUi(() => LoginText = Lang.Number(ModLaunch.mcLoginMsLoader.Progress, "P0"));
                    Thread.Sleep(50);
                }

                if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Finished)
                {
                    loginSucceeded = true;
                    ModBase.RunInUi(() => ModMain.frmLaunchLeft?.RefreshPage());
                }
                else if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Aborted)
                {
                    throw new ThreadInterruptedException();
                }
                else if (ModLaunch.mcLoginMsLoader.Error is null)
                {
                    throw new InvalidOperationException(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                }
                else
                {
                    throw ModLaunch.mcLoginMsLoader.Error;
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
                    ModBase.Log(
                        ex,
                        $"{Lang.Text("Launch.Account.Microsoft.LoginFailed.Message")}\r\n{ex.Message}",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.Error.OperationFailed"));
                }
                else
                {
                    ModBase.Log(
                        ex,
                        Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"),
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"));
                }
            }
            finally
            {
                ProfileService.IsCreatingProfile = false;
                ModBase.RunInUi(() =>
                {
                    IsLoginEnabled = true;
                    IsBackVisible = true;
                    LoginText = Lang.Text("Launch.Account.Login");
                });
            }
        }, "Ms Login");
    }
}
