using System.IO;
using System.IO.Compression;

namespace PCL.Core.Minecraft;

public class GameCore
{
    private readonly string _corePath;
    public GameCore(string corePath)
    {
        if (!File.Exists(corePath)) throw new FileNotFoundException($"未找到指定文件：{corePath}");
        this._corePath = corePath;
    }
    /// <summary>
    /// 将指定的 Jar 文件添加到到游戏核心
    /// </summary>
    /// <param name="jarPath">要添加到 Jar 的文件</param>
    /// <exception cref="FileNotFoundException">提供的文件路径不存在</exception>
    public void AddToCore(string jarPath)
    {
        if (!File.Exists(jarPath)) throw new FileNotFoundException($"未找到指定文件：{jarPath}");
        using var coreStream = new FileStream(_corePath,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,16384,true);
        using var jarStream = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);
        using var coreArchive = new ZipArchive(coreStream,ZipArchiveMode.Update);
        using var jarArchive = new ZipArchive(jarStream);
        // Better Than Wolves 的 Mod File 是 .zip 结尾的
        var filter = jarPath.EndsWith(".jar") ? "" : "MINECRAFT-JAR";
        foreach (var entry in jarArchive.Entries)
        {
            if (!entry.FullName.Contains(filter)) continue;
            using var coreArchiveStream = coreArchive.CreateEntry(entry.FullName).Open();
            using var jarArchiveStream = jarArchive.GetEntry(entry.FullName)?.Open();
            jarArchiveStream?.CopyTo(coreArchiveStream);
        }
        // 删除包含签名文件的目录，避免 Oracle JDK 加载时验证签名失败导致无法启动
        coreArchive.GetEntry("META-INF")?.Delete();
    }
}
