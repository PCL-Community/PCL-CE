// SPDX-License-Identifier: Apache-2.0
// Adapted from Apache Maven ComparableVersion:
// https://github.com/apache/maven/blob/master/compat/maven-artifact/src/main/java/org/apache/maven/artifact/versioning/ComparableVersion.java

using System;
using System.Collections.Generic;
using System.Numerics;

namespace PCL;

/// <summary>Maven ComparableVersion compatible ordering used by Forge/NeoForge ranges and JarJar selection.</summary>
internal sealed class MavenComparableVersion : IComparable<MavenComparableVersion>
{
    private const int MaxVersionLength = 256;

    private interface IItem
    {
        int Type { get; }
        bool IsNull { get; }
        int CompareTo(IItem other);
    }

    private sealed class NumericItem : IItem
    {
        public static readonly NumericItem Zero = new("0");
        private readonly BigInteger _value;

        public NumericItem(string value) => _value = BigInteger.Parse(_StripLeadingZeroes(value));
        public int Type => 0;
        public bool IsNull => _value.IsZero;

        public int CompareTo(IItem other)
        {
            if (other is null) return _value.IsZero ? 0 : 1;
            return other.Type switch
            {
                0 => _value.CompareTo(((NumericItem)other)._value),
                1 or 2 or 3 => 1,
                _ => throw new InvalidOperationException("Invalid Maven version item")
            };
        }
    }

    private sealed class StringItem : IItem
    {
        private static readonly string[] Qualifiers =
            { "alpha", "beta", "milestone", "rc", "snapshot", "", "sp" };
        private const string ReleaseIndex = "5";
        private readonly string _value;

        public StringItem(string value, bool followedByDigit)
        {
            if (followedByDigit && value.Length == 1)
                value = value switch { "a" => "alpha", "b" => "beta", "m" => "milestone", _ => value };
            _value = value == "cr" ? "rc" : value;
        }

        public int Type => 1;
        public bool IsNull => _value.Length == 0;

        private static string _ComparableQualifier(string qualifier)
        {
            if (qualifier is "ga" or "final" or "release") return ReleaseIndex;
            var index = Array.IndexOf(Qualifiers, qualifier);
            return index < 0 ? Qualifiers.Length + "-" + qualifier : index.ToString();
        }

        public int CompareTo(IItem other)
        {
            if (other is null)
                return string.CompareOrdinal(_ComparableQualifier(_value), ReleaseIndex);
            return other.Type switch
            {
                0 => -1,
                1 => string.CompareOrdinal(_ComparableQualifier(_value),
                    _ComparableQualifier(((StringItem)other)._value)),
                2 => -other.CompareTo(this),
                3 => _CompareCombination((CombinationItem)other),
                _ => throw new InvalidOperationException("Invalid Maven version item")
            };
        }

        private int _CompareCombination(CombinationItem other)
        {
            var result = CompareTo(other.StringPart);
            if (result != 0) return result;
            return CompareTo(null) == 0 ? -other.DigitPart.CompareTo(null) : -1;
        }
    }

    private sealed class CombinationItem : IItem
    {
        public StringItem StringPart { get; }
        public IItem DigitPart { get; }

        public CombinationItem(string value)
        {
            var index = 0;
            while (index < value.Length && !char.IsDigit(value[index])) index++;
            StringPart = new StringItem(value.Substring(0, index), true);
            DigitPart = _ParseItem(false, true, value.Substring(index));
        }

        public int Type => 3;
        public bool IsNull => false;

        public int CompareTo(IItem other)
        {
            if (other is null)
            {
                var result = StringPart.CompareTo(null);
                return result == 0 ? DigitPart.CompareTo(null) : result;
            }

            return other.Type switch
            {
                0 => -1,
                1 => _CompareString((StringItem)other),
                2 => -other.CompareTo(this),
                3 => _CompareCombination((CombinationItem)other),
                _ => throw new InvalidOperationException("Invalid Maven version item")
            };
        }

        private int _CompareString(StringItem other)
        {
            var result = StringPart.CompareTo(other);
            if (result != 0) return result;
            return StringPart.CompareTo(null) == 0 ? DigitPart.CompareTo(null) : 1;
        }

        private int _CompareCombination(CombinationItem other)
        {
            var result = StringPart.CompareTo(other.StringPart);
            return result == 0 ? DigitPart.CompareTo(other.DigitPart) : result;
        }
    }

    private sealed class ListItem : List<IItem>, IItem
    {
        public int Type => 2;
        public bool IsNull => Count == 0;

