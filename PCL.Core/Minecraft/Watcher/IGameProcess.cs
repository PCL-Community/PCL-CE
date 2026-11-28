using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>游戏进程的最小观察接口，便于在单元测试中替换真实进程。</summary>
internal interface IGameProcess : IDisposable
{
    int Id { get; }

    bool HasExited { get; }

    DateTime? StartTime { get; }

    int? ExitCode { get; }

    event Action<string>? OutputLineReceived;

    event Action<string>? ErrorLineReceived;

    void StartReading();

    Task WaitForExitAsync(CancellationToken cancellationToken = default);

    bool WaitForExit(TimeSpan timeout);

    void Kill(bool entireProcessTree);
}

/// <summary>基于 <see cref="Process" /> 的默认实现。</summary>
internal sealed class SystemGameProcess : IGameProcess
{
    private readonly Process _process;

    private bool _disposed;

    public SystemGameProcess(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
    }

    public int Id
    {
        get
        {
            try
            {
                return _process.Id;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public DateTime? StartTime
    {
        get
        {
            try
            {
                return _process.StartTime;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            try
            {
                return _process.HasExited ? _process.ExitCode : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public event Action<string>? OutputLineReceived;

    public event Action<string>? ErrorLineReceived;

    public void StartReading()
    {
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _process.WaitForExit(1000);
        }
        catch (Exception)
        {
            // 进程可能已经被释放，输出流已经关闭。
        }
    }

    public bool WaitForExit(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            return HasExited;

        try
        {
            return _process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Kill(bool entireProcessTree)
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree);
        }
        catch (Exception)
        {
            // 进程可能恰好已经退出。
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.ErrorDataReceived -= OnErrorDataReceived;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (_disposed || e.Data is null) return;
        OutputLineReceived?.Invoke(e.Data);
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (_disposed || e.Data is null) return;
        ErrorLineReceived?.Invoke(e.Data);
    }
}