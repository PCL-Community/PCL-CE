using System;
using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Cache.Models;

public class HttpCacheUpdateHandle(HttpCacheRepository repo) : IDisposable
{
    
    private BlockingStream? _fileStream;
    private bool _disposed;
    /// <summary>
    /// 该对象对应的缓存信息
    /// </summary>
    public HttpCacheDetails? Details { get; set; }
    /// <summary>
    /// 获取当前文件的写入流
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public BlockingStream GetOutputStream()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(HttpCacheUpdateHandle));
        return _fileStream ??= new BlockingStream();
    }

    ~HttpCacheUpdateHandle()
    {
        _Dispose(false);
    }

    public void Dispose()
    {
        _Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void _Dispose(bool dispose)
    {
        _DisposeAsync(dispose).GetAwaiter().GetResult();
    }

    private async Task _DisposeAsync(bool dispose)
    {
        if (_disposed) return;
        await repo.TryEndUpdateAsync(this);
        Details?.Status = HttpCacheStatus.Ok;
        _disposed = true;
        if (dispose) _fileStream?.Dispose();
    }
}