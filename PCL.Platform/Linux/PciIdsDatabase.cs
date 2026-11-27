namespace PCL.Platform;

/// <summary>读取系统 pci.ids 数据库，把 PCI 厂商/设备 ID 转换成可读名称与核心代号。</summary>
internal static class PciIdsDatabase
{
    private static readonly string[] CandidatePaths =
    [
        "/usr/share/hwdata/pci.ids",
        "/usr/share/misc/pci.ids",
        "/usr/share/pci.ids",
        "/usr/local/share/pci.ids"
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, string>>
        LazyEntries = new(LoadEntries);

    public static bool TryGetDevice(
        string vendorId,
        string deviceId,
        out string? codename,
        out string? model)
    {
        codename = null;
        model = null;
        var key = $"{Normalize(vendorId)}:{Normalize(deviceId)}";
        if (!LazyEntries.Value.TryGetValue(key, out var name))
            return false;

        var text = name.Trim();
        var bracket = text.IndexOf('[');
        if (bracket > 0 && text.EndsWith(']'))
        {
            codename = text[..bracket].Trim();
            model = text[(bracket + 1)..^1].Trim();
        }
        else
        {
            model = text;
        }

        return !string.IsNullOrWhiteSpace(model);
    }

    private static Dictionary<string, string> LoadEntries()
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in CandidatePaths)
            try
            {
                if (!File.Exists(path))
                    continue;

                ParseFile(path, entries);
                if (entries.Count != 0)
                    return entries;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

        return entries;
    }

    private static void ParseFile(string path, Dictionary<string, string> entries)
    {
        string? vendor = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (line.StartsWith("C ", StringComparison.Ordinal))
                break;

            if (line[0] != '\t')
            {
                var separator = line.IndexOfAny([' ', '\t']);
                vendor = separator <= 0
                    ? null
                    : line[..separator].Trim();
                continue;
            }

            if (vendor is null || (line.Length > 1 && line[1] == '\t'))
                continue;

            var text = line[1..];
            var nameSeparator = text.IndexOfAny([' ', '\t']);
            if (nameSeparator <= 0)
                continue;

            var device = text[..nameSeparator].Trim();
            var name = text[(nameSeparator + 1)..].Trim();
            if (device.Length != 4 || name.Length == 0)
                continue;

            entries.TryAdd($"{vendor}:{device}", name);
        }
    }

    private static string Normalize(string id)
    {
        var value = id.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];
        return value.ToLowerInvariant();
    }
}