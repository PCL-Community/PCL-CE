using System.IO;
using System.Text.RegularExpressions;
using PCL.Core.Logging;

namespace PCL;

internal sealed class CrashModInfo
{
    public string? ModId { get; init; }

    public string? DisplayName { get; init; }

    public string? FileName { get; init; }

    public string? Version { get; init; }

    public string Source { get; init; } = "";

    public string DisplayNameOrFileName =>
        !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName :
        !string.IsNullOrWhiteSpace(FileName) ? FileName :
        ModId ?? "";
}

internal sealed class CrashModIndex
{
    private readonly List<CrashModInfo> _mods = [];

    public static CrashModIndex Create(CrashLogSet logs, McInstance? instance)
    {
        var index = new CrashModIndex();
        index._ReadCrashReport(logs.CrashReport?.Text);
        index._ReadForgeDebugLog(logs.Debug?.Text);
        index._ReadLoaderMessages(logs.Game?.Text);
        if (instance is not null)
            index._ReadInstanceModFiles(instance);
        index._NormalizeAndDeduplicate();
        return index;
    }

    public IEnumerable<CrashModInfo> ResolveMany(IEnumerable<string> hints)
    {
        var found = new List<CrashModInfo>();
        foreach (var hint in hints)
        foreach (var mod in Resolve(hint))
            if (!found.Any(item => _SameDisplay(item, mod)))
                found.Add(mod);
        return found;
    }

    public IEnumerable<CrashModInfo> Resolve(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return [];

        var normalizedHint = _Normalize(hint);
        if (string.IsNullOrWhiteSpace(normalizedHint))
            return [];

        var exact = _mods
            .Where(mod =>
                string.Equals(_Normalize(mod.ModId), normalizedHint, StringComparison.Ordinal) ||
                string.Equals(_Normalize(mod.FileName), normalizedHint, StringComparison.Ordinal) ||
                string.Equals(_Normalize(mod.DisplayName), normalizedHint, StringComparison.Ordinal))
            .ToList();
        if (exact.Count != 0)
            return exact;

        return _mods
            .Where(mod =>
                _Normalize(mod.ModId).Contains(normalizedHint, StringComparison.Ordinal) ||
                _Normalize(mod.FileName).Contains(normalizedHint, StringComparison.Ordinal) ||
                _Normalize(mod.DisplayName).Contains(normalizedHint, StringComparison.Ordinal))
            .ToList();
    }

    public List<string> ResolveToDisplayNames(IEnumerable<string> hints)
    {
        var hintList = hints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct()
            .ToList();
        var mods = ResolveMany(hintList)
            .Select(mod => mod.DisplayNameOrFileName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return mods.Count != 0 ? mods : hintList;
    }

    private void _ReadCrashReport(string? text)
    {
        if (string.IsNullOrEmpty(text) ||
            !text.Contains("A detailed walkthrough of the error", StringComparison.Ordinal))
            return;

        var details = text.Replace("A detailed walkthrough of the error", "¨");
        var isFabric = details.Contains("Fabric Mods", StringComparison.Ordinal);
        if (isFabric)
        {
            details = details.Replace("Fabric Mods", "¨");
            LogWrapper.Info("Crash", "崩溃报告中检测到 Fabric Mod 信息格式");
        }

        var isQuilt = details.Contains("quilt-loader", StringComparison.Ordinal);
        if (isQuilt)
        {
            details = details.Replace("Mod Table Version", "¨");
            LogWrapper.Info("Crash", "崩溃报告中检测到 Quilt Mod 信息格式");
        }

        details = CrashText.AfterLast(details, "¨");
        foreach (var rawLine in details.Split('\n'))
        {
            var line = rawLine.Trim('\r', '\n');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isFabric && line.StartsWith(
                    "\t\t",
                    StringComparison.Ordinal) && !CrashRegex.IsMatch(line, @"\t\tfabric[\w-]*: Fabric"))
            {
                _Add(new CrashModInfo
                {
                    ModId = CrashRegex.First(line, @"(?<=\t\t)[^:]+"),
                    DisplayName = CrashRegex.First(line, @"(?<=: )[^\n]+(?= [^\n]+)"),
                    Version = CrashRegex.First(line, @"(?<= )[\w\.-]+$"),
                    Source = "crash-report-fabric"
                });
                continue;
            }

            if (line.Contains(".jar", StringComparison.OrdinalIgnoreCase) &&
                line.Length - line.Replace(".jar", "", StringComparison.OrdinalIgnoreCase).Length == 4)
                _Add(new CrashModInfo
                {
                    FileName = CrashRegex.First(line,
                        @"(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar",
                        RegexOptions.IgnoreCase),
                    DisplayName = CrashRegex.First(line, @"(?<=\t)[^\t\|]+(?=\s+\()"),
                    ModId = CrashRegex.First(line, @"(?<=\| )[\w\.-]+(?= \|)"),
                    Source = "crash-report-forge"
                });
        }
    }

