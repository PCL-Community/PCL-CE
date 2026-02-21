using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.UI.Controls;

namespace PCL;

public partial class MyMsgLogin
{
    private readonly JObject Data;
    private string DeviceCode; // 用于轮询的设备代码
    private string OAuthUrl = ""; // OAuth 轮询验证地址
    private string UserCode; // 需要用户在网页上输入的设备代码
    private string Website; // 验证网页的网址

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
            LabCaption.Text = "登录网页将自动开启，授权码将自动填充。" + Constants.vbCrLf + Constants.vbCrLf +
                              "如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" + Constants.vbCrLf +
                              $"如果没有自动填充，请在页面内粘贴此授权码 {UserCode} （将自动复制）" + Constants.vbCrLf +
                              $"你也可以用其他设备打开 {Website} 并输入授权码。";
        }
        else
        {
            Website = (string)Data["verification_uri"];
            LabCaption.Text = $"登录网页将自动开启，请在网页中输入授权码 {UserCode}（将自动复制）。" + Constants.vbCrLf + Constants.vbCrLf +
                              "如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" + Constants.vbCrLf +
                              $"你也可以用其他设备打开 {Website} 并输入上述授权码。";
        }

        // 设置 UI
        LabTitle.Text = "登录 Minecraft";
        Btn1.EventData = Website;
        Btn2.EventData = UserCode;
        // 启动工作线程
        ModBase.RunInNewThread(WorkThread, "MyMsgLogin");
    }

    private void WorkThread()
    {
        Thread.Sleep(3000);
        if (MyConverter.IsExited)
            return;
        ModBase.OpenWebsite(Website);
        ModBase.ClipboardSet(UserCode);
        Thread.Sleep((Data["interval"].ToObject<int>() - 1) * 1000);
        // 轮询
        var UnknownFailureCount = 0;
        while (!MyConverter.IsExited)
            try
            {
                var Result = ModNet.NetRequestOnce("https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                    "POST",
                    "grant_type=urn:ietf:params:oauth:grant-type:device_code" + "&" + "client_id=" +
                    ModSecret.OAuthClientId + "&" + "device_code=" + DeviceCode + "&" +
                    "scope=XboxLive.signin%20offline_access", "application/x-www-form-urlencoded",
                    5000 + UnknownFailureCount * 5000, MakeLog: false);
                // 获取结果
                var ResultJson = (JObject)ModBase.GetJson(Result);
                ModProfile.ProfileLog($"令牌过期时间：{ResultJson["expires_in"]} 秒");
                ModMain.Hint("网页登录成功！", ModMain.HintType.Finish);
                Finished(new[] { ResultJson["access_token"].ToString(), ResultJson["refresh_token"].ToString() });
                return;
            }
            catch (ModNet.HttpWebException ex)
            {
                var response = ex.InnerHttpException.WebResponse;
                if (response.Contains("authorization_declined"))
                {
                    Finished(new Exception("$你拒绝了 PCL 申请的权限……"));
                    return;
                }

                if (response.Contains("expired_token"))
                {
                    Finished(new Exception("$登录用时太长啦，重新试试吧！"));
                    return;
                }

                if (response.Contains("Account security interrupt"))
                {
                    Finished(new Exception("$非常抱歉，该账号由于安全问题无法登陆，请前往 Microsoft 账户页获取更多信息。"));
                    return;
                }

                if (response.Contains("service abuse"))
                {
                    Finished(new Exception("$非常抱歉，该账号已被微软封禁，无法登录。"));
                    return;
                }

                if (response.Contains("AADSTS70000")) // 可能不能判 “invalid_grant”，见 #269
                {
                    Finished(new ModBase.RestartException());
                    return;
                }

                if (response.Contains("authorization_pending"))
                {
                    Thread.Sleep(2000);
                }
                else if (UnknownFailureCount <= 2)
                {
                    UnknownFailureCount += 1;
                    ModBase.Log(ex, $"正版验证轮询第 {UnknownFailureCount} 次失败");
                    ModBase.Log("原始返回内容: " + response);
                    Thread.Sleep(2000);
                }
                else
                {
                    Finished(new Exception("正版验证轮询失败", ex));
                    return;
                }
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
                    Finished(new Exception("正版验证轮询失败", ex));
                    return;
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
            Btn1.Name = Btn1.Name + ModBase.GetUuid();
            Btn2.Name = Btn2.Name + ModBase.GetUuid();
            Btn3.Name = Btn3.Name + ModBase.GetUuid();
            MyConverter = Converter;
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            Data = (JObject)Converter.Content;
            OAuthUrl = Conversions.ToString(Converter.AuthUrl);
            Init();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "正版验证弹窗初始化失败", ModBase.LogLevel.Hint);
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
            ModBase.Log("[Control] 正版验证弹窗：" + LabTitle.Text + Constants.vbCrLf + LabCaption.Text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "正版验证弹窗加载失败", ModBase.LogLevel.Hint);
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
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
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
    public void Btn1_Click()
    {
    }

    public void Btn3_Click()
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