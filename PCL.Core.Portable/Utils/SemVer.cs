// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace PCL.Core.Utils;

[Serializable]
public class SemVer(int major, int minor, int patch, string? prerelease = null, string? buildMetadata = null)
    : IComparable<SemVer>, IEquatable<SemVer>
{
    public int Major { get; } = major;
    public int Minor { get; } = minor;
    public int Patch { get; } = patch;
    public string Prerelease { get; } = prerelease ?? string.Empty;
    public string BuildMetadata { get; } = buildMetadata ?? string.Empty;

    public static SemVer Parse(string version)
    {
        if (!TryParse(version, out SemVer? result))
            throw new ArgumentException("Invalid semantic version format.", nameof(version));
        return result!;
    }

    public static bool TryParse(string? version, out SemVer? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(version))
            return false;

        ReadOnlySpan<char> value = version;
        int index = value[0] == 'v' ? 1 : 0;
        if (!TryReadCoreNumber(value, ref index, out int major) ||
            !TryReadSeparator(value, ref index, '.') ||
            !TryReadCoreNumber(value, ref index, out int minor) ||
            !TryReadSeparator(value, ref index, '.') ||
            !TryReadCoreNumber(value, ref index, out int patch))
        {
            return false;
        }

        string prerelease = string.Empty;
        string buildMetadata = string.Empty;
        if (index < value.Length && value[index] == '-')
        {
            int start = ++index;
            if (!TryReadIdentifiers(value, ref index, stopAtBuildMetadata: true, enforceNumericLeadingZero: true))
                return false;
            prerelease = value[start..index].ToString();
        }

        if (index < value.Length && value[index] == '+')
        {
            int start = ++index;
            if (!TryReadIdentifiers(value, ref index, stopAtBuildMetadata: false, enforceNumericLeadingZero: false))
                return false;
            buildMetadata = value[start..index].ToString();
        }

        if (index != value.Length)
            return false;

        result = new SemVer(major, minor, patch, prerelease, buildMetadata);
        return true;
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null)
            return 1;

        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
            return comparison;

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
            return comparison;

        comparison = Patch.CompareTo(other.Patch);
        return comparison != 0
            ? comparison
            : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString()
    {
        int length = CountDigits(Major) + CountDigits(Minor) + CountDigits(Patch) + 2;
        if (Prerelease.Length > 0)
            length += Prerelease.Length + 1;
        if (BuildMetadata.Length > 0)
            length += BuildMetadata.Length + 1;

        return string.Create(length, this, static (destination, version) =>
        {
            int written = 0;
            version.Major.TryFormat(destination[written..], out int count, provider: CultureInfo.InvariantCulture);
            written += count;
            destination[written++] = '.';
            version.Minor.TryFormat(destination[written..], out count, provider: CultureInfo.InvariantCulture);
            written += count;
            destination[written++] = '.';
            version.Patch.TryFormat(destination[written..], out count, provider: CultureInfo.InvariantCulture);
            written += count;

            if (version.Prerelease.Length > 0)
            {
                destination[written++] = '-';
                version.Prerelease.AsSpan().CopyTo(destination[written..]);
                written += version.Prerelease.Length;
            }

            if (version.BuildMetadata.Length > 0)
            {
                destination[written++] = '+';
                version.BuildMetadata.AsSpan().CopyTo(destination[written..]);
            }
        });
    }

    public override bool Equals(object? obj) => Equals(obj as SemVer);

    public bool Equals(SemVer? other) =>
        other is not null &&
        Major == other.Major &&
        Minor == other.Minor &&
        Patch == other.Patch &&
        string.Equals(Prerelease, other.Prerelease, StringComparison.Ordinal) &&
        string.Equals(BuildMetadata, other.BuildMetadata, StringComparison.Ordinal);

    public override int GetHashCode() =>
        HashCode.Combine(Major, Minor, Patch, Prerelease, BuildMetadata);

    public static bool operator ==(SemVer? left, SemVer? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(SemVer? left, SemVer? right) => !(left == right);

    public static bool operator <(SemVer? left, SemVer? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(SemVer? left, SemVer? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(SemVer? left, SemVer? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >=(SemVer? left, SemVer? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    private static bool TryReadCoreNumber(ReadOnlySpan<char> value, ref int index, out int number)
    {
        number = 0;
        int start = index;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            int digit = value[index] - '0';
            if (number > (int.MaxValue - digit) / 10)
                return false;
            number = number * 10 + digit;
            index++;
        }

        int length = index - start;
        return length > 0 && (length == 1 || value[start] != '0');
    }

    private static bool TryReadSeparator(ReadOnlySpan<char> value, ref int index, char separator)
    {
        if (index >= value.Length || value[index] != separator)
            return false;
        index++;
        return true;
    }

    private static bool TryReadIdentifiers(
        ReadOnlySpan<char> value,
        ref int index,
        bool stopAtBuildMetadata,
        bool enforceNumericLeadingZero)
    {
        bool hasIdentifier = false;
        while (index < value.Length)
        {
            int start = index;
            bool numeric = true;
            while (index < value.Length && value[index] != '.' &&
                   (!stopAtBuildMetadata || value[index] != '+'))
            {
                char character = value[index];
                if (!IsIdentifierCharacter(character))
                    return false;
                numeric &= IsAsciiDigit(character);
                index++;
            }

            int length = index - start;
            if (length == 0 || enforceNumericLeadingZero && numeric && length > 1 && value[start] == '0')
                return false;
            hasIdentifier = true;

            if (index >= value.Length || stopAtBuildMetadata && value[index] == '+')
                break;
            index++;
        }

        return hasIdentifier;
    }

    private static int ComparePrerelease(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.SequenceEqual(right))
            return 0;
        if (left.IsEmpty)
            return 1;
        if (right.IsEmpty)
            return -1;

        while (true)
        {
            ReadOnlySpan<char> leftIdentifier = ReadIdentifier(ref left);
            ReadOnlySpan<char> rightIdentifier = ReadIdentifier(ref right);
            int comparison = CompareIdentifier(leftIdentifier, rightIdentifier);
            if (comparison != 0)
                return comparison;
            if (left.IsEmpty || right.IsEmpty)
                return left.IsEmpty.CompareTo(right.IsEmpty) * -1;
        }
    }

    private static ReadOnlySpan<char> ReadIdentifier(ref ReadOnlySpan<char> value)
    {
        int separator = value.IndexOf('.');
        if (separator < 0)
        {
            ReadOnlySpan<char> identifier = value;
            value = [];
            return identifier;
        }

        ReadOnlySpan<char> result = value[..separator];
        value = value[(separator + 1)..];
        return result;
    }

    private static int CompareIdentifier(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        bool leftNumeric = IsNumeric(left);
        bool rightNumeric = IsNumeric(right);
        if (leftNumeric && rightNumeric)
        {
            int lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0 ? lengthComparison : left.SequenceCompareTo(right);
        }

        if (leftNumeric != rightNumeric)
            return leftNumeric ? -1 : 1;
        return left.SequenceCompareTo(right);
    }

    private static bool IsNumeric(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!IsAsciiDigit(character))
                return false;
        }
        return true;
    }

    private static bool IsIdentifierCharacter(char value) =>
        IsAsciiDigit(value) ||
        value is >= 'A' and <= 'Z' ||
        value is >= 'a' and <= 'z' ||
        value == '-';

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static int CountDigits(int value)
    {
        if (value == 0)
            return 1;

        int count = value < 0 ? 1 : 0;
        uint magnitude = value < 0 ? (uint)(-(long)value) : (uint)value;
        while (magnitude > 0)
        {
            magnitude /= 10;
            count++;
        }
        return count;
    }
}
