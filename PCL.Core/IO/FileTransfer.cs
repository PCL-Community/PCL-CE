using System;
using System.IO;

namespace PCL.Core.IO;

/// <summary>
/// Transfer a <see cref="FileItem"/> from predefined <see cref="FileItem.Sources"/>
/// to <see cref="FileItem.TargetPath"/>, return the real path by <paramref name="resultCallback"/>,
/// or <c>null</c> if transfer has failed
/// </summary>
public delegate void FileTransfer(FileItem item, Action<string?> resultCallback);

/// <summary>
/// Mark a transfer as failed. File service will try the next transfer automatically.
/// </summary>
/// <param name="reason">failed reason</param>
/// <param name="item">the failed file item</param>
/// <param name="innerException">the exception causing the fail</param>
public class TransferFailedException(string reason, FileItem item, Exception? innerException = null)
    : Exception($"{reason}: {item}", innerException)
{
    public FileItem FileItem { get; } = item;
    public string Reason { get; } = reason;
}

public static class FileTransfers
{
    public static readonly FileTransfer DoNothing = ((item, callback) => callback(item.TargetPath));
    
    public static readonly FileTransfer CreateIfNotExist = ((item, callback) =>
    {
        if (!File.Exists(item.TargetPath)) File.Create(item.TargetPath).Close();
        callback(item.TargetPath);
    });
}
