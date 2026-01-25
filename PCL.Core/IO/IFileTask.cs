using System.Collections.Generic;
using PCL.Core.Utils;

namespace PCL.Core.IO;

public interface IFileTask
{
    public IEnumerable<FileItem> Items { get; }

    public IEnumerable<FileTransfer> GetTransfer(FileItem item);
    
    public FileProcess? GetProcess(FileItem item);

    public bool OnProcessFinished(FileItem item, AnyType? result);
    
    public void OnTaskFinished(object? result);
}
