using System.Collections.Generic;
using System.IO;
using PCL.Core.Minecraft.Modpack;

namespace PCL;

/// <summary>
///     基于目录的 <see cref="IModpackArchiveReader" /> 实现，用于直接安装已解压的文件夹形式整合包
///     （例如嵌套整合包内层），避免重新打包为 zip。
/// </summary>
internal sealed class FolderModpackArchiveReader : IModpackArchiveReader
{
    private readonly string _root;

    public FolderModpackArchiveReader(string rootDirectory)
    {
        _root = rootDirectory;
    }

    public IEnumerable<string> EntryNames
    {
        get
        {
            foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
                yield return Path.GetRelativePath(_root, file).Replace("\\", "/");
        }
    }

    public bool EntryExists(string entryName)
    {
        return File.Exists(Path.Combine(_root, entryName));
    }

    public string ReadEntryText(string entryName)
    {
        return ModBase.ReadFile(Path.Combine(_root, entryName));
    }

    public void ExtractEntryToFile(string entryName, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? "");
        File.Copy(Path.Combine(_root, entryName), destinationPath, true);
    }
}
