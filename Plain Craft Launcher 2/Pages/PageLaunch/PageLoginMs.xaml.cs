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
        ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true));
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        BtnLogin.IsEnabled = false;
        BtnBack.Visibility = Visibility.Collapsed;
        BtnLogin.Text = Lang.Number(0d, "P0");
        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModProfile.selectedProfile = null;
                ModLaunch.mcLoginMsLoader.Start(ModProfile.GetLoginData(ModLaunch.McLoginType.Ms), true);
                while (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Loading)
                {
                    ModBase.RunInUi(() => BtnLogin.Text = Lang.Number(ModLaunch.mcLoginMsLoader.Progress, "P0"));
                    Thread.Sleep(50);
                }

                if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Finished)
                    ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true));
                else if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Aborted)
                    throw new ThreadInterruptedException();
                else if (ModLaunch.mcLoginMsLoader.Error is null)
                    throw new Exception(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                else
                    throw new Exception(ModLaunch.mcLoginMsLoader.Error.Message, ModLaunch.mcLoginMsLoader.Error);
            }
            catch (ThreadInterruptedException ex)
            {
                ModMain.Hint(Lang.Text("Launch.Account.LoginCancelled"));
            }
            catch (Exception ex)
            {
                if (ex.Message == "$$")
                {
                }
                else if (ex.Message.StartsWith("$"))
                {
                    ModMain.Hint(ex.Message.TrimStart('$'), ModMain.HintType.Critical);
                }
                else if (ex is AuthenticationException && ex.Message.ContainsF("SSL/TLS"))
                {
                    ModBase.Log(ex,
                        Lang.Text("Launch.Account.Microsoft.LoginFailed.Message") + "\r\n" + ex.Message,
                        ModBase.LogLevel.Msgbox);
                }
                else
                {
                    ModBase.Log(ex, Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"), ModBase.LogLevel.Msgbox);
                }
            }
            finally
            {
                ModBase.RunInUi(() =>
                {
                    BtnLogin.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                    BtnLogin.Text = Lang.Text("Launch.Account.Login");
                });
            }
        }, "Ms Login");
    }
}
