namespace PCL;

/// <summary>
/// Owns launcher search source models, similarity scoring, and ranked search helpers.
/// </summary>
public static class LauncherSearch
{
    /// <summary>
    ///     获取搜索文本的相似度。
    /// </summary>
    /// <param name="Source">被搜索的长内容。</param>
    /// <param name="Query">用户输入的搜索文本。</param>
    private static double SearchSimilarity(string Source, string Query)
    {
        var qp = 0;
        var lenSum = 0d;
        Source = Source.ToLower().Replace(" ", "");
        Query = Query.ToLower().Replace(" ", "");
        var sourceLength = Source.Length;
        var queryLength = Query.Length; // 用于计算最后因数的长度缓存
        while (qp < queryLength)
        {
            // 对 qp 作为开始位置计算
            var sp = 0;
            var lenMax = 0;
            var spMax = 0;
            // 查找以 qp 为头的最大子串
            while (sp < Source.Length)
            {
                // 对每个 sp 作为开始位置计算最大子串
                var len = 0;
                while (qp + len < queryLength && sp + len < Source.Length && Source[sp + len] == Query[qp + len])
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
                Source = Source.Substring(0, spMax) +
                         (Source.Count() > spMax + lenMax
                             ? Source.Substring(spMax + lenMax)
                             : string.Empty); // 将源中的对应字段替换空
                // 存储 lenSum
                var IncWeight = Math.Pow(1.4d, 3 + lenMax) - 3.6d; // 根据长度加成
                IncWeight *= 1d + 0.3d * Math.Max(0, 3 - Math.Abs(qp - spMax)); // 根据位置加成
                lenSum += IncWeight;
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
        foreach (var Pair in source)
        {
            if (Pair.Aliases.Any())
                sum += Pair.Aliases.Max(a => SearchSimilarity(a, query)) * Pair.Weight;
            totalWeight += Pair.Weight;
        }

        return sum / totalWeight;
    }

    /// <summary>
    ///     进行多段文本加权搜索，获取相似度较高的数项结果。
    /// </summary>
    /// <param name="MaxBlurCount">返回的最大模糊结果数。</param>
    /// <param name="MinBlurSimilarity">返回结果要求的最低相似度。</param>
    public static List<SearchEntry<T>> Search<T>(List<SearchEntry<T>> Entries, string Query, int MaxBlurCount = 5,
        double MinBlurSimilarity = 0.1d)
    {
        var ResultList = new List<SearchEntry<T>>();

        if (Entries is null || !Entries.Any()) return ResultList;

        // Preprocess query into parts
        var queryParts = Query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length == 0)
        {
            ResultList.AddRange(Entries);
            return ResultList;
        }

        // Precompute query parts in lowercase for case-insensitive comparison
        var queryPartsLower = queryParts.Select(q => q.ToLower()).ToArray();

        // Process each entry to compute similarity and absolute match status
        foreach (var Entry in Entries)
        {
            Entry.Similarity = SearchSimilarityWeighted(Entry.SearchSource, Query);

            // Preprocess search source keys: remove spaces and convert to lowercase
            var processedSources = Entry.SearchSource.Select(s =>
            {
                for (var i = 0; i < s.Aliases.Length; i++)
                    s.Aliases[i] = s.Aliases[i].Replace(" ", "").ToLower();
                return s.Aliases;
            }).ToList();

            // Check if all query parts are matched exactly by at least one source
            var isAbsoluteRight = true;
            foreach (var qp in queryPartsLower)
            {
                var found = false;
                foreach (var ps in processedSources)
                    if (ps.Any(p => p.Contains(qp)))
                    {
                        found = true;
                        break;
                    }

                if (!found)
                {
                    isAbsoluteRight = false;
                    break;
                }
            }

            Entry.AbsoluteRight = isAbsoluteRight;
        }

        // Sort by absolute match (descending), then by similarity (descending)
        var sortedEntries = Entries.OrderByDescending(e => e.AbsoluteRight).ThenByDescending(e => e.Similarity)
            .ToList();

        // Build the final result list
        var blurCount = 0;
        foreach (var Entry in sortedEntries)
            if (Entry.AbsoluteRight)
            {
                ResultList.Add(Entry);
            }
            else
            {
                if (Entry.Similarity < MinBlurSimilarity || blurCount >= MaxBlurCount) break;
                ResultList.Add(Entry);
                blurCount += 1;
            }

        return ResultList;
    }
}

public delegate bool ComparisonBoolean<T>(T Left, T Right);
