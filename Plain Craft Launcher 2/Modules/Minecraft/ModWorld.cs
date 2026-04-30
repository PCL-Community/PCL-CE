using System.IO;
using System.Text;
using fNbt;
using PCL.Core.Utils;

namespace PCL;

public static class ModWorld
{
    #region 压缩包处理

    /// <summary>
    ///     尝试处理存档。
    /// </summary>
    /// <exception cref="ModBase.CancelledException">确定这是一个存档文件（夹），但存档文件损坏时抛出的异常。</exception>
    /// <exception cref="Exception"></exception>
    public static void ReadWorld(string SavePath)
    {
        if (File.Exists(SavePath))
        {
            var ExtractPath = $@"{ModBase.PathTemp}Cache\{RandomUtils.NextInt(0, 1000_0000)}\";
            if (Directory.Exists(ExtractPath))
                ModBase.DeleteDirectory(ExtractPath);
            ModBase.ExtractFile(SavePath, ExtractPath);
            SavePath = ExtractPath;
        }

        var world = new McWorld(SavePath);
        if (!File.Exists(world.LevelDatPath))
            throw new Exception("无效的 Minecraft 存档");
        if (!world.Read())
        {
            ModMain.Hint("存档文件可能已损坏，无法读取！", ModMain.HintType.Critical);
            throw new ModBase.CancelledException();
        }

        var sb = new StringBuilder();
        if (world.VersionName is not null)
            sb.AppendLine($"存档版本：{world.VersionName}");
        if (world.VersionId is not null)
            sb.AppendLine($"存档数据版本：{world.VersionId}");
        if (sb.Length == 0)
            sb.AppendLine("无法获取存档的版本信息，存档版本可能低于 15w32a（对应正式版 1.9）！");
        ModMain.MyMsgBox(sb.ToString(), "存档版本信息");
    }

    #endregion

    #region 存档

    /// <summary>
    ///     存档。
    /// </summary>
    public class McWorld
    {
        /// <summary>
        ///     存档路径。文件夹，以 “\” 结尾。
        /// </summary>
        public string SavePath;

        /// <summary>
        ///     版本 ID。
        /// </summary>
        public string VersionId;

        /// <summary>
        ///     版本名。
        /// </summary>
        public string VersionName;

        /// <summary>
        ///     存档。
        /// </summary>
        /// <param name="SavePath">存档路径。文件夹，以 “\” 结尾。</param>
        public McWorld(string SavePath)
        {
            if (!SavePath.EndsWithF(@"\"))
                SavePath = SavePath + @"\";
            this.SavePath = SavePath;
        }

        public string LevelDatPath =>
            File.Exists(SavePath + "level.dat") ? SavePath + "level.dat" : SavePath + "level.dat_old";

        /// <summary>
        ///     读取存档。返回是否成功。
        /// </summary>
        public bool Read()
        {
            try
            {
                ModBase.Log($"[World] 读取存档：{SavePath}");
                if (!File.Exists(LevelDatPath))
                {
                    ModBase.Log("[World] 存档没有 level.dat 文件，读取失败");
                    return false;
                }

                using (var fs = new FileStream(LevelDatPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var gameData = new NbtFile();
                    gameData.LoadFromStream(fs, NbtCompression.AutoDetect);
                    var gameVersion = gameData.RootTag.Get<NbtCompound>("Version");
                    if (gameVersion is null)
                    {
                        ModBase.Log("[World] Version 标签存在问题，读取失败");
                        return false;
                    }

                    VersionName = gameVersion.Get<NbtString>("Name").Value;
                    VersionId = gameVersion.Get<NbtInt>("Id").Value.ToString();
                }

                return true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取存档时出错");
                return false;
            }
        }
    }

    #endregion
}