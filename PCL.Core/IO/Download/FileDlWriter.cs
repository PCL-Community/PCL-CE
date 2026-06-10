using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download;

/// <summary>
/// File-based download writer that writes to a temp file and renames on completion.
/// </summary>
public class FileDlWriter : IDlWriter, IDisposable
{
    private readonly string _finalPath;
    private readonly string _tempPath;
    private FileStream? _stream;

    public bool IsSupportParallel => false;

    public FileDlWriter(string finalPath, string tempExtension = ".PCLDownloading")
    {
        _finalPath = finalPath ?? throw new ArgumentNullException(nameof(finalPath));
        _tempPath = finalPath + tempExtension;
    }

    public Task<Stream> CreateStreamAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_finalPath)
            ?? throw new ArgumentException("下载路径无效", nameof(_finalPath)));

        RemoveTempFile();
        _stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write,
            FileShare.None, 8192, useAsync: true);

        return Task.FromResult((Stream)_stream);
    }

    public Task StopAsync()
    {
        _stream?.Dispose();
        _stream = null;
        RemoveTempFile();
        return Task.CompletedTask;
    }

    public Task FinishAsync()
    {
        _stream?.Dispose();
        _stream = null;

        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                File.Move(_tempPath, _finalPath, overwrite: true);
                return Task.CompletedTask;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }

        throw new IOException($"无法重命名临时文件：{_tempPath} -> {_finalPath}");
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }

    private void RemoveTempFile()
    {
        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                if (File.Exists(_tempPath))
                    File.Delete(_tempPath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
