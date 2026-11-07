using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;

namespace PCL.UI.Dialogs;

/// <summary>
///     微软设备码登录弹窗：展示授权码与网页地址，自动打开网页并复制代码，随后按设备码间隔轮询授权结果。
///     关闭弹窗会取消轮询；结果通过 <see cref="DialogViewModel.CompletionHandler" /> 回传。
/// </summary>
public sealed class MyMsgLogin : MyMsgDialogBase
{
    private readonly SelectableTextBlock _caption;

    public MyMsgLogin(DialogViewModel dialog) : base(dialog)
    {
        ViewModel = new MyMsgLoginViewModel();
        ViewModel.Attach(dialog);
        ViewModel.ClipboardWriter = CopyToClipboardAsync;
        ViewModel.Finished += BringMainWindowToFront;

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
        _caption.Bind(
            TextBlock.TextProperty,
            new Binding(nameof(MyMsgLoginViewModel.CaptionText))
            {
                Source = ViewModel
            });
        DialogTheme.BindBrush(_caption, TextBlock.ForegroundProperty, "ColorBrush1");
        SetBody(_caption);

        if (Content is Grid root)
        {
            root.HorizontalAlignment = HorizontalAlignment.Center;
            root.VerticalAlignment = VerticalAlignment.Center;
        }

        LocalizationService.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => ViewModel.Start();
    }

    public MyMsgLoginViewModel ViewModel { get; }

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
        ViewModel.ReloadDeviceCode();
    }

    /// <summary>控件离树即视为弹窗被关闭：停止轮询，并把未完成的结果以取消异常回报给调用方。</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        ViewModel.Finished -= BringMainWindowToFront;
        ViewModel.Cancel();
        BringMainWindowToFront();
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;
        await clipboard.SetTextAsync(text);
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
                ViewModel.OpenWebsite();
                return;
            case 2:
                ViewModel.CopyCode();
                return;
            case 3:
                ViewModel.CancelLogin();
                return;
            default:
                InvokeActionOrClose(number);
                return;
        }
    }
}