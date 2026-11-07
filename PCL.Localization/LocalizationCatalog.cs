using System.Reflection;
using System.Xml.Linq;

namespace PCL.Localization;

/// <summary>
///     中立本地化目录：不依赖任何界面框架，供测试、i18n 校验工具与未来的非 UI 入口读取语言资源。
///     数据来源为本程序集内嵌的 Languages/*.axaml（与界面层使用同一份文件）。
/// </summary>
public static class LocalizationCatalog
{
    private const string ResourcePrefix = "PCL.Localization.Languages.";
    private const string ResourceSuffix = ".axaml";

    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Lazy<IReadOnlyList<string>> LazyLanguages = new(LoadLanguages);

    /// <summary>全部语言代码（如 zh-CN、en-US）。</summary>
    public static IReadOnlyList<string> Languages => LazyLanguages.Value;

    /// <summary>判断某个语言代码是否存在对应资源。</summary>
    public static bool IsSupported(string languageCode) =>
        Languages.Contains(languageCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>读取某一语言的全部键。</summary>
    public static IReadOnlyList<string> GetKeys(string languageCode) =>
        GetEntries(languageCode).Keys.ToArray();

    /// <summary>读取某一语言的键值对（保持资源文件中的顺序）。</summary>
    public static IReadOnlyDictionary<string, string> GetEntries(string languageCode)
    {
        var stream = GetResourceStream(languageCode)
                     ?? throw new FileNotFoundException($"未找到语言资源：{languageCode}");
        using (stream)
        {
            var document = XDocument.Load(stream);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (document.Root is null) return entries;

            foreach (var element in document.Root.Descendants())
            {
                var key = element.Attribute(XamlNamespace + "Key")?.Value;
                if (string.IsNullOrWhiteSpace(key) || entries.ContainsKey(key)) continue;
                entries[key] = element.Value;
            }

            return entries;
        }
    }

    private static Stream? GetResourceStream(string languageCode)
    {
        var assembly = typeof(LocalizationCatalog).Assembly;
        var resourceName = $"{ResourcePrefix}{languageCode}{ResourceSuffix}";
        return assembly.GetManifestResourceStream(resourceName);
    }

    private static IReadOnlyList<string> LoadLanguages()
    {
        var assembly = typeof(LocalizationCatalog).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                           && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
