using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Network;


namespace PCL;

public static partial class ModMinecraft
{
    #region 文件夹

    /// <summary>
    ///     当前的 Minecraft 文件夹路径，以“\”结尾。
    /// </summary>
    public static string McFolderSelected;

    /// <summary>
    ///     当前的 Minecraft 文件夹列表。
    /// </summary>
    public static List<McFolder> McFolderList = new();

    public class McFolder // 必须是 Class，否则不是引用类型，在 ForEach 中不会得到刷新
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
        public string Location;

        public string Name;
        public Types Type;

        public override bool Equals(object obj)
        {
            if (!(obj is McFolder))
                return false;
            var folder = (McFolder)obj;
            return (Name ?? "") == (folder.Name ?? "") && (Location ?? "") == (folder.Location ?? "") &&
                   Type == folder.Type;
        }

        public override string ToString()
        {
            return Location;
        }
    }

    /// <summary>
    ///     加载 Minecraft 文件夹列表。
    /// </summary>
    public static ModLoader.LoaderTask<int, int> McFolderListLoader = new("Minecraft Folder List",
        _ => McFolderListLoadSub(), Priority: ThreadPriority.AboveNormal);

    private static void McFolderListLoadSub()
    {
        try
        {
            // 初始化
            var cacheMcFolderList = new List<McFolder>();

            #region 读取自定义（Custom）文件夹，可能没有结果

            // 格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
            foreach (string folder in (IEnumerable)((dynamic)States.Game.Folders).Split("|"))
            {
                if (string.IsNullOrEmpty(folder))
                    continue;
                if (!folder.Contains(">") || !folder.EndsWithF(@"\"))
                {
                    ModMain.Hint("无效的 Minecraft 文件夹：" + folder, ModMain.HintType.Critical);
                    continue;
                }

                var name = folder.Split(">")[0];
                var path = folder.Split(">")[1];
                try
                {
                    ModBase.CheckPermissionWithException(path);
                    cacheMcFolderList.Add(new McFolder { Name = name, Location = path, Type = McFolder.Types.Custom });
                }
                catch (Exception ex)
                {
                    ModMain.MyMsgBox(
                        "失效的 Minecraft 文件夹：" + "\r\n" + path + "\r\n" + "\r\n" +
                        ex.Message, "Minecraft 文件夹失效", IsWarn: true);
                    ModBase.Log(ex, $"无法访问 Minecraft 文件夹 {path}");
                }
            }

            #endregion

            #region 读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果

            var currentMcFolderList = new List<McFolder>();
            var originalMcFolderList = new List<McFolder>();
            // 扫描当前文件夹
            try
            {
                if (Directory.Exists(ModBase.ExePath + @"versions\"))
                    originalMcFolderList.Add(new McFolder
                        { Name = "当前文件夹", Location = ModBase.ExePath, Type = McFolder.Types.Original });
                foreach (var folder in new DirectoryInfo(ModBase.ExePath).GetDirectories())
                    if (Directory.Exists(folder.FullName + @"versions\") || folder.Name == ".minecraft")
                    {
                        var newCurrentFolder = new McFolder
                            { Name = folder.Name, Location = folder.FullName + @"\", Type = McFolder.Types.Original };
                        originalMcFolderList.Add(newCurrentFolder);
                        currentMcFolderList.Add(newCurrentFolder);
                    }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败");
            }

            // 扫描官启文件夹
            var MojangPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\";
            if ((!currentMcFolderList.Any() || (MojangPath ?? "") != (currentMcFolderList[0].Location ?? "")) &&
                Directory.Exists(MojangPath + @"versions\")) // 当前文件夹不是官启文件夹
                // 具有权限且存在 versions 文件夹
                originalMcFolderList.Add(new McFolder
                    { Name = "官方启动器文件夹", Location = MojangPath, Type = McFolder.Types.Original });

            ModBase.Log(cacheMcFolderList.Count + " 个自定义文件夹，" + originalMcFolderList.Count + " 个原始文件夹");

            var unAdded = false;
            foreach (var newOriginalFolder in originalMcFolderList)
            {
                foreach (var cacheFolder in cacheMcFolderList)
                    if ((cacheFolder.Location ?? "") == (newOriginalFolder.Location ?? ""))
                    {
                        if ((cacheFolder.Name ?? "") != (newOriginalFolder.Name ?? ""))
                            cacheFolder.Type = McFolder.Types.RenamedOriginal;
                        else
                            cacheFolder.Type = McFolder.Types.Original;
                        unAdded = true;
                    }

                if (!unAdded)
                    cacheMcFolderList.Add(newOriginalFolder); // 如果没有重命名，则添加当前文件夹
            }

            #endregion

            #region 读取自定义文件夹情况并写入设置

            // 将自定义文件夹情况同步到设置
            var config = new List<string>();
            foreach (var Folder in cacheMcFolderList)
                config.Add(Folder.Name + ">" + Folder.Location);
            if (!config.Any())
                config.Add(""); // 防止 0 元素 Join 返回 Nothing
            States.Game.Folders = config.Join("|");

            #endregion

            // 若没有可用文件夹，则创建 .minecraft
            if (!cacheMcFolderList.Any())
            {
                Directory.CreateDirectory(ModBase.ExePath + @".minecraft\versions\");
                cacheMcFolderList.Add(new McFolder
                    { Name = "当前文件夹", Location = ModBase.ExePath + @".minecraft\", Type = McFolder.Types.Original });
            }

            foreach (var Folder in cacheMcFolderList) McFolderLauncherProfilesJsonCreate(Folder.Location);
            if (Conversions.ToBoolean(Config.Debug.AddRandomDelay))
                Thread.Sleep(RandomUtils.NextInt(200, 2000));

            // 回设
            McFolderList = cacheMcFolderList;
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "加载 Minecraft 文件夹列表失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    /// </summary>
    public static void McFolderLauncherProfilesJsonCreate(string Folder)
    {
        try
        {
            if (File.Exists(Folder + "launcher_profiles.json"))
                return;
            var ResultJson = @"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ + DateTime.Now.ToString("yyyy'-'MM'-'dd") + "T" + DateTime.Now.ToString("HH':'mm':'ss") +
                             @".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}";
            ModBase.WriteFile(Folder + "launcher_profiles.json", ResultJson, Encoding: Encoding.GetEncoding("GB18030"));
            ModBase.Log("[Minecraft] 已创建 launcher_profiles.json：" + Folder);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "创建 launcher_profiles.json 失败（" + Folder + "）", ModBase.LogLevel.Feedback);
        }
    }

    #endregion
}
