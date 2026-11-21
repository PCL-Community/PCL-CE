// SPDX-License-Identifier: Apache-2.0
// Adapted from Apache Maven ComparableVersion:
// https://github.com/apache/maven/blob/master/compat/maven-artifact/src/main/java/org/apache/maven/artifact/versioning/ComparableVersion.java

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PCL;

/// <summary>
///     Maven ComparableVersion compatible ordering used by Forge/NeoForge ranges and JarJar
///     selection.
/// </summary>
public sealed class MavenComparableVersion : IComparable<MavenComparableVersion>
{
    private const int MaxVersionLength = 256;

    private readonly ListItem _items = [];

    public MavenComparableVersion(string version)
    {
        if (version is null)
            throw new ArgumentNullException(nameof(version));
        if (version.Length > MaxVersionLength)
            throw new ArgumentException("Maven version is too long", nameof(version));
        _Parse(version.ToLowerInvariant());
    }

    public int CompareTo(MavenComparableVersion? other)
    {
        return other is null
            ? 1
            : _items.CompareTo(other._items);
    }

    public static int Compare(string left, string right)
    {
        var leftTooLong = left.Length > MaxVersionLength;
        var rightTooLong = right.Length > MaxVersionLength;

        if (!leftTooLong && !rightTooLong)
            return new MavenComparableVersion(left).CompareTo(new MavenComparableVersion(right));

        if (leftTooLong != rightTooLong)
            return leftTooLong ? 1 : -1;

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
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
            switch (ch)
            {
                case '.':
                {
                    list.Add(
                        i == start
                            ? NumericItem.Zero
                            : _ParseItem(
                                isCombination,
                                isDigit,
                                version.Substring(start, i - start)));
                    isCombination = false;
                    start = i + 1;
                    break;
                }

                case '-':
                {
                    if (i == start)
                    {
                        list.Add(NumericItem.Zero);
                    }
                    else
                    {
                        if (!isDigit
                            && i != version.Length - 1
                            && char.IsDigit(version[i + 1]))
                        {
                            isCombination = true;
                            continue;
                        }

                        list.Add(
                            _ParseItem(
                                isCombination,
                                isDigit,
                                version.Substring(start, i - start)));
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
                    break;
                }

                case >= '0' and <= '9':
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
                    break;
                }

                default:
                {
                    if (isDigit && i > start)
                    {
                        list.Add(
                            _ParseItem(
                                isCombination,
                                true,
                                version.Substring(start, i - start)));
                        start = i;
                        var child = new ListItem();
                        list.Add(child);
                        list = child;
                        stack.Push(list);
                        isCombination = false;
                    }

                    isDigit = false;
                    break;
                }
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

            list.Add(
                _ParseItem(
                    isCombination,
                    isDigit,
                    version[start..]));
        }

        while (stack.Count > 0) stack.Pop().Normalize();
    }

    private static IItem _ParseItem(bool combination, bool digit, string value)
    {
        return combination
            ? new CombinationItem(value.Replace("-", ""))
            : digit
                ? new NumericItem(value)
                : new StringItem(value, false);
    }

    private static string _StripLeadingZeroes(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "0";
        
        var i = 0;
        while (i < value.Length && value[i] == '0')
            i++;
        
        return i == value.Length ? "0" : value[i..];
    }

    private interface IItem
    {
        int Type { get; }
        bool IsNull { get; }
        int CompareTo(IItem other);
    }

    private sealed class NumericItem(string value) : IItem
    {
        public static readonly NumericItem Zero = new("0");
        private readonly BigInteger _value = BigInteger.Parse(_StripLeadingZeroes(value));

        public int Type => 0;
        public bool IsNull => _value.IsZero;

        public int CompareTo(IItem other)
        {
            return other is null
                ? _value.IsZero ? 0 : 1
                : other.Type switch
                {
                    0 => _value.CompareTo(((NumericItem)other)._value),
                    1 or 2 or 3 => 1,
                    _ => throw new InvalidOperationException("Invalid Maven version item")
                };
        }
    }

    private sealed class StringItem : IItem
    {
        private const string ReleaseIndex = "5";

        private static readonly string[] _Qualifiers =
        [
            "alpha",
            "beta",
            "milestone",
            "rc",
            "snapshot",
            "",
            "sp"
        ];

        private readonly string _value;

