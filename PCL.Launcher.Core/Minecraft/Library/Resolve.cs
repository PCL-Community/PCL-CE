using PCL.Core.App;

namespace PCL;

public static partial class Libraries
{
    /// <summary>
    ///     获取对应的支持库文件地址。
    /// </summary>
    /// <param name="original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    /// <param name="withHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
    public static string McLibGet(
        string original,
        bool withHead = true,
        bool ignoreLiteLoader = false,
        string? customMcFolder = null)
    {
        customMcFolder ??= GameFolders.mcFolderSelected ?? "";

        var splited = original.Split(":");
        var versionParts = splited[2].Split('.');
        var mcLibGetRet = withHead
            ? Path.Combine(
                customMcFolder,
                "libraries",
                splited[0].Replace(".", @"\"),
                splited[1],
                splited[2],
                $"{splited[1]}-{splited[2]}.jar")
            : Path.Combine(
                splited[0].Replace(".", @"\"),
                splited[1],
                splited[2],
                $"{splited[1]}-{splited[2]}.jar");

        if (!mcLibGetRet.Contains(@"optifine\OptiFine\1.") || versionParts.Length <= 1)
            return mcLibGetRet;

        var majorVersion = (int)Math.Round(LauncherText.Val(versionParts[1].BeforeFirst("_")));
        var minorVersion = (int)
            Math.Round(
                versionParts.Length > 2 ? LauncherText.Val(versionParts[2].BeforeFirst("_")) : 0d);

        var installerPath =
            $@"{customMcFolder}libraries\{splited[0].Replace(".", @"\")}\"
            + $@"{splited[1]}\{splited[2]}\{splited[1]}-{splited[2]}-installer.jar";
        if ((majorVersion != 12 && (majorVersion != 20 || minorVersion < 4) && majorVersion < 21)
            || !File.Exists(installerPath))
            return mcLibGetRet;
        LauncherCommon.Log($"已将 {original} 替换为对应的 Installer 文件");
        mcLibGetRet = mcLibGetRet.Replace(".jar", "-installer.jar");

        return mcLibGetRet;
    }

    /// <summary>
    ///     根据 JSON 中的 downloads.artifact.path 获取支持库文件地址。
    /// </summary>
    private static string McLibGetByArtifactPath(string artifactPath, string? customMcFolder = null)
    {
        customMcFolder ??= GameFolders.mcFolderSelected ?? "";
        if (string.IsNullOrWhiteSpace(artifactPath))
            throw new ArgumentException("支持库下载路径无效", nameof(artifactPath));

        var librariesRoot = Path.GetFullPath(Path.Combine(customMcFolder, "libraries"));
        var normalizedArtifactPath = artifactPath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedArtifactPath))
            throw new IOException($"支持库下载路径不能为绝对路径: {artifactPath}");

        var localPath = Path.GetFullPath(Path.Combine(librariesRoot, normalizedArtifactPath));
        var librariesRootWithSeparator =
            librariesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return !localPath.StartsWith(librariesRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? throw new IOException($"支持库下载路径不能位于 libraries 文件夹外: {artifactPath}")
            : localPath;
    }

    /// <summary>
    ///     检查设置，是否应当忽略文件检查？
    /// </summary>
    public static bool ShouldIgnoreFileCheck(McInstance version)
    {
        return Config.Instance.DisableAssetVerifyV2[version.PathInstance]
            || Config.Instance.AssetVerifySolutionV1[version.PathInstance] == 2;
    }
}