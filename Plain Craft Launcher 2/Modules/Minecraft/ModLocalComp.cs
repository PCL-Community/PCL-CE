using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;
using static PCL.ModComp;
using static PCL.ModLoader;

namespace PCL;

public static class ModLocalComp
{
    private const int localModCacheVersion = 7;

    private static readonly Lazy<HashCache> _hashCache = new(() =>
        new HashCache(ModBase.pathTemp + @"Cache\HashCache.db"));

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
        public static bool IsModFile(string path)
        {
            if (path is null || !path.Contains("."))
                return false;
            path = path.ToLower();
            if (path.EndsWithF(".jar", true) || path.EndsWithF(".zip", true) || path.EndsWithF(".litemod", true) ||
                path.EndsWithF(".jar.disabled", true) || path.EndsWithF(".zip.disabled", true) ||
                path.EndsWithF(".litemod.disabled", true) || path.EndsWithF(".jar.old", true) ||
                path.EndsWithF(".zip.old", true) || path.EndsWithF(".litemod.old", true))
                return true;
            return false;
        }

        /// <summary>
        ///     检查是否为指定类型的组件文件。
        /// </summary>
        public static bool IsCompFile(string path, CompType compType)
        {
            if (path is null || !path.Contains("."))
                return false;
            path = path.ToLower();
            switch (compType)
            {
                case CompType.Mod:
                {
                    return IsModFile(path);
                }
                case CompType.ResourcePack:
                case CompType.Shader:
                {
                    return path.EndsWithF(".zip", true);
                }
                case CompType.DataPack:
                {
                    return path.EndsWithF(".zip", true) || path.EndsWithF(".zip.disabled", true);
                }
                case CompType.Schematic:
                {
                    return path.EndsWithF(".litematic", true) || path.EndsWithF(".nbt", true) ||
                           path.EndsWithF(".schematic", true) || path.EndsWithF(".schem", true);
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

            return ModBase.pathImage + "Icons/NoIcon.png";
        }

        #region Litematic 文件处理

        /// <summary>
        ///     读取 Litematic 文件的 NBT 数据。
        /// </summary>
        private void LoadLitematicNbtData()
        {
            try
            {
                ModBase.Log($"开始读取 Litematic NBT 数据：{path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                ModBase.Log(ex, "读取 Litematic NBT 数据时出错（" + path + "）");
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
                ModBase.Log($"开始读取 Schem NBT 数据：{path}", ModBase.LogLevel.Debug);

                // 使用自动检测压缩格式
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                ModBase.Log(ex, "读取 Schem NBT 数据时出错（" + path + "）");
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
                ModBase.Log($"开始读取 Schematic NBT 数据：{path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                ModBase.Log(ex, "读取 Schematic NBT 数据时出错（" + path + "）");
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
                ModBase.Log($"开始读取 NBT 结构文件数据：{path}", ModBase.LogLevel.Debug);
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                ModBase.Log(ex, "读取 NBT 结构文件数据时出错（" + path + "）");
            }
        }

        #endregion

        #region 基础

        /// <summary>
        ///     资源的文件的地址。
        /// </summary>
        public readonly string path;

        /// <summary>
        ///     是否为文件夹项。
        /// </summary>
        public bool IsFolder => path.EndsWithF(@"\__FOLDER__", true);

        /// <summary>
        ///     获取实际的文件夹路径（去除 __FOLDER__ 标记）。
        /// </summary>
        public string ActualPath
        {
            get
            {
                if (IsFolder) return path.Replace(@"\__FOLDER__", "");

                return path;
            }
        }

        public LocalCompFile(string path)
        {
            this.path = path ?? "";
        }

        /// <summary>
        ///     NBT数据是否已加载（用于延迟加载优化）。
        /// </summary>
        private bool _nbtDataLoaded;

        /// <summary>
        ///     Mod 资源的完整路径，去除最后的 .disabled 和 .old。
        /// </summary>
        public string RawPath => ModBase.GetPathFromFullPath(path) + RawFileName;

        /// <summary>
        ///     资源的完整文件名。
        /// </summary>
        public string FileName
        {
            get
            {
                if (IsFolder && !string.IsNullOrEmpty(Name)) return Name;

                return ModBase.GetFileNameFromPath(path);
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

                if (path.EndsWithF(".disabled", true) || path.EndsWithF(".old", true)) return LocalFileStatus.Disabled;

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
                        _Name = ModBase.GetFileNameWithoutExtentionFromPath(path);
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
                if (field is null)
                {
                    field = new List<string>();
                    if (IsFolder)
                    {
                        field.Add("文件夹");
                    }
                    else
                    {
                        var extension = System.IO.Path.GetExtension(RawPath).ToLower();
                        switch (extension ?? "")
                        {
                            case ".litematic":
                            {
                                field.Add("原理图");
                                break;
                            }
                            case ".schem":
                            case ".schematic":
                            {
                                field.Add("Schematic结构");
                                break;
                            }
                            case ".nbt":
                            {
                                field.Add("原版结构");
                                break;
                            }
                        }
                    }
                }

                return field;
            }
        }

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
                if (!possibleModId.Contains(value))
                    possibleModId.Add(value);
                if (_ModId is null)
                    _ModId = value;
            }
        }

        private string _ModId;

        /// <summary>
        ///     其他可能的 ModID。
        /// </summary>
        public List<string> possibleModId = new();

        /// <summary>
        ///     Mod 的主页。
        /// </summary>
        public string Url
        {
            get
            {
                if (field is null)
                    Load();
                return field;
            }
            set
            {
                if (field is null && value is not null && value.StartsWithF("http")) field = value;
            }
        }

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

        private void AddDependency(string modID, string versionRequirement = null)
        {
            // 确保信息正确
            if (modID is null || modID.Length < 2)
                return;
            modID = modID.ToLower();
            if (modID == "name" || (ModBase.Val(modID).ToString() ?? "") == (modID ?? ""))
                return; // 跳过 name 与纯数字 id
            if (versionRequirement is null ||
                (!versionRequirement.Contains(".") && !versionRequirement.Contains("-")) ||
                versionRequirement.Contains("$"))
                versionRequirement = null;
            else if (!versionRequirement.StartsWithF("[") && !versionRequirement.StartsWithF("(") &&
                     !versionRequirement.EndsWithF("]") && !versionRequirement.EndsWithF(")"))
                versionRequirement = "[" + versionRequirement + ",)";
            // 向依赖项中添加
            if (_Dependencies.ContainsKey(modID))
            {
                if (_Dependencies[modID] is null)
                    _Dependencies[modID] = versionRequirement;
            }
            else
            {
                _Dependencies.Add(modID, versionRequirement);
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
        private bool isLoaded;

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
        private bool isInfoWithoutClassAvailable = false;

        // 3. 尝试从 .class 文件中获取信息
        // 成功：标记 IsInfoWithClassAvailable。
        // 失败：什么也不干。
        /// <summary>
        ///     是否已进行 .class 文件的信息获取。
        /// </summary>
        private bool isInfoWithClassLoaded;

        /// <summary>
        ///     是否已在 .class 文件中完成了所需信息的加载。
        /// </summary>
        private bool isInfoWithClassAvailable;

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
            possibleModId = new List<string>();
            _Dependencies = new Dictionary<string, string>();
            isLoaded = false;
            _FileUnavailableReason = null;
            isInfoWithClassLoaded = false;
            isInfoWithClassAvailable = false;
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
                    isLoaded = true;
                    return;
                }

                if (!File.Exists(path))
                {
                    _FileUnavailableReason = new FileNotFoundException("未找到资源文件（" + path + ")");
                    isLoaded = true;
                    return;
                }

                // 对于原理图文件，只设置基本状态，不解析NBT数据
                if (path.EndsWithF(".litematic", true) || path.EndsWithF(".nbt", true) ||
                    path.EndsWithF(".schem", true) || path.EndsWithF(".schematic", true))
                {
                    _Name = ModBase.GetFileNameWithoutExtentionFromPath(path);
                    isLoaded = true;
                    return;
                }

                // 对于其他文件类型，正常加载
                Load();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"加载基本信息失败：{path}");
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
                if (path.EndsWithF(".litematic", true))
                    LoadLitematicNbtData();
                else if (path.EndsWithF(".nbt", true))
                    LoadStructureNbtData();
                else if (path.EndsWithF(".schem", true))
                    LoadSchemNbtData();
                else if (path.EndsWithF(".schematic", true)) LoadSchematicNbtData();

                _nbtDataLoaded = true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"延迟加载NBT数据失败：{path}");
            }
        }

        /// <summary>
        ///     进行文件可用性检查与 .class 以外的信息获取。
        /// </summary>
        public void Load(bool forceReload = false)
        {
            if (isLoaded && !forceReload)
                return;
            // 初始化
            Init();

            // 基础可用性检查
            if (path.Length < 2)
            {
                _FileUnavailableReason = new FileNotFoundException("错误的资源文件路径（" + (path ?? "null") + "）");
                isLoaded = true;
                return;
            }

            // 对于文件夹项，检查实际文件夹路径是否存在
            if (IsFolder)
            {
                if (!Directory.Exists(ActualPath))
                {
                    _FileUnavailableReason = new DirectoryNotFoundException("未找到文件夹（" + ActualPath + "）");
                    isLoaded = true;
                    return;
                }

                // 文件夹项不需要进一步处理
                isLoaded = true;
                return;
            }

            if (!File.Exists(path))
            {
                _FileUnavailableReason = new FileNotFoundException("未找到资源文件（" + path + "）");
                isLoaded = true;
                return;
            }

            // 对于投影文件，跳过 zip 解析
            if (path.EndsWithF(".litematic", true) || path.EndsWithF(".nbt", true) || path.EndsWithF(".schem", true) ||
                path.EndsWithF(".schematic", true))
            {
                try
                {
                    _Name = ModBase.GetFileNameWithoutExtentionFromPath(path);
                    // 根据文件类型加载数据
                    if (path.EndsWithF(".litematic", true))
                    {
                        LoadLitematicNbtData();
                    }
                    else if (path.EndsWithF(".schem", true) || path.EndsWithF(".schematic", true))
                    {
                        if (path.EndsWithF(".schem", true))
                            LoadSchemNbtData();
                        else
                            LoadSchematicNbtData();
                    }
                    else if (path.EndsWithF(".nbt", true))
                    {
                        LoadStructureNbtData();
                    }

                    _nbtDataLoaded = true;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "投影文件信息获取失败（" + path + "）", ModBase.LogLevel.Developer);
                    _FileUnavailableReason = ex;
                }

                isLoaded = true;
                return;
            }

            // 对于其他文件，尝试作为 Jar 文件打开
            ZipArchive jar = null;
            try
            {
                jar = new ZipArchive(new FileStream(path, FileMode.Open));
                // 信息获取
                LookupMetadata(jar);
            }
            catch (UnauthorizedAccessException ex)
            {
                ModBase.Log(ex, "资源文件由于无权限无法打开（" + path + "）", ModBase.LogLevel.Developer);
                _FileUnavailableReason = new UnauthorizedAccessException("没有读取此文件的权限，请尝试右键以管理员身份运行 PCL", ex);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "资源文件无法打开（" + path + "）", ModBase.LogLevel.Developer);
                _FileUnavailableReason = ex;
            }
            finally
            {
                if (jar is not null)
                    jar.Dispose();
            }

            // 完成标记
            isLoaded = true;
        }

        /// <summary>
        ///     从 Jar 文件中获取 Mod 信息。
        /// </summary>
        private void LookupMetadata(ZipArchive jar)
        {
            #region 尝试使用 mcmod.info

            do
            {
                try
                {
                    // 获取信息文件
                    var infoEntry = jar.GetEntry("mcmod.info");
                    string infoString = null;
                    if (infoEntry is not null)
                    {
                        infoString = ModBase.ReadFile(infoEntry.Open());
                        if (infoString.Length < 15)
                            infoString = null;
                    }

                    if (infoString is null)
                        break;
                    // 获取可用 Json 项
                    JsonObject infoObject;
                    var jsonObject = (JsonNode)ModBase.GetJson(infoString);
                    if (jsonObject.GetValueKind() == JsonValueKind.Array)
                        infoObject = (JsonObject)jsonObject[0];
                    else
                        infoObject = (JsonObject)jsonObject["modList"][0];
                    // 从文件中获取 Mod 信息项
                    Name = (string)infoObject["name"];
                    Description = (string)infoObject["description"];
                    Version = (string)infoObject["version"];
                    Url = (string)infoObject["url"];
                    ModId = (string)infoObject["modid"];
                    var authorJson = (JsonArray)infoObject["authorList"];
                    if (authorJson is not null)
                    {
                        var author = new List<string>();
                        foreach (var Token in authorJson)
                            author.Add(Token.ToString());
                        if (author.Any())
                            Authors = author.Join(", ");
                    }

                    var logoFile = (string)infoObject["logoFile"];
                    if (logoFile is not null)
                    {
                        var logoItem = jar.GetEntry(logoFile);
                        if (logoItem is not null)
                        {
                            var md5 = ModBase.GetStringMD5(logoItem.Length + logoItem.CompressedLength + path);
                            Logo = System.IO.Path.Combine(ModBase.pathTemp, "Cache", "Images", $"{md5}.png");
                            using (var entryStream = logoItem.Open())
                            {
                                ModBase.WriteFile(Logo, entryStream);
                            }
                        }
                    }

                    var reqs = (JsonArray)infoObject["requiredMods"];
                    if (reqs is not null)
                        foreach (string item in reqs) // 将迭代变量重命名为 item
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

                    reqs = (JsonArray)infoObject["dependencies"];
                    if (reqs is not null)
                        foreach (string rawToken in reqs)
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
                    ModBase.Log(ex, "读取 mcmod.info 时出现未知错误（" + path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 fabric.mod.json

            do
            {
                try
                {
                    var fabricEntry = jar.GetEntry("fabric.mod.json");
                    string fabricText = null;
                    if (fabricEntry is not null)
                    {
                        fabricText = ModBase.ReadFile(fabricEntry.Open(), Encoding.UTF8);
                        if (!fabricText.Contains("schemaVersion")) fabricText = null;
                    }

                    if (fabricText is null) break;

                    var fabricObject = (JsonObject)ModBase.GetJson(fabricText);

                    if (fabricObject.ContainsKey("name")) Name = fabricObject["name"].ToString();
                    if (fabricObject.ContainsKey("version")) Version = fabricObject["version"].ToString();
                    if (fabricObject.ContainsKey("description")) Description = fabricObject["description"].ToString();
                    if (fabricObject.ContainsKey("id")) ModId = fabricObject["id"].ToString();
                    if (fabricObject.ContainsKey("contact") && fabricObject["contact"]["homepage"] is not null)
                        Url = fabricObject["contact"]["homepage"].ToString();

                    var authorJson = (JsonArray)fabricObject["authors"];
                    if (authorJson is not null)
                    {
                        var authorList = authorJson.Select(t => t.ToString()).ToList();
                        if (authorList.Any()) Authors = string.Join(", ", authorList);
                    }

                    if (fabricObject.ContainsKey("icon"))
                    {
                        var logoFile = fabricObject["icon"].ToString();
                        var logoItem = jar.GetEntry(logoFile);
                        if (logoItem is not null)
                        {
                            var md5 = ModBase.GetStringMD5(logoItem.Length + logoItem.CompressedLength + path);
                            Logo = System.IO.Path.Combine(ModBase.pathTemp, "Cache", "Images", $"{md5}.png");
                            using (var entryStream = logoItem.Open())
                            {
                                ModBase.WriteFile(Logo, entryStream);
                            }
                        }
                    }

                    // 依赖处理 (省略了 VB 中的注释部分，按逻辑实现)
                    if (fabricObject.ContainsKey("depends"))
                        foreach (var dep in (JsonObject)fabricObject["depends"])
                            AddDependency(dep.Key, dep.Value.ToString());
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 fabric.mod.json 时出错（" + path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 quilt.mod.json

            do
            {
                try
                {
                    // 获取 quilt.mod.json 文件
                    var quiltEntry = jar.GetEntry("quilt.mod.json");
                    string quiltText = null;
                    if (quiltEntry is not null)
                    {
                        quiltText = ModBase.ReadFile(quiltEntry.Open(), Encoding.UTF8);
                        if (!quiltText.Contains("schema_version"))
                            quiltText = null;
                    }

                    if (quiltText is null)
                        break;
                    var quiltObject = (JsonObject)((JsonObject)ModBase.GetJson(quiltText))["quilt_loader"];
                    // 从文件中获取 Mod 信息项
                    if (quiltObject.ContainsKey("id"))
                        ModId = (string)quiltObject["id"];
                    if (quiltObject.ContainsKey("version"))
                        Version = (string)quiltObject["version"];
                    if (quiltObject.ContainsKey("metadata"))
                    {
                        var quiltMetadata = (JsonObject)quiltObject["metadata"];
                        if (quiltMetadata.ContainsKey("name"))
                            Name = (string)quiltMetadata["name"];
                        if (quiltMetadata.ContainsKey("description"))
                            Description = (string)quiltMetadata["description"];
                        if (quiltMetadata.ContainsKey("contact"))
                            Url = (string)(quiltMetadata["contact"]["homepage"] ?? "");
                    }

                    if (quiltObject.ContainsKey("icon"))
                    {
                        var logoFile = (string)quiltObject["icon"];
                        if (logoFile is not null)
                        {
                            var logoItem = jar.GetEntry(logoFile);
                            if (logoItem is not null)
                            {
                                var md5 = ModBase.GetStringMD5(logoItem.Length + logoItem.CompressedLength + path);
                                Logo = System.IO.Path.Combine(ModBase.pathTemp, "Cache", "Images", $"{md5}.png");
                                using (var entryStream = logoItem.Open())
                                {
                                    ModBase.WriteFile(Logo, entryStream);
                                }
                            }
                        }
                    }

                    goto Finished;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取 quilt.mod.json 时出现未知错误（" + path + "）", ModBase.LogLevel.Developer);
                }
            } while (false);

            #endregion

            #region 尝试使用 mods.toml

            try
            {
                // 获取 mods.toml 文件
                var tomlEntry = jar.GetEntry("META-INF/mods.toml");
                string tomlText = null;
                if (tomlEntry is not null)
                {
                    using (var reader = new StreamReader(tomlEntry.Open()))
                    {
                        tomlText = reader.ReadToEnd();
                    }

                    if (tomlText.Length < 15) tomlText = null;
                }

                if (tomlText is not null)
                {
                    // 文件标准化：统一换行符为 \n，去除注释、头尾的空格、空行
                    var lines = new List<string>();
                    var rawLines = tomlText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

                    foreach (var rawLine in rawLines)
                    {
                        var line = rawLine;
                        if (line.StartsWithF("#")) continue; // 去除注释
                        if (line.Contains("#")) line = line.Substring(0, line.IndexOfF("#"));
                        // 去除头尾的空格（包含全角空格）
                        line = line.Trim(' ', '\t', '　');
                        if (!string.IsNullOrEmpty(line)) lines.Add(line);
                    }

                    // 读取文件数据
                    // TomlData 存储段落名及其对应的键值对
                    var tomlData = new List<KeyValuePair<string, Dictionary<string, object>>>
                    {
                        new("", new Dictionary<string, object>())
                    };

                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (line.StartsWithF("[") && line.EndsWithF("]"))
                        {
                            // 段落标记
                            var header = line.Trim('[', ']');
                            tomlData.Add(
                                new KeyValuePair<string, Dictionary<string, object>>(header,
                                    new Dictionary<string, object>()));
                        }
                        else if (line.Contains("="))
                        {
                            // 字段标记
                            var key = line.Substring(0, line.IndexOfF("=")).TrimEnd(' ', '\t', '　');
                            var rawValue = line.Substring(line.IndexOfF("=") + 1).TrimStart(' ', '\t', '　');
                            object value;

                            if (rawValue.StartsWithF("\"") && rawValue.EndsWithF("\""))
                            {
                                // 单行字符串
                                value = rawValue.Trim('\"');
                            }
                            else if (rawValue.StartsWithF("'''"))
                            {
                                // 多行字符串
                                var valueLines = new List<string> { rawValue.Replace("'''", "") };
                                if (!rawValue.EndsWithF("'''") || rawValue.Length == 3)
                                    while (i < lines.Count - 1)
                                    {
                                        i++;
                                        var valueLine = lines[i];
                                        if (valueLine.EndsWithF("'''"))
                                        {
                                            valueLines.Add(valueLine.Replace("'''", ""));
                                            break;
                                        }

                                        valueLines.Add(valueLine);
                                    }

                                value = string.Join("\n", valueLines).Trim('\n').Replace("\n", "\r\n");
                            }
                            else if (rawValue.ToLower() == "true" || rawValue.ToLower() == "false")
                            {
                                // 布尔型
                                value = rawValue.ToLower() == "true";
                            }
                            else if (double.TryParse(rawValue, out var num))
                            {
                                // 数字型 (模拟 VB 的 Val)
                                value = num;
                            }
                            else
                            {
                                // 默认当做字符串存储
                                value = rawValue;
                            }

                            // 将值存入当前最后的段落中
                            var lastPair = tomlData[tomlData.Count - 1];
                            lastPair.Value[key] = value;
                        }
                    }

                    // 从解析出的数据中提取 Mod 信息
                    Dictionary<string, object> modEntry = null;
                    foreach (var subData in tomlData)
                        if (subData.Key == "mods")
                        {
                            modEntry = subData.Value;
                            break;
                        }

                    if (modEntry is not null && modEntry.ContainsKey("modId"))
                    {
                        ModId = modEntry["modId"].ToString();
                        // 假设 _ModId 是内部属性，如果为 null 说明设置失败
                        if (_ModId is not null)
                        {
                            if (modEntry.ContainsKey("displayName")) Name = modEntry["displayName"].ToString();
                            if (modEntry.ContainsKey("description")) Description = modEntry["description"].ToString();
                            if (modEntry.ContainsKey("version")) Version = modEntry["version"].ToString();

                            // [0] 是全局段落（无 Header）
                            if (tomlData[0].Value.ContainsKey("displayURL"))
                                Url = tomlData[0].Value["displayURL"].ToString();
                            if (tomlData[0].Value.ContainsKey("authors"))
                                Authors = tomlData[0].Value["authors"].ToString();

                            // 读取依赖
                            foreach (var subData in tomlData)
                                if (subData.Key.ToLower() == $"dependencies.{ModId.ToLower()}")
                                {
                                    var depEntry = subData.Value;
                                    if (depEntry.ContainsKey("modId") &&
                                        depEntry.ContainsKey("mandatory") && (bool)depEntry["mandatory"] &&
                                        depEntry.ContainsKey("side") &&
                                        depEntry["side"].ToString().ToLower() != "server")
                                        AddDependency(
                                            depEntry["modId"].ToString(),
                                            depEntry.ContainsKey("versionRange")
                                                ? depEntry["versionRange"].ToString()
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
                ModBase.Log(ex, "读取 mods.toml 时出现未知错误（" + path + "）", ModBase.LogLevel.Developer);
            }

            #endregion

            #region 尝试使用 fml_cache_annotation.json

            do
            {
                try
                {
                    // 获取 fml_cache_annotation.json 文件
                    var fmlEntry = jar.GetEntry("META-INF/fml_cache_annotation.json");
                    string fmlText = null;
                    if (fmlEntry is not null)
                    {
                        fmlText = ModBase.ReadFile(fmlEntry.Open(), Encoding.UTF8);
                        if (!fmlText.Contains("Lnet/minecraftforge/fml/common/Mod;"))
                            fmlText = null;
                    }

                    if (fmlText is null)
                        break;
                    var fmlJson = (JsonObject)ModBase.GetJson(fmlText);
                    // 获取可用 Json 项
                    JsonObject fmlObject = null;
                    foreach (var ModFilePair in fmlJson)
                    {
                        var modFileAnnos = (JsonArray)ModFilePair.Value["annotations"];
                        if (modFileAnnos is not null)
                            // 先获取 Mod
                            foreach (var ModFileAnno in modFileAnnos)
                            {
                                var name = (string)(ModFileAnno["name"] ?? "");
                                if (name == "Lnet/minecraftforge/fml/common/Mod;")
                                {
                                    fmlObject = (JsonObject)ModFileAnno["values"];
                                    goto Got;
                                }
                            }
                    }

                    break;
                    Got: ;

                    // 从文件中获取 Mod 信息项
                    if (fmlObject.ContainsKey("useMetadata") &&
                        (fmlObject["useMetadata"]["value"] ?? "").ToString().ToLower() == "true")
                    {
                        // 要求使用 mcmod.info 中的信息
                        var value = (string)fmlObject["modid"]["value"];
                        if (value is null)
                            break;
                        value = value.ToLower().RegexSeek(RegexPatterns.ModIdMatch);
                        if (value is not null && value.ToLower() != "name" && value.Length > 1 &&
                            (ModBase.Val(value).ToString() ?? "") != (value ?? ""))
                            if (!possibleModId.Contains(value))
                                possibleModId.Add(value);
                        break;
                    }

                    if (fmlObject.ContainsKey("name"))
                        Name = (string)fmlObject["name"]["value"];
                    if (fmlObject.ContainsKey("version"))
                        Version = (string)fmlObject["version"]["value"];
                    if (fmlObject.ContainsKey("modid"))
                        ModId = (string)fmlObject["modid"]["value"];
                    if (!fmlObject.ContainsKey("serverSideOnly") ||
                        !fmlObject["serverSideOnly"]["value"].ToObject<bool>())
                    {
                        // 添加 Minecraft 依赖
                        var depMinecraft = (string)((fmlObject["acceptedMinecraftVersions"] is not null
                            ? fmlObject["acceptedMinecraftVersions"]["value"]
                            : "") ?? "");
                        if (!string.IsNullOrEmpty(depMinecraft))
                            AddDependency("minecraft", depMinecraft);
                        // 添加其他依赖
                        var deps = (string)((fmlObject["dependencies"] is not null
                            ? fmlObject["dependencies"]["value"]
                            : "") ?? "");
                        if (!string.IsNullOrEmpty(deps))
                            foreach (var item in deps.Split(";"))
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
                    ModBase.Log(ex, "读取 fml_cache_annotation.json 时出现未知错误（" + path + "）");
                }
            } while (false);

            #endregion

            #region 尝试识别资源包图标

            try
            {
                // 检查并提取资源包的 pack.png 图标
                var packPngEntry = jar.GetEntry("pack.png");
                if (packPngEntry is not null)
                    try
                    {
                        var md5 = ModBase.GetStringMD5(packPngEntry.Length + packPngEntry.CompressedLength + path);
                        Logo = System.IO.Path.Combine(ModBase.pathTemp, "Cache", "Images", $"{md5}.png");
                        using (var entryStream = packPngEntry.Open())
                        {
                            ModBase.WriteFile(Logo, entryStream);
                        }

                        ModBase.Log("成功提取资源包图标：" + path, ModBase.LogLevel.Debug);
                    }
                    catch (Exception logoEx)
                    {
                        ModBase.Log(logoEx, "提取 pack.png 图标失败（" + path + "）", ModBase.LogLevel.Developer);
                    }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "识别资源包图标时出现未知错误（" + path + "）", ModBase.LogLevel.Developer);
            }

            #endregion

            Finished: ;

            #region 将 Version 代号转换为 META-INF 中的版本

            if (_Version == "version")
                try
                {
                    var metaEntry = jar.GetEntry("META-INF/MANIFEST.MF");
                    if (metaEntry is not null)
                    {
                        var metaString = ModBase.ReadFile(metaEntry.Open()).Replace(" :", ":").Replace(": ", ":");
                        if (metaString.Contains("Implementation-Version:"))
                        {
                            metaString = metaString.Substring(metaString.IndexOfF("Implementation-Version:") +
                                                              "Implementation-Version:".Count());
                            metaString = metaString.Substring(0, metaString.IndexOfAny("\r\n".ToCharArray()))
                                .Trim();
                            Version = metaString;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log("获取 META-INF 中的版本信息失败（" + path + "）", ModBase.LogLevel.Developer);
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
            get => field;
            set
            {
                field = value;
                OnCompUpdate?.Invoke(this);
            }
        }

        /// <summary>
        ///     本地文件对应的联网文件信息。
        /// </summary>
        public CompFile compFile;

        /// <summary>
        ///     该 Mod 对应的联网最新版本。
        /// </summary>
        public CompFile UpdateFile
        {
            get => field;
            set
            {
                field = value;
                OnCompUpdate?.Invoke(this);
            }
        }

        /// <summary>
        ///     该 Mod 的更新日志网址。
        /// </summary>
        public List<string> changelogUrls = new();

        /// <summary>
        ///     所有网络信息是否已成功加载。
        /// </summary>
        public bool compLoaded;

        /// <summary>
        ///     将网络信息保存为 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject();
            if (Comp is not null)
                json.Add("Comp", Comp.ToJson());
            json.Add("ChangelogUrls", new JsonArray(changelogUrls.Select(s => (JsonNode)s).ToArray()));
            json.Add("CompLoaded", compLoaded);
            if (compFile is not null)
                json.Add("CompFile", compFile.ToJson());
            if (UpdateFile is not null)
                json.Add("UpdateFile", UpdateFile.ToJson());
            return json;
        }

        /// <summary>
        ///     从 Json 中读取网络信息。
        /// </summary>
        public void FromJson(JsonObject json)
        {
            compLoaded = (bool)json["CompLoaded"];
            if (json.ContainsKey("Comp"))
                Comp = new CompProject((JsonObject)json["Comp"]);
            if (json.ContainsKey("ChangelogUrls"))
                changelogUrls = json["ChangelogUrls"].ToObject<List<string>>();
            if (json.ContainsKey("CompFile"))
                compFile = new CompFile((JsonObject)json["CompFile"], CompType.Mod);
            if (json.ContainsKey("UpdateFile"))
                UpdateFile = new CompFile((JsonObject)json["UpdateFile"], CompType.Mod);
        }

        /// <summary>
        ///     该文件是否可以更新。
        /// </summary>
        public bool CanUpdate => !Config.Preference.Hide.FunctionModUpdate && changelogUrls.Any();

        /// <summary>
        ///     获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
        /// </summary>
        public uint CurseForgeHash
        {
            get
            {
                if (_CurseForgeHash is null)
                {
                    var buf = _hashCache.Value
                        .GetMurmurHash2Async(path)
                        .GetAwaiter().GetResult()
                        .HexToBytes();
                    _CurseForgeHash = BitConverter.ToUInt32(buf);
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
                if (field is null)
                    field = _hashCache.Value.GetSHA1Async(path).GetAwaiter().GetResult();

                return field;
            }
        }

        #endregion

        #region API

        public override string ToString()
        {
            return $"{State} - {path}";
        }

        public override bool Equals(object obj)
        {
            var target = obj as LocalCompFile;
            return target is not null && (path ?? "") == (target.path ?? "");
        }

        #endregion
    }

    /// <summary>
    ///     获取文件夹描述信息。
    /// </summary>
    private static string GetFolderDescription(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                return "空文件夹";
            return "文件夹";
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"获取文件夹描述失败：{folderPath}");
            return "文件夹";
        }
    }

    public class CompLocalLoaderData
    {
        public string compPath;
        public CompType compType;

        public KeyValuePair<List<LocalCompFile>, JsonObject> detailInfo;
        public PageInstanceCompResource frm;
        public McInstance gameVersion;
        public List<CompLoaderType> loaders;
    }

    // 加载资源列表
    public static LoaderTask<CompLocalLoaderData, List<LocalCompFile>> compResourceListLoader =
        new("Comp Resource List Loader", CompResourceListLoad);

    private static void CompResourceListLoad(LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader)
    {
        try
        {
            ModBase.RunInUiWait(() =>
            {
                if (loader.input.frm is not null) loader.input.frm.Load.ShowProgress = false;
            });

            // 等待 Mod 更新完成
            if (PageInstanceCompResource.updatingVersions.Contains(loader.input.compPath))
            {
                ModBase.Log("[Mod] 等待资源更新完成后才能继续加载资源列表：" + loader.input.compPath);
                try
                {
                    ModBase.RunInUiWait(() =>
                    {
                        if (loader.input.frm is not null)
                            loader.input.frm.Load.Text = Lang.Text("Instance.Resource.Update.WaitingForUpdate");
                    });
                    while (PageInstanceCompResource.updatingVersions.Contains(loader.input.compPath))
                    {
                        if (loader.IsAborted)
                            return;
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    ModBase.RunInUiWait(() =>
                    {
                        if (loader.input.frm is not null)
                            loader.input.frm.Load.Text = Lang.Text("Instance.Resource.List.Loading");
                    });
                }

                loader.input.frm.LoaderRun(LoaderFolderRunType.UpdateOnly);
            }

            // 获取 Mod 文件夹下的可用文件列表
            var modList = new List<LocalCompFile>();
            if (Directory.Exists(loader.input.compPath))
            {
                var rawName = loader.input.compPath.ToLower();

                if (loader.input.compType == CompType.Schematic)
                {
                    var currentFolderPath = "";
                    if (loader.input.frm is not null) currentFolderPath = loader.input.frm.CurrentFolderPath;

                    var searchPath = string.IsNullOrEmpty(currentFolderPath)
                        ? loader.input.compPath
                        : currentFolderPath;

                    try
                    {
                        var dirInfo = new DirectoryInfo(searchPath);
                        foreach (var Dir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                            modList.Add(new LocalCompFile(Path.Combine(Dir.FullName, "__FOLDER__")));
                        foreach (var File in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                            try
                            {
                                if (LocalCompFile.IsCompFile(File.FullName, loader.input.compType))
                                    modList.Add(new LocalCompFile(File.FullName));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, $"处理文件失败：{File.FullName}");
                            }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"枚举文件失败：{searchPath}");
                    }
                }
                else
                {
                    try
                    {
                        foreach (var File in ModBase.EnumerateFiles(loader.input.compPath))
                            try
                            {
                                if ((File.DirectoryName.ToLower() ?? "") != (rawName.TrimEnd('\\') ?? ""))
                                    if (!(PageInstanceLeft.McInstance is not null &&
                                          PageInstanceLeft.McInstance.Info.HasForge &&
                                          PageInstanceLeft.McInstance.Info.Drop < 130 && (File.Directory.Name ?? "") ==
                                          (PageInstanceLeft.McInstance.Info.VanillaName ?? "")))
                                        continue;

                                if (LocalCompFile.IsCompFile(File.FullName, loader.input.compType))
                                    modList.Add(new LocalCompFile(File.FullName));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, $"处理文件失败：{File.FullName}");
                            }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"枚举文件夹失败：{loader.input.compPath}");
                    }
                }
            }

            // 确定是否显示进度
            loader.Progress = 0.05d;
            if (modList.Count > 50)
                ModBase.RunInUi(() =>
                {
                    if (loader.input.frm is not null) loader.input.frm.Load.ShowProgress = true;
                });

            // 获取本地文件缓存
            var cachePath = ModBase.pathTemp + @"Cache\LocalComp.json";
            var cache = new JsonObject();
            try
            {
                var cacheContent = ModBase.ReadFile(cachePath);
                if (!string.IsNullOrWhiteSpace(cacheContent))
                {
                    cache = (JsonObject)ModBase.GetJson(cacheContent);
                    if (!cache.ContainsKey("version") || cache["version"].ToObject<int>() != localModCacheVersion)
                    {
                        ModBase.Log("[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息", ModBase.LogLevel.Debug);
                        cache = new JsonObject();
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取本地 Mod 信息缓存失败，已重置");
                cache = new JsonObject();
            }

            cache["version"] = localModCacheVersion;

            // 加载 Mod 列表 - 优化：对于原理图文件，延迟加载NBT数据
            var modUpdateList = new List<LocalCompFile>();
            foreach (var ModEntry in modList)
            {
                loader.Progress += 0.94d / modList.Count;
                if (loader.IsAborted)
                    return;
                if (ModEntry.IsFolder)
                    continue;

                // 优化：对于原理图文件，只进行基础加载，不解析NBT数据
                if (loader.input.compType == CompType.Schematic)
                    ModEntry.LoadBasicInfo();
                else
                    // 加载 McMod 对象
                    ModEntry.Load();
                
                // 读取 Comp 缓存
                if (ModEntry.State == LocalCompFile.LocalFileStatus.Unavailable)
                    continue;
                var cacheKey = ModEntry.ModrinthHash + loader.input.gameVersion.Info.VanillaName +
                               loader.input.loaders.Join("");
                if (cache.ContainsKey(cacheKey))
                {
                    ModEntry.FromJson((JsonObject)cache[cacheKey]);
                    // 如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                    if (ModEntry.compLoaded &&
                        DateTime.Now - cache[cacheKey]["Comp"]["CacheTime"].ToObject<DateTime>() <
                        new TimeSpan(6, 0, 0))
                        continue;
                }

                modUpdateList.Add(ModEntry);
            }

            loader.Progress = 0.99d;
            ModBase.Log(
                $"[Mod] 共有 {modList.Count} 个 Mod，其中 {modUpdateList.Where(m => m.Comp is null).Count()} 个需要联网获取信息，{modUpdateList.Where(m => m.Comp is not null).Count()} 个需要更新信息");

            // 排序
            modList.Sort((left, right) =>
            {
                if (left.State == LocalCompFile.LocalFileStatus.Unavailable !=
                    (right.State == LocalCompFile.LocalFileStatus.Unavailable))
                    return left.State == LocalCompFile.LocalFileStatus.Unavailable ? 1 : -1;

                return right.FileName.CompareTo(left.FileName);
            });

            // 回设
            if (loader.IsAborted)
                return;
            loader.output = modList;

            // 开始联网加载
            if (modUpdateList.Any())
            {
                // TODO: 添加信息获取中提示
                loader.input.detailInfo = new KeyValuePair<List<LocalCompFile>, JsonObject>(modUpdateList, cache);
                compUpdateDetailLoader.Start(loader.input, true);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "Mod 列表加载失败");
            throw;
        }
    }

    // 联网加载 Mod 详情
    public static LoaderTask<CompLocalLoaderData, int> compUpdateDetailLoader =
        new("Comp List Detail Loader", CompUpdateDetailLoad);

    private static void CompUpdateDetailLoad(LoaderTask<CompLocalLoaderData, int> loader)
    {
        var mods = loader.input.detailInfo.Key;
        var cache = loader.input.detailInfo.Value;
        // 获取作为检查目标的加载器和版本
        var modLoaders = loader.input.loaders;
        var compType = loader.input.compType;
        var mcInstance = loader.input.gameVersion.Info.VanillaName;

        // 开始网络获取
        ModBase.Log($"[Mod] 目标加载器：{string.Join("/", modLoaders)}，版本：{mcInstance}");
        var endedThreadCount = 0;
        var isFailed = false;
        var currentTaskId = Task.CurrentId ?? -1;

        // 从 Modrinth 获取信息
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程 ID
                var modrinthHashes = mods.Select(m => m.ModrinthHash).ToList();
                var modrinthVersion = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files", "POST",
                    $"{{\"hashes\": [\"{string.Join("\",\"", modrinthHashes)}\"], \"algorithm\": \"sha1\"}}",
                    "application/json"));
                ModBase.Log($"[Mod] 从 Modrinth 获取到 {modrinthVersion.Count} 个本地 Mod 的对应信息");

                // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                if (modrinthVersion.Count == 0) return;
                var modrinthMapping = new Dictionary<string, List<LocalCompFile>>();
                foreach (var Entry in mods)
                {
                    if (modrinthVersion[Entry.ModrinthHash] is null) continue;
                    if (modrinthVersion[Entry.ModrinthHash]["files"][0]["hashes"]["sha1"].ToString() !=
                        Entry.ModrinthHash) continue;

                    var projectId = modrinthVersion[Entry.ModrinthHash]["project_id"].ToString();
                    // 读取已加载的缓存，加快结果出现速度
                    if (compProjectCache.ContainsKey(projectId) && Entry.Comp is null)
                        Entry.Comp = compProjectCache[projectId];

                    if (!modrinthMapping.ContainsKey(projectId)) modrinthMapping[projectId] = new List<LocalCompFile>();
                    modrinthMapping[projectId].Add(Entry);

                    // 记录对应的 CompFile
                    var fileInfo = new CompFile((JsonObject)modrinthVersion[Entry.ModrinthHash], CompType.Mod);
                    if (Entry.compFile is null || Entry.compFile.ReleaseDate < fileInfo.ReleaseDate)
                        Entry.compFile = fileInfo;
                }

                if (loader.IsAbortedWithThread(currentTaskId)) return;
                ModBase.Log($"[Mod] 需要从 Modrinth 获取 {modrinthMapping.Count} 个本地 Mod 的工程信息");

                // 步骤 3：获取工程信息
                if (!modrinthMapping.Any()) return;
                var modrinthProject = (JsonArray)ModBase.GetJson(ModDownload.DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{string.Join("\",\"", modrinthMapping.Keys)}\"]",
                    "GET", "", "application/json"));

                foreach (var ProjectJson in modrinthProject)
                {
                    var project = new CompProject((JsonObject)ProjectJson);
                    foreach (var Entry in modrinthMapping[project.Id]) Entry.Comp = project;
                }

                ModBase.Log("[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息");

                // 步骤 4：获取更新信息
                var targetLoaders = compType == CompType.DataPack
                    ? "datapack"
                    : string.Join("\",\"", modLoaders).ToLower();
                var modrinthUpdate = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.modrinth.com/v2/version_files/update", "POST",
                    $"{{\"hashes\": [\"{string.Join("\",\"", modrinthMapping.SelectMany(l => l.Value.Select(m => m.ModrinthHash)))}\"], \"algorithm\": \"sha1\", " +
                    $"\"loaders\": [\"{targetLoaders}\"],\"game_versions\": [\"{mcInstance}\"]}}", "application/json"));

                foreach (var Entry in mods)
                {
                    if (modrinthUpdate[Entry.ModrinthHash] is null || Entry.compFile is null) continue;
                    var updateFile = new CompFile((JsonObject)modrinthUpdate[Entry.ModrinthHash], CompType.Mod);
                    if (!updateFile.Available) continue;

                    if (ModBase.modeDebug)
                        ModBase.Log($"[Mod] 本地文件 {Entry.compFile.FileName} 在 Modrinth 上的最新版为 {updateFile.FileName}");
                    if (Entry.compFile.ReleaseDate >= updateFile.ReleaseDate ||
                        Entry.compFile.Hash == updateFile.Hash) continue;

                    // 设置更新日志与更新文件
                    if (Entry.UpdateFile is not null && updateFile.Hash == Entry.UpdateFile.Hash)
                    {
                        Entry.changelogUrls.Add(
                            $"https://modrinth.com/mod/{modrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={mcInstance}");
                        Entry.UpdateFile.DownloadUrls.AddRange(updateFile.DownloadUrls);
                        Entry.UpdateFile = updateFile;
                    }
                    else if (Entry.UpdateFile is null || updateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate)
                    {
                        Entry.changelogUrls = new List<string>
                        {
                            $"https://modrinth.com/mod/{modrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={mcInstance}"
                        };
                        Entry.UpdateFile = updateFile;
                    }
                }

                ModBase.Log("[Mod] 从 Modrinth 获取本地 Mod 信息结束");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
                isFailed = true;
            }
            finally
            {
                Interlocked.Increment(ref endedThreadCount);
            }
        }, "Mod List Detail Loader Modrinth");

        // 从 CurseForge 获取信息（工程 ID 与文件 ID 均为数字）
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程信息
                var curseForgeHashes = new List<uint>();
                foreach (var entry in mods)
                {
                    curseForgeHashes.Add(entry.CurseForgeHash);
                    if (loader.IsAbortedWithThread(currentTaskId)) return;
                }

                var curseForgeResponse = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.curseforge.com/v1/fingerprints/432", "POST",
                    $"{{\"fingerprints\": [{string.Join(",", curseForgeHashes)}]}}", "application/json"));
                var curseForgeRaw = (JsonArray)curseForgeResponse["data"]["exactMatches"];
                ModBase.Log($"[Mod] 从 CurseForge 获取到 {curseForgeRaw.Count} 个本地 Mod 的对应信息");

                // 步骤 2：尝试读取工程信息缓存，构建本地文件与工程 ID 的对应关系
                if (!curseForgeRaw.Any()) return;
                var curseForgeMapping = new Dictionary<string, List<LocalCompFile>>();
                foreach (var project in curseForgeRaw)
                {
                    var projectId = project["id"].ToString();
                    var hash = project["file"]["fileFingerprint"].ToObject<uint>();
                    foreach (var Entry in mods)
                    {
                        if (Entry.CurseForgeHash != hash) continue;
                        // 读取已加载的缓存，加快结果出现速度
                        if (compProjectCache.ContainsKey(projectId) && Entry.Comp is null)
                            Entry.Comp = compProjectCache[projectId];

                        if (!curseForgeMapping.ContainsKey(projectId))
                            curseForgeMapping[projectId] = new List<LocalCompFile>();
                        curseForgeMapping[projectId].Add(Entry);

                        // 记录对应的 CompFile
                        var fileInfo = new CompFile((JsonObject)project["file"], CompType.Mod);
                        if (Entry.compFile is null || Entry.compFile.ReleaseDate < fileInfo.ReleaseDate)
                            Entry.compFile = fileInfo;
                    }
                }

                if (loader.IsAbortedWithThread(currentTaskId)) return;
                ModBase.Log($"[Mod] 需要从 CurseForge 获取 {curseForgeMapping.Count} 个本地 Mod 的工程信息");

                // 步骤 3：获取工程信息，同时归纳需要检查更新的文件 ID
                if (!curseForgeMapping.Any()) return;
                var curseForgeProject = (JsonArray)((JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.curseforge.com/v1/mods", "POST",
                    $"{{\"modIds\": [{string.Join(",", curseForgeMapping.Keys)}]}}", "application/json")))["data"];

                var updateFileIds = new Dictionary<int, List<LocalCompFile>>(); // FileId -> 本地 Mod 文件列表
                var fileIdToProjectSlug = new Dictionary<int, string>();
                foreach (var projectJson in curseForgeProject)
                {
                    if (projectJson["isAvailable"] is not null && !projectJson["isAvailable"].ToObject<bool>())
                        continue;

                    // 设置 Entry 中的工程信息
                    var project = new CompProject((JsonObject)projectJson);
                    if (!curseForgeMapping.ContainsKey(project.Id)) continue;
                    foreach (var Entry in curseForgeMapping[project.Id])
                    {
                        // 若已从 Modrinth 获取到工程信息，则保留它，仅再次触发修改事件以刷新 UI
                        if (Entry.Comp is not null && !Entry.Comp.FromCurseForge)
                        {
                            var existing = Entry.Comp;
                            Entry.Comp = existing;
                            continue;
                        }

                        Entry.Comp = project;
                    }

                    // 仅在加载器唯一时查找可能有更新的文件
                    if (modLoaders.Count != 1) continue;
                    string newestVersion = null;
                    var newestFileIds = new List<int>();
                    foreach (var indexEntry in (JsonArray)projectJson["latestFilesIndexes"])
                    {
                        // 加载器唯一且匹配
                        if (compType != CompType.DataPack &&
                            (indexEntry["modLoader"] is null ||
                             (int)modLoaders.Single() != indexEntry["modLoader"].ToObject<int>()))
                            continue;

                        var indexVersion = indexEntry["gameVersion"].ToString();
                        if (indexVersion != mcInstance) continue; // MC 版本匹配
                        // latestFilesIndexes 按时间从新到老排序，只保留最新的 MC 版本
                        if (newestVersion is not null &&
                            McVersionComparer.CompareVersion(newestVersion, indexVersion) > -1)
                            continue;

                        if (newestVersion != indexVersion)
                        {
                            newestVersion = indexVersion;
                            newestFileIds.Clear();
                        }

                        newestFileIds.Add(indexEntry["fileId"].ToObject<int>());
                    }

                    foreach (var fileId in newestFileIds)
                    {
                        if (!updateFileIds.ContainsKey(fileId)) updateFileIds[fileId] = new List<LocalCompFile>();
                        updateFileIds[fileId].AddRange(curseForgeMapping[project.Id]);
                        fileIdToProjectSlug[fileId] = project.Slug;
                    }
                }

                if (loader.IsAbortedWithThread(currentTaskId)) return;
                ModBase.Log(
                    $"[Mod] 已从 CurseForge 获取本地 Mod 信息，需要获取 {updateFileIds.Count} 个用于检查更新的文件信息");

                // 步骤 4：获取更新文件信息
                if (!updateFileIds.Any()) return;
                var curseForgeFiles = (JsonArray)((JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                    "https://api.curseforge.com/v1/mods/files", "POST",
                    $"{{\"fileIds\": [{string.Join(",", updateFileIds.Keys)}]}}", "application/json")))["data"];

                var updateFiles = new Dictionary<LocalCompFile, CompFile>();
                foreach (var fileJson in curseForgeFiles)
                {
                    var updateFile = new CompFile((JsonObject)fileJson, CompType.Mod);
                    if (!updateFile.Available) continue;
                    if (!int.TryParse(updateFile.Id, out var fileId) || !updateFileIds.ContainsKey(fileId))
                        continue;
                    foreach (var Entry in updateFileIds[fileId])
                    {
                        if (updateFiles.ContainsKey(Entry) && updateFiles[Entry].ReleaseDate >= updateFile.ReleaseDate)
                            continue;
                        updateFiles[Entry] = updateFile;
                    }
                }

                foreach (var pair in updateFiles)
                {
                    var Entry = pair.Key;
                    var updateFile = pair.Value;
                    if (Entry.compFile is null) continue;
                    if (ModBase.modeDebug)
                        ModBase.Log(
                            $"[Mod] 本地文件 {Entry.compFile.FileName} 在 CurseForge 上的最新版为 {updateFile.FileName}");
                    if (Entry.compFile.ReleaseDate >= updateFile.ReleaseDate ||
                        Entry.compFile.Hash == updateFile.Hash) continue;

                    var changelogUrl =
                        $"https://www.curseforge.com/minecraft/mc-mods/{fileIdToProjectSlug[int.Parse(updateFile.Id)]}/files/{updateFile.Id}";

                    // 设置更新日志与更新文件
                    if (Entry.UpdateFile is not null && updateFile.Hash == Entry.UpdateFile.Hash)
                    {
                        // 合并下载源
                        Entry.changelogUrls.Add(changelogUrl);
                        Entry.UpdateFile.DownloadUrls.AddRange(updateFile.DownloadUrls);
                    }
                    else if (Entry.UpdateFile is null || updateFile.ReleaseDate > Entry.UpdateFile.ReleaseDate)
                    {
                        // 替换
                        Entry.changelogUrls = new List<string> { changelogUrl };
                        Entry.UpdateFile = updateFile;
                    }
                }

                ModBase.Log("[Mod] 从 CurseForge 获取 Mod 更新信息结束");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
                isFailed = true;
            }
            finally
            {
                Interlocked.Increment(ref endedThreadCount);
            }
        }, "Mod List Detail Loader CurseForge");

        // 等待线程结束
        while (endedThreadCount < 2)
        {
            if (loader.IsAborted) return;
            Thread.Sleep(10);
        }

        // 保存缓存
        var cachedMods = mods.Where(m => m.Comp is not null).ToList();
        ModBase.Log($"[Mod] 联网获取本地 Mod 信息完成，为 {cachedMods.Count} 个 Mod 更新缓存");
        if (!cachedMods.Any()) return;

        foreach (var Entry in cachedMods)
        {
            Entry.compLoaded = !isFailed;
            cache[Entry.ModrinthHash + mcInstance + string.Join("", modLoaders)] = Entry.ToJson();
        }

        ModBase.WriteFile(Path.Combine(ModBase.pathTemp, "Cache", "LocalComp.json"),
            cache.ToJsonString(ModBase.modeDebug ? new JsonSerializerOptions(JsonCompat.SerializerOptions) { WriteIndented = true } : null));

        // 刷新 UI
        ModBase.RunInUi(() =>
        {
            if (ModMain.frmInstanceMod?.Filter == PageInstanceCompResource.FilterType.CanUpdate)
                ModMain.frmInstanceMod?.RefreshUI();
            else
                ModMain.frmInstanceMod?.RefreshBars();
        });
    }

    public static List<CompLoaderType> GetCurrentVersionModLoader()
    {
        var modLoaders = new List<CompLoaderType>();
        if (PageInstanceLeft.McInstance.Info.HasForge)
            modLoaders.Add(CompLoaderType.Forge);
        if (PageInstanceLeft.McInstance.Info.HasNeoForge)
            modLoaders.Add(CompLoaderType.NeoForge);
        if (PageInstanceLeft.McInstance.Info.HasFabric)
            modLoaders.Add(CompLoaderType.Fabric);
        if (PageInstanceLeft.McInstance.Info.HasQuilt)
            modLoaders.AddRange(new[] { CompLoaderType.Fabric, CompLoaderType.Quilt });
        if (PageInstanceLeft.McInstance.Info.HasLiteLoader)
            modLoaders.Add(CompLoaderType.LiteLoader);
        if (!modLoaders.Any())
            modLoaders.AddRange(new[]
            {
                CompLoaderType.Forge, CompLoaderType.NeoForge, CompLoaderType.Fabric, CompLoaderType.LiteLoader,
                CompLoaderType.Quilt
            });
        return modLoaders;
    }

    public static string GetPathNameByCompType(CompType theType)
    {
        switch (theType)
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

    private static readonly Regex regexIsJarFile = new(@"\.jar(\.disabled)?$");

    /// <summary>
    ///     通过文件名关键字和 Mod ID 比如 <c>fabric</c> <c>api</c> 和 <c>fabric-api</c> 来获取给定实例 mods 目录中某个 Mod 的
    ///     <see cref="LocalCompFile" /> 对象
    ///     <br />
    ///     <b>为了不浪费性能，关键字统一用小写</b>
    /// </summary>
    /// <returns>
    ///     如果文件名包含主关键字，以及其他关键字中的任意一个，同时 Mod ID 一致，即认为匹配，返回对应的对象，若没有匹配的文件则返回空值。
    /// </returns>
    public static LocalCompFile GetModLocalCompByKeywords(McInstance instance, string modId,
        string mainKeyword, params string[] keywords)
    {
        if (modId is null)
            return null;
        return GetModLocalCompByKeywords(instance, new[] { modId }, mainKeyword, keywords);
    }

    public static LocalCompFile GetModLocalCompByKeywords(McInstance instance, string[] modIds,
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
            if (!regexIsJarFile.IsMatch(lowerFilePath))
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
