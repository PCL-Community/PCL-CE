using System;
using System.Text.RegularExpressions;

namespace PCL.Core.Helper
{
    public class SemVer : IComparable<SemVer>, IEquatable<SemVer>
    {
        // 正则表达式模式（含可选 v 前缀）
        private const string Pattern = @"^v?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
            @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
            @"(?:\+(?<build>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";

        private static readonly Regex SemVerRegex = new Regex(
            Pattern,
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
        public string BuildMetadata { get; }

        public SemVer(int major, int minor, int patch, string prerelease = null, string buildMetadata = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? string.Empty;
            BuildMetadata = buildMetadata ?? string.Empty;
        }

        public static SemVer Parse(string version)
        {
            if (!TryParse(version, out SemVer result))
            {
                throw new ArgumentException("Invalid semantic version format");
            }
            return result;
        }

        public static bool TryParse(string version, out SemVer result)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                result = null;
                return false;
            }

            var match = SemVerRegex.Match(version);
            if (!match.Success)
            {
                result = null;
                return false;
            }

            result = CreateFromMatch(match);
            return true;
        }

        private static SemVer CreateFromMatch(Match match)
        {
            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            int patch = int.Parse(match.Groups["patch"].Value);
            string prerelease = match.Groups["prerelease"].Value;
            string build = match.Groups["build"].Value;

            return new SemVer(major, minor, patch, prerelease, build);
        }

        public int CompareTo(SemVer other)
        {
            if (other is null) return 1;

            int compare = Major.CompareTo(other.Major);
            if (compare != 0) return compare;

            compare = Minor.CompareTo(other.Minor);
            if (compare != 0) return compare;

            compare = Patch.CompareTo(other.Patch);
            if (compare != 0) return compare;

            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        private static int ComparePrerelease(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal))
                return 0;

            // 正式版优先级高于预发布版
            if (string.IsNullOrEmpty(a)) return 1;
            if (string.IsNullOrEmpty(b)) return -1;

            string[] identifiersA = a.Split('.');
            string[] identifiersB = b.Split('.');

            int minLength = Math.Min(identifiersA.Length, identifiersB.Length);
            for (int i = 0; i < minLength; i++)
            {
                string idA = identifiersA[i];
                string idB = identifiersB[i];

                bool aIsNumeric = int.TryParse(idA, out int numA);
                bool bIsNumeric = int.TryParse(idB, out int numB);

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
        public override bool Equals(object obj)
        {
            return Equals(obj as SemVer);
        }

        public bool Equals(SemVer other)
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
                int hash = 17;
                hash = hash * 23 + Major.GetHashCode();
                hash = hash * 23 + Minor.GetHashCode();
                hash = hash * 23 + Patch.GetHashCode();
                hash = hash * 23 + Prerelease.GetHashCode();
                hash = hash * 23 + BuildMetadata.GetHashCode();
                return hash;
            }
        }

        // 运算符重载
        public static bool operator ==(SemVer left, SemVer right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(SemVer left, SemVer right) => !(left == right);
        public static bool operator <(SemVer left, SemVer right) =>
            left is null ? right != null : left.CompareTo(right) < 0;
        public static bool operator >(SemVer left, SemVer right) =>
            left != null && left.CompareTo(right) > 0;
        public static bool operator <=(SemVer left, SemVer right) =>
            left is null || left.CompareTo(right) <= 0;
        public static bool operator >=(SemVer left, SemVer right) =>
            left is null ? right is null : left.CompareTo(right) >= 0;
    }
}