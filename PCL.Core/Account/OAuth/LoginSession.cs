using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Account.OAuth;

public abstract class LoginSession<TAccount> where TAccount : class
{
    protected readonly TaskCompletionSource<TAccount> _tcs = new();

    public event EventHandler<AuthStep> StateChanged;

    protected void OnStateChanged(AuthStep step)
    {
        // 触发事件，通知 UI 更新
        StateChanged?.Invoke(this, step);
    }

    public string? AuthUrl { get; protected set; }
    public string? AccessToken { get; protected set; }
    public string? RefreshToken { get; protected set; }
    public int ExpireIn { get; protected set; }
    public Task<TAccount> WaitForResultAsync(CancellationToken ct)
    {
        ct.Register(() => _tcs.TrySetCanceled());
        return _tcs.Task;
    }

    public abstract Task BeginAsync();
}