using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.IO;

public class FileMatchPair<TValue>(FileMatch match, TValue value)
{
    public bool Match(FileItem item) => match(item);
    public TValue Value => value;
}

public static class FileMatchPairExtension
{
    extension<TSource>(IEnumerable<FileMatchPair<TSource>> pairs)
    {
        public IEnumerable<TSource> MatchAll(FileItem item)
            => pairs.Where(pair => pair.Match(item)).Select(pair => pair.Value);

        public TSource? MatchFirst(FileItem item)
        {
            var pair = pairs.FirstOrDefault(pair => pair.Match(item));
            return pair == null ? default : pair.Value;
        }
    }
}
