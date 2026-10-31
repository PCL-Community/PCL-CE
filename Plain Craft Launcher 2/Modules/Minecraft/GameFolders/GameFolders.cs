using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;
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
                HintService.Show(Hints.Error(Lang.Text("Select.Folder.Invalid", folder)));
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
                DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Select.Folder.InvalidTitle"),
                    Content = Lang.Text("Select.Folder.Invalid.WithDetail", path, ex.ToString()),
                    Buttons = DialogButtons.Ok,
                    IsWarn = true,
                    ForceWait = true
                });
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
                    new McFolder
                    {
                        Name = Lang.Text("Select.Folder.CurrentFolder"),
                        Location = Common.ExePath,
                        Type = McFolder.Types.Original
                    });
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
                new McFolder
                {
                    Name = Lang.Text("Select.Folder.OfficialLauncherFolder"),
                    Location = mojangPath,
                    Type = McFolder.Types.Original
                });

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
                cacheFolder.Type = (cacheFolder.Name ?? "") != (newOriginalFolder.Name ?? "")
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
            new McFolder
            {
                Name = Lang.Text("Select.Folder.CurrentFolder"),
                Location = $@"{Common.ExePath}.minecraft\",
                Type = McFolder.Types.Original
            });
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

        public string Name = name ?? "";
        public Types Type = type;

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
