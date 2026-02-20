using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Account.OAuth;

public abstract class LoginSession<TAccount> where TAccount : class
{
    protected readonly TaskCompletionSource<TAccount> Tcs = new();

    public event EventHandler<AuthStep>? StateChanged;

    protected void OnStateChanged(AuthStep step)
    {
        // 触发事件，通知 UI 更新
        StateChanged?.Invoke(this, step);
    }

    public string? AuthUrl { get; protected set; }
    public string? AccessToken { get; protected set; }
    public string? RefreshToken { get; protected set; }
    public int ExpireIn { get; protected set; }
    public Task<TAccount> WaitForResultAsync(CancellationToken ct = default)
    {
        ct.Register(() => Tcs.TrySetCanceled());
        return Tcs.Task;
    }

    public abstract Task BeginAsync();
}