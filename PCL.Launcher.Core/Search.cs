namespace PCL;

public static class SearchService
{
    /// <summary>
    ///     本地搜索返回的最大模糊结果数。
    /// </summary>
    public const int MaxLocalSearchDepth = 25;

    /// <summary>
    ///     获取搜索文本的相似度。
    /// </summary>
    /// <param name="source">被搜索的长内容。</param>
    /// <param name="query">用户输入的搜索文本。</param>
    private static double SearchSimilarity(string source, string query)
    {
        var qp = 0;
        var lenSum = 0d;
        source = source.ToLower().Replace(" ", "");
        query = query.ToLower().Replace(" ", "");
        var sourceLength = source.Length;
        var queryLength = query.Length;
        while (qp < queryLength)
        {
            var sp = 0;
            var lenMax = 0;
            var spMax = 0;
            while (sp < source.Length)
            {
                var len = 0;
                while (qp + len < queryLength
                       && sp + len < source.Length
                       && source[sp + len] == query[qp + len])
                    len += 1;
                if (len > lenMax)
                {
                    lenMax = len;
                    spMax = sp;
                }

                sp += Math.Max(1, len);
            }

            if (lenMax > 0)
            {
                source = string.Concat(
                    source.AsSpan(0, spMax),
                    source.Length > spMax + lenMax
                        ? source[(spMax + lenMax)..]
                        : string.Empty);

                var incWeight = Math.Pow(1.4d, 3 + lenMax) - 3.6d;
                incWeight *= 1d + 0.3d * Math.Max(0, 3 - Math.Abs(qp - spMax));
                lenSum += incWeight;
            }

            qp += Math.Max(1, lenMax);
        }

        return lenSum
            / queryLength
            * (3d / Math.Pow(sourceLength + 15, 0.5d))
            * (queryLength <= 2 ? 3 - queryLength : 1);
    }

    /// <summary>
    ///     获取多段文本加权后的相似度。
    /// </summary>
    private static double SearchSimilarityWeighted(List<SearchSource> source, string query)
    {
        var totalWeight = 0d;
        var sum = 0d;
        foreach (var pair in source)
        {
            if (pair.aliases.Length != 0)
                sum += pair.aliases.Max(a => SearchSimilarity(a, query)) * pair.weight;
            totalWeight += pair.weight;
        }

        return sum / totalWeight;
    }

    /// <summary>
    ///     进行多段文本加权搜索，获取相似度较高的数项结果。
    /// </summary>
    /// <param name="maxBlurCount">返回的最大模糊结果数。</param>
    /// <param name="minBlurSimilarity">返回结果要求的最低相似度。</param>
    public static List<SearchEntry<T>> Search<T>(
        List<SearchEntry<T>>? entries,
        string query,
        int maxBlurCount = 5,
        double minBlurSimilarity = 0.1d)
    {
        var resultList = new List<SearchEntry<T>>();

        if (entries is null
            || entries.Count == 0)
            return resultList;

        var queryParts = query.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length == 0)
        {
            resultList.AddRange(entries);
            return resultList;
        }

        var queryPartsLower = queryParts.Select(q => q.ToLower()).ToArray();

        foreach (var entry in entries)
        {
            var searchSource = entry.searchSource ?? [];
            entry.similarity = searchSource.Count > 0
                ? SearchSimilarityWeighted(searchSource, query)
                : 0d;

            var processedSources = searchSource
                .Select(s =>
                {
                    for (var i = 0; i < s.aliases.Length; i++)
                        s.aliases[i] = s.aliases[i].Replace(" ", "").ToLower();
                    return s.aliases;
                })
                .ToList();

            var isAbsoluteRight = queryPartsLower
                .Select(qp => processedSources
                    .Any(ps => ps
                        .Any(p => p.Contains(qp))))
                .All(found => found);

            entry.absoluteRight = isAbsoluteRight;
        }

        var sortedEntries = entries
            .OrderByDescending(e => e.absoluteRight)
            .ThenByDescending(e => e.similarity)
            .ToList();

        var blurCount = 0;
        foreach (var entry in sortedEntries)
            if (entry.absoluteRight)
            {
                resultList.Add(entry);
            }
            else
            {
                if (entry.similarity < minBlurSimilarity
                    || blurCount >= maxBlurCount) break;
                resultList.Add(entry);
                blurCount += 1;
            }

        return resultList;
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
        public T? item;

        /// <summary>
        ///     该项目用于搜索的文本源。
        ///     在搜索时，会对每个文本源单独加权，但单个文本源内的多个别名只取最高的一个的相似度。
        /// </summary>
        public List<SearchSource>? searchSource;

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
        public readonly string[] aliases;
        public readonly double weight;

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
}