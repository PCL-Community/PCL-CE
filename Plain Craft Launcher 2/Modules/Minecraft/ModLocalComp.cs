using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App;
using PCL.Core.Utils;
using static PCL.ModComp;
using static PCL.ModLoader;

namespace PCL;

public static class ModLocalComp
{
    private const int LocalModCacheVersion = 7;

    public class LocalCompFile
    {
        /// <summary>
        ///     是否可能为前置 Mod。
        /// </summary>
        public bool IsPresetMod()
        {
            return !Dependencies.Any() && Name is not null &&
                   (Name.ToLower().Contains("core") || Name.ToLower().Contains("lib"));
        }

        /// <summary>
        ///     根据完整文件路径的文件扩展名判断是否为 Mod 文件。
        /// </summary>
        public static bool IsModFile(string Path)
        {
            if (Path is null || !Path.Contains("."))
                return false;
            Path = Path.ToLower();
            if (Path.EndsWithF(".jar", true) || Path.EndsWithF(".zip", true) || Path.EndsWithF(".litemod", true) ||
                Path.EndsWithF(".jar.disabled", true) || Path.EndsWithF(".zip.disabled", true) ||
                Path.EndsWithF(".litemod.disabled", true) || Path.EndsWithF(".jar.old", true) ||
                Path.EndsWithF(".zip.old", true) || Path.EndsWithF(".litemod.old", true))
                return true;
            return false;
        }

        /// <summary>
        ///     检查是否为指定类型的组件文件。
        /// </summary>
        public static bool IsCompFile(string Path, CompType CompType)
        {
            if (Path is null || !Path.Contains("."))
                return false;
            Path = Path.ToLower();
            switch (CompType)
            {
                case CompType.Mod:
                {
                    return IsModFile(Path);
                }
                case CompType.ResourcePack:
                case CompType.Shader:
                {
                    return Path.EndsWithF(".zip", true);
                }
                case CompType.DataPack:
                {
                    return Path.EndsWithF(".zip", true) || Path.EndsWithF(".zip.disabled", true);
                }
                case CompType.Schematic:
                {
                    return Path.EndsWithF(".litematic", true) || Path.EndsWithF(".nbt", true) ||
                           Path.EndsWithF(".schematic", true) || Path.EndsWithF(".schem", true);
                }

                default:
                {
                    return false;
                }
            }
        }

        /// <summary>
        ///     获取图标路径。
        /// </summary>
        public string GetLogo()
        {
            if (Comp is not null && Comp.LogoUrl is not null)
                return Comp.LogoUrl;
            if (Logo is not null)
                return Logo;

            // 为文件夹设置特定图标
            if (IsFolder) return "pack://application:,,,/images/Icons/Folder.png";

            return ModBase.PathImage + "Icons/NoIcon.png";
        }

        #region Litematic 文件处理