        public StringItem(string value, bool followedByDigit)
        {
            if (followedByDigit && value.Length == 1)
                value = value switch
                {
                    "a" => "alpha",
                    "b" => "beta",
                    "m" => "milestone",
                    _ => value
                };
            _value = value == "cr" ? "rc" : value;
        }

        public int Type => 1;
        public bool IsNull => _value.Length == 0;

        public int CompareTo(IItem other)
        {
            return other is null
                ? string.CompareOrdinal(_ComparableQualifier(_value), ReleaseIndex)
                : other.Type switch
                {
                    0 => -1,
                    1 => string.CompareOrdinal(
                        _ComparableQualifier(_value),
                        _ComparableQualifier(((StringItem)other)._value)),
                    2 => -other.CompareTo(this),
                    3 => _CompareCombination((CombinationItem)other),
                    _ => throw new InvalidOperationException("Invalid Maven version item")
                };
        }

        private static string _ComparableQualifier(string qualifier)
        {
            if (qualifier is "ga" or "final" or "release")
                return ReleaseIndex;
            
            var index = Array.IndexOf(_Qualifiers, qualifier);
            return index < 0
                ? $"{_Qualifiers.Length}-{qualifier}"
                : index.ToString();
        }

        private int _CompareCombination(CombinationItem other)
        {
            var result = CompareTo(other.StringPart);
            return result != 0
                ? result
                : CompareTo(null) == 0
                    ? -other.DigitPart.CompareTo(null)
                    : -1;
        }
    }

    private sealed class CombinationItem : IItem
    {
        public CombinationItem(string value)
        {
            var index = 0;
            while (index < value.Length && !char.IsDigit(value[index]))
                index++;
            
            StringPart = new StringItem(value[..index], true);
            DigitPart = _ParseItem(false, true, value[index..]);
        }

        public StringItem StringPart { get; }
        public IItem DigitPart { get; }

        public int Type => 3;
        public bool IsNull => false;

        public int CompareTo(IItem other)
        {
            if (other is not null)
                return other.Type switch
                {
                    0 => -1,
                    1 => _CompareString((StringItem)other),
                    2 => -other.CompareTo(this),
                    3 => _CompareCombination((CombinationItem)other),
                    _ => throw new InvalidOperationException("Invalid Maven version item")
                };
            
            var result = StringPart.CompareTo(null);
            return result == 0
                ? DigitPart.CompareTo(null)
                : result;
        }

        private int _CompareString(StringItem other)
        {
            var result = StringPart.CompareTo(other);
            return result != 0
                ? result
                : StringPart.CompareTo(null) == 0
                    ? DigitPart.CompareTo(null)
                    : 1;
        }

        private int _CompareCombination(CombinationItem other)
        {
            var result = StringPart.CompareTo(other.StringPart);
            return result == 0
                ? DigitPart.CompareTo(other.DigitPart)
                : result;
        }
    }

    private sealed class ListItem : List<IItem>, IItem
    {
        public int Type => 2;
        public bool IsNull => Count == 0;

        public int CompareTo(IItem other)
        {
            if (other is null)
            {
                return this
                    .Select(item => item.CompareTo(null))
                    .FirstOrDefault(result => result != 0);
            }

            switch (other.Type)
            {
                case 0:
                    return -1;
                
                case 1 or 3:
                {
                    var result = Count == 0
                        ? -other.CompareTo(null)
                        : this[0].CompareTo(other);
                    for (var i = 1; result == 0 && i < Count; i++)
                        result = this[i].CompareTo(null);
                    return result;
                }
            }

            if (other is not ListItem list)
                throw new InvalidOperationException("Invalid Maven version item");

            var max = Math.Max(Count, list.Count);
            for (var i = 0; i < max; i++)
            {
                var left = i < Count ? this[i] : null;
                var right = i < list.Count ? list[i] : null;
                var result = left?.CompareTo(right)
                    ?? -right?.CompareTo(null)
                    ?? 0;
                
                if (result != 0)
                    return result;
            }

            return 0;
        }

        public void Normalize()
        {
            for (var i = Count - 1; i >= 0; i--)
            {
                var item = this[i];
                if (!item.IsNull)
                    continue;
                
                if (i == Count - 1
                    || this[i + 1].Type == 1
                    || this[i + 1] is ListItem { Count: > 0 } next
                    && next[0].Type is 1 or 3)
                    RemoveAt(i);
            }

            if (Count != 1
                || this[0] is not ListItem only)
                return;
            
            Clear();
            AddRange(only);
        }
    }
}