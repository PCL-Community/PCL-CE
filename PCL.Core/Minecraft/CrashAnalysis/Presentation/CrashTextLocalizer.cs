namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     CrashAnalysis 专用文本访问器。
///     它只读取项目正式语言资源，不提供 C# 侧兜底字典。
///     如果资源缺失，会保留 Lang.Text 的缺失标记并记录缺失 key，
///     以便静态检查或调试阶段及时发现真正的问题。
/// </summary>
public sealed partial class CrashTextLocalizer(CrashMarkdownLocalizer localize)
{
    private readonly List<string> _missingKeys = [];
    private readonly List<string> _missingParameters = [];

    public IReadOnlyList<string> MissingKeys => _missingKeys;
    public IReadOnlyList<string> MissingParameters => _missingParameters;

    public string Text(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        return _Localize(key, parameters);
    }

    private string _Localize(string key, IReadOnlyDictionary<string, string>? parameters)
    {
        var result = localize(key, parameters ?? new Dictionary<string, string>());
        if (_LooksLikeMissingKey(result, key))
        {
            _missingKeys.Add(key);
            result = "!" + key + "!";
        }

        if (parameters is not null)
            foreach (var pair in parameters)
            {
                var value = pair.Value;
                if (value.StartsWith("Crash.", StringComparison.Ordinal))
                    value = _Localize(value, null);
                result = result.Replace("{" + pair.Key + "}", value, StringComparison.Ordinal);
            }

        foreach (Match match in _NamedPlaceholderRegex().Matches(result))
            _missingParameters.Add(key + ":" + match.Groups["name"].Value);

        return result;
    }

    private static bool _LooksLikeMissingKey(string result, string key)
    {
        return string.IsNullOrWhiteSpace(result) ||
               result == key ||
               result == "!" + key + "!" ||
               result.StartsWith("!Crash.", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_.-]*)\}")]
    private static partial Regex _NamedPlaceholderRegex();
}