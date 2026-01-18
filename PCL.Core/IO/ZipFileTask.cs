using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using PCL.Core.Utils;

namespace PCL.Core.IO;

/// <summary>
/// 若写入压缩文件过程中抛出异常，将会抛出该异常，并将原异常作为 <see cref="Exception.InnerException"/> 提供。
/// </summary>
public class ZipFileTaskException(Exception innerException) : Exception(innerException.Message, innerException);

/// <summary>
/// 用于创建 ZIP 格式归档的文件任务。
/// </summary>
/// <param name="zipPath">参考 <see cref="ZipPath"/></param>
/// <param name="items">归档包含的文件</param>
public class ZipFileTask(string zipPath, IEnumerable<FileItem> items) : FileTask(items, true)
{
    /// <summary>
    /// ZIP 文件的路径。若该路径已存在，写入时默认直接覆盖原文件，该行为可以通过 <see cref="OpenFileMode"/> 指定。
    /// </summary>
    public string ZipPath { get; } = zipPath;
    
    /// <summary>
    /// 压缩等级。取值范围为 0~9，默认为 3。
    /// </summary>
    public int ZipLevel { get; set; } = 3;
    
    public bool DeleteWhenException { get; set; } = true;
    
    /// <summary>
    /// 写入 ZIP 文件时使用的 <see cref="FileMode"/>。
    /// </summary>
    public FileMode OpenFileMode { get; set; } = FileMode.Create;
    
    /// <summary>
    /// 写入 ZIP 文件时使用的 <see cref="FileShare"/>。
    /// </summary>
    public FileShare OpenFileShare { get; set; } = FileShare.None;
    
    private readonly List<FileItem> _filesToArchive = [];
    
    public override bool OnProcessFinished(FileItem item, AnyType? result)
    {
        _filesToArchive.Add(item);
        return base.OnProcessFinished(item, result);
    }

    public override void OnTaskFinished(object? result)
    {
        var fileCreated = false;
        try
        {
            using var zipFile = File.Open(ZipPath, OpenFileMode, FileAccess.Write, OpenFileShare);
            fileCreated = true;
            using var zipStream = new ZipOutputStream(zipFile);
            zipStream.SetLevel(ZipLevel);
            foreach (var item in _filesToArchive)
            {
                var entryName = ZipEntry.CleanName(item.Name);
                zipStream.PutNextEntry(new ZipEntry(entryName));
                using var fileInput =
                    File.Open(item.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fileInput.CopyTo(zipStream);
                zipStream.CloseEntry();
            }
            zipStream.Finish();
        }
        catch (Exception ex)
        {
            if (fileCreated && DeleteWhenException) File.Delete(ZipPath);
            throw new ZipFileTaskException(ex);
        }
    }
}
