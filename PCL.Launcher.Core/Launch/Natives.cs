using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class GameLaunch
{
    /// <summary>
    ///     各平台需要解压的 Natives 动态库后缀。Windows 仅解压 .dll，其余平台还需 .so 与 .dylib。
    /// </summary>
    private static IReadOnlyList<string> NativeLibraryExtensions =>
        PCL.Core.Platform.PlatformRuntime.Current.Capabilities.NativeLibraryExtensions;

    private static void McLaunchNatives(Loading.LoaderTask<List<Libraries.McLibToken>, int> loader)
    {
        var target = $"{GetNativesFolder()}{Path.DirectorySeparatorChar}";
        Directory.CreateDirectory(target);

        McLaunchLog("正在解压 Natives 文件");
        var existFiles = new List<string>();
        foreach (var native in loader.input!.Where(native => native.IsNatives))
            _ExtractNativeArchive(native, target, existFiles);

        _CleanupStaleNatives(target, existFiles);
    }

    private static void _ExtractNativeArchive(
        Libraries.McLibToken native,
        string target,
        List<string> existFiles)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(new FileStream(native.LocalPath, FileMode.Open));
        }
        catch (InvalidDataException ex)
        {
            LauncherCommon.Log(ex, $"打开 Natives 文件失败（{native.LocalPath}）");
            File.Delete(native.LocalPath);
            throw new Exception(
                Lang.Text("Minecraft.Launch.Error.NativesCorrupted", native.LocalPath));
        }

        using (zip)
        {
            foreach (var entry in zip.Entries)
            {
                var fileName = entry.FullName;
                if (!NativeLibraryExtensions.Any(extension => fileName.EndsWithF(extension, true)))
                    continue;

                var filePath = target + fileName;
                existFiles.Add(filePath);
                var originalFile = new FileInfo(filePath);
                if (originalFile.Exists)
                {
                    if (originalFile.Length == entry.Length)
                    {
                        if (LauncherCommon.ModeDebug)
                            McLaunchLog($"无需解压：{filePath}");
                        continue;
                    }

                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        McLaunchLog(
                            $"删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：{filePath}");
                        McLaunchLog($"实际的错误信息：{ex}");
                        break;
                    }
                }

                LauncherCommon.WriteFile(filePath, entry.Open());
                McLaunchLog($"已解压：{filePath}");
            }
        }
    }

    private static void _CleanupStaleNatives(string target, List<string> existFiles)
    {
        foreach (var fileName in Directory.GetFiles(target))
        {
            if (existFiles.Contains(fileName))
                continue;
            try
            {
                McLaunchLog($"删除：{fileName}");
                File.Delete(fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤");
                McLaunchLog($"实际的错误信息：{ex}");
                return;
            }
        }
    }

    /// <summary>
    ///     获取 Natives 文件夹路径，不以路径分隔符结尾。
    /// </summary>
    private static string GetNativesFolder()
    {
        var instance = InstanceList.McMcInstanceSelected!;
        var result = Path.Combine(instance.PathInstance, $"{instance.Name}-natives");
        if (SystemInfo.IsGBKEncoding || result.IsAscii())
            return result;

        result = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft",
            "bin",
            "natives");
        return result.IsAscii()
            ? result
            : PCL.Core.Platform.PlatformRuntime.Current.Capabilities.UsesWindowsPaths
                ? Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL", "natives")
                : Path.Combine(Path.GetTempPath(), "PCLCE", "natives");
    }
}