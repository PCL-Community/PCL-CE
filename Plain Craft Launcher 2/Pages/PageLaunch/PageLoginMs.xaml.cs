using System.Security.Authentication;
using System.Windows;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageLoginMs
{
    public PageLoginMs()
    {
        // Handles
        InitializeComponent();
        BtnBack.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        UiThread.Post(() => ModMain.frmLaunchLeft.RefreshPage(true));
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        BtnLogin.IsEnabled = false;
        BtnBack.Visibility = Visibility.Collapsed;
        BtnLogin.Text = Lang.Number(0d, "P0");
        PCL.Core.App.Basics.RunInNewThread(() =>
        {
            try
            {
                ModProfile.selectedProfile = null;
                ModLaunch.mcLoginMsLoader.Start(ModProfile.GetLoginData(ModLaunch.McLoginType.Ms), true);
                while (ModLaunch.mcLoginMsLoader.State == LoadState.Loading)
                {
                    UiThread.Post(() => BtnLogin.Text = Lang.Number(ModLaunch.mcLoginMsLoader.Progress, "P0"));
                    Thread.Sleep(50);
                }

                if (ModLaunch.mcLoginMsLoader.State == LoadState.Finished)
                    UiThread.Post(() => ModMain.frmLaunchLeft.RefreshPage(true));
                else if (ModLaunch.mcLoginMsLoader.State == LoadState.Aborted)
                    throw new ThreadInterruptedException();
                else if (ModLaunch.mcLoginMsLoader.Error is null)
                    throw new Exception(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                else
                    throw new Exception(ModLaunch.mcLoginMsLoader.Error.Message, ModLaunch.mcLoginMsLoader.Error);
            }
            catch (ThreadInterruptedException ex)
            {
                HintService.Hint(Lang.Text("Launch.Account.LoginCancelled"));
            }
            catch (Exception ex)
            {
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
                    LauncherLog.Log(
                        ex,
                        $"{Lang.Text("Launch.Account.Microsoft.LoginFailed.Message")}\r\n{ex.Message}",
                        LauncherLogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.Error.OperationFailed"));
                }
                else
                {
                    LauncherLog.Log(
                        ex,
                        Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"),
                        LauncherLogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"));
                }
            }
            finally
            {
                UiThread.Post(() =>
                {
                    BtnLogin.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                    BtnLogin.Text = Lang.Text("Launch.Account.Login");
                });
            }
        }, "Ms Login");
    }
}
