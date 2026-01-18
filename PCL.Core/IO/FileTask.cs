using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.Utils;

namespace PCL.Core.IO;

/// <summary>
/// Default implementation of <see cref="IFileTask"/>.
/// </summary>
/// <param name="items">See <see cref="Items"/></param>
/// <param name="ignoreResult">See <see cref="IgnoreResult"/></param>
public class FileTask(IEnumerable<FileItem> items, bool ignoreResult = false) : IFileTask
{
    /// <summary>
    /// <see cref="FileItem"/> instances.
    /// </summary>
    public IEnumerable<FileItem> Items { get; } = items;

    /// <summary>
    /// Ignore <see cref="ProcessFinished"/> event and drop the result.
    /// </summary>
    public bool IgnoreResult { get; set; } = ignoreResult;

    /// <summary>
    /// Create an empty task. (Anyone really use this?)
    /// </summary>
    /// <param name="ignoreResult">See <see cref="IgnoreResult"/></param>
    public FileTask(bool ignoreResult = false) : this([], ignoreResult) { }
    
    /// <summary>
    /// Create a task with file items
    /// </summary>
    /// <param name="items">file items</param>
    public FileTask(params FileItem[] items) : this(items.AsEnumerable()) { }
    
    /// <summary>
    /// Event invoked with the finished item and the result after a process finished
    /// </summary>
    public event Action<FileItem, AnyType?>? ProcessFinished;
    
    /// <summary>
    /// Event invoked with the result after the task finished
    /// </summary>
    public event Action<object?>? TaskFinished;

    #region Implementation

    public virtual IEnumerable<FileTransfer> GetTransfer(FileItem item) => [];
    
    public virtual FileProcess? GetProcess(FileItem item) => null;
    
    public virtual bool OnProcessFinished(FileItem item, AnyType? result)
    {
        if (ProcessFinished == null) return IgnoreResult;
        ProcessFinished.Invoke(item, result);
        return true;
    }

    public virtual void OnTaskFinished(object? result)
    {
        TaskFinished?.Invoke(result);
    }

    #endregion

    #region Static Methods

    private class SingleFileTask(FileItem item, FileTransfer? transfer, FileProcess? process) : FileTask(item)
    {
        public override IEnumerable<FileTransfer> GetTransfer(FileItem item) => (transfer == null) ? [] : [transfer];
        public override FileProcess? GetProcess(FileItem item) => process;
    }

    public static FileTask FromSingleFile(FileItem item, FileTransfer? transfer = null, FileProcess? process = null)
        => new SingleFileTask(item, transfer, process);

    #endregion
}
