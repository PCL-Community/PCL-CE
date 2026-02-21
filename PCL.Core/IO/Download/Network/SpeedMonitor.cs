using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download.Network;

public class SpeedMonitor : IAsyncDisposable
{
    private readonly CancellationTokenSource _targetCts;
    private readonly double _minSpeedBytesPerSec;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _gracePeriod;

    private long _bytesReadInCurrentInterval;
    private bool _isRunning = true;
    private readonly Task _monitorTask;
    private readonly CancellationTokenSource _internalCts;

    public SpeedMonitor(
        CancellationTokenSource targetCts,
        double minSpeedBytesPerSec,
        TimeSpan checkInterval,
        TimeSpan? gracePeriod = null)
    {
        _targetCts = targetCts;
        _minSpeedBytesPerSec = minSpeedBytesPerSec;
        _checkInterval = checkInterval;
        _gracePeriod = gracePeriod ?? TimeSpan.FromSeconds(3);

        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(_targetCts.Token);

        _monitorTask = Task.Run(_MonitorLoopAsync);
    }

    public void ReportBytesRead(int bytesRead)
    {
        Interlocked.Add(ref _bytesReadInCurrentInterval, bytesRead);
    }

    private async Task _MonitorLoopAsync()
    {
        try
        {
            if (_gracePeriod > TimeSpan.Zero)
            {
                await Task.Delay(_gracePeriod, _targetCts.Token).ConfigureAwait(false);
            }

            while (_isRunning && !_targetCts.IsCancellationRequested)
            {
                await Task.Delay(_checkInterval, _targetCts.Token).ConfigureAwait(false);


                var bytesRead = Interlocked.Exchange(ref _bytesReadInCurrentInterval, 0);
                var currentSpeedBps = bytesRead / _checkInterval.TotalSeconds;

                if (currentSpeedBps < _minSpeedBytesPerSec)
                {
                    try
                    {
                        await _targetCts.CancelAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        // ignore
                    }

                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _isRunning = false;

        if (!_internalCts.IsCancellationRequested)
        {
            await _internalCts.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await _monitorTask.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        _internalCts.Dispose();
    }
}