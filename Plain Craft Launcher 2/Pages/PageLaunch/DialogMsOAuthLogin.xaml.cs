using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.IO.Net.Http;

namespace PCL;

public class DialogMsOAuthLogin
{
    private readonly JsonObject _data;
    private bool _finished;
    private Task? _workingThread;
    private TextBlock _caption = null!;
    private MyButton _btnCancel = null!;
    private string _website = "";
    private string _userCode = "";

    public DialogMsOAuthLogin(DialogControl dialog, JsonObject data, string authUrl, Action<object> onFinished)
    {
        _data = data;

        _userCode = (string)data["user_code"]!;
        var deviceCode = (string)data["device_code"]!;
        ModBase.ClipboardSet(deviceCode);

        string captionText;
        if (data["verification_uri_complete"] is not null)
        {
            _website = (string)data["verification_uri_complete"]!;
            captionText = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", _userCode, _website);
        }
        else
        {
            _website = (string)data["verification_uri"]!;
            captionText = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", _userCode, _website);
        }

        // Build content
        _caption = new TextBlock
        {
            Text = captionText,
            FontSize = 15,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Normal,
            Padding = new Thickness(1),
        };

        var scrollViewer = new MyScrollViewer
        {
            Content = _caption,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            DeltaMult = 0.7,
        };

        dialog.Title = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");
        dialog.DialogContent = scrollViewer;

        // Buttons
        var btnReopen = dialog.AddButton(new DialogButton(
            Lang.Text("Launch.Account.LoginDialog.ReopenWebpage"),
            onClick: () => ModBase.OpenWebsite(_website), id: 1));
        CustomEventService.SetEventData(btnReopen, _website);

        var btnCopy = dialog.AddButton(new DialogButton(
            Lang.Text("Launch.Account.LoginDialog.CopyCode"),
            onClick: () => ModBase.ClipboardSet(_userCode), id: 2));
        CustomEventService.SetEventData(btnCopy, _userCode);

        _btnCancel = dialog.AddButton(new DialogButton(
            Lang.Text("Common.Action.Cancel"), isCancel: true, id: 3));

        // Finish callback
        dialog.OnClosed += result =>
        {
            if (_finished) return;
            _finished = true;
            if (result == 3)
                onFinished(new ThreadInterruptedException());
        };

        _btnCancel.Click += (_, _) =>
        {
            _finished = true;
            onFinished(new ThreadInterruptedException());
            dialog.Close(3);
        };

        // Start OAuth polling
        _workingThread = WorkThread(deviceCode, data, authUrl, onFinished, dialog);
    }

    private async Task WorkThread(string deviceCode, JsonObject data, string oAuthUrl,
        Action<object> onFinished, DialogControl dialog)
    {
        try
        {
            await Task.Delay(2000).ConfigureAwait(false);
            if (_finished) return;

            if (!string.IsNullOrEmpty(_website))
                ModBase.OpenWebsite(_website);

            ModBase.ClipboardSet(_userCode);

            var delayTime = (data["interval"]!.ToObject<int>() - 1) * 1000;
            var unknownFailureCount = 0;

            while (!_finished)
            {
                try
                {
                    var bodyData = $"grant_type=urn:ietf:params:oauth:grant-type:device_code&client_id={Secrets.MSOAuthClientId}&device_code={deviceCode}&scope=XboxLive.signin%20offline_access";
                    using var resp = await HttpRequest
                        .Create("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                        .WithFormContent(bodyData)
                        .SendAsync(enableLogging: false)
                        .ConfigureAwait(false);

                    if (!resp.IsSuccess)
                    {
                        var error = await resp.AsJsonAsync<ErrorBody>().ConfigureAwait(false);
                        switch (error?.Error)
                        {
                            case "authorization_pending":
                                await Task.Delay(delayTime).ConfigureAwait(false);
                                continue;
                            default:
                                throw new Exception(error?.Error ?? "Unable to get body");
                        }
                    }

                    var ctx = await resp.AsStringAsync().ConfigureAwait(false);
                    var resultJson = (JsonObject)ModBase.GetJson(ctx);
                    ModProfile.ProfileLog($"令牌过期时间：{resultJson["expires_in"]} 秒");
                    ModMain.Hint(Lang.Text("Launch.Account.LoginDialog.Success"), ModMain.HintType.Finish);

                    _finished = true;
                    onFinished(new[] { resultJson["access_token"]!.ToString(), resultJson["refresh_token"]!.ToString() });
                    dialog.Close(0);
                    return;
                }
                catch (Exception ex)
                {
                    if (unknownFailureCount <= 2)
                    {
                        unknownFailureCount++;
                        ModBase.Log(ex, $"正版验证轮询第 {unknownFailureCount} 次失败");
                        await Task.Delay(2000).ConfigureAwait(false);
                    }
                    else
                    {
                        _finished = true;
                        onFinished(new Exception(Lang.Text("Launch.Account.LoginDialog.PollingFailed"), ex));
                        dialog.Close(-1);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.LoginDialog.Error.Init"), ModBase.LogLevel.Hint);
        }
    }

    private record ErrorBody(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string Desc);
}
