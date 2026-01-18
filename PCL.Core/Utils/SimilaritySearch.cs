namespace PCL.Core.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 提供文本相似度搜索功能。
/// </summary>
public static class SimilaritySearch {
    //region: SearchSimilarity Constants
    // 这些常量来自原始算法，用于调整评分权重。

    /// <summary>
    /// 匹配长度权重计算的指数底数。越长匹配的得分呈指数增长。
    /// </summary>
    private const double LengthPowerBase = 1.4;

    /// <summary>
    /// 匹配长度权重计算的偏移量。
    /// </summary>
    private const double LengthWeightOffset = 3.6;

    /// <summary>
    /// 匹配位置邻近度的奖励因子。
    /// </summary>
    private const double PositionBonusFactor = 0.3;

    /// <summary>
    /// 计算位置奖励时，允许的最大位置差异。
    /// </summary>
    private const int MaxPositionBonusDistance = 3;

    /// <summary>
    /// 源文本长度对最终得分的影响因子。
    /// </summary>
    private const double SourceLengthImpactFactor = 3.0;

    /// <summary>
    /// 源文本长度惩罚的平滑参数。
    /// </summary>
    private const double SourceLengthSmoothing = 15.0;

    /// <summary>
    /// 对长度为1的查询的得分奖励。
    /// </summary>
    private const double ShortQueryBonusFactor = 2.0;
    //endregion

