using System;
using System.Text.RegularExpressions;

namespace PCL.Core.Utils;

[Serializable]
public class SemVer(int major, int minor, int patch, string? prerelease = null, string? buildMetadata = null)
    : IComparable<SemVer>, IEquatable<SemVer>
{
    public int Major => major;
    public int Minor => minor;
    public int Patch => patch;
    public string Prerelease => prerelease ?? string.Empty;
    public string BuildMetadata => buildMetadata ?? string.Empty;

    public static SemVer Parse(string version)
    {
        if (!TryParse(version, out var result))
        {
            throw new ArgumentException("Invalid semantic version format");
        }
        return result!;
    }

    public static bool TryParse(string version, out SemVer? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        var match = RegexPatterns.SemVer.Match(version);
        if (!match.Success)
        {
            result = null;
            return false;
        }

        result = _CreateFromMatch(match);
        return true;
    }

    private static SemVer _CreateFromMatch(Match match)
    {
        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        var patch = int.Parse(match.Groups["patch"].Value);
        var prerelease = match.Groups["prerelease"].Value;
        var build = match.Groups["build"].Value;

        return new SemVer(major, minor, patch, prerelease, build);
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null) return 1;

        var compare = Major.CompareTo(other.Major);
        if (compare != 0) return compare;

        compare = Minor.CompareTo(other.Minor);
        if (compare != 0) return compare;

        compare = Patch.CompareTo(other.Patch);
        if (compare != 0) return compare;

        return _ComparePrerelease(Prerelease, other.Prerelease);
    }

    private static int _ComparePrerelease(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return 0;

        // 正式版优先级高于预发布版
        if (string.IsNullOrEmpty(a)) return 1;
        if (string.IsNullOrEmpty(b)) return -1;

        var identifiersA = a.Split('.');
        var identifiersB = b.Split('.');

        var minLength = Math.Min(identifiersA.Length, identifiersB.Length);
        for (var i = 0; i < minLength; i++)
        {
            var idA = identifiersA[i];
            var idB = identifiersB[i];

            var aIsNumeric = int.TryParse(idA, out var numA);
            var bIsNumeric = int.TryParse(idB, out var numB);

            int result;
            if (aIsNumeric && bIsNumeric)
            {
                result = numA.CompareTo(numB);
            }
            else if (aIsNumeric || bIsNumeric)
            {
                // 数值标识符比非数值标识符优先级低
                result = aIsNumeric ? -1 : 1;
            }
            else
            {
                result = string.Compare(idA, idB, StringComparison.Ordinal);
            }

            if (result != 0)
                return result;
        }

        return identifiersA.Length.CompareTo(identifiersB.Length);
    }

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";

        if (!string.IsNullOrEmpty(Prerelease))
            version += $"-{Prerelease}";

        if (!string.IsNullOrEmpty(BuildMetadata))
            version += $"+{BuildMetadata}";

        return version;
    }

    // 实现相等性比较
    public override bool Equals(object? obj)
    {
        return Equals(obj as SemVer);
    }

    public bool Equals(SemVer? other)
    {
        return other != null &&
               Major == other.Major &&
               Minor == other.Minor &&
               Patch == other.Patch &&
               string.Equals(Prerelease, other.Prerelease, StringComparison.Ordinal) &&
               string.Equals(BuildMetadata, other.BuildMetadata, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + Major.GetHashCode();
            hash = hash * 23 + Minor.GetHashCode();
            hash = hash * 23 + Patch.GetHashCode();
            hash = hash * 23 + Prerelease.GetHashCode();
            hash = hash * 23 + BuildMetadata.GetHashCode();
            return hash;
        }
    }

    // 运算符重载
    public static bool operator ==(SemVer? left, SemVer? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(SemVer? left, SemVer? right) => !(left == right);
    public static bool operator <(SemVer? left, SemVer? right) =>
        left is null ? right != null : left.CompareTo(right) < 0;
    public static bool operator >(SemVer? left, SemVer? right) =>
        left != null && left.CompareTo(right) > 0;
    public static bool operator <=(SemVer? left, SemVer? right) =>
        left is null || left.CompareTo(right) <= 0;
    public static bool operator >=(SemVer? left, SemVer? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;
}
