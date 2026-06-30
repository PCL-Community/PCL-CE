namespace PCL;

/// <summary>
///     用作加载器输入的列表，按元素顺序比较相等性。
/// </summary>
public sealed class LoaderInputList<T> : List<T>
{
    public override bool Equals(object? obj)
    {
        return obj is List<T> other && this.SequenceEqual(other);
    }

    public static bool operator ==(LoaderInputList<T>? left, LoaderInputList<T>? right)
    {
        return ReferenceEquals(left, right) || (left is not null && right is not null && left.SequenceEqual(right));
    }

    public static bool operator !=(LoaderInputList<T>? left, LoaderInputList<T>? right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in this)
            hash.Add(item);
        return hash.ToHashCode();
    }
}