    /// <summary>
    /// 获取搜索文本的相似度。（已优化）
    /// </summary>
    /// <param name="source">被搜索的长内容。</param>
    /// <param name="query">用户输入的搜索文本。</param>
    /// <returns>一个表示相似度的 double 值。</returns>
    private static double SearchSimilarity(string source, string query) {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query)) {
            return 0.0;
        }

        // 预处理：转为小写并移除空格，然后转换为 ReadOnlySpan 以提高性能。
        var sourceSpan = source.ToLower().Replace(" ", "").AsSpan();
        var querySpan = query.ToLower().Replace(" ", "").AsSpan();

        if (sourceSpan.IsEmpty || querySpan.IsEmpty) {
            return 0.0;
        }

        // 使用布尔数组来跟踪源文本中已被匹配的字符，避免重复分配字符串。
        var usedSourceIndices = new bool[sourceSpan.Length];
        double weightedLengthSum = 0;
        var queryIndex = 0;

        while (queryIndex < querySpan.Length) {
            var longestMatchLength = 0;
            var bestMatchSourceStartIndex = -1;

            // 寻找以当前 queryIndex 为起点的最长匹配子串
            for (var sourceIndex = 0; sourceIndex < sourceSpan.Length; sourceIndex++) {
                var currentMatchLength = 0;
                // 计算从当前 sourceIndex 和 queryIndex 开始的匹配长度
                // 同时确保源字符未被使用
                while ((queryIndex + currentMatchLength) < querySpan.Length &&
                       (sourceIndex + currentMatchLength) < sourceSpan.Length &&
                       !usedSourceIndices[sourceIndex + currentMatchLength] &&
                       sourceSpan[sourceIndex + currentMatchLength] == querySpan[queryIndex + currentMatchLength]) {
                    currentMatchLength++;
                }

                // 卫语句：如果当前匹配不比最长匹配更长，则直接继续下一次循环
                if (currentMatchLength <= longestMatchLength) {
                    continue;
                }

                // 如果满足条件，更新最长匹配信息
                longestMatchLength = currentMatchLength;
                bestMatchSourceStartIndex = sourceIndex;
            }

            if (longestMatchLength > 0) {
                // 标记源中对应的字符为已使用
                for (var i = 0; i < longestMatchLength; i++) {
                    usedSourceIndices[bestMatchSourceStartIndex + i] = true;
                }

                // 根据长度加成
                var incrementWeight = Math.Pow(LengthPowerBase, 3 + longestMatchLength) - LengthWeightOffset;

                // 根据位置加成
                var positionDifference = Math.Abs(queryIndex - bestMatchSourceStartIndex);
                var positionBonus = 1.0 + PositionBonusFactor * Math.Max(0, MaxPositionBonusDistance - positionDifference);
                incrementWeight *= positionBonus;

                weightedLengthSum += incrementWeight;
            }

            // 推进查询指针
            queryIndex += Math.Max(1, longestMatchLength);
        }

        // 计算最终结果：(加权匹配总和 / 查询长度) * 源长度影响比例 * 短查询奖励
        var normalizedScore = weightedLengthSum / querySpan.Length;
        var sourceLengthPenalty = SourceLengthImpactFactor / Math.Sqrt(sourceSpan.Length + SourceLengthSmoothing);
        var shortQueryBonus = query.Length == 1 ? ShortQueryBonusFactor : 1.0;

        return normalizedScore * sourceLengthPenalty * shortQueryBonus;
    }

    /// <summary>
    /// 获取多段文本加权后的相似度。
    /// </summary>
    private static double SearchSimilarityWeighted(List<KeyValuePair<string, double>> source, string query) {
        if (source.Count == 0) return 0.0;

        var totalWeight = source.Sum(pair => pair.Value);
        if (totalWeight == 0) return 0.0;

        var weightedSum = source.Sum(pair => SearchSimilarity(pair.Key, query) * pair.Value);

        return weightedSum / totalWeight;
    }

    /// <summary>
    /// 检查一个条目的所有搜索源是否完全匹配查询的所有部分。
    /// </summary>
    private static bool IsAbsoluteMatch(IEnumerable<KeyValuePair<string, double>> searchSources, string[] queryParts) {
        // 预处理搜索源：转小写并移除空格，避免在循环中重复操作
        var processedSources = searchSources
            .Select(s => s.Key.Replace(" ", "").ToLower())
            .ToList();

        // 必须所有查询词都在至少一个源中找到
        return queryParts.All(queryPart => processedSources.Any(source => source.Contains(queryPart)));
    }

    /// <summary>
    /// 进行多段文本加权搜索，获取相似度较高的数项结果。
    /// </summary>
    /// <typeparam name="T">搜索条目的泛型类型。</typeparam>
    /// <param name="entries">要搜索的条目列表。</param>
    /// <param name="query">用户输入的查询字符串。</param>
    /// <param name="maxBlurCount">返回的最大模糊结果数。</param>
    /// <param name="minBlurSimilarity">返回结果要求的最低相似度。</param>
    /// <returns>排序和过滤后的搜索结果列表。</returns>
    public static List<SearchEntry<T>> Search<T>(
        List<SearchEntry<T>> entries,
        string query,
        int maxBlurCount = 5,
        double minBlurSimilarity = 0.1) {
        if (entries.Count == 0) {
            return [];
        }

        if (string.IsNullOrWhiteSpace(query)) {
            return entries; // 或者返回空列表，取决于业务需求
        }

        var queryParts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(q => q.ToLower())
            .ToArray();

        if (queryParts.Length == 0) {
            return entries;
        }

        // 1. 计算每个条目的相似度和是否完全匹配
        foreach (var entry in entries) {
            entry.Similarity = SearchSimilarityWeighted(entry.SearchSource, query);
            entry.AbsoluteRight = IsAbsoluteMatch(entry.SearchSource, queryParts);
        }

        // 2. 排序：完全匹配的优先，其次按相似度降序
        var sortedEntries = entries
            .OrderByDescending(e => e.AbsoluteRight)
            .ThenByDescending(e => e.Similarity);

        // 3. 构建最终结果列表
        var sortedEntriesList = sortedEntries.ToList();
        
        var absoluteMatches = sortedEntriesList.Where(e => e.AbsoluteRight);

        var blurMatches = sortedEntriesList
            .Where(e => !e.AbsoluteRight && e.Similarity >= minBlurSimilarity)
            .Take(maxBlurCount);

        return absoluteMatches.Concat(blurMatches).ToList();
    }
}

/// <summary>
/// 用于搜索的项目。使用主构造函数 (C# 12+)。
/// </summary>
/// <typeparam name="T">该项目对应的源数据类型。</typeparam>
public class SearchEntry<T>(T item, List<KeyValuePair<string, double>> searchSource) {
    /// <summary>
    /// 该项目对应的源数据。
    /// </summary>
    public T Item { get; set; } = item;

    /// <summary>
    /// 该项目用于搜索的源文本及其权重。
    /// </summary>
    public List<KeyValuePair<string, double>> SearchSource { get; set; } = searchSource;

    /// <summary>
    /// 计算出的相似度。
    /// </summary>
    public double Similarity { get; set; }

    /// <summary>
    /// 是否完全匹配。
    /// 如果查询的所有部分都能在搜索源中找到，则为 true。
    /// </summary>
    public bool AbsoluteRight { get; set; }
}