    private void _ReadForgeDebugLog(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var line in CrashRegex.All(text, "(?<=valid mod file ).*", RegexOptions.Multiline))
            _Add(new CrashModInfo
            {
                FileName = CrashRegex.First(line, ".*(?= with)"),
                ModId = CrashRegex.First(line, @"(?<=with \{)[^\}]+"),
                Version = CrashRegex.First(line, @"(?<=versions \{)[^\}]+"),
                Source = "debug-log"
            });
    }

    private void _ReadLoaderMessages(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var line in CrashRegex.All(text, @"(?<=ModID: )[^,\n]+|(?<=ModId )[^\n]+(?= for )",
                     RegexOptions.IgnoreCase))
            _Add(new CrashModInfo
            {
                ModId = line.Trim(),
                Source = "loader-message"
            });
    }

    private void _ReadInstanceModFiles(McInstance instance)
    {
        foreach (var directory in _GetCandidateModDirectories(instance))
            try
            {
                var info = new DirectoryInfo(directory);
                if (!info.Exists)
                    continue;

                foreach (var file in info.EnumerateFiles("*.jar"))
                    _Add(new CrashModInfo
                    {
                        FileName = file.Name,
                        DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                        Source = "instance-mods"
                    });
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Crash", "读取实例 Mod 文件列表失败（" + directory + "）");
            }
    }

    private static IEnumerable<string> _GetCandidateModDirectories(McInstance instance)
    {
        yield return Path.Combine(instance.PathInstance, "mods");
        string? pathIndie = null;
        try
        {
            pathIndie = instance.PathIndie;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "读取实例隔离路径失败");
        }

        if (!string.IsNullOrEmpty(pathIndie))
            yield return Path.Combine(pathIndie, "mods");
    }

    private void _Add(CrashModInfo mod)
    {
        if (string.IsNullOrWhiteSpace(mod.ModId) &&
            string.IsNullOrWhiteSpace(mod.DisplayName) &&
            string.IsNullOrWhiteSpace(mod.FileName))
            return;
        _mods.Add(mod);
    }

    private void _NormalizeAndDeduplicate()
    {
        var distinct = new List<CrashModInfo>();
        foreach (var mod in _mods
                     .Where(mod => !distinct.Any(item => _SameDisplay(item, mod))))
            distinct.Add(mod);

        _mods.Clear();
        _mods.AddRange(distinct);
        LogWrapper.Info("Crash", "构建 Mod 索引，找到 " + _mods.Count + " 个候选 Mod");
    }

    private static bool _SameDisplay(CrashModInfo a, CrashModInfo b)
    {
        return string.Equals(_Normalize(a.ModId), _Normalize(b.ModId), StringComparison.Ordinal) &&
               string.Equals(_Normalize(a.FileName), _Normalize(b.FileName), StringComparison.Ordinal) &&
               string.Equals(_Normalize(a.DisplayName), _Normalize(b.DisplayName), StringComparison.Ordinal);
    }

    private static string _Normalize(string? value)
    {
        return (value ?? string.Empty)
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "")
            .Replace(".jar", "");
    }
}