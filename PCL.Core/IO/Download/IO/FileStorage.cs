using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader.IO;

public class FileStorage : IDisposable
{
    private readonly SafeFileHandle _fileHandle;

    public FileStorage(string filePath, long totalSize)
    {
        _fileHandle = File.OpenHandle(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Write,
            FileOptions.Asynchronous
        );

        RandomAccess.SetLength(_fileHandle, totalSize);
    }

    public ValueTask WriteChunkAsync(long offset, ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        return RandomAccess.WriteAsync(_fileHandle, buffer, offset, token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fileHandle.Dispose();
    }
}