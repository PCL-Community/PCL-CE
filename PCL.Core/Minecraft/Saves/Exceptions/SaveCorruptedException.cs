using System;

namespace PCL.Core.Minecraft.Saves.Exceptions;

/// <summary>
/// 存档损坏异常 —— level.dat 存在但无法解析或无法写入时抛出。
/// </summary>
public class SaveCorruptedException : Exception
{
    /// <summary>存档文件夹的绝对路径。</summary>
    public string FolderPath { get; }

    public SaveCorruptedException(string folderPath)
        : base($"存档损坏：无法解析 '{folderPath}' 中的 level.dat")
    {
        FolderPath = folderPath;
    }

    public SaveCorruptedException(string folderPath, string message)
        : base(message)
    {
        FolderPath = folderPath;
    }

    public SaveCorruptedException(string folderPath, string message, Exception inner)
        : base(message, inner)
    {
        FolderPath = folderPath;
    }
}
