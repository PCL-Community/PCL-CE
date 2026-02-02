using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Pipes;

public class PipeClient(
    string serverName,
    string pipeName,
    uint timeoutMilliseconds = 1000) : IDisposable, IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipeClient = new(
        serverName, pipeName, PipeDirection.InOut);

    private readonly TimeSpan _timeOut = TimeSpan.FromMilliseconds(timeoutMilliseconds);

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly SemaphoreSlim _readLock = new(1, 1);

    private StreamReader _Reader => field ??= new StreamReader(this._pipeClient);

    private StreamWriter _Writer => field ??= new StreamWriter(this._pipeClient);

    public bool IsConnected => this._pipeClient.IsConnected;

    /// <exception cref="TimeoutException">Throws if timed out.</exception>
    public async Task ConnectAsync(CancellationToken token = default)
    {
        if (this.IsConnected)
        {
            return;
        }

        using var timeOutcts = new CancellationTokenSource(this._timeOut);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeOutcts.Token);

        try
        {
            await this._pipeClient.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeOutcts.IsCancellationRequested)
        {
            throw new TimeoutException($"Pipe connection timed out after {this._timeOut.TotalMilliseconds}", ex);
        }
    }

    public async Task WriteLineAsync(string content, CancellationToken token = default)
    {
        await this._EnsureConnectedAsync(token).ConfigureAwait(false);
        await this._writeLock.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var mem = content.AsMemory();
            // 别问为啥不用string重载，他编译器不高兴，不让我用 :<
            await this._Writer.WriteLineAsync(mem, token).ConfigureAwait(false);
            await this._Writer.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            this._writeLock.Release();
        }
    }

    public async Task WriteAsync(string content, CancellationToken token = default)
    {
        await this._EnsureConnectedAsync(token).ConfigureAwait(false);
        await this._writeLock.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var mem = content.AsMemory();
            await this._Writer.WriteAsync(mem, token).ConfigureAwait(false);
            await this._Writer.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            this._writeLock.Release();
        }
    }

    public async Task<string?> ReadLineAsync(CancellationToken token = default)
    {
        await this._EnsureConnectedAsync(token).ConfigureAwait(false);
        await this._readLock.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var content = await this._Reader.ReadLineAsync(token).ConfigureAwait(false);
            return content;
        }
        finally
        {
            this._readLock.Release();
        }
    }

    public async Task<string> ReadToEndAsync(CancellationToken token = default)
    {
        await this._EnsureConnectedAsync(token).ConfigureAwait(false);
        await this._readLock.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var content = await this._Reader.ReadToEndAsync(token).ConfigureAwait(false);
            return content;
        }
        finally
        {
            this._readLock.Release();
        }
    }

    private async Task _EnsureConnectedAsync(CancellationToken token)
    {
        if (!this.IsConnected)
        {
            await this.ConnectAsync(token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        this._Reader.Dispose();
        this._Writer.Dispose();
        this._pipeClient.Dispose();
        this._writeLock.Dispose();
        this._readLock.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        this._Reader.Dispose();
        await this._Writer.DisposeAsync().ConfigureAwait(true);
        await this._pipeClient.DisposeAsync().ConfigureAwait(true);
        this._writeLock.Dispose();
        this._readLock.Dispose();
    }
}