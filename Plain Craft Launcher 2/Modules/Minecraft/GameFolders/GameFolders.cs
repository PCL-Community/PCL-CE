using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static class GameFolders
{
    /// <summary>
    ///     当前的 Minecraft 文件夹路径，以"\"结尾。
    /// </summary>
    public static string? mcFolderSelected;

    /// <summary>
    ///     当前的 Minecraft 文件夹列表。
    /// </summary>
    public static List<McFolder> mcFolderList = [];

    /// <summary>
    ///     加载 Minecraft 文件夹列表。
    /// </summary>
    public static readonly Loading.LoaderTask<int, int> McFolderListLoader = new(
        "Minecraft Folder List",
        _ => McFolderListLoadSub(),
        priority: ThreadPriority.AboveNormal);

    /// <summary>
    ///     旧版本把内置文件夹的译文直接写进了配置，升级后需要把这些译文迁移回资源键，
    ///     否则内置文件夹名称会永远停留在升级时的语言。
    /// </summary>
    private static readonly Dictionary<string, string> LegacyBuiltinFolderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["当前文件夹"] = "Select.Folder.CurrentFolder",
            ["目前資料夾"] = "Select.Folder.CurrentFolder",
            ["Current folder"] = "Select.Folder.CurrentFolder",
            ["官方启动器文件夹"] = "Select.Folder.OfficialLauncherFolder",
            ["官方啟動器資料夾"] = "Select.Folder.OfficialLauncherFolder",
            ["Official launcher folder"] = "Select.Folder.OfficialLauncherFolder"
        };

    private static void McFolderListLoadSub()
    {
        try
        {
            var cacheMcFolderList = _LoadCustomFolders();
            var originalMcFolderList = _ScanOriginalFolders();

            Common.Log($"{cacheMcFolderList.Count} 个自定义文件夹，{originalMcFolderList.Count} 个原始文件夹");

            _MergeOriginalFolders(cacheMcFolderList, originalMcFolderList);
            _SaveFolderConfig(cacheMcFolderList);
            _EnsureMinecraftFolder(cacheMcFolderList);

            foreach (var folder in cacheMcFolderList)
                McFolderLauncherProfilesJsonCreate(folder.Location);
            if (Config.Debug.AddRandomDelay)
                Thread.Sleep(RandomUtils.NextInt(200, 2000));

            mcFolderList = cacheMcFolderList;
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Select.Folder.Error.Load"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.Load"));
        }
    }

    private static List<McFolder> _LoadCustomFolders()
    {
        var cacheMcFolderList = new List<McFolder>();

        foreach (string folder in (IEnumerable)((dynamic)States.Game.Folders).Split("|"))
        {
            if (string.IsNullOrEmpty(folder))
                continue;

            if (!folder.Contains('>') || !folder.EndsWithF(@"\"))
            {
                HintService.Hint(
                    Lang.Text("Select.Folder.Invalid", folder),
                    HintType.Error);
                continue;
            }

            var name = folder.Split(">")[0];
            var path = folder.Split(">")[1];
            try
            {
                Common.CheckPermissionWithException(path);
                cacheMcFolderList.Add(
                    new McFolder
                    {
                        Name = name,
                        Location = path,
                        Type = McFolder.Types.Custom
                    });
            }
            catch (Exception ex)
            {
                Launcher.MyMsgBox(
                    Lang.Text("Select.Folder.Invalid.WithDetail", path, ex.ToString()),
                    Lang.Text("Select.Folder.InvalidTitle"),
                    isWarn: true);
                Common.Log(ex, $"无法访问 Minecraft 文件夹 {path}");
            }
        }

        return cacheMcFolderList;
    }

    private static List<McFolder> _ScanOriginalFolders()
    {
        var currentMcFolderList = new List<McFolder>();
        var originalMcFolderList = new List<McFolder>();
        try
        {
            if (Directory.Exists($@"{Common.ExePath}versions\"))
                originalMcFolderList.Add(
                    McFolder.CreateBuiltin(
                        "Select.Folder.CurrentFolder",
                        Common.ExePath,
                        McFolder.Types.Original));
            foreach (var folder in new DirectoryInfo(Common.ExePath).GetDirectories())
            {
                if (!Directory.Exists(Path.Combine(folder.FullName, "versions"))
                    && folder.Name != ".minecraft")
                    continue;

                var newCurrentFolder = new McFolder
                {
                    Name = folder.Name,
                    Location = $@"{folder.FullName}\",
                    Type = McFolder.Types.Original
                };
                originalMcFolderList.Add(newCurrentFolder);
                currentMcFolderList.Add(newCurrentFolder);
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败");
        }

        var mojangPath = $@"{Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft")}\";
        if ((currentMcFolderList.Count == 0
                || mojangPath != currentMcFolderList[0].Location)
            && Directory.Exists(Path.Combine(mojangPath, "versions")))
            originalMcFolderList.Add(
                McFolder.CreateBuiltin(
                    "Select.Folder.OfficialLauncherFolder",
                    mojangPath,
                    McFolder.Types.Original));

        return originalMcFolderList;
    }

    private static void _MergeOriginalFolders(
        List<McFolder> cacheMcFolderList,
        List<McFolder> originalMcFolderList)
    {
        foreach (var newOriginalFolder in originalMcFolderList)
        {
            var unAdded = true;
            foreach (var cacheFolder in cacheMcFolderList.Where(cacheFolder =>
                         (cacheFolder.Location ?? "") == (newOriginalFolder.Location ?? "")))
            {
                var newOriginalName = newOriginalFolder.Name ?? "";
                // 旧版本保存的是译文；仅当它确实是该内置文件夹的旧译名时才迁移，
                // 以免覆盖用户手动改过的名称。
                if (newOriginalName.StartsWith(
                        McFolder.BuiltinNamePrefix,
                        StringComparison.Ordinal) &&
                    LegacyBuiltinFolderNames.TryGetValue(
                        cacheFolder.Name ?? "",
                        out var legacyKey) &&
                    newOriginalName == McFolder.BuiltinNamePrefix + legacyKey)
                    cacheFolder.Name = newOriginalName;

                cacheFolder.Type = (cacheFolder.Name ?? "") != newOriginalName
                    ? McFolder.Types.RenamedOriginal
                    : McFolder.Types.Original;
                unAdded = false;
            }

            if (unAdded)
                cacheMcFolderList.Add(newOriginalFolder);
        }
    }

    private static void _SaveFolderConfig(List<McFolder> cacheMcFolderList)
    {
        var config = cacheMcFolderList
            .Select(folder => $"{folder.Name}>{folder.Location}")
            .ToList();
        if (config.Count == 0)
            config.Add("");
        States.Game.Folders = config.Join("|");
    }

    private static void _EnsureMinecraftFolder(List<McFolder> cacheMcFolderList)
    {
        if (cacheMcFolderList.Count != 0)
            return;

        Directory.CreateDirectory($@"{Common.ExePath}.minecraft\versions\");
        cacheMcFolderList.Add(
            McFolder.CreateBuiltin(
                "Select.Folder.CurrentFolder",
                $@"{Common.ExePath}.minecraft\",
                McFolder.Types.Original));
    }

    /// <summary>
    ///     为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    /// </summary>
    public static void McFolderLauncherProfilesJsonCreate(string folder)
    {
        try
        {
            if (File.Exists(Path.Combine(folder, "launcher_profiles.json")))
                return;

            var now = DateTime.Now;
            var resultJson = $$"""
                               {
                                   "profiles":  {
                                       "PCL": {
                                           "icon": "Grass",
                                           "name": "PCL",
                                           "lastVersionId": "latest-release",
                                           "type": "latest-release",
                                           "lastUsed": "{{now.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)}}T{{now.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture)}}.0000Z"
                                       }
                                   },
                                   "selectedProfile": "PCL",
                                   "clientToken": "23323323323323323323323323323333"
                               }
                               """;
            Common.WriteFile(
                Path.Combine(folder, "launcher_profiles.json"),
                resultJson,
                encoding: Encoding.GetEncoding("GB18030"));
            Common.Log($"[Minecraft] 已创建 launcher_profiles.json：{folder}");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"创建 launcher_profiles.json 失败（{folder}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Folder.Error.OperationFailed"));
        }
    }

    public class McFolder(
        string? location = null,
        string? name = null,
        McFolder.Types type = default)
    {
        public enum Types
        {
            Original,
            RenamedOriginal,
            Custom
        }

        /// <summary>
        ///     文件夹路径。
        ///     以 \ 结尾，例如 "D:\Game\MC\.minecraft\"。
        /// </summary>
        public string Location = location ?? "";

        /// <summary>
        ///     文件夹的显示名称。<br/>
        ///     若是内置文件夹，该字段保存本地化资源键（以 <c>$</c> 开头），显示时请使用 <see cref="DisplayName" />。
        /// </summary>
        public string Name = name ?? "";

        public Types Type = type;

        /// <summary>
        ///     内置文件夹名称的资源键前缀，用于区分「资源键」与「用户自定义名称」。
        /// </summary>
        internal const string BuiltinNamePrefix = "$";

        /// <summary>
        ///     当前语言下的显示名称。内置文件夹会按当前语言实时翻译。
        /// </summary>
        public string DisplayName =>
            Name.Length > BuiltinNamePrefix.Length &&
            Name.StartsWith(BuiltinNamePrefix, StringComparison.Ordinal)
                ? Lang.Text(Name[BuiltinNamePrefix.Length..])
                : Name;

        /// <summary>
        ///     创建一个使用内置名称的文件夹。<br/>
        ///     写入 <see cref="Name" /> 的是资源键（带前缀），因此可以安全持久化。
        /// </summary>
        /// <param name="nameKey">内置名称的本地化资源键。</param>
        /// <param name="location">文件夹路径。</param>
        /// <param name="type">文件夹类型。</param>
        public static McFolder CreateBuiltin(string nameKey, string location, Types type)
        {
            return new McFolder
            {
                Name = BuiltinNamePrefix + nameKey,
                Location = location,
                Type = type
            };
        }

        public override bool Equals(object? obj)
        {
            if (obj is not McFolder folder)
                return false;
            return (Name ?? "") == (folder.Name ?? "")
                && (Location ?? "") == (folder.Location ?? "")
                && Type == folder.Type;
        }

        public override string ToString()
        {
            return Location;
        }
    }
}
