using System.Collections.Generic;
using System.IO;

namespace PCL.Core.IO;

public enum FileType
{
    /// <summary>
    /// Not a special file.<br/>
    /// Storage path relative to <see cref="FileService.DefaultDirectory"/>.
    /// </summary>
    Plain,
    /// <summary>
    /// Common data: per-instance config JSON, game instance database, etc.<br/>
    /// Storage path relative to <see cref="FileService.DataPath"/>.
    /// </summary>
    Data,
    /// <summary>
    /// Shared data among all launcher instances: shared config JSON, global database, etc.<br/>
    /// Storage path relative to <see cref="FileService.SharedDataPath"/>.
    /// </summary>
    SharedData,
    /// <summary>
    /// File that can be restored anytime: help files, tools downloaded from the web, etc.<br/>
    /// Storage path relative to <see cref="FileService.LocalDataPath"/>.
    /// </summary>
    LocalData,
    /// <summary>
    /// Temporary file.<br/>
    /// Storage path relative to <see cref="FileService.TempPath"/>.
    /// </summary>
    Temporary,
}

/// <summary>
/// File process unit.
/// </summary>
/// <param name="Name">The file name, including extension.</param>
/// <param name="Type">The special type of the file, or <see cref="FileType.Plain"/> as the default value.</param>
/// <param name="Sources">Transfer sources, such as a web URL or a local path.</param>
/// <param name="ForceTransfer">Trigger transfer regardless of whether <see cref="TargetPath"/> exists</param>
public record FileItem(
    string Name,
    FileType Type = FileType.Plain,
    IEnumerable<string>? Sources = null,
    bool ForceTransfer = false)
{
    /// <summary>
    /// The parent directory path of the file. Special types should keep <c>null</c> value.
    /// </summary>
    public string? TargetDirectory
    {
        get;
        set => field = (value == null) ? null : Path.Combine(FileService.DefaultDirectory, value);
    }

    /// <summary>
    /// The path to storage the file.<br/>
    /// If <see cref="TargetDirectory"/> has been set, return the path with <see cref="Name"/> relative to it;
    /// if the value has been set manually, return the value directly;
    /// otherwise return a combined path depends on <see cref="Type"/> and <see cref="Name"/>.
    /// </summary>
    public string TargetPath
    {
        get
        {
            if (TargetDirectory != null) return Path.Combine(TargetDirectory, Name);
            if (field != null) return field;
            var directory = Type switch
            {
                // special types
                FileType.Data => FileService.DataPath,
                FileType.LocalData => FileService.LocalDataPath,
                FileType.SharedData => FileService.SharedDataPath,
                FileType.Temporary => FileService.TempPath,
                // other types: relative to default
                _ => FileService.DefaultDirectory
            };
            var value = Path.Combine(directory, Name);
            field = value;
            return value;
        }
        set => field = Path.Combine(FileService.DefaultDirectory, value);
    }

    public FileInfo GetFileInfo() => new(TargetPath);

    public void CreateDirectory()
    {
        var dir = Path.GetDirectoryName(TargetPath);
        if (dir is not null && dir.Length > 0)
            Directory.CreateDirectory(dir);
    }
    
    public override int GetHashCode() => TargetPath.GetHashCode();

    public override string ToString()
    {
        var str = TargetPath;
        if (Sources != null) str += $" [{string.Join(", ", Sources)}]";
        return str;
    }

    public static FileItem FromLocalFile(string name, FileType fileType = FileType.Plain, string? path = null)
    {
        var item = new FileItem(name, fileType);
        if (path != null) item.TargetPath = path;
        return item;
    }

    public static FileItem FromLocalPath(string path, FileType fileType = FileType.Plain)
    {
        var name = Path.GetFileName(path);
        return FromLocalFile(name, fileType, path);
    }
}
