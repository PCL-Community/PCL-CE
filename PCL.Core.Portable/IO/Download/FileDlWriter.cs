// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// 先写临时文件、完成后原子替换目标文件的异步写入器。
/// </summary>
public sealed class FileDlWriter : IDlWriter, IDisposable, IAsyncDisposable
{
    private const int RetryCount = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly string _finalPath;
    private readonly string _tempPath;
    private FileStream? _stream;

    public bool IsSupportParallel => false;

    public FileDlWriter(string finalPath, string tempExtension = ".PCLDownloading")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        ArgumentNullException.ThrowIfNull(tempExtension);

        _finalPath = Path.GetFullPath(finalPath);
        _tempPath = _finalPath + tempExtension;
    }

    public async ValueTask<Stream> CreateStreamAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_finalPath)!);

        await RemoveTempFileAsync(cancellationToken).ConfigureAwait(false);
        _stream = new FileStream(
            _tempPath,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
        return _stream;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await DisposeStreamAsync().ConfigureAwait(false);
        await RemoveTempFileAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
        await DisposeStreamAsync().ConfigureAwait(false);

        for (var retry = 0; retry < RetryCount; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(_tempPath, _finalPath, overwrite: true);
                return;
            }
            catch (IOException) when (retry < RetryCount - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new IOException($"无法重命名临时文件：{_tempPath} -> {_finalPath}");
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeStreamAsync().ConfigureAwait(false);
    }

    private async ValueTask DisposeStreamAsync()
    {
        if (_stream is null)
            return;

        await _stream.DisposeAsync().ConfigureAwait(false);
        _stream = null;
    }

    private async ValueTask RemoveTempFileAsync(CancellationToken cancellationToken)
    {
        for (var retry = 0; retry < RetryCount; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Delete(_tempPath);
                return;
            }
            catch (IOException) when (retry < RetryCount - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
