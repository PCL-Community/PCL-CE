using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile(string path)
    {
        public enum LocalFileStatus
        {
            Fine = 0,
            Disabled = 1,
            Unavailable = 2
        }

        private static readonly SearchValues<char> DescriptionTrailingChars =
            SearchValues.Create("qwertyuiopasdfghjklzxcvbnm0123456789");

        private readonly List<EmbeddedConflict> _conflictDeclarations = [];

        private readonly List<EmbeddedDependency> _dependencyDeclarations = [];

        private readonly Dictionary<string, string?> _dependencyRaw =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _providedIds = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _providedVersions =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     资源的文件的地址。
        /// </summary>
        public readonly string path = path;

        private string? _authors;

        private Dictionary<string, string?> _dependencies = new();

        private string? _description;

        private string? _litematicEnclosingSize;

        private string? _litematicOriginalName;

        private int? _litematicRegionCount;

        private long? _litematicTimeCreated;

        private long? _litematicTimeModified;

        private int? _litematicTotalBlocks;

        private int? _litematicTotalVolume;

        private int? _litematicVersion;

        private string? _modId;

        private string? _name;

        /// <summary>
        ///     NBT数据是否已加载（用于延迟加载优化）。
        /// </summary>
        private bool _nbtDataLoaded;

        private string? _schemOriginalName;

        private int? _spongeVersion;

        private string? _structureAuthor;

        private int? _structureDataVersion;

        private string? _structureGameVersion;

        private string? _version;

        /// <summary>
        ///     其他可能的 ModID。
        /// </summary>
        public List<string> possibleModId = [];

        /// <summary>
        ///     是否为文件夹项。
        /// </summary>
        public bool IsFolder => path.EndsWithF(@"\__FOLDER__", true);

        /// <summary>
        ///     获取实际的文件夹路径（去除 __FOLDER__ 标记）。
        /// </summary>
        public string ActualPath => IsFolder ? path.Replace(@"\__FOLDER__", "") : path;

        /// <summary>
        ///     Mod 资源的完整路径，去除最后的 .disabled 和 .old。
        /// </summary>
        public string RawPath => Common.GetPathFromFullPath(path) + RawFileName;

        /// <summary>
        ///     资源的完整文件名。
        /// </summary>
        public string FileName =>
            IsFolder && !string.IsNullOrEmpty(Name) ? Name : Common.GetFileNameFromPath(path);

        /// <summary>
        ///     Mod 资源的完整文件名，去除最后的 .disabled 和 .old。
        /// </summary>
        public string RawFileName => FileName.Replace(".disabled", "").Replace(".old", "");

        /// <summary>
        ///     资源的状态。对于 Mod 有 Disabled
        /// </summary>
        public LocalFileStatus State
        {
            get
            {
                Load();
                return !IsFileAvailable
                    ? LocalFileStatus.Unavailable
                    : path.EndsWithF(".disabled", true) || path.EndsWithF(".old", true)
                        ? LocalFileStatus.Disabled
                        : LocalFileStatus.Fine;
            }
        }

        /// <summary>
        ///     Mod 的名称。若不可用则为 ModID 或无扩展的文件名。
        /// </summary>
        public string Name
        {
            get
            {
                if (_name is null)
                    Load();

                _name ??= _modId;
                if (_name is not null)
                    return _name;

                _name = IsFolder
                    ? Common.GetFolderNameFromPath(ActualPath)
                    : Path.GetFileNameWithoutExtension(path);
                return _name;
            }
            set
            {
                if (_name is null
                    && !value.Contains("modname")
                    && !value.Equals("name", StringComparison.CurrentCultureIgnoreCase)
                    && value.Length > 1
                    && Common.Val(value).ToString(CultureInfo.InvariantCulture) != value)
                    _name = value;
            }
        }

        /// <summary>
        ///     Mod 的描述信息。
        /// </summary>
        public string? Description
        {
            get
            {
                if (_description is null)
                    Load();
                if (_description is null && _fileUnavailableReason is not null)
                    _description = _fileUnavailableReason.Message;
                return _description;
            }
            set
            {
                if (_description is not null || value is null || value.Length <= 2)
                    return;
                _description = value.Trim('\n');
                if (_description.ToLower().AsSpan().LastIndexOfAny(DescriptionTrailingChars)
                    == _description.Length - 1)
                    _description += ".";
            }
        }

        /// <summary>
        ///     文件类型标签。
        /// </summary>
        public List<string> Tags
        {
            get
            {
                if (field is not null)
                    return field;

                field = [];
                if (IsFolder)
                {
                    field.Add("文件夹");
                    return field;
                }

                var tag = Path.GetExtension(RawPath).ToLower() switch
                {
                    ".litematic" => "原理图",
                    ".schem" or ".schematic" => "Schematic结构",
                    ".nbt" => "原版结构",
                    _ => null
                };
                if (tag is not null)
                    field.Add(tag);

                return field;
            }
        }

        /// <summary>
        ///     Mod 的版本，不保证符合版本格式规范。
        /// </summary>
        public string? Version
        {
            get
            {
                if (_version is null)
                    Load();
                return _version;
            }
            set
            {
                if (_version is not null && _version.RegexCheck(@"[0-9.\-]+"))
                    return;
                if (value?.ContainsF("version", true) == true)
                    value = "version";
                _version = value;
            }
        }

        /// <summary>
        ///     用于依赖检查的 ModID。
        /// </summary>
        public string? ModId
        {
            get
            {
                if (_modId is null)
                    Load();
                return _modId;
            }
            set
            {
                if (value is null)
                    return;
                value = value.RegexSeek(RegexPatterns.ModIdMatch);
                if (value is null
                    || value.Length <= 1
                    || Common.Val(value).ToString(CultureInfo.InvariantCulture) == value)
                    return;
                if (value.Equals("name", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("modid", StringComparison.OrdinalIgnoreCase))
                    return;
                if (!possibleModId.Contains(value))
                    possibleModId.Add(value);
                _modId ??= value;
            }
        }

        /// <summary>
        ///     Mod 的主页。
        /// </summary>
        public string? Url
        {
            get
            {
                if (field is null)
                    Load();
                return field;
            }
            set
            {
                if (field is null && value is not null && value.StartsWithF("http"))
                    field = value;
            }
        }

        /// <summary>
        ///     Mod 的作者列表。
        /// </summary>
        public string? Authors
        {
            get
            {
                if (_authors is null)
                    Load();
                return _authors;
            }
            set
            {
                if (_authors is null && !string.IsNullOrWhiteSpace(value))
                    _authors = value;
            }
        }

        /// <summary>
        ///     Litematic 文件的创建时间戳。
        /// </summary>
        public long? LitematicTimeCreated
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicTimeCreated;
            }
        }

        /// <summary>
        ///     Litematic 文件的修改时间戳。
        /// </summary>
        public long? LitematicTimeModified
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicTimeModified;
            }
        }

        /// <summary>
        ///     Schem 读取到的原始名称。
        /// </summary>
        public string? SchemOriginalName
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _schemOriginalName;
            }
        }

        /// <summary>
        ///     Litematic 读取到的原始名称。
        /// </summary>
        public string? LitematicOriginalName
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicOriginalName;
            }
        }

        /// <summary>
        ///     Litematic 文件的版本。
        /// </summary>
        public int? LitematicVersion
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicVersion;
            }
        }

        /// <summary>
        ///     Litematic 文件的包围盒大小。
        /// </summary>
        public string? LitematicEnclosingSize
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicEnclosingSize;
            }
        }

        /// <summary>
        ///     Litematic 文件的区域数量。
        /// </summary>
        public int? LitematicRegionCount
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicRegionCount;
            }
        }

        /// <summary>
        ///     Litematic 文件的总方块数。
        /// </summary>
        public int? LitematicTotalBlocks
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicTotalBlocks;
            }
        }

        /// <summary>
        ///     Litematic 文件的总体积。
        /// </summary>
        public int? LitematicTotalVolume
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicTotalVolume;
            }
        }

        /// <summary>
        ///     原版结构文件的游戏版本。
        /// </summary>
        public string? StructureGameVersion
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _structureGameVersion;
            }
        }

        /// <summary>
        ///     原版结构文件的数据版本。
        /// </summary>
        public int? StructureDataVersion
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _structureDataVersion;
            }
        }

        /// <summary>
        ///     原版结构文件的作者。
        /// </summary>
        public string? StructureAuthor
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _structureAuthor;
            }
        }

        /// <summary>
        ///     Sponge Schematic 文件的版本。
        /// </summary>
        public int? SpongeVersion
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _spongeVersion;
            }
        }

        /// <summary>
        ///     Mod 图标路径。
        /// </summary>
        public string? Logo { get; set; }

        /// <summary>
        ///     依赖项，其中包括了 Minecraft 的版本要求。格式为 ModID - VersionRequirement，若无版本要求则为 Nothing。
        /// </summary>
        public Dictionary<string, string?> Dependencies
        {
            get
            {
                Load();
                return _dependencies;
            }
        }

        /// <summary>
        ///     依赖的原始版本约束（未经 Maven 括号规整），保留 Fabric/Quilt semver 谓词原貌，供
        ///     <see cref="McConstraintMatcher" /> 按加载器方言求值；key=ModId，无版本要求则为 null。
        ///     <see cref="Dependencies" /> 仍存规整值以兼容既有消费方。
        /// </summary>
        public Dictionary<string, string?> DependencyRaw
        {
            get
            {
                Load();
                return _dependencyRaw;
            }
        }

        /// <summary>
        ///     全部依赖声明。同一 ModId 的不同范围、声明方或可选性不会被字典合并，供 JIJ 关系分析使用。
        /// </summary>
        public IReadOnlyList<EmbeddedDependency> DependencyDeclarations
        {
            get
            {
                Load();
                return _dependencyDeclarations;
            }
        }

        /// <summary>
        ///     本文件额外提供的 Mod id 别名：multi-mod jar 的兄弟 <c>[[mods]]</c> id 与 Fabric <c>provides</c>。
        ///     依赖索引把它们当作与主 ModId 同源、同版本的 provider，避免依赖别名时误报缺失。
        /// </summary>
        public HashSet<string> ProvidedIds
        {
            get
            {
                Load();
                return _providedIds;
            }
        }

        /// <summary>multi-mod JAR 中兄弟 ModId 各自声明的版本；Fabric provides 未指定版本时回退主版本。</summary>
        public IReadOnlyDictionary<string, string> ProvidedVersions
        {
            get
            {
                Load();
                return _providedVersions;
            }
        }

        /// <summary>全部冲突声明；同一目标的不同范围与严重级别分别保留。</summary>
        public IReadOnlyList<EmbeddedConflict> ConflictDeclarations
        {
            get
            {
                Load();
                return _conflictDeclarations;
            }
        }

        private void _AddConflict(
            string modId,
            string? versionRange,
            bool hard,
            string? declarerId = null)
        {
            if (modId.Length < 2)
                return;

            modId = modId.ToLower();
            if (DependencyIds.IsPlatform(modId))
                return;

            if (!string.IsNullOrWhiteSpace(versionRange) && versionRange.Contains('$'))
                return;

            var raw = string.IsNullOrWhiteSpace(versionRange)
                ? null
                : versionRange;
            if (_HasConflict(modId, raw, hard, declarerId))
                return;

            _conflictDeclarations.Add(
                new EmbeddedConflict
                {
                    DeclarerId = declarerId,
                    Target = modId,
                    Raw = raw,
                    Hard = hard
                });
        }

        private bool _HasConflict(
            string modId,
            string? raw,
            bool hard,
            string? declarerId)
        {
            return _conflictDeclarations.Any(c =>
                string.Equals(
                    c.Target,
                    modId,
                    StringComparison.OrdinalIgnoreCase)
                && c.Raw == raw
                && c.Hard == hard
                && string.Equals(
                    c.DeclarerId,
                    declarerId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void AddDependency(
            string modId,
            string? versionRequirement = null,
            bool optional = false)
        {
            if (modId.Length < 2)
                return;

            modId = modId.ToLower();
            if (DependencyIds.IsLoaderId(modId))
                return;

            if (modId == "name"
                || Common.Val(modId).ToString(CultureInfo.InvariantCulture) == modId)
                return;

            var raw =
                string.IsNullOrWhiteSpace(versionRequirement) || versionRequirement.Contains('$')
                    ? null
                    : versionRequirement;
            var hasRequired = _dependencyDeclarations.Any(d =>
                string.Equals(d.Id, modId, StringComparison.OrdinalIgnoreCase) && !d.Optional);

            _AddDependencyDeclaration(modId, raw, optional, hasRequired);
            _UpdateRawDependency(modId, raw, optional, hasRequired);

            var normalizedVersion = _NormalizeVersionRange(versionRequirement);
            if (_dependencies.TryAdd(modId, normalizedVersion))
                return;

            _dependencies[modId] ??= normalizedVersion;
        }

        private void _AddDependencyDeclaration(
            string modId,
            string? raw,
            bool optional,
            bool hasRequired)
        {
            if (!optional)
                _dependencyDeclarations.RemoveAll(d =>
                    string.Equals(d.Id, modId, StringComparison.OrdinalIgnoreCase) && d.Optional);

            if ((!optional || !hasRequired)
                && !_dependencyDeclarations.Any(d =>
                    string.Equals(d.Id, modId, StringComparison.OrdinalIgnoreCase)
                    && d.Raw == raw
                    && d.Optional == optional))
                _dependencyDeclarations.Add(
                    new EmbeddedDependency
                    {
                        Id = modId,
                        Raw = raw,
                        Optional = optional
                    });
        }

        private void _UpdateRawDependency(
            string modId,
            string? raw,
            bool optional,
            bool hasRequired)
        {
            if (!_dependencyRaw.TryGetValue(modId, out var oldRaw)
                || oldRaw is null
                || (!optional && !hasRequired))
                _dependencyRaw[modId] = raw;
        }

        private static string? _NormalizeVersionRange(string? versionRequirement)
        {
            if (versionRequirement is null
                || (!versionRequirement.Contains('.') && !versionRequirement.Contains('-'))
                || versionRequirement.Contains('$'))
                return null;

            if (!versionRequirement.StartsWithF("[")
                && !versionRequirement.StartsWithF("(")
                && !versionRequirement.EndsWithF("]")
                && !versionRequirement.EndsWithF(")"))
                return $"[{versionRequirement},)";

            return versionRequirement;
        }

        private static List<(string? DeclarerId, string Body)> _ExtractInlineDepArrays(string text)
        {
            var result = new List<(string?, string)>();
            foreach (Match match in InlineDependencyArrayRegex().Matches(text))
            {
                var start = match.Index + match.Length;
                if (!_TryFindInlineArrayEnd(text, start, out var end))
                    continue;

                result.Add((_ParseDeclarerId(match.Value), text.Substring(start, end - start)));
            }

            return result;
        }

        private static string? _ParseDeclarerId(string matchValue)
        {
            var header = matchValue[..matchValue.IndexOf('=')].Trim();
            var declarerId = header["dependencies".Length..].Trim().TrimStart('.').Trim('"');
            return declarerId.Length == 0 ? null : declarerId;
        }

        private static bool _TryFindInlineArrayEnd(string text, int start, out int end)
        {
            var depth = 1;
            var inString = false;
            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (inString)
                {
                    if (ch == '"')
                        inString = false;
                    continue;
                }

                switch (ch)
                {
                    case '"':
                        inString = true;
                        break;
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        if (depth != 0)
                            break;
                        end = i;
                        return true;
                }
            }

            end = -1;
            return false;
        }

        [GeneratedRegex(@"dependencies[.\w""-]*\s*=\s*\[", RegexOptions.IgnoreCase)]
        private static partial Regex InlineDependencyArrayRegex();
    }
}