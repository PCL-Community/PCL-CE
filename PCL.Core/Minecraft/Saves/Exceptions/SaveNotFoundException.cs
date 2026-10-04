using System;

namespace PCL.Core.Minecraft.Saves.Exceptions;

/// <summary>
/// 存档未找到异常 —— level.dat 缺失或指定文件夹非有效存档时抛出。
/// </summary>
public class SaveNotFoundException : Exception
{
    /// <summary>存档文件夹的绝对路径。</summary>
    public string FolderPath { get; }

    public SaveNotFoundException(string folderPath)
        : base($"未找到存档：'{folderPath}' 中缺少 level.dat")
    {
        FolderPath = folderPath;
    }

    public SaveNotFoundException(string folderPath, string message)
        : base(message)
    {
        FolderPath = folderPath;
    }

    public SaveNotFoundException(string folderPath, string message, Exception inner)
        : base(message, inner)
    {
        FolderPath = folderPath;
    }
}
