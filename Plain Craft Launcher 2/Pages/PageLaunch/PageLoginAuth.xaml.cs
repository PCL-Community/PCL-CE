using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Yggdrasil;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class PageLoginAuth
{
    public static string draggedAuthServer;

    // 预设服务器
    private static readonly Dictionary<string, string> predefinedAuthServers = new()
    {
        { Lang.Text("Launch.Account.Auth.Preset.LittleSkin"), "https://littleskin.cn/api/yggdrasil" },
        { Lang.Text("Common.Option.Customize"), "" }
    };

    private bool _isRegisterMode = true;

    public PageLoginAuth()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        Loaded += (_, _) => ReloadRegisterButton();
        // Handles
        BtnBack.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
        TextServer.TextChanged += TextServer_TextChanged;
        BtnLink.Click += Btn_Click;
    }

    private void Reload()
    {
        var serverItems = TextServer.Items;
        serverItems.Clear();
        foreach (var serverName in predefinedAuthServers.Keys)
            serverItems.Add(new MyComboBoxItem { Content = serverName });
        TextServer.Text = draggedAuthServer;
        draggedAuthServer = null;
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        TextServer.Text = null;
        TextName.Text = null;
        TextPass.Password = null;
        ModMain.frmLaunchLeft.RefreshPage(true);
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextServer.Text) || string.IsNullOrWhiteSpace(TextName.Text) ||
            string.IsNullOrWhiteSpace(TextPass.Password))
        {
            ModMain.Hint(Lang.Text("Launch.Account.Auth.EmptyFields"), ModMain.HintType.Critical);
            return;
        }

        if (!TextServer.Text.IsMatch(RegexPatterns.HttpUri))
        {
            ModMain.Hint(Lang.Text("Launch.Account.Auth.InvalidServer"), ModMain.HintType.Critical);
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnBack.IsEnabled = false;
        var loginData = new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
        {
            baseUrl = TextServer.Text.EndsWithF("/") ? $"{TextServer.Text}authserver" : $"{TextServer.Text}/authserver",
            userName = TextName.Text, password = TextPass.Password, description = "Authlib-Injector",
            type = ModLaunch.McLoginType.Auth
        };
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                ModProfile.isCreatingProfile = true;
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
                        ModMain.Hint(Lang.Text("Launch.Account.Auth.Cancelled"));
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
                    ModMain.Hint(ex.Message.TrimStart('$'), ModMain.HintType.Critical);
                }
                else
                {
                    ModBase.Log(ex, Lang.Text("Launch.Account.Auth.LoginFailed"), ModBase.LogLevel.Msgbox);
                }
            }
            finally
            {
                ModProfile.isCreatingProfile = false;
                BtnLogin.IsEnabled = true;
                BtnBack.IsEnabled = true;
                BtnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
            }
        }));
    }

    // 获取验证服务器名称
    private void GetServerName()
    {
        var serverUriInput = TextServer.Text;
        if (string.IsNullOrWhiteSpace(serverUriInput))
        {
            TextServerName.Visibility = Visibility.Hidden;
            return;
        }

        Dispatcher.BeginInvoke(async () =>
        {
            string serverUri = null;
            string serverName = null;
            try
            {
                serverUri = await ApiLocation.TryRequestAsync(serverUriInput);
                using var resp = await HttpRequest.Create(serverUri).SendAsync();
                var responseText = await resp.AsStringAsync();
                serverName = await Task.Run(() => ModBase.GetJson(responseText)["meta"]?["serverName"]?.ToString());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从服务器获取名称失败");
            }

            if (serverUri is not null) TextServer.Text = serverUri;
            if (serverName is null)
            {
                TextServerName.Visibility = Visibility.Hidden;
            }
            else
            {
                TextServerName.Text = Lang.Text("Launch.Account.Auth.ServerLabel", serverName);
                TextServerName.Visibility = Visibility.Visible;
            }
        });
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
        BtnLink.Visibility = new HttpValidator().Validate(address).IsValid
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TextServer_TextChanged(object sender, TextChangedEventArgs e)
    {
        predefinedAuthServers.TryGetValue(TextServer.Text, out var server);
        if (server is not null) TextServer.Text = server;
    }
}