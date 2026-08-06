using System.Collections.Generic;
using System.Linq;
using PCL.Core.Minecraft.Modpack;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// 内存中的 <see cref="IModpackArchiveReader"/> 实现，用于格式识别与清单解析的单元测试。
/// </summary>
internal sealed class FakeModpackArchive : IModpackArchiveReader
{
    private readonly Dictionary<string, string> _entries;

    public FakeModpackArchive(params KeyValuePair<string, string>[] entries)
    {
        _entries = entries.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    public FakeModpackArchive(params string[] entryNames)
    {
        _entries = entryNames.ToDictionary(name => name, _ => "");
    }

    public IEnumerable<string> EntryNames => _entries.Keys;

    public bool EntryExists(string entryName)
    {
        return _entries.ContainsKey(entryName);
    }

    public string ReadEntryText(string entryName)
    {
        return _entries[entryName];
    }
}
