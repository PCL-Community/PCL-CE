using System;
using System.Collections.Generic;

namespace PCL.Core.Utils;


/// <summary>
/// 可以使用 Equals 和等号的 List。
/// </summary>
public class EqualableList<T> : List<T>, IEquatable<EqualableList<T>>
{
    private static bool _Comparer<TSelf>(EqualableList<TSelf> left, EqualableList<TSelf> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0, loopTo = left.Count - 1; i <= loopTo; i++)
        {
            var fir = left[i];
            var sec = right[i];
            if (fir is null)
            {
                if (sec is null)
                {
                    continue;
                }

                return false;
            }

            if (!fir.Equals(sec))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool Equals(EqualableList<T>? list)
    {
        if (list is null)
        {
            return false;
        }

        return _Comparer(this, list);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is not EqualableList<T> list)
        {
            return false;
        }

        return _Comparer(this, list);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in this)
            hash.Add(item);
        return hash.ToHashCode();
    }

    public static bool operator ==(EqualableList<T> left, EqualableList<T> right)
    {
        return EqualityComparer<EqualableList<T>>.Default.Equals(left, right);
    }

    public static bool operator !=(EqualableList<T> left, EqualableList<T> right)
    {
        return !(left == right);
    }
}
