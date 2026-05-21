using System.Security.Authentication;
using System.Windows;
using Microsoft.VisualBasic;
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
        ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true));
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
                ModProfile.SelectedProfile = null;
                ModLaunch.McLoginMsLoader.Start(ModProfile.GetLoginData(ModLaunch.McLoginType.Ms), true);
                while (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Loading)
                {
                    ModBase.RunInUi(() => BtnLogin.Text = Lang.Number(ModLaunch.McLoginMsLoader.Progress, "P0"));
                    Thread.Sleep(50);
                }

                if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Finished)
                    ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true));
                else if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Aborted)
                    throw new ThreadInterruptedException();
                else if (ModLaunch.McLoginMsLoader.Error is null)
                    throw new Exception("未知错误！");
                else
                    throw new Exception(ModLaunch.McLoginMsLoader.Error.Message, ModLaunch.McLoginMsLoader.Error);
            }
            catch (ThreadInterruptedException ex)
            {
                ModMain.Hint("已取消登录！");
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
                        """
                        正版登录验证失败，请考虑在 [设置 → 其他] 中关闭 [在正版登录时验证 SSL 证书]，然后再试。

                        原始错误信息：
                        """, ModBase.LogLevel.Msgbox);
                }
                else
                {
                    ModBase.Log(ex, "正版登录尝试失败", ModBase.LogLevel.Msgbox);
                }
            }
            finally
            {
                ModBase.RunInUi(() =>
                {
                    BtnLogin.IsEnabled = true;
                    BtnBack.Visibility = Visibility.Visible;
                    BtnLogin.Text = "登录";
                });
            }
        }, "Ms Login");
    }
}