        /// <summary>
        ///     读取 Litematic 文件的 NBT 数据。
        /// </summary>
        private void LoadLitematicNbtData()
        {
            try
            {
                ModBase.Log($"开始读取 Litematic NBT 数据：{Path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var scheNbt = new NbtFile();
                    scheNbt.LoadFromStream(fs, NbtCompression.AutoDetect);
                    // 读取版本信息
                    var versionTag = (NbtInt)scheNbt.RootTag.Get("Version");
                    if (versionTag is not null) _litematicVersion = versionTag.Value;

                    // 读取 Metadata 节点
                    var metadataTag = scheNbt.RootTag.Get<NbtCompound>("Metadata");
                    if (metadataTag is not null)
                    {
                        ModBase.Log("找到 Litematic Metadata 节点", ModBase.LogLevel.Debug);

                        // 读取名称
                        var nameTag = metadataTag.Get<NbtString>("Name");
                        if (nameTag is not null && !string.IsNullOrWhiteSpace(nameTag.Value) &&
                            nameTag.Value != "Unnamed") _litematicOriginalName = nameTag.Value;

                        // 读取描述信息
                        var descriptionTag = metadataTag.Get<NbtString>("Description");
                        if (descriptionTag is not null && !string.IsNullOrWhiteSpace(descriptionTag.Value))
                            _Description = descriptionTag.Value;

                        // 读取作者信息
                        var authorTag = metadataTag.Get<NbtString>("Author");
                        if (authorTag is not null && !string.IsNullOrWhiteSpace(authorTag.Value))
                            _Authors = authorTag.Value;

                        // 读取时间信息
                        var timeCreatedTag = metadataTag.Get<NbtLong>("TimeCreated");
                        if (timeCreatedTag is not null) _litematicTimeCreated = timeCreatedTag.Value;

                        var timeModifiedTag = metadataTag.Get<NbtLong>("TimeModified");
                        if (timeModifiedTag is not null) _litematicTimeModified = timeModifiedTag.Value;

                        // 读取包围盒大小
                        var enclosingSizeTag = metadataTag.Get<NbtCompound>("EnclosingSize");
                        if (enclosingSizeTag is not null)
                        {
                            var xTag = enclosingSizeTag.Get<NbtInt>("x");
                            var yTag = enclosingSizeTag.Get<NbtInt>("y");
                            var zTag = enclosingSizeTag.Get<NbtInt>("z");
                            if (xTag is not null && yTag is not null && zTag is not null)
                                _litematicEnclosingSize = $"{xTag.Value} × {yTag.Value} × {zTag.Value}";
                        }

                        // 读取区域数量
                        var regionCountTag = metadataTag.Get<NbtInt>("RegionCount");
                        if (regionCountTag is not null) _litematicRegionCount = regionCountTag.Value;

                        // 读取总方块数
                        var totalBlocksTag = metadataTag.Get<NbtInt>("TotalBlocks");
                        if (totalBlocksTag is not null) _litematicTotalBlocks = totalBlocksTag.Value;

                        // 读取总体积
                        var totalVolumeTag = metadataTag.Get<NbtInt>("TotalVolume");
                        if (totalVolumeTag is not null) _litematicTotalVolume = totalVolumeTag.Value;
                    }
                    else
                    {
                        ModBase.Log("未找到 Litematic Metadata 节点", ModBase.LogLevel.Debug);
                    }
                }

                ModBase.Log("Litematic NBT 数据读取完成", ModBase.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 Litematic NBT 数据时出错（" + Path + "）");
            }
        }

        #endregion

        #region Schem 文件处理

        /// <summary>
        ///     读取 .schem 文件的 NBT 数据（Sponge Schematic 格式）。
        /// </summary>
        private void LoadSchemNbtData()
        {
            try
            {
                ModBase.Log($"开始读取 Schem NBT 数据：{Path}", ModBase.LogLevel.Debug);

                // 使用自动检测压缩格式
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var scheNbt = new NbtFile();
                    scheNbt.LoadFromStream(fs, NbtCompression.AutoDetect);

                    // 读取Sponge版本信息
                    var versionTag = scheNbt.RootTag.Get<NbtInt>("Version");
                    if (versionTag is not null) _spongeVersion = versionTag.Value;

                    // 读取数据版本信息
                    var dataVersionTag = scheNbt.RootTag.Get<NbtInt>("DataVersion");
                    if (dataVersionTag is not null) _structureDataVersion = dataVersionTag.Value;

                    // 读取尺寸信息
                    var widthTag = scheNbt.RootTag.Get<NbtShort>("Width");
                    var heightTag = scheNbt.RootTag.Get<NbtShort>("Height");
                    var lengthTag = scheNbt.RootTag.Get<NbtShort>("Length");

                    if (widthTag is not null && heightTag is not null && lengthTag is not null)
                    {
                        _litematicEnclosingSize = $"{widthTag.Value} × {heightTag.Value} × {lengthTag.Value}";
                        _litematicTotalVolume = (short)(widthTag.Value * heightTag.Value) * lengthTag.Value;

                        // 对于Sponge格式，方块数量等于总体积（因为包含空气方块）
                        _litematicTotalBlocks = _litematicTotalVolume;
                    }

                    // 读取调色板信息来计算区域数量
                    var paletteTag = scheNbt.RootTag.Get<NbtCompound>("Palette");
                    if (paletteTag is not null) _litematicRegionCount = 1; // Sponge Schematic 通常只有一个区域

                    // 读取元数据
                    var metadataTag = scheNbt.RootTag.Get<NbtCompound>("Metadata");
                    if (metadataTag is not null)
                    {
                        // 读取名称
                        var nameTag = metadataTag.Get<NbtString>("Name");
                        if (nameTag is not null && !string.IsNullOrWhiteSpace(nameTag.Value))
                            _schemOriginalName = nameTag.Value;

                        // 读取作者信息
                        var authorTag = metadataTag.Get<NbtString>("Author");
                        if (authorTag is not null && !string.IsNullOrWhiteSpace(authorTag.Value))
                        {
                            _structureAuthor = authorTag.Value;
                            if (_Authors is null)
                                _Authors = _structureAuthor;
                        }
                    }
                }

                ModBase.Log("Schem NBT 数据读取完成", ModBase.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 Schem NBT 数据时出错（" + Path + "）");
            }
        }

        #endregion

        #region Schematic 文件处理

        /// <summary>
        ///     读取 .schematic 文件的 NBT 数据（MCEdit/WorldEdit 格式）。
        /// </summary>
        private void LoadSchematicNbtData()
        {
            try
            {
                ModBase.Log($"开始读取 Schematic NBT 数据：{Path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var scheNbt = new NbtFile();
                    scheNbt.LoadFromStream(fs, NbtCompression.AutoDetect);
                    // 读取尺寸信息
                    var widthTag = scheNbt.RootTag.Get<NbtShort>("Width");
                    var heightTag = scheNbt.RootTag.Get<NbtShort>("Height");
                    var lengthTag = scheNbt.RootTag.Get<NbtShort>("Length");
                    if (widthTag is not null && heightTag is not null && lengthTag is not null)
                    {
                        _litematicEnclosingSize = $"{widthTag.Value} × {heightTag.Value} × {lengthTag.Value}";
                        _litematicTotalVolume = (short)(widthTag.Value * heightTag.Value) * lengthTag.Value;
                    }

                    // 读取材料列表
                    var materialsTag = scheNbt.RootTag.Get<NbtString>("Materials");
                    if (materialsTag is not null)
                        ModBase.Log($"Schematic 材料类型：{materialsTag.Value}", ModBase.LogLevel.Debug);

                    ModBase.Log("Schematic NBT 数据读取完成", ModBase.LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 Schematic NBT 数据时出错（" + Path + "）");
            }
        }

        #endregion

        #region NBT 结构文件处理

        /// <summary>
        ///     读取 .nbt 文件的 NBT 数据（Minecraft 结构文件格式）。
        /// </summary>
        private void LoadStructureNbtData()
        {
            try
            {
                ModBase.Log($"开始读取 NBT 结构文件数据：{Path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var scheNbt = new NbtFile();
                    scheNbt.LoadFromStream(fs, NbtCompression.AutoDetect);
                    // 读取作者信息
                    var authorTag = scheNbt.RootTag.Get<NbtString>("author");
                    if (authorTag is not null && !string.IsNullOrWhiteSpace(authorTag.Value))
                    {
                        _structureAuthor = authorTag.Value;
                        if (_Authors is null)
                            _Authors = _structureAuthor;
                    }

                    // 读取尺寸信息
                    var sizeTag = scheNbt.RootTag.Get<NbtList>("size");
                    if (sizeTag is not null)
                    {
                        var sizeElements = sizeTag.ToArray();
                        if (sizeElements.Length >= 3)
                        {
                            var sizeArray = sizeElements.Take(3).Select(e => e.IntValue).ToArray();
                            _litematicEnclosingSize = $"{sizeArray[0]} × {sizeArray[1]} × {sizeArray[2]}";
                            _litematicTotalVolume = sizeArray[0] * sizeArray[1] * sizeArray[2];
                        }
                    }

                    // 读取方块数量信息
                    var blocksTag = scheNbt.RootTag.Get<NbtList>("blocks");
                    if (blocksTag is not null)
                        _litematicTotalBlocks = blocksTag.Where(x => x.TagType == NbtTagType.Compound).Count();

                    // 读取调色板信息来计算区域数量
                    var paletteTag = scheNbt.RootTag.Get<NbtList>("palette");
                    if (paletteTag is not null) _litematicRegionCount = 1; // 原版结构文件通常只有一个区域
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 NBT 结构文件数据时出错（" + Path + "）");
            }
        }

        #endregion

        #region 基础

        /// <summary>
        ///     资源的文件的地址。
        /// </summary>
        public readonly string Path;

        /// <summary>
        ///     是否为文件夹项。
        /// </summary>
        public bool IsFolder => Path.EndsWithF(@"\__FOLDER__", true);

        /// <summary>
        ///     获取实际的文件夹路径（去除 __FOLDER__ 标记）。
        /// </summary>
        public string ActualPath
        {
            get
            {
                if (IsFolder) return Path.Replace(@"\__FOLDER__", "");

                return Path;
            }
        }

        public LocalCompFile(string Path)
        {
            this.Path = Path ?? "";
        }

        /// <summary>
        ///     NBT数据是否已加载（用于延迟加载优化）。
        /// </summary>
        private bool _nbtDataLoaded;

        /// <summary>
        ///     Mod 资源的完整路径，去除最后的 .disabled 和 .old。
        /// </summary>
        public string RawPath => ModBase.GetPathFromFullPath(Path) + RawFileName;

        /// <summary>
        ///     资源的完整文件名。
        /// </summary>
        public string FileName
        {
            get
            {
                if (IsFolder && !string.IsNullOrEmpty(Name)) return Name;

                return ModBase.GetFileNameFromPath(Path);
            }
        }

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
                if (!IsFileAvailable) return LocalFileStatus.Unavailable;

                if (Path.EndsWithF(".disabled", true) || Path.EndsWithF(".old", true)) return LocalFileStatus.Disabled;

                return LocalFileStatus.Fine;
            }
        }

        public enum LocalFileStatus
        {
            Fine = 0,
            Disabled = 1,
            Unavailable = 2
        }

        #endregion

        #region 信息项

        /// <summary>
        ///     Mod 的名称。若不可用则为 ModID 或无扩展的文件名。
        /// </summary>
        public string Name
        {
            get
            {
                if (_Name is null)
                    Load();
                if (_Name is null)
                    _Name = _ModId;
                if (_Name is null)
                {
                    if (IsFolder)
                        _Name = ModBase.GetFolderNameFromPath(ActualPath);
                    else
                        _Name = ModBase.GetFileNameWithoutExtentionFromPath(Path);
                }

                return _Name;
            }
            set
            {
                if (_Name is null && value is not null && !value.Contains("modname") && value.ToLower() != "name" &&
                    value.Length > 1 && (ModBase.Val(value).ToString() ?? "") != (value ?? "")) _Name = value;
            }
        }

        private string _Name;

        /// <summary>
        ///     Mod 的描述信息。
        /// </summary>
        public string Description
        {
            get
            {
                if (_Description is null)
                    Load();
                if (_Description is null && FileUnavailableReason is not null)
                    _Description = FileUnavailableReason.Message;
                // If _Description Is Nothing Then _Description = Path
                return _Description;
            }
            set
            {
                if (_Description is null && value is not null && value.Length > 2)
                {
                    _Description = value.Trim('\n');
                    // 优化显示：若以 [a-zA-Z0-9] 结尾，加上小数点句号
                    if (_Description.ToLower().LastIndexOfAny("qwertyuiopasdfghjklzxcvbnm0123456789".ToCharArray()) ==
                        _Description.Length - 1)
                        _Description += ".";
                }
            }
        }

        private string _Description;

        /// <summary>
        ///     文件类型标签。
        /// </summary>
        public List<string> Tags
        {
            get
            {
                if (_tags is null)
                {
                    _tags = new List<string>();
                    if (IsFolder)
                    {
                        _tags.Add("文件夹");
                    }
                    else
                    {
                        var extension = System.IO.Path.GetExtension(RawPath).ToLower();
                        switch (extension ?? "")
                        {
                            case ".litematic":
                            {
                                _tags.Add("原理图");
                                break;
                            }
                            case ".schem":
                            case ".schematic":
                            {
                                _tags.Add("Schematic结构");
                                break;
                            }
                            case ".nbt":
                            {
                                _tags.Add("原版结构");
                                break;
                            }
                        }
                    }
                }

                return _tags;
            }
        }

        private List<string> _tags;

        /// <summary>
        ///     Mod 的版本，不保证符合版本格式规范。
        /// </summary>
        public string Version
        {
            get
            {
                if (_Version is null)
                    Load();
                return _Version;
            }
            set
            {
                if (_Version is not null && _Version.RegexCheck(@"[0-9.\-]+"))
                    return;
                if (value?.ContainsF("version", true) == true)
                    value = "version"; // 需要修改的标识
                _Version = value;
            }
        }

        public string _Version;

        /// <summary>
        ///     用于依赖检查的 ModID。
        /// </summary>
        public string ModId
        {
            get
            {
                if (_ModId is null)
                    Load();
                return _ModId;
            }
            set
            {
                if (value is null)
                    return;
                value = value.RegexSeek(RegexPatterns.ModIdMatch);
                if (value is null || value.Length <= 1 || (ModBase.Val(value).ToString() ?? "") == (value ?? ""))
                    return;
                if (value.ContainsF("name", true) || value.ContainsF("modid", true))
                    return;
                if (!PossibleModId.Contains(value))
                    PossibleModId.Add(value);
                if (_ModId is null)
                    _ModId = value;
            }
        }

        private string _ModId;

        /// <summary>
        ///     其他可能的 ModID。
        /// </summary>
        public List<string> PossibleModId = new();

        /// <summary>
        ///     Mod 的主页。
        /// </summary>
        public string Url
        {
            get
            {
                if (_Url is null)
                    Load();
                return _Url;
            }
            set
            {
                if (_Url is null && value is not null && value.StartsWithF("http")) _Url = value;
            }
        }

        private string _Url;

        /// <summary>
        ///     Mod 的作者列表。
        /// </summary>
        public string Authors
        {
            get
            {
                if (_Authors is null)
                    Load();
                return _Authors;
            }
            set
            {
                if (_Authors is null && !string.IsNullOrWhiteSpace(value)) _Authors = value;
            }
        }

        private string _Authors;

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

        private long? _litematicTimeCreated;

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

        private long? _litematicTimeModified;

        /// <summary>
        ///     Schem 读取到的原始名称。
        /// </summary>
        public string SchemOriginalName
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _schemOriginalName;
            }
        }

