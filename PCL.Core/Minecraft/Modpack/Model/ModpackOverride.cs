namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 一条覆写目录指令 —— 把压缩包内的某个目录释放到实例目录下的某个位置。
/// </summary>
/// <param name="ArchiveDirectory">
/// 压缩包内的源目录，相对于 <see cref="ModpackArchive.RootPrefix"/>；
/// 空字符串表示整个逻辑根。
/// </param>
/// <param name="TargetSubPath">
/// 相对于实例目录的目标子路径；空字符串表示实例目录本身。
/// </param>
public sealed record ModpackOverride(string ArchiveDirectory, string TargetSubPath = "")
{
    /// <summary>释放到实例根目录的覆写指令。</summary>
    public static ModpackOverride ToInstanceRoot(string archiveDirectory) => new(archiveDirectory);
}
