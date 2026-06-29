using PCL.Core.App.Essentials;
using PCL.Core.UI.MsgBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PCL;

public sealed class MsgBoxActor(Grid panMsg, FrameworkElement background) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Dictionary<Guid, CancellationTokenRegistration> _cancellations = [];

    public void Start()
    {
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var reader = MsgBoxService.Reader;
        try
        {
            await foreach (var request in reader.ReadAllAsync(ct))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => ShowOnUi(request),
                    DispatcherPriority.Normal,
                    ct);

                if (request.Timeout is not null || request.CancellationToken.CanBeCanceled)
                {
                    var combinedCt = request.CancellationToken;
                    if (request.Timeout is not null)
                    {
                        var timeoutCts = new CancellationTokenSource((TimeSpan)request.Timeout);
                        combinedCt =
                            CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken,
                                timeoutCts.Token).Token;
                    }

                    var reg = combinedCt.Register(() =>
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CancelRequest(request.RequestId);
                        });
                    });

                    lock (_cancellations)
                    {
                        _cancellations[request.RequestId] = reg;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normally exit, ignore
        }
    }

    private void ShowOnUi(MsgBoxRequest request)
    {
        background.Visibility = Visibility.Visible;

        IMsgBoxControl control = CreateControl(request);
        control.Completed += OnControlCOmpleted;

        panMsg.Children.Add((UIElement)control);
        control.InvokeShowAnimation();
    }

    private void OnControlCOmpleted(object? sender, MsgBoxResponse response)
    {
        if (sender is not IMsgBoxControl control)
        {
            return;
        }

        control.Completed -= OnControlCOmpleted;

        response.Button?.OnClick?.Invoke();

        lock (_cancellations)
        {
            if (_cancellations.TryGetValue(response.RequestId, out var reg))
            {
                reg.Dispose();
                _cancellations.Remove(response.RequestId);
            }
        }

        _ = DoCloseAsync(control, response);
    }

    private async Task DoCloseAsync(IMsgBoxControl control, MsgBoxResponse response)
    {
        await control.InvokeCloseAnimationAsync(response).ConfigureAwait(true);
        panMsg.Children.Remove((UIElement)control);

        if (panMsg.Children.Count == 0)
        {
            background.Visibility = Visibility.Collapsed;
        }

        MsgBoxService.Complete(response.RequestId, response);

    }

    private void CancelRequest(Guid requestId)
    {
        IMsgBoxControl? target = null;
        foreach (var child in panMsg.Children)
        {
            if (child is IMsgBoxControl c &&
                c.Request.RequestId == requestId)
            {
                target = c;
                break;
            }
        }

        if (target is not null)
        {
            DoCloseAsync(target, MsgBoxResponse.Cancelled(requestId)).GetAwaiter().GetResult(); // idk what should i do to handle this async method
        }
    }

    private IMsgBoxControl CreateControl(MsgBoxRequest request) =>
        request.RequestType switch
        {
            // add MVVM control type at here
            MsgBoxRequestType.Text => new MyMsgText(request),
            MsgBoxRequestType.Select => new MyMsgSelect(request),
            MsgBoxRequestType.Input => new MyMsgInput(request),
            MsgBoxRequestType.Login => new MyMsgLogin(request, request.Content as JsonObject),
            MsgBoxRequestType.Markdown => new MyMsgMarkdown(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, null)
        };

    /// <summary>处理键盘事件（由 FormMain_KeyDown 调用）</summary>
    public void HandleKeyEvent(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat || panMsg.Children.Count == 0)
            return;

        var msg = panMsg.Children[0];

        if (e.Key == Key.Enter)
        {
            Action? enterAction = msg switch
            {
                MyMsgInput input => () => input.Btn1_Click(sender, null),
                MyMsgSelect select => () => select.Btn1_Click(sender, null),
                MyMsgText text => () => text.Btn1_Click(sender, null),
                MyMsgMarkdown markdown => () => markdown.Btn1_Click(sender, null),
                MyMsgLogin login => () => login.Btn1_Click(sender, null),
                _ => null
            };
            enterAction?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Action? escapeAction = msg switch
            {
                MyMsgInput input => input.Btn2.Visibility == Visibility.Visible
                    ? () => input.Btn2_Click(sender, null)
                    : () => input.Btn1_Click(sender, null),
                MyMsgSelect select => select.Btn2.Visibility == Visibility.Visible
                    ? () => select.Btn2_Click(sender, null)
                    : () => select.Btn1_Click(sender, null),
                MyMsgText text => text.Btn3.Visibility == Visibility.Visible
                    ? () => text.Btn3_Click(sender, null)
                    : text.Btn2.Visibility == Visibility.Visible
                        ? () => text.Btn2_Click(sender, null)
                        : () => text.Btn1_Click(sender, null),
                MyMsgMarkdown markdown => markdown.Btn3.Visibility == Visibility.Visible
                    ? () => markdown.Btn3_Click(sender, null)
                    : markdown.Btn2.Visibility == Visibility.Visible
                        ? () => markdown.Btn2_Click(sender, null)
                        : () => markdown.Btn1_Click(sender, null),
                MyMsgLogin login => login.Btn3.Visibility == Visibility.Visible
                    ? () => login.Btn3_Click(sender, null)
                    : () => login.Btn1_Click(sender, null),
                _ => null
            };
            escapeAction?.Invoke();
            e.Handled = true;
            return;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Dispose();
    }
}