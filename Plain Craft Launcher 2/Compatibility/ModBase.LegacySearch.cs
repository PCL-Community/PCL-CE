using CoreSimilaritySearch = PCL.Core.Utils.SimilaritySearch;

namespace PCL;

public static partial class ModBase
{
    #region 搜索

    private static List<KeyValuePair<string, double>> ToCoreSearchSources(IEnumerable<SearchSource> sources)
    {
        var result = new List<KeyValuePair<string, double>>();
        if (sources is null)
            return result;

        foreach (var source in sources)
        {
            if (source.aliases is null)
                continue;
            foreach (var alias in source.aliases)
                result.Add(new KeyValuePair<string, double>(alias, source.weight));
        }

        return result;
    }

    /// <summary>
    ///     用于搜索的项目。
    /// </summary>
    public class SearchEntry<T>
    {
        /// <summary>
        ///     是否完全匹配。
        /// </summary>
        public bool absoluteRight;

        /// <summary>
        ///     该项目对应的源数据。
        /// </summary>
        public T item;

        /// <summary>
        ///     该项目用于搜索的文本源。
        ///     在搜索时，会对每个文本源单独加权，但单个文本源内的多个别名只取最高的一个的相似度。
        /// </summary>
        public List<SearchSource> searchSource;

        /// <summary>
        ///     相似度。
        /// </summary>
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

    /// <summary>
    ///     本地搜索返回的最大模糊结果数。
    /// </summary>
    public const int MaxLocalSearchDepth = 25;

    /// <summary>
    ///     进行多段文本加权搜索，获取相似度较高的数项结果。
    /// </summary>
    /// <param name="maxBlurCount">返回的最大模糊结果数。</param>
    /// <param name="minBlurSimilarity">返回结果要求的最低相似度。</param>
    public static List<SearchEntry<T>> Search<T>(List<SearchEntry<T>> entries, string query, int maxBlurCount = 5,
        double minBlurSimilarity = 0.1d)
    {
        if (entries is null || entries.Count == 0)
            return [];

        var coreEntries = entries
            .Select((entry, index) => new Core.Utils.SearchEntry<(SearchEntry<T> Legacy, int Index)>(
                (entry, index),
                ToCoreSearchSources(entry.searchSource)))
            .ToList();

        var coreResults = CoreSimilaritySearch.Search(coreEntries, query, maxBlurCount, minBlurSimilarity);
        foreach (var coreEntry in coreResults)
        {
            coreEntry.Item.Legacy.absoluteRight = coreEntry.AbsoluteRight;
            coreEntry.Item.Legacy.similarity = coreEntry.Similarity;
        }

        return coreResults
            .Select(result => result.Item.Legacy)
            .ToList();
    }

    #endregion
}