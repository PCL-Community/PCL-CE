using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Minecraft.Folder;

public class FolderManager {
    /// <summary>
    /// 当前选择的 Minecraft 文件夹。
    /// </summary>
    public McFolder? CurrentFolder { get; set; }

    /// <summary>
    /// 当前选择的 Minecraft 实例
    /// </summary>
    public IMcInstance? CurrentInst { get; set; }

    /// <summary>
    /// Minecraft 文件夹列表。
    /// </summary>
    public List<McFolder> McFolderList { get; } = [];

    public async Task McFolderListLoadAsync() {
        try {
            var cacheMcFolderList = new List<McFolder>();

            #region Load Custom Folders

            foreach (var folder in Config.Launch.Folders.Split('|', StringSplitOptions.RemoveEmptyEntries)) {
                if (!folder.Contains('>') || !folder.EndsWith('\\')) {
                    HintWrapper.Show($"无效的 Minecraft 文件夹：{folder}", HintTheme.Error);
                    continue;
                }

                var parts = folder.Split('>');
                var name = parts[0];
                var path = parts[1];

                try {
                    await Directories.CheckPermissionWithExceptionAsync(path);
                    cacheMcFolderList.Add(new McFolder(name, path, McFolderType.Custom));
                } catch (Exception ex) {
                    MsgBoxWrapper.Show($"失效的 Minecraft 文件夹：\n{path}\n\n{ex.Message}", "Minecraft 文件夹失效", MsgBoxTheme.Warning);
                    LogWrapper.Warn(ex, $"无法访问 Minecraft 文件夹 {path}");
                }
            }

            #endregion

            #region Load Default (Original) Folders

            var currentMcFolderList = new List<McFolder>();
            var originalMcFolderList = new List<McFolder>();

            // Scan current directory
            try {
                var versionsPath = Path.Combine(Basics.ExecutablePath, "versions");
                if (Directory.Exists(versionsPath)) {
                    originalMcFolderList.Add(new McFolder("Current Folder", Basics.ExecutablePath, McFolderType.Original));
                }

                foreach (var folder in new DirectoryInfo(Basics.ExecutablePath).GetDirectories()
                    .Where(f => Directory.Exists(Path.Combine(f.FullName, "versions")) || f.Name == ".minecraft")) {
                    var newFolder = new McFolder(folder.Name, Path.Combine(folder.FullName, "\\"), McFolderType.Original);
                    originalMcFolderList.Add(newFolder);
                    currentMcFolderList.Add(newFolder);
                }
            } catch (Exception ex) {
                LogWrapper.Warn(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败");
            }

            // Scan official launcher folder
            var mojangPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft\\");
            if (currentMcFolderList.Count == 0 || (mojangPath != currentMcFolderList.First().Path && Directory.Exists(Path.Combine(mojangPath, "versions")))) {
                originalMcFolderList.Add(new McFolder("Official Launcher Folder", mojangPath, McFolderType.Original));
            }

            LogWrapper.Warn($"{cacheMcFolderList.Count} 个自定义文件夹，{originalMcFolderList.Count} 个原始文件夹");

            // Merge original folders into cache, updating types if needed
            foreach (var newOriginalFolder in originalMcFolderList) {
                var existingFolder = cacheMcFolderList.FirstOrDefault(f => f.Path == newOriginalFolder.Path);
                if (existingFolder is not null) {
                    cacheMcFolderList[cacheMcFolderList.IndexOf(existingFolder)] = existingFolder with {
                        Type = existingFolder.Name != newOriginalFolder.Name ? McFolderType.RenamedOriginal : McFolderType.Original
                    };
                } else {
                    cacheMcFolderList.Add(newOriginalFolder);
                }
            }

            #endregion

            #region Sync Custom Folders to Settings

            var newSetup = cacheMcFolderList
                .Select(f => $"{f.Name}>{f.Path}")
                .ToList();

            if (newSetup.Count == 0) {
                newSetup.Add("");
            }

            Config.Launch.Folders = string.Join("|", newSetup);

            #endregion

            // Create default .minecraft folder if none exist
            if (cacheMcFolderList.Count == 0) {
                var defaultPath = Path.Combine(Basics.ExecutablePath, ".minecraft", "versions");
                Directory.CreateDirectory(defaultPath);
                cacheMcFolderList.Add(new McFolder("当前文件夹", Path.Combine(Basics.ExecutablePath, ".minecraft"), McFolderType.Original));
            }

            // Update launcher_profiles.json for each folder
            foreach (var folder in cacheMcFolderList) {
                await McFolderLauncherProfilesJsonCreateAsync(folder.Path);

                var instanceManager = new InstanceManager(folder);
                await instanceManager.McInstanceListLoadAsync();
                folder.InstanceList = instanceManager;
            }

            // TODO: 未来去除这个 $ 符号
            CurrentFolder = cacheMcFolderList.Find(mcFolder => Files.ArePathsEqual(mcFolder.Path,
                Path.Combine(Basics.ExecutablePath, Config.Launch.SelectedFolder.TrimStart('$'))));
            if (CurrentFolder == null || !Directory.Exists(CurrentFolder.Path)) {
                // Invalid folder
                if (CurrentFolder == null) {
                    LogWrapper.Info("没有选择 Minecraft 文件夹，使用第一个");
                } else {
                    LogWrapper.Info($"Minecraft 文件夹非法或不存在: {CurrentFolder}");
                }
                // TODO: 这边也是
                Config.Launch.SelectedFolder = McFolderList[0].Path.Replace(Basics.ExecutablePath, "$");
            }

            // 随机延迟
            if (Config.System.Debug.AddRandomDelay) {
                await Task.Delay(RandomUtils.NextInt(200, 2000));
            }

            // 更新全局文件夹列表
            McFolderList.Clear();
            McFolderList.AddRange(cacheMcFolderList);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "加载 Minecraft 文件夹列表失败");
        }
    }

    /// <summary>
    /// 为指定 Minecraft 文件夹创建 launcher_profiles.json 文件（如果不存在）。
    /// </summary>
    private static async Task McFolderLauncherProfilesJsonCreateAsync(string folder) {
        try {
            var filePath = Path.Combine(folder, "launcher_profiles.json");
            if (File.Exists(filePath)) {
                return;
            }

            var profiles = new LauncherProfiles {
                Profiles = new Dictionary<string, Profile> {
                    ["PCL"] = new() {
                        Icon = "Grass",
                        Name = "PCL",
                        LastVersionId = "latest-release",
                        Type = "latest-release",
                        LastUsed = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffZ")
                    }
                },
                SelectedProfile = "PCL",
                ClientToken = "23323323323323323323323323323333"
            };

            var resultJson = JsonSerializer.Serialize(profiles, Files.PrettierJsonOptions);
            await Files.WriteFileAsync(filePath, resultJson, encoding: Encodings.GB18030);
            LogWrapper.Info("Minecraft", $"已创建 launcher_profiles.json：{folder}");
        } catch (Exception ex) {
            LogWrapper.Warn(ex, $"创建 launcher_profiles.json 失败（{folder}）");
        }
    }
}
