using System.Linq;
using System.Text.RegularExpressions;

namespace PCL.Core.IO;

/// <summary>
/// Match a <see cref="FileItem"/>.
/// </summary>
/// <param name="item">the item to match</param>
/// <returns>match result</returns>
public delegate bool FileMatch(FileItem item);

public static class FileMatches
{
    public static FileMatch Any { get; } = (_ => true);
    public static FileMatch None { get; } = (_ => false);
    
    public static FileMatch WithNameExtension(params string[] extensions)
        => (item => extensions.Any(ext => item.Name.EndsWith(ext)));
    
    public static FileMatch WithNameRegex(Regex expression)
        => (item => expression.IsMatch(item.Name));

    public static FileMatch WithSourcesProtocol(params string[] protocols)
        => (item => item.Sources?.Any(source => protocols.Any(protocol => source.StartsWith($"{protocol}:"))) == true);
}

public static class FileMatchExtension
{
    public static FileMatchPair<TValue> Pair<TValue>(this FileMatch match, TValue value) => new(match, value);
}
