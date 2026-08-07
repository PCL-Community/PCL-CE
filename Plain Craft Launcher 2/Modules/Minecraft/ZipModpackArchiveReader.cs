using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PCL.Core.Minecraft.Modpack;

namespace PCL;

/// <summary>
///     基于 <see cref="ZipArchive" /> 的 <see cref="IModpackArchiveReader" /> 实现，供格式识别使用。
/// </summary>
internal sealed class ZipModpackArchiveReader : IModpackArchiveReader
{
    private readonly ZipArchive _archive;

    public ZipModpackArchiveReader(ZipArchive archive)
    {
        _archive = archive;
    }

    public IEnumerable<string> EntryNames
    {
        get
        {
            foreach (var entry in _archive.Entries)
                yield return entry.FullName;
        }
    }

    public bool EntryExists(string entryName)
    {
        return _archive.GetEntry(entryName) is not null;
    }

    public string ReadEntryText(string entryName)
    {
        using var stream = _archive.GetEntry(entryName)?.Open();
        return stream is null ? "" : ModBase.ReadFile(stream);
    }

    public void ExtractEntryToFile(string entryName, string destinationPath)
    {
        using var stream = _archive.GetEntry(entryName)?.Open();
        if (stream is null)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? "");
        using var file = File.Create(destinationPath);
        stream.CopyTo(file);
    }
}
