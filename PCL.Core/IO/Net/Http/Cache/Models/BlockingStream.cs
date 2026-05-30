using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace PCL.Core.IO.Net.Http.Cache.Models;

public class BlockingStream:MemoryStream
{
    private SemaphoreSlim _lock = new(0);

    [Obsolete("请使用支持取消重载的 Read", error:true)]
    public new int Read(byte[] buffer, int offset, int count)
    {
        return base.Read(buffer, offset, count);
    }

    public int Read(Span<byte> buffer, CancellationToken token)
    {
        _lock.Wait(token);
        return base.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
    {
        _lock.Wait(cancellationToken);
        return base.ReadAsync(buffer, cancellationToken);
    }

    internal void Readable()
    {
        _lock.Release();
    }

    protected override void Dispose(bool disposing)
    {
        _lock.Dispose();
        base.Dispose(disposing);
    }
}