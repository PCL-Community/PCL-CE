using PCL.Core.App.Essentials;
using PCL.Core.UI.MsgBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PCL;

public sealed class MsgBoxActor(Grid panMsg, FrameworkElement background) : IDisposable
{
    private readonly Grid _panMsg = panMsg;
    private readonly FrameworkElement _background = background;
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
        _background.Visibility = Visibility.Visible;

        IMsgBoxControl control = CreateControl(request);
        control.Completed += OnControlCOmpleted;

        _panMsg.Children.Add((UIElement)control);
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
        await control.InvokeCloseAnimationAsync().ConfigureAwait(true);
        _panMsg.Children.Remove((UIElement)control);

        if (_panMsg.Children.Count == 0)
        {
            _background.Visibility = Visibility.Collapsed;
        }

        MsgBoxService.Complete(response.RequestId, response);

    }

    private void CancelRequest(Guid requestId)
    {
        IMsgBoxControl? target = null;
        foreach (var child in _panMsg.Children)
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
        request switch
        {
            // add MVVM control type at here
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, null)
        };

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Dispose();
    }
}