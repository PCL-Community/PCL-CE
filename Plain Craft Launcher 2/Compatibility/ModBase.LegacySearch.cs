namespace PCL;

public static partial class ModBase
{
    #region 搜索

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
        var queryLength = query.Length; // 用于计算最后因数的长度缓存
        while (qp < queryLength)
        {
            // 对 qp 作为开始位置计算
            var sp = 0;
            var lenMax = 0;
            var spMax = 0;
            // 查找以 qp 为头的最大子串
            while (sp < source.Length)
            {
                // 对每个 sp 作为开始位置计算最大子串
                var len = 0;
                while (qp + len < queryLength && sp + len < source.Length && source[sp + len] == query[qp + len])
                    len += 1;
                // 存储 len
                if (len > lenMax)
                {
                    lenMax = len;
                    spMax = sp;
                }

                // 根据结果增加 sp
                sp += Math.Max(1, len);
            }

            if (lenMax > 0)
            {
                source = string.Concat(source.AsSpan(0, spMax), source.Length > spMax + lenMax
                    ? source[(spMax + lenMax)..]
                    : string.Empty); // 将源中的对应字段替换空
                // 存储 lenSum
                var incWeight = Math.Pow(1.4d, 3 + lenMax) - 3.6d; // 根据长度加成
                incWeight *= 1d + 0.3d * Math.Max(0, 3 - Math.Abs(qp - spMax)); // 根据位置加成
                lenSum += incWeight;
            }

            // 根据结果增加 qp
            qp += Math.Max(1, lenMax);
        }

        // 计算结果：重复字段量 × 源长度影响比例
        return lenSum / queryLength * (3d / Math.Pow(sourceLength + 15, 0.5d)) *
               (queryLength <= 2 ? 3 - queryLength : 1);
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
        var resultList = new List<SearchEntry<T>>();

        if (entries is null || entries.Count == 0) return resultList;

        // Preprocess query into parts
        var queryParts = query.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length == 0)
        {
            resultList.AddRange(entries);
            return resultList;
        }

        // Precompute query parts in lowercase for case-insensitive comparison
        var queryPartsLower = queryParts.Select(q => q.ToLower()).ToArray();

        // Process each entry to compute similarity and absolute match status
        foreach (var entry in entries)
        {
            entry.similarity = SearchSimilarityWeighted(entry.searchSource, query);

            // Preprocess search source keys: remove spaces and convert to lowercase
            var processedSources = entry.searchSource.Select(s =>
            {
                for (var i = 0; i < s.aliases.Length; i++)
                    s.aliases[i] = s.aliases[i].Replace(" ", "").ToLower();
                return s.aliases;
            }).ToList();

            // Check if all query parts are matched exactly by at least one source
            var isAbsoluteRight = queryPartsLower
                .Select(qp => processedSources
                    .Any(ps => ps
                        .Any(p => p
                            .Contains(qp))))
                .All(found => found);

            entry.absoluteRight = isAbsoluteRight;
        }

        // Sort by absolute match (descending), then by similarity (descending)
        var sortedEntries = entries
            .OrderByDescending(e => e.absoluteRight)
            .ThenByDescending(e => e.similarity)
            .ToList();

        // Build the final result list
        var blurCount = 0;
        foreach (var entry in sortedEntries)
            if (entry.absoluteRight)
            {
                resultList.Add(entry);
            }
            else
            {
                if (entry.similarity < minBlurSimilarity || blurCount >= maxBlurCount) break;
                resultList.Add(entry);
                blurCount += 1;
            }

        return resultList;
    }

    #endregion
}