        public void Normalize()
        {
            for (var i = Count - 1; i >= 0; i--)
            {
                var item = this[i];
                if (!item.IsNull) continue;
                if (i == Count - 1 || this[i + 1].Type == 1)
                    RemoveAt(i);
                else if (this[i + 1] is ListItem next && next.Count > 0 && next[0].Type is 1 or 3)
                    RemoveAt(i);
            }

            if (Count == 1 && this[0] is ListItem only)
            {
                Clear();
                AddRange(only);
            }
        }

        public int CompareTo(IItem other)
        {
            if (other is null)
            {
                foreach (var item in this)
                {
                    var result = item.CompareTo(null);
                    if (result != 0) return result;
                }
                return 0;
            }

            if (other.Type == 0) return -1;
            if (other.Type is 1 or 3)
            {
                var result = Count == 0 ? -other.CompareTo(null) : this[0].CompareTo(other);
                for (var i = 1; result == 0 && i < Count; i++) result = this[i].CompareTo(null);
                return result;
            }
            if (other is not ListItem list) throw new InvalidOperationException("Invalid Maven version item");

            var max = Math.Max(Count, list.Count);
            for (var i = 0; i < max; i++)
            {
                var left = i < Count ? this[i] : null;
                var right = i < list.Count ? list[i] : null;
                var result = left is null ? (right is null ? 0 : -right.CompareTo(null)) : left.CompareTo(right);
                if (result != 0) return result;
            }
            return 0;
        }
    }

    private readonly ListItem _items = new();

    public MavenComparableVersion(string version)
    {
        if (version is null) throw new ArgumentNullException(nameof(version));
        if (version.Length > MaxVersionLength) throw new ArgumentException("Maven version is too long", nameof(version));
        _Parse(version.ToLowerInvariant());
    }

    public int CompareTo(MavenComparableVersion? other) => other is null ? 1 : _items.CompareTo(other._items);
    public static int Compare(string left, string right)
    {
        var leftTooLong = left.Length > MaxVersionLength;
        var rightTooLong = right.Length > MaxVersionLength;
        if (leftTooLong || rightTooLong)
        {
            if (leftTooLong != rightTooLong) return leftTooLong ? 1 : -1;
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return new MavenComparableVersion(left).CompareTo(new MavenComparableVersion(right));
    }

    private void _Parse(string version)
    {
        var list = _items;
        var stack = new Stack<ListItem>();
        stack.Push(list);
        var isDigit = false;
        var isCombination = false;
        var start = 0;

        for (var i = 0; i < version.Length; i++)
        {
            var ch = version[i];
            if (ch == '.')
            {
                list.Add(i == start ? NumericItem.Zero :
                    _ParseItem(isCombination, isDigit, version.Substring(start, i - start)));
                isCombination = false;
                start = i + 1;
            }
            else if (ch == '-')
            {
                if (i == start)
                    list.Add(NumericItem.Zero);
                else
                {
                    if (!isDigit && i != version.Length - 1 && char.IsDigit(version[i + 1]))
                    {
                        isCombination = true;
                        continue;
                    }
                    list.Add(_ParseItem(isCombination, isDigit, version.Substring(start, i - start)));
                }
                start = i + 1;
                if (list.Count > 0)
                {
                    var child = new ListItem();
                    list.Add(child);
                    list = child;
                    stack.Push(list);
                }
                isCombination = false;
            }
            else if (ch is >= '0' and <= '9')
            {
                if (!isDigit && i > start)
                {
                    isCombination = true;
                    if (list.Count > 0)
                    {
                        var child = new ListItem();
                        list.Add(child);
                        list = child;
                        stack.Push(list);
                    }
                }
                isDigit = true;
            }
            else
            {
                if (isDigit && i > start)
                {
                    list.Add(_ParseItem(isCombination, true, version.Substring(start, i - start)));
                    start = i;
                    var child = new ListItem();
                    list.Add(child);
                    list = child;
                    stack.Push(list);
                    isCombination = false;
                }
                isDigit = false;
            }
        }

        if (version.Length > start)
        {
            if (!isDigit && list.Count > 0)
            {
                var child = new ListItem();
                list.Add(child);
                list = child;
                stack.Push(list);
            }
            list.Add(_ParseItem(isCombination, isDigit, version.Substring(start)));
        }

        while (stack.Count > 0) stack.Pop().Normalize();
    }

    private static IItem _ParseItem(bool combination, bool digit, string value)
    {
        if (combination) return new CombinationItem(value.Replace("-", ""));
        return digit ? new NumericItem(value) : new StringItem(value, false);
    }

    private static string _StripLeadingZeroes(string value)
    {
        if (string.IsNullOrEmpty(value)) return "0";
        var i = 0;
        while (i < value.Length && value[i] == '0') i++;
        return i == value.Length ? "0" : value.Substring(i);
    }
}
