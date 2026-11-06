using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using static PCL.Comp;

namespace PCL;

public static partial class LocalComp
{
    public partial class LocalCompFile
    {
        public delegate void OnCompUpdateEventHandler(LocalCompFile sender);

        public List<string> ChangelogUrls = [];
        public CompFile CompFile = null!;
        public bool CompLoaded;

        private uint? _curseForgeHash;

        public CompProject? Comp
        {
            get;
            set
            {
                field = value;
                OnCompUpdate?.Invoke(this);
            }
        }

        public CompFile UpdateFile
        {
            get;
            set
            {
                field = value;
                OnCompUpdate?.Invoke(this);
            }
        } = null!;

        public bool CanUpdate =>
            !Config.Preference.Hide.FunctionModUpdate
            && !IsUpdateDisabled
            && ChangelogUrls.Count != 0;

        private static bool IsUpdateDisabled =>
            LauncherState.SelectedInstance is { } instance
            && Config.Instance.DisableModUpdate[instance.PathInstance];

        public uint CurseForgeHash
        {
            get
            {
                if (_curseForgeHash is null)
                {
                    var buf = HashCache
                        .Value.GetMurmurHash2Async(path)
                        .GetAwaiter()
                        .GetResult()
                        .HexToBytes();
                    _curseForgeHash = BitConverter.ToUInt32(buf);
                }

                return (uint)_curseForgeHash;
            }
        }

        public string ModrinthHash
        {
            get
            {
                field ??= HashCache.Value.GetSHA1Async(path).GetAwaiter().GetResult();
                return field;
            }
        }

        public event OnCompUpdateEventHandler? OnCompUpdate;

        /// <summary>
        ///     将网络信息保存为 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject();
            if (Comp is not null)
                json.Add(nameof(Comp), Comp.ToJson());

            json.Add(
                nameof(ChangelogUrls),
                new JsonArray([.. ChangelogUrls.Select(s => (JsonNode)s)]));
            json.Add(nameof(CompLoaded), CompLoaded);

            if (CompFile is not null)
                json.Add(nameof(CompFile), CompFile.ToJson());
            if (UpdateFile is not null)
                json.Add(nameof(UpdateFile), UpdateFile.ToJson());

            return json;
        }

        /// <summary>
        ///     从 Json 中读取网络信息。
        /// </summary>
        public void FromJson(JsonObject json)
        {
            CompLoaded = json[nameof(CompLoaded)]?.ToObject<bool>() ?? false;
            if (json[nameof(Comp)] is JsonObject compJson)
                Comp = new CompProject(compJson);
            if (json.ContainsKey(nameof(ChangelogUrls)))
                ChangelogUrls = json[nameof(ChangelogUrls)]?.ToObject<List<string>>() ?? [];
            if (json[nameof(CompFile)] is JsonObject compFileJson)
                CompFile = new CompFile(compFileJson, CompType.Mod);
            if (json[nameof(UpdateFile)] is JsonObject updateFileJson)
                UpdateFile = new CompFile(updateFileJson, CompType.Mod);
        }

        public override string ToString()
        {
            return $"{State} - {path}";
        }

        public override bool Equals(object? obj)
        {
            var target = obj as LocalCompFile;
            return target is not null && path == target.path;
        }
    }
}