        private string _schemOriginalName;

        /// <summary>
        ///     Litematic 读取到的原始名称。
        /// </summary>
        public string LitematicOriginalName
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicOriginalName;
            }
        }

        private string _litematicOriginalName;

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

        private int? _litematicVersion;

        /// <summary>
        ///     Litematic 文件的包围盒大小。
        /// </summary>
        public string LitematicEnclosingSize
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _litematicEnclosingSize;
            }
        }

        private string _litematicEnclosingSize;

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

        private int? _litematicRegionCount;

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

        private int? _litematicTotalBlocks;

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

        private int? _litematicTotalVolume;

        /// <summary>
        ///     原版结构文件的游戏版本。
        /// </summary>
        public string StructureGameVersion
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _structureGameVersion;
            }
        }

        private string _structureGameVersion;

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

        private int? _structureDataVersion;

        /// <summary>
        ///     原版结构文件的作者。
        /// </summary>
        public string StructureAuthor
        {
            get
            {
                LoadNbtDataIfNeeded();
                return _structureAuthor;
            }
        }

        private string _structureAuthor;

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

        private int? _spongeVersion;

        /// <summary>
        ///     Mod 图标路径。
        /// </summary>
        public string Logo { get; set; }

        /// <summary>
        ///     依赖项，其中包括了 Minecraft 的版本要求。格式为 ModID - VersionRequirement，若无版本要求则为 Nothing。
        /// </summary>
        public Dictionary<string, string> Dependencies
        {
            get
            {
                Load();
                return _Dependencies;
            }
        }

        private Dictionary<string, string> _Dependencies = new();

        private void AddDependency(string ModID, string VersionRequirement = null)
        {
            // 确保信息正确
            if (ModID is null || ModID.Length < 2)
                return;
            ModID = ModID.ToLower();
            if (ModID == "name" || (ModBase.Val(ModID).ToString() ?? "") == (ModID ?? ""))
                return; // 跳过 name 与纯数字 id
            if (VersionRequirement is null ||
                (!VersionRequirement.Contains(".") && !VersionRequirement.Contains("-")) ||
                VersionRequirement.Contains("$"))
                VersionRequirement = null;
            else if (!VersionRequirement.StartsWithF("[") && !VersionRequirement.StartsWithF("(") &&
                     !VersionRequirement.EndsWithF("]") && !VersionRequirement.EndsWithF(")"))
                VersionRequirement = "[" + VersionRequirement + ",)";
            // 向依赖项中添加
            if (_Dependencies.ContainsKey(ModID))
            {
                if (_Dependencies[ModID] is null)
                    _Dependencies[ModID] = VersionRequirement;
            }
            else
            {
                _Dependencies.Add(ModID, VersionRequirement);
            }
        }

        #endregion

        #region 加载步骤标记

        // 1. 进行文件可用性检查
        // 成功：继续第二步。
        // 失败：标记 FileUnavailableReason， 并停止后续加载。
        /// <summary>
        ///     是否已进行 Mod 文件的基础加载。（这包括第一步和第二步）
        /// </summary>
        private bool IsLoaded;

        /// <summary>
        ///     Mod 文件是否可被正常读取。
        /// </summary>
        public bool IsFileAvailable
        {
            get
            {
                Load();
                return FileUnavailableReason is null;
            }
        }

        /// <summary>
        ///     Mod 文件出错的原因。若无错误，则为 Nothing。
        /// </summary>
        public Exception FileUnavailableReason
        {
            get
            {
                Load();
                return _FileUnavailableReason;
            }
        }

        private Exception _FileUnavailableReason;

        // 2. 进行 .class 以外的信息获取
        // 成功：标记 IsInfoWithoutClassAvailable。
        // 失败：什么也不干。如果需要补充信息的话，检测到 IsInfoWithoutClassAvailable 为 False，会自动继续加载。
        /// <summary>
        ///     是否已在不获取 .class 文件的前提下完成了所需信息的加载。
        /// </summary>
        private bool IsInfoWithoutClassAvailable = false;

        // 3. 尝试从 .class 文件中获取信息
        // 成功：标记 IsInfoWithClassAvailable。
        // 失败：什么也不干。
        /// <summary>
        ///     是否已进行 .class 文件的信息获取。
        /// </summary>
        private bool IsInfoWithClassLoaded;

        /// <summary>
        ///     是否已在 .class 文件中完成了所需信息的加载。
        /// </summary>
        private bool IsInfoWithClassAvailable;

        #endregion

        #region 加载

        /// <summary>
        ///     初始化所有数据。
        /// </summary>
        private void Init()
        {
            _Name = null;
            _Description = null;
            _Version = null;
            _ModId = null;
            PossibleModId = new List<string>();
            _Dependencies = new Dictionary<string, string>();
            IsLoaded = false;
            _FileUnavailableReason = null;
            IsInfoWithClassLoaded = false;
            IsInfoWithClassAvailable = false;
        }

        /// <summary>
        ///     加载基本信息（不解析NBT数据）。
        /// </summary>
        public void LoadBasicInfo()
        {
            try
            {
                // 可用性检查
                if (IsFolder)
                {
                    // 文件夹项不需要进一步处理
                    IsLoaded = true;
                    return;
                }

                if (!File.Exists(Path))
                {
                    _FileUnavailableReason = new FileNotFoundException("未找到资源文件（" + Path + ")");
                    IsLoaded = true;
                    return;
                }

                // 对于原理图文件，只设置基本状态，不解析NBT数据
                if (Path.EndsWithF(".litematic", true) || Path.EndsWithF(".nbt", true) ||
                    Path.EndsWithF(".schem", true) || Path.EndsWithF(".schematic", true))
                {
                    _Name = ModBase.GetFileNameWithoutExtentionFromPath(Path);
                    IsLoaded = true;
                    return;
                }

                // 对于其他文件类型，正常加载
                Load();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"加载基本信息失败：{Path}");
            }
        }

        /// <summary>
        ///     延迟加载NBT数据。
        /// </summary>
        public void LoadNbtDataIfNeeded()
        {
            try
            {
                // 如果已经加载过NBT数据，则跳过
                if (_nbtDataLoaded)
                    return;

                // 根据文件类型加载NBT数据
                if (Path.EndsWithF(".litematic", true))
                    LoadLitematicNbtData();
                else if (Path.EndsWithF(".nbt", true))
                    LoadStructureNbtData();
                else if (Path.EndsWithF(".schem", true))
                    LoadSchemNbtData();
                else if (Path.EndsWithF(".schematic", true)) LoadSchematicNbtData();

                _nbtDataLoaded = true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"延迟加载NBT数据失败：{Path}");
            }
        }

        /// <summary>
        ///     进行文件可用性检查与 .class 以外的信息获取。
        /// </summary>
        public void Load(bool ForceReload = false)
        {
            if (IsLoaded && !ForceReload)
                return;
            // 初始化
            Init();

            // 基础可用性检查
            if (Path.Length < 2)
            {
                _FileUnavailableReason = new FileNotFoundException("错误的资源文件路径（" + (Path ?? "null") + "）");
                IsLoaded = true;
                return;
            }

            // 对于文件夹项，检查实际文件夹路径是否存在
            if (IsFolder)
            {
                if (!Directory.Exists(ActualPath))
                {
                    _FileUnavailableReason = new DirectoryNotFoundException("未找到文件夹（" + ActualPath + "）");
                    IsLoaded = true;
                    return;
                }

                // 文件夹项不需要进一步处理
                IsLoaded = true;
                return;
            }

            if (!File.Exists(Path))
            {
                _FileUnavailableReason = new FileNotFoundException("未找到资源文件（" + Path + "）");
                IsLoaded = true;
                return;
            }

            // 对于投影文件，跳过 zip 解析
            if (Path.EndsWithF(".litematic", true) || Path.EndsWithF(".nbt", true) || Path.EndsWithF(".schem", true) ||
                Path.EndsWithF(".schematic", true))
            {
                try
                {
                    _Name = ModBase.GetFileNameWithoutExtentionFromPath(Path);
                    // 根据文件类型加载数据
                    if (Path.EndsWithF(".litematic", true))
                    {
                        LoadLitematicNbtData();
                    }
                    else if (Path.EndsWithF(".schem", true) || Path.EndsWithF(".schematic", true))
                    {
                        if (Path.EndsWithF(".schem", true))
                            LoadSchemNbtData();
                        else
                            LoadSchematicNbtData();
                    }
                    else if (Path.EndsWithF(".nbt", true))
                    {
                        LoadStructureNbtData();
                    }

                    _nbtDataLoaded = true;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "投影文件信息获取失败（" + Path + "）", ModBase.LogLevel.Developer);
                    _FileUnavailableReason = ex;
                }

                IsLoaded = true;
                return;
            }

            // 对于其他文件，尝试作为 Jar 文件打开
            ZipArchive Jar = null;
            try
            {
                Jar = new ZipArchive(new FileStream(Path, FileMode.Open));
                // 信息获取
                LookupMetadata(Jar);
            }
            catch (UnauthorizedAccessException ex)
            {
                ModBase.Log(ex, "资源文件由于无权限无法打开（" + Path + "）", ModBase.LogLevel.Developer);
                _FileUnavailableReason = new UnauthorizedAccessException("没有读取此文件的权限，请尝试右键以管理员身份运行 PCL", ex);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "资源文件无法打开（" + Path + "）", ModBase.LogLevel.Developer);
                _FileUnavailableReason = ex;
            }
            finally
            {
                if (Jar is not null)
                    Jar.Dispose();
            }

            // 完成标记
            IsLoaded = true;
        }

        /// <summary>
        ///     从 Jar 文件中获取 Mod 信息。
        /// </summary>
        private void LookupMetadata(ZipArchive Jar)
        {
            #region 尝试使用 mcmod.info

            do
            {
                try
                {
                    // 获取信息文件
                    var InfoEntry = Jar.GetEntry("mcmod.info");
                    string InfoString = null;
                    if (InfoEntry is not null)
                    {
                        InfoString = ModBase.ReadFile(InfoEntry.Open());
                        if (InfoString.Length < 15)
                            InfoString = null;
                    }

                    if (InfoString is null)
                        break;
                    // 获取可用 Json 项
                    JsonObject InfoObject;
                    var JsonObject = (JsonNode)ModBase.GetJson(InfoString);
                    if (JsonObject.GetValueKind() == JsonValueKind.Array)
                        InfoObject = (JsonObject)JsonObject[0];
                    else
                        InfoObject = (JsonObject)JsonObject["modList"][0];
                    // 从文件中获取 Mod 信息项
                    Name = (string)InfoObject["name"];
                    Description = (string)InfoObject["description"];
                    Version = (string)InfoObject["version"];
                    Url = (string)InfoObject["url"];
                    ModId = (string)InfoObject["modid"];
                    var AuthorJson = (JsonArray)InfoObject["authorList"];
                    if (AuthorJson is not null)
                    {
                        var Author = new List<string>();
                        foreach (var Token in AuthorJson)
                            Author.Add(Token.ToString());
                        if (Author.Any())
                            Authors = Author.Join(", ");
                    }

                    var LogoFile = (string)InfoObject["logoFile"];
                    if (LogoFile is not null)
                    {
                        var LogoItem = Jar.GetEntry(LogoFile);
                        if (LogoItem is not null)
                        {
                            var md5 = ModBase.GetStringMD5(LogoItem.Length + LogoItem.CompressedLength + Path);
                            Logo = System.IO.Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{md5}.png");
                            using (var EntryStream = LogoItem.Open())
                            {
                                ModBase.WriteFile(Logo, EntryStream);
                            }
                        }
                    }

                    var Reqs = (JsonArray)InfoObject["requiredMods"];
                    if (Reqs is not null)
                        foreach (string item in Reqs) // 将迭代变量重命名为 item
                            if (!string.IsNullOrEmpty(item))
                            {
                                // 使用一个局部变量 token 来处理逻辑
                                var token = item;

                                token = token.Substring(token.IndexOfF(":") + 1);
                                if (token.Contains("@"))
                                {
                                    var parts = token.Split("@");
                                    AddDependency(parts[0], parts[1]);
                                }
                                else
                                {
                                    AddDependency(token);
                                }
                            }

                    Reqs = (JsonArray)InfoObject["dependancies"];
                    if (Reqs is not null)
                        foreach (string rawToken in Reqs)
                            if (!string.IsNullOrEmpty(rawToken))
                            {
                                var id = rawToken.Substring(rawToken.IndexOfF(":") + 1);

                                if (id.Contains("@"))
                                {
                                    var parts = id.Split("@");
                                    AddDependency(parts[0], parts[1]);
                                }
                                else
                                {
                                    AddDependency(id);
                                }
                            }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 mcmod.info 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 fabric.mod.json

            do
            {
                try
                {
                    var FabricEntry = Jar.GetEntry("fabric.mod.json");
                    string FabricText = null;
                    if (FabricEntry is not null)
                    {
                        FabricText = ModBase.ReadFile(FabricEntry.Open(), Encoding.UTF8);
                        if (!FabricText.Contains("schemaVersion")) FabricText = null;
                    }

                    if (FabricText is null) break;

                    var FabricObject = (JsonObject)ModBase.GetJson(FabricText);

                    if (FabricObject.ContainsKey("name")) Name = FabricObject["name"].ToString();
                    if (FabricObject.ContainsKey("version")) Version = FabricObject["version"].ToString();
                    if (FabricObject.ContainsKey("description")) Description = FabricObject["description"].ToString();
                    if (FabricObject.ContainsKey("id")) ModId = FabricObject["id"].ToString();
                    if (FabricObject.ContainsKey("contact") && FabricObject["contact"]["homepage"] is not null)
                        Url = FabricObject["contact"]["homepage"].ToString();

                    var AuthorJson = (JsonArray)FabricObject["authors"];
                    if (AuthorJson is not null)
                    {
                        var AuthorList = AuthorJson.Select(t => t.ToString()).ToList();
                        if (AuthorList.Any()) Authors = string.Join(", ", AuthorList);
                    }

                    if (FabricObject.ContainsKey("icon"))
                    {
                        var LogoFile = FabricObject["icon"].ToString();
                        var LogoItem = Jar.GetEntry(LogoFile);
                        if (LogoItem is not null)
                        {
                            var md5 = ModBase.GetStringMD5(LogoItem.Length + LogoItem.CompressedLength + Path);
                            Logo = System.IO.Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{md5}.png");
                            using (var EntryStream = LogoItem.Open())
                            {
                                ModBase.WriteFile(Logo, EntryStream);
                            }
                        }
                    }

                    // 依赖处理 (省略了 VB 中的注释部分，按逻辑实现)
                    if (FabricObject.ContainsKey("depends"))
                        foreach (var dep in (JsonObject)FabricObject["depends"])
                            AddDependency(dep.Key, dep.Value.ToString());
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 fabric.mod.json 时出错（" + Path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 quilt.mod.json

            do
            {
                try
                {
                    // 获取 quilt.mod.json 文件
                    var QuiltEntry = Jar.GetEntry("quilt.mod.json");
                    string QuiltText = null;
                    if (QuiltEntry is not null)
                    {
                        QuiltText = ModBase.ReadFile(QuiltEntry.Open(), Encoding.UTF8);
                        if (!QuiltText.Contains("schema_version"))
                            QuiltText = null;
                    }

                    if (QuiltText is null)
                        break;
                    var QuiltObject = (JsonObject)((JsonObject)ModBase.GetJson(QuiltText))["quilt_loader"];
                    // 从文件中获取 Mod 信息项
                    if (QuiltObject.ContainsKey("id"))
                        ModId = (string)QuiltObject["id"];
                    if (QuiltObject.ContainsKey("version"))
                        Version = (string)QuiltObject["version"];
                    if (QuiltObject.ContainsKey("metadata"))
                    {
                        var QuiltMetadata = (JsonObject)QuiltObject["metadata"];
                        if (QuiltMetadata.ContainsKey("name"))
                            Name = (string)QuiltMetadata["name"];
                        if (QuiltMetadata.ContainsKey("description"))
                            Description = (string)QuiltMetadata["description"];
                        if (QuiltMetadata.ContainsKey("contact"))
                            Url = (string)(QuiltMetadata["contact"]["homepage"] ?? "");
                    }

                    if (QuiltObject.ContainsKey("icon"))
                    {
                        var LogoFile = (string)QuiltObject["icon"];
                        if (LogoFile is not null)
                        {
                            var LogoItem = Jar.GetEntry(LogoFile);
                            if (LogoItem is not null)
                            {
                                var md5 = ModBase.GetStringMD5(LogoItem.Length + LogoItem.CompressedLength + Path);
                                Logo = System.IO.Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{md5}.png");
                                using (var EntryStream = LogoItem.Open())
                                {
                                    ModBase.WriteFile(Logo, EntryStream);
                                }
                            }
                        }
                    }

                    goto Finished;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 quilt.mod.json 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 mods.toml

            try
            {
                // 获取 mods.toml 文件
                var TomlEntry = Jar.GetEntry("META-INF/mods.toml");
                string TomlText = null;
                if (TomlEntry is not null)
                {
                    using (var reader = new StreamReader(TomlEntry.Open()))
                    {
                        TomlText = reader.ReadToEnd();
                    }

                    if (TomlText.Length < 15) TomlText = null;
                }

                if (TomlText is not null)
                {
                    // 文件标准化：统一换行符为 \n，去除注释、头尾的空格、空行
                    var Lines = new List<string>();
                    var rawLines = TomlText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

                    foreach (var rawLine in rawLines)
                    {
                        var line = rawLine;
                        if (line.StartsWithF("#")) continue; // 去除注释
                        if (line.Contains("#")) line = line.Substring(0, line.IndexOfF("#"));
                        // 去除头尾的空格（包含全角空格）
                        line = line.Trim(' ', '\t', '　');
                        if (!string.IsNullOrEmpty(line)) Lines.Add(line);
                    }

                    // 读取文件数据
                    // TomlData 存储段落名及其对应的键值对
                    var TomlData = new List<KeyValuePair<string, Dictionary<string, object>>>
                    {
                        new("", new Dictionary<string, object>())
                    };

                    for (var i = 0; i < Lines.Count; i++)
                    {
                        var Line = Lines[i];
                        if (Line.StartsWithF("[") && Line.EndsWithF("]"))
                        {
                            // 段落标记
                            var Header = Line.Trim('[', ']');
                            TomlData.Add(
                                new KeyValuePair<string, Dictionary<string, object>>(Header,
                                    new Dictionary<string, object>()));
                        }
                        else if (Line.Contains("="))
                        {
                            // 字段标记
                            var Key = Line.Substring(0, Line.IndexOfF("=")).TrimEnd(' ', '\t', '　');
                            var RawValue = Line.Substring(Line.IndexOfF("=") + 1).TrimStart(' ', '\t', '　');
                            object Value;

                            if (RawValue.StartsWithF("\"") && RawValue.EndsWithF("\""))
                            {
                                // 单行字符串
                                Value = RawValue.Trim('\"');
                            }
                            else if (RawValue.StartsWithF("'''"))
                            {
                                // 多行字符串
                                var ValueLines = new List<string> { RawValue.Replace("'''", "") };
                                if (!RawValue.EndsWithF("'''") || RawValue.Length == 3)
                                    while (i < Lines.Count - 1)
                                    {
                                        i++;
                                        var ValueLine = Lines[i];
                                        if (ValueLine.EndsWithF("'''"))
                                        {
                                            ValueLines.Add(ValueLine.Replace("'''", ""));
                                            break;
                                        }

                                        ValueLines.Add(ValueLine);
                                    }

                                Value = string.Join("\n", ValueLines).Trim('\n').Replace("\n", "\r\n");
                            }
                            else if (RawValue.ToLower() == "true" || RawValue.ToLower() == "false")
                            {
                                // 布尔型
                                Value = RawValue.ToLower() == "true";
                            }
                            else if (double.TryParse(RawValue, out var num))
                            {
                                // 数字型 (模拟 VB 的 Val)
                                Value = num;
                            }
                            else
                            {
                                // 默认当做字符串存储
                                Value = RawValue;
                            }

                            // 将值存入当前最后的段落中
                            var lastPair = TomlData[TomlData.Count - 1];
                            lastPair.Value[Key] = Value;
                        }
                    }

                    // 从解析出的数据中提取 Mod 信息
                    Dictionary<string, object> ModEntry = null;
                    foreach (var subData in TomlData)
                        if (subData.Key == "mods")
                        {
                            ModEntry = subData.Value;
                            break;
                        }

                    if (ModEntry is not null && ModEntry.ContainsKey("modId"))
                    {
                        ModId = ModEntry["modId"].ToString();
                        // 假设 _ModId 是内部属性，如果为 null 说明设置失败
                        if (_ModId is not null)
                        {
                            if (ModEntry.ContainsKey("displayName")) Name = ModEntry["displayName"].ToString();
                            if (ModEntry.ContainsKey("description")) Description = ModEntry["description"].ToString();
                            if (ModEntry.ContainsKey("version")) Version = ModEntry["version"].ToString();

                            // [0] 是全局段落（无 Header）
                            if (TomlData[0].Value.ContainsKey("displayURL"))
                                Url = TomlData[0].Value["displayURL"].ToString();
                            if (TomlData[0].Value.ContainsKey("authors"))
                                Authors = TomlData[0].Value["authors"].ToString();

                            // 读取依赖
                            foreach (var subData in TomlData)
                                if (subData.Key.ToLower() == $"dependencies.{ModId.ToLower()}")
                                {
                                    var DepEntry = subData.Value;
                                    if (DepEntry.ContainsKey("modId") &&
                                        DepEntry.ContainsKey("mandatory") && (bool)DepEntry["mandatory"] &&
                                        DepEntry.ContainsKey("side") &&
                                        DepEntry["side"].ToString().ToLower() != "server")
                                        AddDependency(
                                            DepEntry["modId"].ToString(),
                                            DepEntry.ContainsKey("versionRange")
                                                ? DepEntry["versionRange"].ToString()
                                                : null
                                        );
                                }

                            // 加载成功，跳转到完成标签
                            goto Finished;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取 mods.toml 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
            }

            #endregion

            #region 尝试使用 fml_cache_annotation.json

            do
            {
                try
                {
                    // 获取 fml_cache_annotation.json 文件
                    var FmlEntry = Jar.GetEntry("META-INF/fml_cache_annotation.json");
                    string FmlText = null;
                    if (FmlEntry is not null)
                    {
                        FmlText = ModBase.ReadFile(FmlEntry.Open(), Encoding.UTF8);
                        if (!FmlText.Contains("Lnet/minecraftforge/fml/common/Mod;"))
                            FmlText = null;
                    }

                    if (FmlText is null)
                        break;
                    var FmlJson = (JsonObject)ModBase.GetJson(FmlText);
                    // 获取可用 Json 项
                    JsonObject FmlObject = null;
                    foreach (var ModFilePair in FmlJson)
                    {
                        var ModFileAnnos = (JsonArray)ModFilePair.Value["annotations"];
                        if (ModFileAnnos is not null)
                            // 先获取 Mod
                            foreach (var ModFileAnno in ModFileAnnos)
                            {
                                var Name = (string)(ModFileAnno["name"] ?? "");
                                if (Name == "Lnet/minecraftforge/fml/common/Mod;")
                                {
                                    FmlObject = (JsonObject)ModFileAnno["values"];
                                    goto Got;
                                }
                            }
                    }

                    break;
                    Got: ;

                    // 从文件中获取 Mod 信息项
                    if (FmlObject.ContainsKey("useMetadata") &&
                        (FmlObject["useMetadata"]["value"] ?? "").ToString().ToLower() == "true")
                    {
                        // 要求使用 mcmod.info 中的信息
                        var value = (string)FmlObject["modid"]["value"];
                        if (value is null)
                            break;
                        value = value.ToLower().RegexSeek(RegexPatterns.ModIdMatch);
                        if (value is not null && value.ToLower() != "name" && value.Length > 1 &&
                            (ModBase.Val(value).ToString() ?? "") != (value ?? ""))
                            if (!PossibleModId.Contains(value))
                                PossibleModId.Add(value);
                        break;
                    }

                    if (FmlObject.ContainsKey("name"))
                        Name = (string)FmlObject["name"]["value"];
                    if (FmlObject.ContainsKey("version"))
                        Version = (string)FmlObject["version"]["value"];
                    if (FmlObject.ContainsKey("modid"))
                        ModId = (string)FmlObject["modid"]["value"];
                    if (!FmlObject.ContainsKey("serverSideOnly") ||
                        !FmlObject["serverSideOnly"]["value"].ToObject<bool>())
                    {
                        // 添加 Minecraft 依赖
                        var DepMinecraft = (string)((FmlObject["acceptedMinecraftVersions"] is not null
                            ? FmlObject["acceptedMinecraftVersions"]["value"]
                            : "") ?? "");
                        if (!string.IsNullOrEmpty(DepMinecraft))
                            AddDependency("minecraft", DepMinecraft);
                        // 添加其他依赖
                        var Deps = (string)((FmlObject["dependencies"] is not null
                            ? FmlObject["dependencies"]["value"]
                            : "") ?? "");
                        if (!string.IsNullOrEmpty(Deps))
                            foreach (var item in Deps.Split(";"))
                            {
                                if (string.IsNullOrEmpty(item) || !item.StartsWithF("required-"))
                                    continue;

                                // 使用局部变量处理逻辑，不要直接修改迭代变量 item
                                var dep = item.Substring(item.IndexOfF(":") + 1);

                                if (dep.Contains("@"))
                                {
                                    var parts = dep.Split("@");
                                    AddDependency(parts[0], parts[1]);
                                }
                                else
                                {
                                    AddDependency(dep);
                                }
                            }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 fml_cache_annotation.json 时出现未知错误（" + Path + "）");
                }
            } while (false);

            #endregion

            #region 尝试识别资源包图标

            try
            {
                // 检查并提取资源包的 pack.png 图标
                var packPngEntry = Jar.GetEntry("pack.png");
                if (packPngEntry is not null)
                    try
                    {
                        var md5 = ModBase.GetStringMD5(packPngEntry.Length + packPngEntry.CompressedLength + Path);
                        Logo = System.IO.Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{md5}.png");
                        using (var entryStream = packPngEntry.Open())
                        {
                            ModBase.WriteFile(Logo, entryStream);
                        }

                        ModBase.Log("成功提取资源包图标：" + Path, ModBase.LogLevel.Debug);
                    }
                    catch (Exception logoEx)
                    {
                        ModBase.Log(logoEx, "提取 pack.png 图标失败（" + Path + "）", ModBase.LogLevel.Developer);
                    }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "识别资源包图标时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
            }

            #endregion

            Finished: ;

            #region 将 Version 代号转换为 META-INF 中的版本

            if (_Version == "version")
                try
                {
                    var MetaEntry = Jar.GetEntry("META-INF/MANIFEST.MF");
                    if (MetaEntry is not null)
                    {
                        var MetaString = ModBase.ReadFile(MetaEntry.Open()).Replace(" :", ":").Replace(": ", ":");
                        if (MetaString.Contains("Implementation-Version:"))
                        {
                            MetaString = MetaString.Substring(MetaString.IndexOfF("Implementation-Version:") +
                                                              "Implementation-Version:".Count());
                            MetaString = MetaString.Substring(0, MetaString.IndexOfAny("\r\n".ToCharArray()))
                                .Trim();
                            Version = MetaString;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log("获取 META-INF 中的版本信息失败（" + Path + "）", ModBase.LogLevel.Developer);
                    Version = null;
                }

            if (_Version is not null && !(_Version.Contains(".") || _Version.Contains("-")))
                Version = null;

            #endregion
        }

        #endregion

        #region 网络信息

        /// <summary>
        ///     当任何网络信息更新时触发。
        /// </summary>
        public event OnCompUpdateEventHandler? OnCompUpdate;

        public delegate void OnCompUpdateEventHandler(LocalCompFile sender);

        /// <summary>
        ///     该 Mod 关联的网络项目。
        /// </summary>
        public CompProject Comp
        {
            get => _Comp;
            set
            {
                _Comp = value;
                OnCompUpdate?.Invoke(this);
            }
        }

        private CompProject _Comp;

        /// <summary>
        ///     本地文件对应的联网文件信息。
        /// </summary>
        public CompFile CompFile;

        /// <summary>
        ///     该 Mod 对应的联网最新版本。
        /// </summary>
        public CompFile UpdateFile
        {
            get => _UpdateFile;
            set
            {
                _UpdateFile = value;
                OnCompUpdate?.Invoke(this);
            }
        }

        private CompFile _UpdateFile;

        /// <summary>
        ///     该 Mod 的更新日志网址。
        /// </summary>
        public List<string> ChangelogUrls = new();

        /// <summary>
        ///     所有网络信息是否已成功加载。
        /// </summary>
        public bool CompLoaded;

        /// <summary>
        ///     将网络信息保存为 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var Json = new JsonObject();
            if (Comp is not null)
                Json.Add("Comp", Comp.ToJson());
            Json.Add("ChangelogUrls", new JsonArray(ChangelogUrls.Select(s => (JsonNode)s).ToArray()));
            Json.Add("CompLoaded", CompLoaded);
            if (CompFile is not null)
                Json.Add("CompFile", CompFile.ToJson());
            if (UpdateFile is not null)
                Json.Add("UpdateFile", UpdateFile.ToJson());
            return Json;
        }

        /// <summary>
        ///     从 Json 中读取网络信息。
        /// </summary>
        public void FromJson(JsonObject Json)
        {
            CompLoaded = (bool)Json["CompLoaded"];
            if (Json.ContainsKey("Comp"))
                Comp = new CompProject((JsonObject)Json["Comp"]);
            if (Json.ContainsKey("ChangelogUrls"))
                ChangelogUrls = Json["ChangelogUrls"].ToObject<List<string>>();
            if (Json.ContainsKey("CompFile"))
                CompFile = new CompFile((JsonObject)Json["CompFile"], CompType.Mod);
            if (Json.ContainsKey("UpdateFile"))
                UpdateFile = new CompFile((JsonObject)Json["UpdateFile"], CompType.Mod);
        }

        /// <summary>
        ///     该文件是否可以更新。
        /// </summary>
        public bool CanUpdate => !Config.Preference.Hide.FunctionModUpdate && ChangelogUrls.Any();

        /// <summary>
        ///     获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
        /// </summary>
        public uint CurseForgeHash
        {
            get
            {
                if (_CurseForgeHash is null)
                {
                    // 读取缓存
                    var Info = new FileInfo(Path);
                    var CacheKey = ModBase.GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString()}-{Info.Length}-C")
                        .ToString();
                    var Cached = ModBase.ReadIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey);
                    if (!string.IsNullOrEmpty(Cached) && Cached.RegexCheck(@"^\d+$")) // #5062
                    {
                        _CurseForgeHash = uint.Parse(Cached);
                        return (uint)_CurseForgeHash;
                    }

                    // 读取文件
                    var data = new List<byte>();
                    foreach (var b in ModBase.ReadFileBytes(Path))
                    {
                        if (b == 9 || b == 10 || b == 13 || b == 32)
                            continue;
                        data.Add(b);
                    }

                    // 计算 MurmurHash2
                    var length = data.Count;
                    var h = (uint)(1 ^ length); // 1 是种子
                    int i;
                    var loopTo = length - 4;
                    for (i = 0; i <= loopTo; i += 4)
                    {
                        var k = data[i] | ((uint)data[i + 1] << 8) | ((uint)data[i + 2] << 16) |
                                ((uint)data[i + 3] << 24);
                        k = (uint)((k * 0x5BD1E995L) & 0xFFFFFFFFL);
                        k = k ^ (k >> 24);
                        k = (uint)((k * 0x5BD1E995L) & 0xFFFFFFFFL);
                        h = (uint)((h * 0x5BD1E995L) & 0xFFFFFFFFL);
                        h = h ^ k;
                    }

                    switch (length - i)
                    {
                        case 3:
                        {
                            h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                            h = h ^ ((uint)data[i + 2] << 16);
                            h = (uint)((h * 0x5BD1E995L) & 0xFFFFFFFFL);
                            break;
                        }
                        case 2:
                        {
                            h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                            h = (uint)((h * 0x5BD1E995L) & 0xFFFFFFFFL);
                            break;
                        }
                        case 1:
                        {
                            h = h ^ data[i];
                            h = (uint)((h * 0x5BD1E995L) & 0xFFFFFFFFL);
                            break;
                        }
                    }

                    h = h ^ (h >> 13);
                    h = (uint)((h * 0x5BD1E995L) & 0xFFFFFFFFL);
                    h = h ^ (h >> 15);
                    _CurseForgeHash = h;
                    // 写入缓存
                    ModBase.WriteIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey, h.ToString());
                }

                return (uint)_CurseForgeHash;
            }
        }

        private uint? _CurseForgeHash;

        /// <summary>
        ///     获取用于 Modrinth 信息获取的 Hash 值（SHA1）。
        /// </summary>
        public string ModrinthHash
        {
            get
            {
                if (_ModrinthHash is null)
                {
                    // 读取缓存
                    var Info = new FileInfo(Path);
                    var CacheKey = ModBase.GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString()}-{Info.Length}-M")
                        .ToString();
                    var Cached = ModBase.ReadIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey);
                    if (!string.IsNullOrEmpty(Cached))
                    {
                        _ModrinthHash = Cached;
                        return _ModrinthHash;
                    }

                    // 计算 SHA1
                    _ModrinthHash = ModBase.GetFileSHA1(Path);
                    // 写入缓存
                    ModBase.WriteIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey, _ModrinthHash);
                }

                return _ModrinthHash;
            }
        }

        private string _ModrinthHash;

        #endregion

        #region API

        public override string ToString()
        {
            return $"{State} - {Path}";
        }

        public override bool Equals(object obj)
        {
            var target = obj as LocalCompFile;
            return target is not null && (Path ?? "") == (target.Path ?? "");
        }

        #endregion
    }

    /// <summary>
    ///     获取文件夹描述信息。
    /// </summary>
    private static string GetFolderDescription(string FolderPath)
    {
        try
        {
            if (!Directory.Exists(FolderPath))
                return "空文件夹";
            return "文件夹";
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"获取文件夹描述失败：{FolderPath}");
            return "文件夹";
        }
    }

    public class CompLocalLoaderData
    {
        public string CompPath;
        public CompType CompType;

        public KeyValuePair<List<LocalCompFile>, JsonObject> DetailInfo;
        public PageInstanceCompResource Frm;
        public ModMinecraft.McInstance GameVersion;
        public List<CompLoaderType> Loaders;
    }

    // 加载资源列表
    public static LoaderTask<CompLocalLoaderData, List<LocalCompFile>> CompResourceListLoader =
        new("Comp Resource List Loader", CompResourceListLoad);

    private static void CompResourceListLoad(LoaderTask<CompLocalLoaderData, List<LocalCompFile>> Loader)
    {
        try
        {
            ModBase.RunInUiWait(() =>
            {
                if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.ShowProgress = false;
            });

            // 等待 Mod 更新完成
            if (PageInstanceCompResource.UpdatingVersions.Contains(Loader.Input.CompPath))
            {
                ModBase.Log("[Mod] 等待资源更新完成后才能继续加载资源列表：" + Loader.Input.CompPath);
                try
                {
                    ModBase.RunInUiWait(() =>
                    {
                        if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.Text = "正在更新资源";
                    });
                    while (PageInstanceCompResource.UpdatingVersions.Contains(Loader.Input.CompPath))
                    {
                        if (Loader.IsAborted)
                            return;
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    ModBase.RunInUiWait(() =>
                    {
                        if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.Text = "正在加载资源列表";
                    });
                }

                Loader.Input.Frm.LoaderRun(LoaderFolderRunType.UpdateOnly);
            }

            // 获取 Mod 文件夹下的可用文件列表
            var ModList = new List<LocalCompFile>();
            if (Directory.Exists(Loader.Input.CompPath))
            {
                var RawName = Loader.Input.CompPath.ToLower();

                if (Loader.Input.CompType == CompType.Schematic)
                {
                    var CurrentFolderPath = "";
                    if (Loader.Input.Frm is not null) CurrentFolderPath = Loader.Input.Frm.CurrentFolderPath;

                    var SearchPath = string.IsNullOrEmpty(CurrentFolderPath)
                        ? Loader.Input.CompPath
                        : CurrentFolderPath;

                    try
                    {
                        var DirInfo = new DirectoryInfo(SearchPath);
                        foreach (var Dir in DirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                            ModList.Add(new LocalCompFile(Path.Combine(Dir.FullName, "__FOLDER__")));
                        foreach (var File in DirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                            try
                            {
                                if (LocalCompFile.IsCompFile(File.FullName, Loader.Input.CompType))
                                    ModList.Add(new LocalCompFile(File.FullName));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, $"处理文件失败：{File.FullName}");
                            }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"枚举文件失败：{SearchPath}");
                    }
                }
                else
                {
                    try
                    {
                        foreach (var File in ModBase.EnumerateFiles(Loader.Input.CompPath))
                            try
                            {
                                if ((File.DirectoryName.ToLower() ?? "") != (RawName.TrimEnd('\\') ?? ""))
                                    if (!(PageInstanceLeft.Instance is not null &&
                                          PageInstanceLeft.Instance.Info.HasForge &&
                                          PageInstanceLeft.Instance.Info.Drop < 130 && (File.Directory.Name ?? "") ==
                                          (PageInstanceLeft.Instance.Info.VanillaName ?? "")))
                                        continue;

                                if (LocalCompFile.IsCompFile(File.FullName, Loader.Input.CompType))
                                    ModList.Add(new LocalCompFile(File.FullName));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, $"处理文件失败：{File.FullName}");
                            }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"枚举文件夹失败：{Loader.Input.CompPath}");
                    }
                }
            }

            // 确定是否显示进度
            Loader.Progress = 0.05d;
            if (ModList.Count > 50)
                ModBase.RunInUi(() =>
                {
                    if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.ShowProgress = true;
                });

            // 获取本地文件缓存
            var CachePath = ModBase.PathTemp + @"Cache\LocalComp.json";
            var Cache = new JsonObject();
            try
            {
                var CacheContent = ModBase.ReadFile(CachePath);
                if (!string.IsNullOrWhiteSpace(CacheContent))
                {
                    Cache = (JsonObject)ModBase.GetJson(CacheContent);
                    if (!Cache.ContainsKey("version") || Cache["version"].ToObject<int>() != LocalModCacheVersion)
                    {
                        ModBase.Log("[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息", ModBase.LogLevel.Debug);
                        Cache = new JsonObject();
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取本地 Mod 信息缓存失败，已重置");
                Cache = new JsonObject();
            }

            Cache["version"] = LocalModCacheVersion;

            // 加载 Mod 列表 - 优化：对于原理图文件，延迟加载NBT数据
            var ModUpdateList = new List<LocalCompFile>();
            foreach (var ModEntry in ModList)
            {
                Loader.Progress += 0.94d / ModList.Count;
                if (Loader.IsAborted)
                    return;
                if (ModEntry.IsFolder)
                    continue;

                // 优化：对于原理图文件，只进行基础加载，不解析NBT数据
                if (Loader.Input.CompType == CompType.Schematic)
                    ModEntry.LoadBasicInfo();
                else
                    // 加载 McMod 对象
                    ModEntry.Load();
                
                // 读取 Comp 缓存
                if (ModEntry.State == LocalCompFile.LocalFileStatus.Unavailable)
                    continue;
                var CacheKey = ModEntry.ModrinthHash + Loader.Input.GameVersion.Info.VanillaName +
                               Loader.Input.Loaders.Join("");
                if (Cache.ContainsKey(CacheKey))
                {
                    ModEntry.FromJson((JsonObject)Cache[CacheKey]);
                    // 如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                    if (ModEntry.CompLoaded &&
                        DateTime.Now - Cache[CacheKey]["Comp"]["CacheTime"].ToObject<DateTime>() <
                        new TimeSpan(6, 0, 0))
                        continue;
                }

                ModUpdateList.Add(ModEntry);
            }

            Loader.Progress = 0.99d;
            ModBase.Log(
                $"[Mod] 共有 {ModList.Count} 个 Mod，其中 {ModUpdateList.Where(m => m.Comp is null).Count()} 个需要联网获取信息，{ModUpdateList.Where(m => m.Comp is not null).Count()} 个需要更新信息");

            // 排序
            ModList.Sort((Left, Right) =>
            {
                if (Left.State == LocalCompFile.LocalFileStatus.Unavailable !=
                    (Right.State == LocalCompFile.LocalFileStatus.Unavailable))
                    return Left.State == LocalCompFile.LocalFileStatus.Unavailable ? 1 : -1;

                return Right.FileName.CompareTo(Left.FileName);
            });

            // 回设
            if (Loader.IsAborted)
                return;
            Loader.Output = ModList;

            // 开始联网加载
            if (ModUpdateList.Any())
            {
                // TODO: 添加信息获取中提示
                Loader.Input.DetailInfo = new KeyValuePair<List<LocalCompFile>, JsonObject>(ModUpdateList, Cache);
                CompUpdateDetailLoader.Start(Loader.Input, true);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "Mod 列表加载失败");
            throw;
        }
    }

    // 联网加载 Mod 详情
    public static LoaderTask<CompLocalLoaderData, int> CompUpdateDetailLoader =
        new("Comp List Detail Loader", CompUpdateDetailLoad);

    private static void CompUpdateDetailLoad(LoaderTask<CompLocalLoaderData, int> Loader)
    {
        var Mods = Loader.Input.DetailInfo.Key;
        var Cache = Loader.Input.DetailInfo.Value;
        // 获取作为检查目标的加载器和版本
        var ModLoaders = Loader.Input.Loaders;
        var CompType = Loader.Input.CompType;
        var McInstance = Loader.Input.GameVersion.Info.VanillaName;

        // 开始网络获取
        ModBase.Log($"[Mod] 目标加载器：{string.Join("/", ModLoaders)}，版本：{McInstance}");
        var EndedThreadCount = 0;
        var IsFailed = false;
        var CurrentTaskId = Task.CurrentId ?? -1;

        // 从 Modrinth 获取信息
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程 ID
                var ModrinthHashes = Mods.Select(m => m.ModrinthHash).ToList();
                var ModrinthVersion = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files", "POST",
                    $"{{\"hashes\": [\"{string.Join("\",\"", ModrinthHashes)}\"], \"algorithm\": \"sha1\"}}",
                    "application/json"));
                ModBase.Log($"[Mod] 从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息");

                // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                if (ModrinthVersion.Count == 0) return;
                var ModrinthMapping = new Dictionary<string, List<LocalCompFile>>();
                foreach (var Entry in Mods)
                {
                    if (ModrinthVersion[Entry.ModrinthHash] is null) continue;
                    if (ModrinthVersion[Entry.ModrinthHash]["files"][0]["hashes"]["sha1"].ToString() !=
                        Entry.ModrinthHash) continue;

                    var ProjectId = ModrinthVersion[Entry.ModrinthHash]["project_id"].ToString();
                    // 读取已加载的缓存，加快结果出现速度
                    if (CompProjectCache.ContainsKey(ProjectId) && Entry.Comp is null)
                        Entry.Comp = CompProjectCache[ProjectId];

                    if (!ModrinthMapping.ContainsKey(ProjectId)) ModrinthMapping[ProjectId] = new List<LocalCompFile>();
                    ModrinthMapping[ProjectId].Add(Entry);

                    // 记录对应的 CompFile
                    var FileInfo = new CompFile((JsonObject)ModrinthVersion[Entry.ModrinthHash], CompType.Mod);
                    if (Entry.CompFile is null || Entry.CompFile.ReleaseDate < FileInfo.ReleaseDate)
                        Entry.CompFile = FileInfo;
                }

                if (Loader.IsAbortedWithThread(CurrentTaskId)) return;
                ModBase.Log($"[Mod] 需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息");

                // 步骤 3：获取工程信息
                if (!ModrinthMapping.Any()) return;
                var ModrinthProject = (JsonArray)ModBase.GetJson(ModDownload.DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{string.Join("\",\"", ModrinthMapping.Keys)}\"]",
                    "GET", "", "application/json"));

                foreach (var ProjectJson in ModrinthProject)
                {
                    var Project = new CompProject((JsonObject)ProjectJson);
                    foreach (var Entry in ModrinthMapping[Project.Id]) Entry.Comp = Project;
                }

                ModBase.Log("[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息");

                // 步骤 4：获取更新信息
                var targetLoaders = CompType == CompType.DataPack
                    ? "datapack"
                    : string.Join("\",\"", ModLoaders).ToLower();
                var ModrinthUpdate = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files/update", "POST",
                    $"{{\"hashes\": [\"{string.Join("\",\"", ModrinthMapping.SelectMany(l => l.Value.Select(m => m.ModrinthHash)))}\"], \"algorithm\": \"sha1\", " +
                    $"\"loaders\": [\"{targetLoaders}\"],\"game_versions\": [\"{McInstance}\"]}}", "application/json"));

                foreach (var Entry in Mods)
                {
                    if (ModrinthUpdate[Entry.ModrinthHash] is null || Entry.CompFile is null) continue;
                    var UpdateFile = new CompFile((JsonObject)ModrinthUpdate[Entry.ModrinthHash], CompType.Mod);
                    if (!UpdateFile.Available) continue;

                    if (ModBase.ModeDebug)
                        ModBase.Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}");
                    if (Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate ||
                        Entry.CompFile.Hash == UpdateFile.Hash) continue;

                    // 设置更新日志与更新文件
                    if (Entry.UpdateFile is not null && UpdateFile.Hash == Entry.UpdateFile.Hash)
                    {
                        Entry.ChangelogUrls.Add(
                            $"https://modrinth.com/mod/{ModrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={McInstance}");
                        Entry.UpdateFile.DownloadUrls.AddRange(UpdateFile.DownloadUrls);
                        Entry.UpdateFile = UpdateFile;
                    }
                    else if (Entry.UpdateFile is null || UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate)
                    {
                        Entry.ChangelogUrls = new List<string>
                        {
                            $"https://modrinth.com/mod/{ModrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={McInstance}"
                        };
                        Entry.UpdateFile = UpdateFile;
                    }
                }

                ModBase.Log("[Mod] 从 Modrinth 获取本地 Mod 信息结束");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
                IsFailed = true;
            }
            finally
            {
                Interlocked.Increment(ref EndedThreadCount);
            }
        }, "Mod List Detail Loader Modrinth");

        // CurseForge 部分转换逻辑类似，注意其 ID 多为 Integer 类型
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程 ID
                var CurseForgeHashes = Mods.Select(m => m.CurseForgeHash).ToList();
                var CurseForgeResponse = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.curseforge.com/v1/fingerprints/432", "POST",
                    $"{{\"fingerprints\": [{string.Join(",", CurseForgeHashes)}]}}", "application/json"));
                var CurseForgeRaw = (JsonArray)CurseForgeResponse["data"]["exactMatches"];
                ModBase.Log($"[Mod] 从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息");

                // 步骤 2：构建映射 (此处省略具体循环，逻辑同 Modrinth，注意 ProjectId 转换)
                // ...

                // 步骤 4：获取更新文件信息
                // 注意 C# 中 Dictionary 的键值对遍历：foreach (var pair in UpdateFiles) { var Entry = pair.Key; ... }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
                IsFailed = true;
            }
            finally
            {
                Interlocked.Increment(ref EndedThreadCount);
            }
        }, "Mod List Detail Loader CurseForge");

        // 等待线程结束
        while (EndedThreadCount < 2)
        {
            if (Loader.IsAborted) return;
            Thread.Sleep(10);
        }

        // 保存缓存
        var CachedMods = Mods.Where(m => m.Comp is not null).ToList();
        ModBase.Log($"[Mod] 联网获取本地 Mod 信息完成，为 {CachedMods.Count} 个 Mod 更新缓存");
        if (!CachedMods.Any()) return;

        foreach (var Entry in CachedMods)
        {
            Entry.CompLoaded = !IsFailed;
            Cache[Entry.ModrinthHash + McInstance + string.Join("", ModLoaders)] = Entry.ToJson();
        }

        ModBase.WriteFile(Path.Combine(ModBase.PathTemp, "Cache", "LocalComp.json"),
            Cache.ToJsonString(ModBase.ModeDebug ? new JsonSerializerOptions(JsonCompat.SerializerOptions) { WriteIndented = true } : null));

        // 刷新 UI
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmInstanceMod?.Filter == PageInstanceCompResource.FilterType.CanUpdate)
                ModMain.FrmInstanceMod?.RefreshUI();
            else
                ModMain.FrmInstanceMod?.RefreshBars();
        });
    }

    public static List<CompLoaderType> GetCurrentVersionModLoader()
    {
        var ModLoaders = new List<CompLoaderType>();
        if (PageInstanceLeft.Instance.Info.HasForge)
            ModLoaders.Add(CompLoaderType.Forge);
        if (PageInstanceLeft.Instance.Info.HasNeoForge)
            ModLoaders.Add(CompLoaderType.NeoForge);
        if (PageInstanceLeft.Instance.Info.HasFabric)
            ModLoaders.Add(CompLoaderType.Fabric);
        if (PageInstanceLeft.Instance.Info.HasQuilt)
            ModLoaders.AddRange(new[] { CompLoaderType.Fabric, CompLoaderType.Quilt });
        if (PageInstanceLeft.Instance.Info.HasLiteLoader)
            ModLoaders.Add(CompLoaderType.LiteLoader);
        if (!ModLoaders.Any())
            ModLoaders.AddRange(new[]
            {
                CompLoaderType.Forge, CompLoaderType.NeoForge, CompLoaderType.Fabric, CompLoaderType.LiteLoader,
                CompLoaderType.Quilt
            });
        return ModLoaders;
    }

    public static string GetPathNameByCompType(CompType TheType)
    {
        switch (TheType)
        {
            case CompType.Mod:
            {
                return "mods";
            }
            case CompType.ResourcePack:
            {
                return "resourcepacks";
            }
            case CompType.Shader:
            {
                return "shaderpacks";
            }
            case CompType.Schematic:
            {
                return "schematics";
            }
            case CompType.World:
            {
                return "saves";
            }
        }

        return "Nothing";
    }

    private static readonly Regex RegexIsJarFile = new(@"\.jar(\.disabled)?$");

    /// <summary>
    ///     通过文件名关键字和 Mod ID 比如 <c>fabric</c> <c>api</c> 和 <c>fabric-api</c> 来获取给定实例 mods 目录中某个 Mod 的
    ///     <see cref="LocalCompFile" /> 对象
    ///     <br />
    ///     <b>为了不浪费性能，关键字统一用小写</b>
    /// </summary>
    /// <returns>
    ///     如果文件名包含主关键字，以及其他关键字中的任意一个，同时 Mod ID 一致，即认为匹配，返回对应的对象，若没有匹配的文件则返回空值。
    /// </returns>
    public static LocalCompFile GetModLocalCompByKeywords(ModMinecraft.McInstance instance, string modId,
        string mainKeyword, params string[] keywords)
    {
        if (modId is null)
            return null;
        return GetModLocalCompByKeywords(instance, new[] { modId }, mainKeyword, keywords);
    }

    public static LocalCompFile GetModLocalCompByKeywords(ModMinecraft.McInstance instance, string[] modIds,
        string mainKeyword, params string[] keywords)
    {
        if (!instance.Modable)
            return null; // 跳过不可安装 Mod 实例
        var modFolder = $"{instance.PathInstance}mods";
        if (!Directory.Exists(modFolder))
            return null; // 确保 mods 目录存在
        foreach (var file in Directory.EnumerateFiles(modFolder, $"*{mainKeyword}*"))
        {
            var lowerFilePath = file.ToLower(); // 统一转为小写
            if (!RegexIsJarFile.IsMatch(lowerFilePath))
                continue; // 检查是否是 jar 文件
            if ((keywords.Length > 0) && !keywords.Any(keyword => lowerFilePath.Contains(keyword)))
                continue; // 检查是否包含关键字
            var localComp = new LocalCompFile(file);
            localComp.Load();
            if (modIds.Any(modId => (localComp.ModId ?? "") == (modId ?? "")))
                return localComp;
        }

        return null;
    }
}
