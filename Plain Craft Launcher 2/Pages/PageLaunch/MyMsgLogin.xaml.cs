using System.Net;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Controls;
using PCL.Network;

namespace PCL;

public partial class MyMsgLogin
{
    private readonly JsonObject Data;
    private string DeviceCode; // 用于轮询的设备代码
    private string OAuthUrl = ""; // OAuth 轮询验证地址
    private string UserCode; // 需要用户在网页上输入的设备代码
    private string Website; // 验证网页的网址

    public MyMsgLogin()
    {
        InitializeComponent();
        // Handles
        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn3.Click += Btn3_Click;
        PanBorder.MouseLeftButtonDown += Drag;
        LabTitle.MouseLeftButtonDown += Drag;
    }

    private void Finished(object Result)
    {
        if (MyConverter.IsExited)
            return;
        MyConverter.IsExited = true;
        MyConverter.Result = Result;
        ModBase.RunInUi(Close);
        Thread.Sleep(200);
        ModMain.FrmMain.ShowWindowToTop();
    }

    private void Init()
    {
        UserCode = (string)Data["user_code"];
        DeviceCode = (string)Data["device_code"];
        ModBase.ClipboardSet(DeviceCode);
        if (Data["verification_uri_complete"] is not null)
        {
            Website = (string)Data["verification_uri_complete"];
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", UserCode, Website);
        }
        else
        {
            Website = (string)Data["verification_uri"];
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", UserCode, Website);
        }

        // 设置 UI
        LabTitle.Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");
        CustomEventService.SetEventData(Btn1, Website);
        CustomEventService.SetEventData(Btn2, UserCode);
        // 启动工作线程
        ModBase.RunInNewThread(WorkThread, "MyMsgLogin");
    }

    private void WorkThread()
    {
        Thread.Sleep(2000);
        if (MyConverter.IsExited)
            return;
        ModBase.OpenWebsite(Website);
        ModBase.ClipboardSet(UserCode);
        Thread.Sleep((Data["interval"].ToObject<int>() - 1) * 1000);
        // 轮询
        var UnknownFailureCount = 0;
        while (!MyConverter.IsExited)
        {
            try
            {
                var bodyData = $"grant_type=urn:ietf:params:oauth:grant-type:device_code&client_id={Secrets.MSOAuthClientId}&device_code={DeviceCode}&scope=XboxLive.signin%20offline_access";

                var Result = Requester.Fetch(
                    "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                    new FetchParam
                    {
                        Method = "POST",
                        Content = bodyData,
                        ContentType = "application/x-www-form-urlencoded",
                        Timeout = 5000 + UnknownFailureCount * 5000, MakeLog = false
                    });
                // 获取结果
                var ResultJson = (JsonObject)ModBase.GetJson(Result);
                ModProfile.ProfileLog($"令牌过期时间：{ResultJson["expires_in"]} 秒");
                ModMain.Hint(Lang.Text("Launch.Account.LoginDialog.Success"), ModMain.HintType.Finish);
                Finished(new[] { ResultJson["access_token"].ToString(), ResultJson["refresh_token"].ToString() });
                return;
            }
            catch (WebException ex)
            {
                throw new Exception("爆！");
            }
            catch (Exception ex)
            {
                if (UnknownFailureCount <= 2)
                {
                    UnknownFailureCount += 1;
                    ModBase.Log(ex, $"正版验证轮询第 {UnknownFailureCount} 次失败");
                    ModBase.Log(ex.Message);
                    Thread.Sleep(2000);
                }
                else
                {
                    Finished(new Exception(Lang.Text("Launch.Account.LoginDialog.PollingFailed"), ex));
                    return;
                }
            }
        }
    }


    #region 弹窗

    private readonly ModMain.MyMsgBoxConverter MyConverter;
    private readonly int Uuid = ModBase.GetUuid();

    public MyMsgLogin(ModMain.MyMsgBoxConverter Converter)
    {
        try
        {
            InitializeComponent();
            Btn1.Name += ModBase.GetUuid();
            Btn2.Name += ModBase.GetUuid();
            Btn3.Name += ModBase.GetUuid();
            MyConverter = Converter;
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            Data = (JsonObject)Converter.Content;
            OAuthUrl = Converter.AuthUrl?.ToString() ?? "";
            Init();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.LoginDialog.Error.Init"), ModBase.LogLevel.Hint);
        }

        Loaded += Load;
    }

    private void Load(object sender, EventArgs e)
    {
        try
        {
            // 动画
            Opacity = 0d;
            ModAnimation.AniStart(
                ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (MyConverter.IsWarn
                        ? new ModBase.MyColor(140d, 80d, 0d, 0d)
                        : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + Uuid);
            // 记录日志
            ModBase.Log($"[Control] 正版验证弹窗：{LabTitle.Text}\r\n{LabCaption.Text}");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.LoginDialog.Error.Load"), ModBase.LogLevel.Hint);
        }
    }

    private void Close()
    {
        // 动画
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsgBackground.Background, 200,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), After: true)
        }, "MyMsgBox " + Uuid);
    }

    // 实现回车和 Esc 的接口（#4857）
    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
    }

    public void Btn3_Click(object sender, MouseButtonEventArgs e)
    {
        Finished(new ThreadInterruptedException());
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        // On Error Resume Next
        if (e.GetPosition(ShapeLine).Y <= 2d)
            ModMain.FrmMain.DragMove();
    }

    #endregion
}