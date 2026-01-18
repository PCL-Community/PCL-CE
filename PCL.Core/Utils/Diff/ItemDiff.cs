using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Utils.Diff
{
    public class ItemDiff
    {
        public static ItemDiffResult<T> ComputeDiff<T, TKey>(
            IEnumerable<T> oldItems,
            IEnumerable<T> newItems,
            Func<T, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            ArgumentNullException.ThrowIfNull(oldItems);
            ArgumentNullException.ThrowIfNull(newItems);
            ArgumentNullException.ThrowIfNull(keySelector);

            var comparer = keyComparer ?? EqualityComparer<TKey>.Default;

            var oldDict = oldItems.ToDictionary(keySelector, comparer);
            var newDict = newItems.ToDictionary(keySelector, comparer);

            var oldKeys = new HashSet<TKey>(oldDict.Keys, comparer);
            var newKeys = new HashSet<TKey>(newDict.Keys, comparer);

            var addedKeys = new HashSet<TKey>(newKeys.Except(oldKeys, comparer), comparer);
            var removedKeys = new HashSet<TKey>(oldKeys.Except(newKeys, comparer), comparer);
            var commonKeys = new HashSet<TKey>(oldKeys.Intersect(newKeys, comparer), comparer);

            var added = addedKeys.Select(k => newDict[k]).ToList();
            var removed = removedKeys.Select(k => oldDict[k]).ToList();
            var unchanged = commonKeys.Select(k => newDict[k]).ToList();

            return new ItemDiffResult<T>(added, removed, unchanged);
        }
    }
}
