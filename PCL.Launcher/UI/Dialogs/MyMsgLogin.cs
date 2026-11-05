using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.UI.Controls;

namespace PCL.UI.Dialogs;

/// <summary>
///     微软设备码登录弹窗：展示授权码与网页地址，自动打开网页并复制代码，随后按设备码间隔轮询授权结果。
///     关闭弹窗会取消轮询；结果通过 <see cref="DialogViewModel.CompletionHandler" /> 回传。
/// </summary>
public sealed class MyMsgLogin : MyMsgDialogBase
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SelectableTextBlock _caption;
    private bool _completed;
    private bool _started;
    private string _userCode = string.Empty;

    private string _website = string.Empty;

    public MyMsgLogin(DialogViewModel dialog) : base(dialog)
    {
        SetButtonText(1, Lang.Text("Launch.Account.LoginDialog.ReopenWebpage"));
        SetButtonText(2, Lang.Text("Launch.Account.LoginDialog.CopyCode"));
        SetButtonText(3, Lang.Text("Common.Action.Cancel"));
        for (var number = 1; number <= 3; number++)
            Button(number).ColorType = MyButton.ColorState.Normal;
        LabTitle.Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");

        _caption = new SelectableTextBlock
        {
            Padding = new Thickness(1d),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            FontSize = DialogTheme.BodyFontSize,
            LineHeight = 18d,
            VerticalAlignment = VerticalAlignment.Top
        };
        DialogTheme.BindBrush(_caption, TextBlock.ForegroundProperty, "ColorBrush1");
        SetBody(_caption);

        if (Content is Grid root)
        {
            root.HorizontalAlignment = HorizontalAlignment.Center;
            root.VerticalAlignment = VerticalAlignment.Center;
        }

        LocalizationService.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => StartFlow();
    }

    private void OnLanguageChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnLanguageChanged);
            return;
        }

        SetButtonText(1, Lang.Text("Launch.Account.LoginDialog.ReopenWebpage"));
        SetButtonText(2, Lang.Text("Launch.Account.LoginDialog.CopyCode"));
        SetButtonText(3, Lang.Text("Common.Action.Cancel"));
        for (var number = 1; number <= 3; number++)
            Button(number).ColorType = MyButton.ColorState.Normal;
        LabTitle.Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");
        if (_started)
            LoadDeviceCode();
    }

    /// <summary>控件离树即视为弹窗被关闭：停止轮询，并把未完成的结果以取消异常回报给调用方。</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _cancellation.Cancel();
        if (_completed) return;

        _completed = true;
        try
        {
            Dialog.CompletionHandler?.Invoke(new OperationCanceledException("登录弹窗在完成前被关闭。"));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "登录弹窗关闭回调失败");
        }

        BringMainWindowToFront();
    }

    private void StartFlow()
    {
        if (_started) return;
        _started = true;

        LoadDeviceCode();
        _ = RunAsync();
    }

    private void LoadDeviceCode()
    {
        var data = Dialog.LoginData;
        var device = data?.ToObject<DeviceCodeData>();
        if (device is null)
        {
            _caption.Text = Lang.Text("Launch.Account.LoginDialog.Error.Init");
            return;
        }

        _userCode = device.UserCode ?? string.Empty;
        if (device.VerificationUriComplete is not null)
        {
            _website = device.VerificationUriComplete;
            _caption.Text = Lang.Text(
                "Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill",
                _userCode,
                _website);
        }
        else
        {
            _website = device.VerificationUri ?? string.Empty;
            _caption.Text = Lang.Text(
                "Launch.Account.LoginDialog.MicrosoftInstructions",
                _userCode,
                _website);
        }
    }

    private async Task RunAsync()
    {
        var token = _cancellation.Token;
        var requestData = Dialog.LoginData;
        var deviceCode = requestData?.ToObject<DeviceCodeData>();

        try
        {
            await Task.Delay(2000, token);
            if (Dialog.IsClosed) return;

            if (!string.IsNullOrEmpty(_website)) UiInteraction.OpenWebsite(_website);
            CopyCode();

            if (requestData is null || deviceCode is null)
            {
                Finish(new InvalidDataException("设备授权数据无效"));
                return;
            }

            var delayTime = TimeSpan.FromSeconds(deviceCode.Interval ?? 5);
            var unknownFailureCount = 0;
            while (!Dialog.IsClosed && !token.IsCancellationRequested)
                try
                {
                    if (Dialog.DeviceCodePoll is null)
                        throw new InvalidOperationException("设备授权没有配置轮询处理程序。");

                    var result = await Dialog.DeviceCodePoll(requestData, token);
                    token.ThrowIfCancellationRequested();
                    if (result is null) throw new InvalidDataException("微软令牌返回为空");

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

                    if (Dialog.LoginResultHandler is not null)
                        try
                        {
                            await Dialog.LoginResultHandler(result, token);
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
                catch (OperationCanceledException) when (Dialog.IsClosed
                                                         || token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (unknownFailureCount <= 2)
                    {
                        unknownFailureCount++;
                        LogWrapper.Warn(ex, "UI.Dialogs", $"正版验证轮询第 {unknownFailureCount} 次失败");
                        try
                        {
                            await Task.Delay(2000, token);
                        }
                        catch (OperationCanceledException) when (Dialog.IsClosed
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

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_userCode)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        _ = CopyAsync(clipboard);
    }

    private async Task CopyAsync(IClipboard clipboard)
    {
        try
        {
            await clipboard.SetTextAsync(_userCode);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI.Dialogs", "复制设备代码失败");
        }
    }

    private void Finish(object result)
    {
        if (Dialog.IsClosed || _completed) return;

        _completed = true;
        Dialog.Close(result is Exception ? new DialogResult(2) : new DialogResult(1));
        _cancellation.Cancel();

        try
        {
            Dialog.CompletionHandler?.Invoke(result);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI.Dialogs", "登录弹窗完成回调失败");
        }

        BringMainWindowToFront();
    }

    /// <summary>
    ///     登录结束（成功、失败或取消）后把主窗口重新置前，避免弹窗退场后主窗口留在后台。
    ///     等弹窗窗口真正关闭后再激活，立即激活会被随后的窗口关闭动作覆盖。
    /// </summary>
    private static void BringMainWindowToFront()
    {
        _ = BringMainWindowToFrontAsync();
    }

    private static async Task BringMainWindowToFrontAsync()
    {
        await Task.Delay(200);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is not
                    IClassicDesktopStyleApplicationLifetime
                    lifetime)
                    return;

                var window = lifetime.MainWindow;
                if (window is null) return;

                if (!window.IsVisible) window.Show();
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                window.Activate();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "UI.Dialogs", "登录结束后置前主窗口失败");
            }
        });
    }

    /// <summary>第一个按钮重新打开验证网页，第二个按钮复制授权码，取消按钮结束登录。</summary>
    protected override void HandleButton(int number)
    {
        switch (number)
        {
            case 1:
                if (!string.IsNullOrEmpty(_website)) UiInteraction.OpenWebsite(_website);
                return;
            case 2:
                CopyCode();
                return;
            case 3:
                Finish(new OperationCanceledException("用户取消了登录。"));
                return;
            default:
                InvokeActionOrClose(number);
                return;
        }
    }
}