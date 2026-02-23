using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.IO.Net.Http.Client;
using PCL.Core.Minecraft.Yggdrasil;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class PageLoginAuth
{
    public static string DraggedAuthServer;

    // 预设服务器
    private static readonly Dictionary<string, string> PredefinedAuthServers = new()
        { { "预设 - LittleSkin", "https://littleskin.cn/api/yggdrasil" }, { "自定义", "" } };

    public PageLoginAuth()
    {
        InitializeComponent();
        Loaded += (_, __) => Reload();
        Loaded += (_, __) => ReloadRegisterButton();
        // Handles
        BtnBack.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
        TextServer.TextChanged += ComboName_TextChanged;
        BtnLink.Click += Btn_Click;
    }

    private void Reload()
    {
        var serverItems = TextServer.Items;
        serverItems.Clear();
        foreach (var serverName in PredefinedAuthServers.Keys)
            serverItems.Add(new MyComboBoxItem { Content = serverName });
        if (DraggedAuthServer is not null)
        {
            TextServer.Text = DraggedAuthServer;
            DraggedAuthServer = null;
        }
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        TextServer.Text = null;
        TextName.Text = null;
        TextPass.Password = null;
        ModMain.FrmLaunchLeft.RefreshPage(true);
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextServer.Text) || string.IsNullOrWhiteSpace(TextName.Text) ||
            string.IsNullOrWhiteSpace(TextPass.Password))
        {
            ModMain.Hint("验证服务器、用户名与密码均不能为空！", ModMain.HintType.Critical);
            return;
        }

        if (!TextServer.Text.IsMatch(RegexPatterns.HttpUri))
        {
            ModMain.Hint("输入的验证服务器地址无效", ModMain.HintType.Critical);
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnBack.IsEnabled = false;
        var LoginData = new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
        {
            BaseUrl = TextServer.Text.EndsWithF("/") ? TextServer.Text + "authserver" : TextServer.Text + "/authserver",
            UserName = TextName.Text, Password = TextPass.Password, Description = "Authlib-Injector",
            Type = ModLaunch.McLoginType.Auth
        };
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                ModProfile.IsCreatingProfile = true;
                ModLaunch.McLoginAuthLoader.Start(LoginData, true);
                while (ModLaunch.McLoginAuthLoader.State == ModBase.LoadState.Loading)
                {
                    BtnLogin.Text = Math.Round(ModLaunch.McLoginAuthLoader.Progress * 100d) + "%";
                    await Task.Delay(50);
                }

                if (ModLaunch.McLoginAuthLoader.State == ModBase.LoadState.Finished)
                    ModMain.FrmLaunchLeft.RefreshPage(true);
                else if (ModLaunch.McLoginAuthLoader.State == ModBase.LoadState.Aborted)
                    ModMain.Hint("已取消登录！");
                else if (ModLaunch.McLoginAuthLoader.Error is null)
                    throw new Exception("未知错误！");
                else
                    throw new Exception(ModLaunch.McLoginAuthLoader.Error.Message, ModLaunch.McLoginAuthLoader.Error);
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
                    ModBase.Log(ex, "第三方登录尝试失败", ModBase.LogLevel.Msgbox);
                }
            }
            finally
            {
                ModProfile.IsCreatingProfile = false;
                BtnLogin.IsEnabled = true;
                BtnBack.IsEnabled = true;
                BtnLogin.Text = "登录";
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
                var response = await HttpRequestBuilder.Create(serverUri, HttpMethod.Get).SendAsync();
                var responseText = await response.AsStringAsync();
                serverName = await Task.Run(() => JObject.Parse(responseText)["meta"]["serverName"].ToString());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从服务器获取名称失败");
            }

            if (serverUri is not null)
                TextServer.Text = serverUri;
            if (serverName is null)
            {
                TextServerName.Visibility = Visibility.Hidden;
            }
            else
            {
                TextServerName.Text = "验证服务器: " + serverName;
                TextServerName.Visibility = Visibility.Visible;
            }
        });
    }

    // 链接处理
    private void ComboName_TextChanged(object sender, TextChangedEventArgs e)
    {
        BtnLink.Content = string.IsNullOrEmpty(TextName.Text) ? "注册账号" : "找回密码";
    }

    private void Btn_Click(object sender, EventArgs e)
    {
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(BtnLink.Content, "注册账号", false)))
        {
            ModBase.OpenWebsite(Conversions.ToString(ModMinecraft.McInstanceSelected is not null
                ? ModBase.Setup.Get("VersionServerAuthRegister", ModMinecraft.McInstanceSelected)
                : ""));
        }
        else
        {
            var Website = Conversions.ToString(ModMinecraft.McInstanceSelected is not null
                ? ModBase.Setup.Get("VersionServerAuthRegister", ModMinecraft.McInstanceSelected)
                : "");
            ModBase.OpenWebsite(Website.Replace("/auth/register", "/auth/forgot"));
        }
    }

    // 切换注册按钮可见性
    private void ReloadRegisterButton()
    {
        var Address = Conversions.ToString(ModMinecraft.McInstanceSelected is not null
            ? ModBase.Setup.Get("VersionServerAuthRegister", ModMinecraft.McInstanceSelected)
            : "");
        BtnLink.Visibility = string.IsNullOrEmpty(new ValidateHttp().Validate(Address))
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TextServer_TextChanged(object sender, TextChangedEventArgs e)
    {
        string server = null;
        PredefinedAuthServers.TryGetValue(TextServer.Text, out server);
        if (server is not null) TextServer.Text = server;
    }
}