using PCL.Controls.MyMsg;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.UI.MsgBox;
using PCL.Core.Utils;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class MyMsgLogin : IMsgBoxControl
{
    private readonly JsonObject? _data;
    private string _deviceCode = "";
    private string _oAuthUrl = "";
    private string _userCode = "";
    private string _website = "";
    private Task? _workingThread;

    public MsgBoxRequest Request { get; }
    public event EventHandler<MsgBoxResponse>? Completed;

    private readonly MsgBoxAnimationProfile _anim;
    private bool _isExited;
    private readonly string _animGroup;

    public MyMsgLogin()
    {
        InitializeComponent();
        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn3.Click += Btn3_Click;
        PanBorder.MouseLeftButtonDown += Drag;
        LabTitle.MouseLeftButtonDown += Drag;
        Request = new MsgBoxRequest();
        _anim = MsgBoxAnimationProfile.ForTheme(MsgBoxTheme.Info);
        _animGroup = "MyMsgLogin designer";
        _data = null;
    }

    public MyMsgLogin(MsgBoxRequest request, JsonObject data)
    {
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = $"MyMsgLogin {Request.RequestId}";
        _data = data;
        InitCommon();
        Init();
    }

    public MyMsgLogin(ModMain.MyMsgBoxConverter converter)
    {
        var isWarn = converter.IsWarn;
        var data = (JsonObject)converter.Content;

        var request = new MsgBoxRequest
        {
            Caption = "",
            Theme = isWarn ? MsgBoxTheme.Warning : MsgBoxTheme.Info,
            Buttons = [new("", 1), new("", 2), new("", 3)],
            IsBlocking = true,
            Content = converter.Content
        };
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = $"MyMsgBox {ModBase.GetUuid()}";
        _data = data;
        _oAuthUrl = converter.AuthUrl.ToString() ?? "";
        _legacyConverter = converter;

        InitCommon();
        Init();
    }

    private readonly ModMain.MyMsgBoxConverter? _legacyConverter;

    private void LegacyComplete(object result)
    {
        if (_legacyConverter is null || _isExited) return;
        _isExited = true;
        _legacyConverter.IsExited = true;
        _legacyConverter.Result = result;
        _legacyConverter.WaitFrame.Continue = false;
    }

    private void InitCommon()
    {
        InitializeComponent();
        Btn1.Name += ModBase.GetUuid();
        Btn2.Name += ModBase.GetUuid();
        Btn3.Name += ModBase.GetUuid();
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        Loaded += Load;
    }

    private void Init()
    {
        if (_data is null) return;
        _userCode = (string)_data["user_code"]!;
        _deviceCode = (string)_data["device_code"]!;
        ModBase.ClipboardSet(_deviceCode);
        if (_data["verification_uri_complete"] is not null)
        {
            _website = (string)_data["verification_uri_complete"]!;
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", _userCode, _website);
        }
        else
        {
            _website = (string)_data["verification_uri"]!;
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", _userCode, _website);
        }

        LabTitle.Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");
        CustomEventService.SetEventData(Btn1, _website);
        CustomEventService.SetEventData(Btn2, _userCode);
        _workingThread = WorkThreadAsync();
    }

    private void Finished(object result)
    {
        if (_isExited) return;
        _isExited = true;

        if (_legacyConverter is not null)
        {
            // 旧路径：直接写 Converter
            LegacyComplete(result);
            ModBase.RunInUi(() => _ = InvokeCloseAnimationAsync(MsgBoxResponse.Cancelled(Request.RequestId)));
        }
        else
        {
            // 新路径：触发 Completed 事件
            var response = result switch
            {
                string[] tokens => new MsgBoxResponse
                {
                    RequestId = Request.RequestId,
                    ButtonValue = 1,
                    Button = new MsgBoxButtonInfo("", 1)
                },
                _ => MsgBoxResponse.Cancelled(Request.RequestId)
            };
            ModBase.RunInUi(() => Completed?.Invoke(this, response));
        }
        Thread.Sleep(200);
        ModMain.frmMain.ShowWindowToTop();
    }

    private record ErrorBody(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string Desc);

    private async Task WorkThreadAsync()
    {
        await Task.Delay(2000).ConfigureAwait(false);
        if (_isExited) return;
        ModBase.OpenWebsite(_website);
        ModBase.ClipboardSet(_userCode);
        var delayTime = (_data!["interval"]!.ToObject<int>() - 1) * 1000;

        var unknownFailureCount = 0;
        while (!_isExited)
        {
            try
            {
                var bodyData = $"grant_type=urn:ietf:params:oauth:grant-type:device_code&client_id={Secrets.MSOAuthClientId}&device_code={_deviceCode}&scope=XboxLive.signin%20offline_access";
                using var result = await HttpRequest
                    .Create("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                    .WithFormContent(bodyData)
                    .SendAsync(enableLogging: false)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    var error = await result.AsJsonAsync<ErrorBody>().ConfigureAwait(false);
                    switch (error?.Error)
                    {
                        case "authorization_pending":
                            await Task.Delay(delayTime).ConfigureAwait(false);
                            continue;
                        default:
                            throw new Exception(error?.Error ?? "Unable to get body");
                    }
                }

                var ctx = await result.AsStringAsync().ConfigureAwait(false);
                var resultJson = (JsonObject)ModBase.GetJson(ctx);
                ModProfile.ProfileLog($"令牌过期时间：{resultJson["expires_in"]} 秒");
                HintService.Hint(Lang.Text("Launch.Account.LoginDialog.Success"), HintType.Success);
                Finished(new[] { resultJson["access_token"]!.ToString(), resultJson["refresh_token"]!.ToString() });
                return;
            }
            catch (Exception ex)
            {
                if (unknownFailureCount <= 2)
                {
                    unknownFailureCount += 1;
                    ModBase.Log(ex, $"正版验证轮询第 {unknownFailureCount} 次失败");
                    ModBase.Log(ex.Message);
                    await Task.Delay(2000).ConfigureAwait(false);
                }
                else
                {
                    Finished(new Exception(Lang.Text("Launch.Account.LoginDialog.PollingFailed"), ex));
                    return;
                }
            }
        }
    }

    private void Load(object sender, EventArgs e)
    {
        try
        {
            Btn3.IsEnabled = false;
            ModAnimation.AniStart(
                ModAnimation.AaCode(() => Btn3.IsEnabled = true, 120000),
                "MyMsgBox " + (Request.RequestId));
            InvokeShowAnimation();
            ModBase.Log($"[Control] 正版验证弹窗：{LabTitle.Text}\r\n{LabCaption.Text}");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.LoginDialog.Error.Load"), ModBase.LogLevel.Hint);
        }
    }

    public void InvokeShowAnimation()
    {
        Opacity = 0d;
        MsgBoxAnimations.AnimateShow(this, TransformPos, TransformRotate, _anim, _animGroup);
    }

    public async Task InvokeCloseAnimationAsync(MsgBoxResponse response)
    {
        await MsgBoxAnimations.AnimateCloseAsync(this, TransformPos, TransformRotate, _anim, _animGroup).ConfigureAwait(true);
        if (Parent is Grid g) g.Children.Remove(this);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        // Btn1 负责打开浏览器（由 CustomEventService.SetEventData 处理）
    }

    public void Btn3_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isExited)
            Finished(new ThreadInterruptedException());
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(ShapeLine).Y <= 2d)
            ModMain.frmMain.DragMove();
    }
}
