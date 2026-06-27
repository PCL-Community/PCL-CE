using PCL.Core.App.IoC;
using PCL.Core.UI.MsgBox;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.Loading)]
[LifecycleScope("msgbox", "消息弹窗", true)]
public partial class MsgBoxService
{
    private static readonly Channel<MsgBoxRequest> _Channel = Channel.CreateUnbounded<MsgBoxRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource<MsgBoxResponse>> _Pending = [];
    public static ChannelReader<MsgBoxRequest> Reader => _Channel.Reader;

    [LifecycleStop]
    private static async Task _StopAsync()
    {
        _Channel.Writer.Complete();
        foreach (var (id, tcs) in _Pending)
        {
            tcs.TrySetCanceled();
        }
        _Pending.Clear();
    }

    public static async Task<MsgBoxResponse> ShowAsync(MsgBoxRequest request, CancellationToken ct = default)
    {
        using var timeoutCts = request.Timeout is not null
            ? new CancellationTokenSource((TimeSpan)request.Timeout)
            : null;
        using var linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(ct, request.CancellationToken, timeoutCts.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(ct, request.CancellationToken);

        var effectiveCt = linkedCts.Token;

        var tcs = new TaskCompletionSource<MsgBoxResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_Pending.TryAdd(request.RequestId, tcs))
        {
            throw new InvalidOperationException($"Duplicate request ID: {request.RequestId}");
        }

        await using var _ = effectiveCt.Register(() =>
        {
            if (_Pending.TryRemove(request.RequestId, out var value))
            {
                value.TrySetCanceled(effectiveCt);
            }
        });

        try
        {
            await _Channel.Writer.WriteAsync(request, effectiveCt).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (_Pending.TryRemove(request.RequestId, out var _))
            {
                // already handled by Register above
            }

            throw;
        }
    }

    public static MsgBoxResponse Show(MsgBoxRequest request)
    {
        if (_IsOnUiThread())
        {
            return _ShowOnUiThread(request);
        }

        return Task.Run(() => ShowAsync(request)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Used by UI layer to complete the request
    /// </summary>
    public static void Complete(Guid requestId, MsgBoxResponse response)
    {
        if (!_IsOnUiThread())
        {
            throw new InvalidOperationException("Complete must be called on the UI thread.");
        }

        if (_Pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    private static bool _IsOnUiThread() => System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true;

    private static MsgBoxResponse _ShowOnUiThread(MsgBoxRequest request)
    {
        var tcs = new TaskCompletionSource<MsgBoxResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_Pending.TryAdd(request.RequestId, tcs))
        {
            throw new InvalidOperationException($"Duplicate request ID: {request.RequestId}");
        }

        _Channel.Writer.TryWrite(request);

        var frame = new DispatcherFrame();
        tcs.Task.ContinueWith(_ => frame.Continue = false,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        Dispatcher.PushFrame(frame);

        // already completed, will not be blocked
        return tcs.Task.GetAwaiter().GetResult();
    }
}