using System.IO;
using System.IO.Compression;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile
    {
        private readonly Lock _loadLock = new();

        private List<LocalCompFile> _embeddedMods = [];
        private Exception? _fileUnavailableReason;
        private bool _isInfoWithClassAvailable;
        private bool _isInfoWithClassLoaded;
        private bool _isInfoWithoutClassAvailable = false;
        private volatile bool _isLoaded;
        private long? _jijExpectedLastModified;
        private long? _jijExpectedSize;
        private volatile bool _jijPending;

        public bool IsFileAvailable
        {
            get
            {
                Load();
                return _fileUnavailableReason is null;
            }
        }

        public Exception FileUnavailableReason
        {
            get
            {
                Load();
                return _fileUnavailableReason!;
            }
        }

        public List<LocalCompFile> EmbeddedMods
        {
            get
            {
                Load();
                return _embeddedMods;
            }
            internal set => _embeddedMods = value;
        }

        internal bool JijPending => _jijPending;
        public string? JijLoader { get; internal set; }
        public string? JijTargetMcVersion { get; internal set; }
        public string? JijIdentifier { get; internal set; }
        public string? JijVersionRange { get; internal set; }
        public string? JijArtifactVersion { get; internal set; }
        internal string? PreferredLoader { get; set; }
        public string? DetectedLoader { get; internal set; }

        internal void MarkLoaded()
        {
            _isLoaded = true;
        }

        internal void SetJijMetadata(string? name, string? modId, string? version)
        {
            _name = name;
            _modId = modId;
            _version = version;
            _isLoaded = true;
        }

        internal void SetJijDependencies(List<EmbeddedDependency>? declarations)
        {
            _dependencies = new Dictionary<string, string?>();
            _dependencyRaw.Clear();
            _dependencyDeclarations.Clear();
            if (declarations is null)
                return;

            foreach (var declaration in declarations)
            {
                if (declaration?.Id is null)
                    continue;
                AddDependency(declaration.Id, declaration.Raw, declaration.Optional);
            }
        }

        internal void SetJijConflicts(
            List<EmbeddedConflict>? conflicts,
            List<string>? provided,
            Dictionary<string, string>? providedVersions)
        {
            _conflictDeclarations.Clear();
            if (conflicts is not null)
                foreach (var c in conflicts.Where(c => !string.IsNullOrEmpty(c?.Target)))
                    _conflictDeclarations.Add(
                        new EmbeddedConflict
                        {
                            DeclarerId = c.DeclarerId,
                            Target = c.Target,
                            Raw = c.Raw,
                            Hard = c.Hard
                        });

            _providedIds.Clear();
            if (provided is not null)
                _providedIds.UnionWith(provided);

            _providedVersions.Clear();
            if (providedVersions is null)
                return;

            foreach (var kv in providedVersions)
                _providedVersions[kv.Key] = kv.Value;
        }

        internal void CopyLoadedStateFrom(LocalCompFile? other)
        {
            if (other is null || !other._isLoaded)
                return;

            lock (_loadLock)
            {
                _CopyBasicMetadataFrom(other);
                _CopyDependencyStateFrom(other);
                _CopyProvidedStateFrom(other);
                _CopyConflictStateFrom(other);
                _CopyJijStateFrom(other);
                _CopyNetworkStateFrom(other);
                _fileUnavailableReason = other._fileUnavailableReason;
                _isLoaded = true;
            }
        }

        private void _CopyBasicMetadataFrom(LocalCompFile other)
        {
            _name = other._name;
            _description = other._description;
            _version = other._version;
            _modId = other._modId;
            possibleModId = [.. other.possibleModId];
        }

        private void _CopyDependencyStateFrom(LocalCompFile other)
        {
            _dependencies = new Dictionary<string, string?>(other._dependencies);
            _dependencyRaw.Clear();

            foreach (var kv in other._dependencyRaw)
                _dependencyRaw[kv.Key] = kv.Value;

            _dependencyDeclarations.Clear();
            foreach (var d in other._dependencyDeclarations)
                _dependencyDeclarations.Add(
                    new EmbeddedDependency
                    {
                        Id = d.Id,
                        Raw = d.Raw,
                        Optional = d.Optional
                    });
        }

        private void _CopyProvidedStateFrom(LocalCompFile other)
        {
            _providedIds.Clear();
            _providedIds.UnionWith(other._providedIds);
            _providedVersions.Clear();

            foreach (var kv in other._providedVersions)
                _providedVersions[kv.Key] = kv.Value;
        }

        private void _CopyConflictStateFrom(LocalCompFile other)
        {
            _conflictDeclarations.Clear();
            foreach (var c in other._conflictDeclarations)
                _conflictDeclarations.Add(
                    new EmbeddedConflict
                    {
                        DeclarerId = c.DeclarerId,
                        Target = c.Target,
                        Raw = c.Raw,
                        Hard = c.Hard
                    });
        }

        private void _CopyJijStateFrom(LocalCompFile other)
        {
            _embeddedMods = other._embeddedMods;
            PreferredLoader = other.PreferredLoader;
            JijLoader = other.JijLoader;
            JijTargetMcVersion = other.JijTargetMcVersion;
            JijIdentifier = other.JijIdentifier;
            JijVersionRange = other.JijVersionRange;
            JijArtifactVersion = other.JijArtifactVersion;
            DetectedLoader = other.DetectedLoader;
        }

        private void _CopyNetworkStateFrom(LocalCompFile other)
        {
            Logo = other.Logo;
            Url = other.Url;
            Authors = other.Authors;
        }

        private void Init()
        {
            _ResetMetadata();
            _ResetDependencyState();
            _ResetJijState();
            _isLoaded = false;
            _fileUnavailableReason = null;
            _isInfoWithClassLoaded = false;
            _isInfoWithClassAvailable = false;
        }

        private void _ResetMetadata()
        {
            _name = null;
            _description = null;
            _version = null;
            _modId = null;
            possibleModId = [];
        }

        private void _ResetDependencyState()
        {
            _dependencies = new Dictionary<string, string?>();
            _dependencyRaw.Clear();
            _dependencyDeclarations.Clear();
            _providedIds.Clear();
            _providedVersions.Clear();
            _conflictDeclarations.Clear();
        }

        private void _ResetJijState()
        {
            _embeddedMods = [];
            _jijExpectedLastModified = null;
            _jijExpectedSize = null;
            JijLoader = null;
            JijTargetMcVersion = null;
            JijIdentifier = null;
            JijVersionRange = null;
            JijArtifactVersion = null;
            DetectedLoader = null;
        }

        public void LoadBasicInfo()
        {
            try
            {
                if (IsFolder)
                {
                    _isLoaded = true;
                    return;
                }

                if (!File.Exists(path))
                {
                    _fileUnavailableReason = new FileNotFoundException($"未找到资源文件（{path})");
                    _isLoaded = true;
                    return;
                }

                if (IsNbtLikeFile())
                {
                    _name = Path.GetFileNameWithoutExtension(path);
                    _isLoaded = true;
                    return;
                }

                Load();
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"加载基本信息失败：{path}");
            }
        }

        public void LoadNbtDataIfNeeded()
        {
            try
            {
                if (_nbtDataLoaded)
                    return;

                LoadNbtDataByExtension();
                _nbtDataLoaded = true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"延迟加载NBT数据失败：{path}");
            }
        }

        public void Load(bool forceReload = false)
        {
            if (_isLoaded && !forceReload)
                return;
            lock (_loadLock)
            {
                if (_isLoaded && !forceReload)
                    return;
                LoadCore();
            }
        }

        private void LoadCore()
        {
            Init();

            if (_RejectInvalidPath())
                return;

            if (IsFolder)
            {
                if (!Directory.Exists(ActualPath))
                    _fileUnavailableReason = new DirectoryNotFoundException(
                        $"未找到文件夹（{ActualPath}）");

                _isLoaded = true;
                return;
            }

            if (!File.Exists(path))
            {
                _fileUnavailableReason = new FileNotFoundException($"未找到资源文件（{path}）");
                _isLoaded = true;
                return;
            }

            if (IsNbtLikeFile())
            {
                _LoadNbtData();
                _isLoaded = true;
                return;
            }

            _LoadArchive();
            _isLoaded = true;
        }

        private bool _RejectInvalidPath()
        {
            if (path.Length >= 2)
                return false;
            _fileUnavailableReason = new FileNotFoundException($"错误的资源文件路径（{path}）");
            _isLoaded = true;
            return true;
        }

        private bool IsNbtLikeFile()
        {
            return path.EndsWithF(".litematic", true)
                || path.EndsWithF(".nbt", true)
                || path.EndsWithF(".schem", true)
                || path.EndsWithF(".schematic", true);
        }

        private void LoadNbtDataByExtension()
        {
            if (path.EndsWithF(".litematic", true))
                LoadLitematicNbtData();
            else if (path.EndsWithF(".nbt", true))
                LoadStructureNbtData();
            else if (path.EndsWithF(".schem", true))
                LoadSchemNbtData();
            else if (path.EndsWithF(".schematic", true))
                LoadSchematicNbtData();
        }

        private void _LoadNbtData()
        {
            try
            {
                _name = Path.GetFileNameWithoutExtension(path);
                LoadNbtDataByExtension();
                _nbtDataLoaded = true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"投影文件信息获取失败（{path}）", Common.LogLevel.Developer);
                _fileUnavailableReason = ex;
            }
        }

        private ZipArchive _OpenArchive()
        {
            return new ZipArchive(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Write | FileShare.Delete));
        }

        private void _LoadArchive()
        {
            ZipArchive? jar = null;
            try
            {
                var fileInfo = new FileInfo(path);
                var jijLastModified = fileInfo.LastWriteTimeUtc.Ticks;
                var jijSize = fileInfo.Length;

                _jijExpectedLastModified = jijLastModified;
                _jijExpectedSize = jijSize;

                jar = _OpenArchive();
                DetectedLoader = JarInJar.DetectLoader(jar, PreferredLoader);
                LookupMetadata(jar, DetectedLoader);

                var jijTree = JarInJar.ResolveCached(
                    path,
                    jar,
                    out var jijComplete,
                    deferJijOnMiss,
                    jijLastModified,
                    jijSize,
                    jijScanContext,
                    PreferredLoader);
                if (jijTree is not null)
                    _embeddedMods = jijTree;

                _jijPending = jijTree is null || !jijComplete;
            }
            catch (UnauthorizedAccessException ex)
            {
                Common.Log(ex, $"资源文件由于无权限无法打开（{path}）", Common.LogLevel.Developer);
                _fileUnavailableReason = new UnauthorizedAccessException(
                    "没有读取此文件的权限，请尝试右键以管理员身份运行 PCL",
                    ex);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"资源文件无法打开（{path}）", Common.LogLevel.Developer);
                _fileUnavailableReason = ex;
            }
            finally
            {
                jar?.Dispose();
            }
        }

        internal bool ResolveJijNow()
        {
            lock (_loadLock)
            {
                if (!_jijPending)
                    return true;

                ZipArchive? jar = null;
                try
                {
                    var fileInfo = new FileInfo(path);
                    var jijLastModified = _jijExpectedLastModified
                        ?? fileInfo.LastWriteTimeUtc.Ticks;
                    var jijSize = _jijExpectedSize
                        ?? fileInfo.Length;

                    jar = _OpenArchive();
                    var tree = JarInJar.ResolveCached(
                        path,
                        jar,
                        out var complete,
                        false,
                        jijLastModified,
                        jijSize,
                        jijScanContext,
                        PreferredLoader);

                    if (tree is not null)
                        _embeddedMods = tree;

                    if (tree is null || !complete)
                        return false;

                    _jijPending = false;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Common.Log(ex, $"后台补解析内嵌模组失败（{path}）", Common.LogLevel.Developer);
                    return false;
                }
                finally
                {
                    jar?.Dispose();
                }
            }
        }
    }
}