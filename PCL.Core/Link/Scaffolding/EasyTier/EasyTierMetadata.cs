using System.IO;
using System.Linq;
using PCL.Core.App;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
///     EasyTier 依赖包的版本与安装路径信息。
///     所有平台差异都来自 <see cref="EasyTierPlatform" />，调用方不需要再判断操作系统。
/// </summary>
public static class EasyTierMetadata
{
    /// <summary>
    ///     当前随启动器分发的 EasyTier 版本。升级时同步更新各平台依赖包。
    /// </summary>
    public const string CurrentEasyTierVer = "2.6.4";

    /// <summary>
    ///     当前平台/架构对应的依赖包描述。
    /// </summary>
    public static EasyTierPackage Package => EasyTierPlatform.Current;

    /// <summary>
    ///     当前版本、当前平台依赖包的安装根目录。
    /// </summary>
    public static string InstallRoot =>
        Path.Combine(
            Paths.SharedLocalData,
            "EasyTier",
            CurrentEasyTierVer,
            Package.FolderName);

    /// <summary>
    ///     EasyTier 核心可执行文件路径。
    /// </summary>
    public static string CorePath => Path.Combine(InstallRoot, Package.CoreFileName);

    /// <summary>
    ///     EasyTier CLI 可执行文件路径。
    /// </summary>
    public static string CliPath => Path.Combine(InstallRoot, Package.CliFileName);

    /// <summary>
    ///     当前平台的 EasyTier 依赖是否完整。
    /// </summary>
    public static bool IsInstalled =>
        Package.RequiredFiles.All(fileName => File.Exists(Path.Combine(InstallRoot, fileName)));

    /// <summary>
    ///     兼容旧调用点。新代码应使用 <see cref="InstallRoot" />。
    /// </summary>
    public static string EasyTierFilePath => InstallRoot;

    /// <summary>
    ///     确保 Unix 平台上的 EasyTier 可执行文件带有执行位。
    ///     Windows 上不做任何操作。
    /// </summary>
    public static void EnsureExecutable()
    {
        EnsureExecutable(Package, InstallRoot);
    }

    internal static void EnsureExecutable(EasyTierPackage package, string installRoot)
    {
        if (package.Os == EasyTierHostOs.Windows)
            return;

        foreach (var fileName in new[]
                 {
                     package.CoreFileName,
                     package.CliFileName
                 })
        {
            var path = Path.Combine(installRoot, fileName);
            if (!File.Exists(path))
                continue;

            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);
        }
    }
}