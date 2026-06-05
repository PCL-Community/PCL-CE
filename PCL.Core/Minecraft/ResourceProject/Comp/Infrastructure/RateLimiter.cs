using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Infrastructure;

public sealed class RateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly SemaphoreSlim _semaphore;
    private int _requestsInWindow;
    private DateTime _windowStart;

    public RateLimiter(int maxRequestsPerMinute = 300)
    {
        _maxRequests = maxRequestsPerMinute;
        _window = TimeSpan.FromMinutes(1);
        _semaphore = new SemaphoreSlim(1, 1);
        _windowStart = DateTime.UtcNow;
    }

    public async Task WaitIfNeeded(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            if (now - _windowStart >= _window)
            {
                _requestsInWindow = 0;
                _windowStart = now;
            }

            if (_requestsInWindow >= _maxRequests)
            {
                var delay = _windowStart + _window - now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                _requestsInWindow = 0;
                _windowStart = DateTime.UtcNow;
            }

            Interlocked.Increment(ref _requestsInWindow);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
