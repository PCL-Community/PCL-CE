using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.ViewModel;
using PCL.UI;

namespace PCL;

/// <summary>微软设备码登录弹窗的视图模型：设备码、验证网页、轮询与取消。</summary>
public partial class MyMsgLoginViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cancellation = new();

    private DialogViewModel? _dialog;

    [ObservableProperty] public partial string CaptionText { get; set; } = string.Empty;

    /// <summary>需要用户在网页上输入的设备代码。</summary>
    public string UserCode { get; private set; } = string.Empty;

    /// <summary>验证网页的网址。</summary>
    public string Website { get; private set; } = string.Empty;

    public bool IsStarted { get; private set; }

    public bool IsCompleted { get; private set; }

    /// <summary>由页面提供的剪贴板写入器，避免视图模型直接依赖窗口与剪贴板。</summary>
    public Func<string, Task>? ClipboardWriter { get; set; }

    /// <summary>登录结束（成功、失败或取消）后通知页面收尾。</summary>
    public event Action? Finished;

    public void Attach(DialogViewModel dialog)
    {
        _dialog = dialog;
    }

    public void Start()
    {
        if (IsStarted)
            return;
        IsStarted = true;

        LoadDeviceCode();
        _ = PollAsync();
    }

    /// <summary>语言切换后重新生成说明文本；仅在实际开始登录后执行。</summary>
    public void ReloadDeviceCode()
    {
        if (IsStarted)
            LoadDeviceCode();
    }

    public void LoadDeviceCode()
    {
        Load(_dialog?.LoginData);
    }

    public void Load(JsonObject? data)
    {
        var device = data?.ToObject<DeviceCodeData>();
        if (device is null)
        {
            CaptionText = Lang.Text("Launch.Account.LoginDialog.Error.Init");
            return;
        }

        UserCode = device.UserCode ?? string.Empty;
        if (device.VerificationUriComplete is not null)
        {
            Website = device.VerificationUriComplete;
            CaptionText = Lang.Text(
                "Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill",
                UserCode,
                Website);
        }
        else
        {
            Website = device.VerificationUri ?? string.Empty;
            CaptionText = Lang.Text(
                "Launch.Account.LoginDialog.MicrosoftInstructions",
                UserCode,
                Website);
        }
    }

    private async Task PollAsync()
    {
        var dialog = _dialog;
        var token = _cancellation.Token;
        var requestData = dialog?.LoginData;
        var deviceCode = requestData?.ToObject<DeviceCodeData>();

        try
        {
            await Task.Delay(2000, token);
            if (dialog is null || dialog.IsClosed)
                return;

            if (!string.IsNullOrEmpty(Website))
                UiInteraction.OpenWebsite(Website);
            CopyCode();

            if (requestData is null || deviceCode is null)
            {
                Finish(new InvalidDataException("设备授权数据无效"));
                return;
            }

            var delayTime = TimeSpan.FromSeconds(deviceCode.Interval ?? 5);
            var unknownFailureCount = 0;
            while (!dialog.IsClosed && !token.IsCancellationRequested)
                try
                {
                    if (dialog.DeviceCodePoll is null)
                        throw new InvalidOperationException("设备授权没有配置轮询处理程序。");

                    var result = await dialog.DeviceCodePoll(requestData, token);
                    token.ThrowIfCancellationRequested();
                    if (result is null)
                        throw new InvalidDataException("微软令牌返回为空");

                    if (result.IsError)
                        switch (result.Error)
                        {
                            case "authorization_pending":
                                await Task.Delay(delayTime, token);
                                continue;

                            case "slow_down":
                                delayTime += TimeSpan.FromSeconds(5);
                                await Task.Delay(delayTime, token);
                                continue;

                            case "expired_token":
                                Finish(
                                    new IdentityModelAuthenticationException(
                                        result.Error,
                                        result.ErrorDescription ?? "设备授权已过期。"));
                                return;

                            case "access_denied":
                            case "authorization_declined":
                                Finish(
                                    new IdentityModelAuthenticationException(
                                        result.Error,
                                        result.ErrorDescription ?? "用户拒绝了设备授权。"));
                                return;

                            default:
                                throw new IdentityModelAuthenticationException(
                                    result.Error ?? "invalid_response",
                                    result.ErrorDescription ?? "设备授权返回了未知错误。");
                        }

                    if (dialog.LoginResultHandler is not null)
                        try
                        {
                            await dialog.LoginResultHandler(result, token);
                            token.ThrowIfCancellationRequested();
                        }
                        catch (Exception handlerException)
                        {
                            Finish(handlerException);
                            return;
                        }

                    HintService.Show(
                        Hints.Success(Lang.Text("Launch.Account.LoginDialog.Success")));
                    Finish(
                        new[]
                        {
                            result.AccessToken ?? string.Empty,
                            result.RefreshToken ?? string.Empty
                        });
                    return;
                }
                catch (OperationCanceledException) when (dialog.IsClosed
                                                         || token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (unknownFailureCount <= 2)
                    {
                        unknownFailureCount++;
                        LogWrapper.Warn(
                            ex,
                            "UI.Dialogs",
                            $"正版验证轮询第 {unknownFailureCount} 次失败");
                        try
                        {
                            await Task.Delay(2000, token);
                        }
                        catch (OperationCanceledException) when (dialog.IsClosed
                                                                 || token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                    else
                    {
                        Finish(
                            new InvalidOperationException(
                                Lang.Text("Launch.Account.LoginDialog.PollingFailed"),
                                ex));
                        return;
                    }
                }
        }
        catch (OperationCanceledException)
        {
            // 弹窗关闭或用户取消，轮询自然结束
        }
        catch (Exception ex)
        {
            Finish(ex);
        }
    }

    [RelayCommand]
    public void OpenWebsite()
    {
        if (!string.IsNullOrEmpty(Website))
            UiInteraction.OpenWebsite(Website);
    }

    [RelayCommand]
    public void CopyCode()
    {
        _ = CopyCodeAsync();
    }

    private async Task CopyCodeAsync()
    {
        if (string.IsNullOrEmpty(UserCode) || ClipboardWriter is null)
            return;
        try
        {
            await ClipboardWriter(UserCode);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI.Dialogs", "复制设备代码失败");
        }
    }

    [RelayCommand]
    public void CancelLogin()
    {
        Finish(new OperationCanceledException("用户取消了登录。"));
    }

    /// <summary>弹窗被关闭：停止轮询，并把未完成的结果以取消异常回报给调用方。</summary>
    public void Cancel()
    {
        _cancellation.Cancel();
        if (IsCompleted)
            return;

        IsCompleted = true;
        try
        {
            _dialog?.CompletionHandler?.Invoke(
                new OperationCanceledException("登录弹窗在完成前被关闭。"));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "登录弹窗关闭回调失败");
        }

        Finished?.Invoke();
    }

    private void Finish(object result)
    {
        var dialog = _dialog;
        if (dialog is null || dialog.IsClosed || IsCompleted)
            return;

        IsCompleted = true;
        dialog.Close(result is Exception ? new DialogResult(2) : new DialogResult(1));
        _cancellation.Cancel();

        try
        {
            dialog.CompletionHandler?.Invoke(result);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "登录弹窗完成回调失败");
        }

        Finished?.Invoke();
    }
}