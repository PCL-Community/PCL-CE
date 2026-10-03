using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace PCL;

/// <summary>
/// 按发布时间从旧到新排列的原版 id 快照。比较和分类先取局部变量，避免读到一半被替换。
/// </summary>
public sealed class VanillaVersionIndex
{
    private static VanillaVersionIndex? _current;
    private static readonly object _Gate = new();
    private static Func<IReadOnlyList<string>>? _embeddedReader;

    private static readonly Regex _Prefix2021 = new(@"^(20|21)\.", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Dictionary<string, int> _lineById;
    private readonly string?[] _releaseFamily;
    private readonly Dictionary<string, int> _familyNewestLine;
    private readonly Dictionary<string, int> _familyOrder;

    public IReadOnlyList<string> Ids { get; }

    /// <summary>1.0 的行号。这一行之前是远古版。</summary>
    public int AnchorLine { get; }

    /// <summary>家族 id，最新在前。顺序是该家族最新一份 Release 的行号降序。</summary>
    public IReadOnlyList<string> Families { get; }

    public VanillaVersionIndex(IReadOnlyList<string> ids)
    {
        var list = new List<string>(ids.Count);
        _lineById = new Dictionary<string, int>(ids.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var raw in ids)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var id = raw.Trim();
            if (_lineById.ContainsKey(id)) continue;
            _lineById.Add(id, list.Count);
            list.Add(id);
        }

        Ids = list;

        AnchorLine = _lineById["1.0"];

        _releaseFamily = new string?[list.Count];
        _familyNewestLine = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            if (McVersionClassifier.Classify(list[i], i, AnchorLine, null) != McVersionCategory.Release)
                continue;
            var family = McVersionClassifier.ReleaseFamily(list[i]);
            if (family is null) continue;
            _releaseFamily[i] = family;
            _familyNewestLine[family] = i;
        }

