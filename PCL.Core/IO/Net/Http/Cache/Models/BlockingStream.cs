using System;
using System.IO;
using System.Threading;

namespace PCL.Core.IO.Net.Http.Cache.Models;

public class BlockingStream:MemoryStream
{
    private SemaphoreSlim _lock = new(0);
    public override int Read(Span<byte> buffer)
    {
        _lock.Wait();
        return base.Read(buffer);
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