using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Pipes;

public class PipeClient(
    string serverName,
    string pipeName,
    uint timeoutMilliseconds = 1000)
{
    private readonly NamedPipeClientStream _pipeClient = new(
        serverName, pipeName, PipeDirection.InOut);

    private readonly TimeSpan _timeOut = TimeSpan.FromMilliseconds(timeoutMilliseconds);

    public bool IsConnected => _pipeClient.IsConnected;

    private StreamReader _Reader
    {
        get
        {
            field ??= new StreamReader(_pipeClient);
            return field;
        }
    }

    private StreamWriter _Writer
    {
        get
        {
            field ??= new StreamWriter(_pipeClient);
            return field;
        }
    }

    public async Task ConnectAsync(CancellationToken token = default)
    {
        if (IsConnected)
        {
            return;
        }

        await _pipeClient.ConnectAsync(_timeOut, token).ConfigureAwait(false);
    }

    public async Task WriteLineAsync(string content, CancellationToken token = default)
    {
        if (!IsConnected)
        {
            await ConnectAsync(token).ConfigureAwait(false);
        }

        var mem = content.AsMemory();
        // 别问为啥不用string重载，他编译器不高兴，不让我用 :<
        await _Writer.WriteLineAsync(mem, token).ConfigureAwait(false);
        await _Writer.FlushAsync(token).ConfigureAwait(false);
    }

    public async Task WriteAsync(string content, CancellationToken token = default)
    {
        if (!IsConnected)
        {
            await ConnectAsync(token).ConfigureAwait(false);
        }

        var mem = content.AsMemory();
        await _Writer.WriteAsync(mem, token).ConfigureAwait(false);
        await _Writer.FlushAsync(token).ConfigureAwait(false);
    }

    public async Task<string?> ReadLineAsync(CancellationToken token = default)
    {
        if (!IsConnected)
        {
            await ConnectAsync(token).ConfigureAwait(false);
        }

        var content = await _Reader.ReadLineAsync(token).ConfigureAwait(false);
        return content;
    }

    public async Task<string> ReadToEndAsync(CancellationToken token = default)
    {
        if (!IsConnected)
        {
            await ConnectAsync(token).ConfigureAwait(false);
        }

        var content = await _Reader.ReadToEndAsync(token).ConfigureAwait(false);
        return content;
    }
}