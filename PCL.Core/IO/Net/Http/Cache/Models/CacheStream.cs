using System;
using System.IO;

namespace PCL.Core.IO.Net.Http.Cache.Models;

public class CacheStream: Stream
{
    private Stream _responseStream;
    private BlockingStream? _destStream;
    private HttpCacheUpdateHandle _handle;
    
    public CacheStream(HttpCacheUpdateHandle handle, byte[] data)
    {
        _responseStream = new MemoryStream(data);
        _destStream = handle.GetOutputStream();
        _handle = handle;
    }

    public CacheStream(HttpCacheUpdateHandle handle, Stream responseStream)
    {
        _responseStream = responseStream;
        _destStream = handle.GetOutputStream();
        _handle = handle;
    }


    public override void Flush() { }
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _responseStream.Read(buffer, offset, count);
        if (read == 0) return read;
        _destStream?.Write(buffer,0, read);
        _destStream?.Readable();
        return read;
    }
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new InvalidOperationException("This stream is readonly.");
    }

    public override void SetLength(long value)
    {
        throw new InvalidOperationException("This stream is readonly.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException("This stream is readonly.");
    }

    public override bool CanRead => _responseStream.CanRead;
    public override bool CanSeek => _responseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _responseStream.Length;
    public override long Position { get => _responseStream.Length;
        set => throw new InvalidOperationException("can not set position on readonly stream");
    }

    protected override void Dispose(bool disposing)
    {
        _destStream?.Dispose();
        _handle.Dispose();
        base.Dispose(disposing);
    }
}