        Families = _familyNewestLine
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToArray();
        _familyOrder = new Dictionary<string, int>(Families.Count, StringComparer.Ordinal);
        for (var i = 0; i < Families.Count; i++)
            _familyOrder[Families[i]] = i;
    }

    public static VanillaVersionIndex? Current => Volatile.Read(ref _current);

    /// <summary>由启动器注册。尚未发布索引时，Capture 用它同步读本地列表。</summary>
    public static void SetEmbeddedReader(Func<IReadOnlyList<string>> reader) =>
        Volatile.Write(ref _embeddedReader, reader);

    /// <summary>由启动器注册。生命周期服务调用它在后台刷新索引，不阻塞启动。</summary>
    public static Action? BeginRefresh;

    /// <summary>当前快照。尚未发布时同步读取已注册的本地列表并发布，不覆盖已经发布的快照。</summary>
    public static VanillaVersionIndex? Capture()
    {
        var index = Volatile.Read(ref _current);
        if (index is not null) return index;
        var reader = Volatile.Read(ref _embeddedReader);
        if (reader is null) return null;
        lock (_Gate)
        {
            index = Volatile.Read(ref _current);
            if (index is not null) return index;
            var ids = reader();
            index = new VanillaVersionIndex(ids);
            Interlocked.Exchange(ref _current, index);
            return index;
        }
    }

    public static void Publish(IReadOnlyList<string> ids)
    {
        var index = new VanillaVersionIndex(ids);
        lock (_Gate)
            Interlocked.Exchange(ref _current, index);
    }

    public static bool TryGetLine(string? candidate, out int line)
    {
        var index = Capture();
        if (index is null)
        {
            line = -1;
            return false;
        }

        return index.MatchLine(candidate, out line);
    }

    public static bool TryResolve(string? candidate, out string canonical)
    {
        var index = Capture();
        if (index is not null && index.MatchLine(candidate, out var line))
        {
            canonical = index.Ids[line];
            return true;
        }

        canonical = "";
        return false;
    }

    public static int CompareLines(int? left, int? right)
    {
        if (left is int leftLine && right is int rightLine)
            return leftLine.CompareTo(rightLine);
        if (left is not null) return 1;
        if (right is not null) return -1;
        return 0;
    }

    public bool MatchLine(string? candidate, out int line)
    {
        line = -1;
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var text = candidate.Trim().Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
        if (_lineById.TryGetValue(text, out line)) return true;
        var dashed = text.Replace('_', '-');
        if (!string.Equals(dashed, text, StringComparison.Ordinal) && _lineById.TryGetValue(dashed, out line))
            return true;
        if (_Prefix2021.IsMatch(text) && _lineById.TryGetValue("1." + text, out line))
            return true;
        line = -1;
        return false;
    }

    public string? FamilyOf(string? id, bool allowSnapshot)
    {
        if (!MatchLine(id, out var line)) return null;
        if (_releaseFamily[line] is string own) return own;
        if (!allowSnapshot) return null;
        if (McVersionClassifier.Classify(Ids[line], line, AnchorLine, null) != McVersionCategory.Snapshot)
            return null;
        for (var i = line + 1; i < _releaseFamily.Length; i++)
            if (_releaseFamily[i] is string next)
                return next;
        for (var i = line - 1; i >= 0; i--)
            if (_releaseFamily[i] is string previous)
                return previous;
        return null;
    }

    public List<string> FamiliesFrom(IEnumerable<string> versions, bool allowSnapshot)
    {
        var best = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var version in versions)
        {
            var family = FamilyOf(version, allowSnapshot);
            if (family is null || !_familyNewestLine.TryGetValue(family, out var newest)) continue;
            best[family] = newest;
        }

        return best.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
    }

    public bool ContainsFamily(string? family) =>
        family is not null && _familyOrder.ContainsKey(family);

    public bool IsFamilyOlderThan110(string family) =>
        _familyOrder.TryGetValue(family, out var order) && order > _familyOrder["1.10"];

    public bool IsFamilyEntirelyBefore(string family, string versionId)
    {
        if (!_familyNewestLine.TryGetValue(family, out var familyLine)) return false;
        if (!MatchLine(versionId, out var versionLine)) return false;
        return familyLine < versionLine;
    }

    public int EarliestReleaseLine(string family)
    {
        for (var i = 0; i < _releaseFamily.Length; i++)
            if (_releaseFamily[i] == family)
                return i;
        throw new KeyNotFoundException(family);
    }

    private int _Line(string id) => _lineById[id];

    public bool IsFormatFit(string? id)
    {
        if (!MatchLine(id, out var line)) return false;
        var canonical = Ids[line];
        var category = McVersionClassifier.Classify(canonical, line, AnchorLine, null);
        return category == McVersionCategory.Release ||
               (category == McVersionCategory.Snapshot && canonical.Contains('.'));
    }

    public static bool IsAtOrAfter(string? versionId, string thresholdId)
    {
        var index = Capture();
        if (index is null || !index.MatchLine(versionId, out var line)) return false;
        return line >= index._Line(thresholdId);
    }

    public static bool IsBefore(string? versionId, string thresholdId)
    {
        var index = Capture();
        if (index is null || !index.MatchLine(versionId, out var line)) return false;
        return line < index._Line(thresholdId);
    }

    public static bool IsAtOrBefore(string? versionId, string thresholdId)
    {
        var index = Capture();
        if (index is null || !index.MatchLine(versionId, out var line)) return false;
        return line <= index._Line(thresholdId);
    }

    /// <summary>在列表中且不新于 1.5.2。</summary>
    public static bool IsAtMost152(string? versionId)
    {
        var index = Capture();
        if (index is null || !index.MatchLine(versionId, out var line)) return false;
        return line <= index._Line("1.5.2");
    }

    public static bool IsLegacyFabricRange(string? versionId)
    {
        var index = Capture();
        if (index is null || !index.MatchLine(versionId, out var line)) return false;
        return line >= index.EarliestReleaseLine("1.3") && line < index._Line("1.14");
    }

    public static bool Is14To15(string? versionId) =>
        IsAtOrAfter(versionId, "1.14") && IsBefore(versionId, "1.16");
}
