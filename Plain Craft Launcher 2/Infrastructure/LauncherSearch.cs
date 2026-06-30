using CoreSimilaritySearch = PCL.Core.Utils.SimilaritySearch;

namespace PCL;

/// <summary>
///     PCL2 搜索调用点使用的兼容模型，内部使用 PCL.Core 的相似度搜索实现。
/// </summary>
public static class LauncherSearch
{
    public const int MaxLocalSearchDepth = 25;

    public static List<SearchEntry<T>> Search<T>(
        List<SearchEntry<T>> entries,
        string query,
        int maxBlurCount = 5,
        double minBlurSimilarity = 0.1d)
    {
        if (entries is null || entries.Count == 0)
            return [];

        var coreEntries = entries
            .Select((entry, index) => new Core.Utils.SearchEntry<(SearchEntry<T> Entry, int Index)>(
                (entry, index),
                ToCoreSearchSources(entry.searchSource)))
            .ToList();

        var coreResults = CoreSimilaritySearch.Search(coreEntries, query, maxBlurCount, minBlurSimilarity);
        foreach (var coreEntry in coreResults)
        {
            coreEntry.Item.Entry.absoluteRight = coreEntry.AbsoluteRight;
            coreEntry.Item.Entry.similarity = coreEntry.Similarity;
        }

        return coreResults.Select(result => result.Item.Entry).ToList();
    }

    private static List<KeyValuePair<string, double>> ToCoreSearchSources(IEnumerable<SearchSource> sources)
    {
        var result = new List<KeyValuePair<string, double>>();
        if (sources is null)
            return result;

        foreach (var source in sources)
        {
            if (source.aliases is null)
                continue;
            result.AddRange(source.aliases.Select(alias => new KeyValuePair<string, double>(alias, source.weight)));
        }

        return result;
    }
}

/// <summary>
///     用于搜索的项目。
/// </summary>
public class SearchEntry<T>
{
    public bool absoluteRight;
    public T item;
    public List<SearchSource> searchSource;
    public double similarity;
}

/// <summary>
///     单个用于搜索的文本源。
/// </summary>
public class SearchSource
{
    public string[] aliases;
    public double weight;

    public SearchSource(string[] aliases, double weight = 1)
    {
        this.aliases = aliases;
        this.weight = weight;
    }

    public SearchSource(string text, double weight = 1)
    {
        aliases = [text];
        this.weight = weight;
    }
}