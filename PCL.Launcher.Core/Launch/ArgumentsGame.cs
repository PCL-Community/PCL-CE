using System.Globalization;
using PCL.Core.App;

namespace PCL;

public static partial class GameLaunch
{
    private static string McLaunchArgumentsGameOld(McInstance version)
    {
        var dataList = new List<string>();

        var basicString = version.JsonObject["minecraftArguments"]!.ToString();
        if (!basicString.Contains("--height")
            && version.releaseTime >= new DateTime(2013, 6, 17))
            basicString += " --height ${resolution_height} --width ${resolution_width}";
        dataList.Add(basicString);

        var result = dataList.Join(" ");
        return _FixOptiFineTweakClass(result, version);
    }

    private static string McLaunchArgumentsGameNew(McInstance instance)
    {
        var dataList = _CollectInheritedGameArguments(instance);
        var result = _MergeArgumentTokens(dataList);
        return _FixOptiFineTweakClass(result, instance);
    }

    private static List<string> _CollectInheritedGameArguments(McInstance instance)
    {
        var dataList = new List<string>();
        var currentInstance = instance;
        while (true)
        {
            if (currentInstance.JsonObject["arguments"]?["game"] != null)
                foreach (var subJson in currentInstance.JsonObject["arguments"]!["game"]!.AsArray())
                    if (subJson!.GetValueKind() == JsonValueKind.String)
                    {
                        dataList.Add(subJson.ToString());
                    }
                    else if (Libraries.McJsonRuleCheck(subJson["rules"]!))
                    {
                        if (subJson["value"]!.GetValueKind() == JsonValueKind.String)
                            dataList.Add(subJson["value"]!.ToString());
                        else
                            dataList.AddRange(
                                subJson["value"]
                                    !
                                    .AsArray()
                                    .Select(value => value!.ToString()));
                    }

            if (string.IsNullOrEmpty(currentInstance.InheritInstanceName))
                break;

            currentInstance = new McInstance(currentInstance.InheritInstanceName);
        }

        return dataList;
    }

    private static string _MergeArgumentTokens(List<string> dataList)
    {
        var deDuplicateDataList = new List<string>();
        for (var i = 0; i < dataList.Count; i++)
        {
            var currentEntry = dataList[i];
            if (dataList[i].StartsWithF("-"))
                while (i < dataList.Count - 1)
                {
                    if (dataList[i + 1].StartsWithF("-"))
                        break;

                    i += 1;
                    currentEntry += $" {dataList[i]}";
                }

            deDuplicateDataList.Add(currentEntry);
        }

        return deDuplicateDataList.Distinct().ToList().Join(" ");
    }

    private static string _FixOptiFineTweakClass(string result, McInstance version)
    {
        if (version.Info is { HasForge: false, HasLiteLoader: false }
            || !version.Info.HasOptiFine)
            return result;

        if (result.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
        {
            LauncherCommon.Log($"[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：{result}");
            result =
                $"{result
                    .Replace(" --tweakClass optifine.OptiFineForgeTweaker", "")
                    .Replace("--tweakClass optifine.OptiFineForgeTweaker ", "")
                } --tweakClass optifine.OptiFineForgeTweaker";
        }

        if (!result.Contains("--tweakClass optifine.OptiFineTweaker"))
            return result;

        LauncherCommon.Log($"[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：{result}");
        result =
            $"{result
                .Replace(" --tweakClass optifine.OptiFineTweaker", "")
                .Replace("--tweakClass optifine.OptiFineTweaker ", "")
            } --tweakClass optifine.OptiFineForgeTweaker";

        try
        {
            LauncherCommon.WriteFile(
                Path.Combine(version.PathInstance, $"{version.Name}.json"),
                LauncherCommon
                    .ReadFile(Path.Combine(version.PathInstance, $"{version.Name}.json"))
                    .Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "替换 OptiFineForge TweakClass 失败");
        }

        return result;
    }

    private static Dictionary<string, string> McLaunchArgumentsReplace(
        McInstance instance,
        ref Loading.LoaderTask<string, List<Libraries.McLibToken>> loader)
    {
        var gameArguments = _CreateArgumentReplacements(instance);
        gameArguments.Add(
            "${version_type}",
            string.IsNullOrEmpty(
                Config.Instance.TypeInfo[InstanceList.McMcInstanceSelected!.PathInstance])
                ? Config.Launch.TypeInfo
                : Config.Instance.TypeInfo[InstanceList.McMcInstanceSelected.PathInstance]);
        gameArguments.Add(
            "${game_directory}",
            LauncherCommon.ShortenPath(InstanceList.McMcInstanceSelected.PathIndie[..^1]));
        gameArguments.Add(
            "${assets_root}",
            LauncherCommon.ShortenPath($"{GameFolders.mcFolderSelected}assets"));
        gameArguments.Add("${user_properties}", "{}");
        _AddAuthArgumentReplacements(gameArguments);

        var gameSize = _GetGameWindowSize();
        _ApplyLegacyWindowSizeFix(ref gameSize, instance);
        gameArguments.Add(
            "${resolution_width}",
            Math.Round(gameSize.Width).ToString(CultureInfo.InvariantCulture));
        gameArguments.Add(
            "${resolution_height}",
            Math.Round(gameSize.Height).ToString(CultureInfo.InvariantCulture));

        gameArguments.Add(
            "${game_assets}",
            LauncherCommon.ShortenPath($@"{GameFolders.mcFolderSelected}assets\virtual\legacy"));
        gameArguments.Add("${assets_index_name}", GameAssets.McAssetsGetIndexName(instance));

        var libList = Libraries.McLibListGet(instance, true);
        loader.output = libList;
        gameArguments.Add("${classpath}", _BuildClassPath(instance, libList));

        return gameArguments;
    }

    private static Dictionary<string, string> _CreateArgumentReplacements(McInstance instance)
    {
        return new Dictionary<string, string>
        {
            {
                "${classpath_separator}", ";"
            },
            {
                "${natives_directory}", LauncherCommon.ShortenPath(GetNativesFolder())
            },
            {
                "${library_directory}",
                LauncherCommon.ShortenPath($"{GameFolders.mcFolderSelected}libraries")
            },
            {
                "${libraries_directory}",
                LauncherCommon.ShortenPath($"{GameFolders.mcFolderSelected}libraries")
            },
            {
                "${launcher_name}", "PCLCE"
            },
            {
                "${launcher_version}", LauncherCommon.VersionCode.ToString()
            },
            {
                "${version_name}", instance.Name
            }
        };
    }

    private static void _AddAuthArgumentReplacements(Dictionary<string, string> gameArguments)
    {
        gameArguments.Add("${auth_player_name}", mcLoginLoader.output.Name);
        gameArguments.Add("${auth_uuid}", mcLoginLoader.output.Uuid);
        gameArguments.Add("${auth_access_token}", mcLoginLoader.output.AccessToken);
        gameArguments.Add("${access_token}", mcLoginLoader.output.AccessToken);
        gameArguments.Add("${auth_session}", mcLoginLoader.output.AccessToken);
        gameArguments.Add("${user_type}", "msa");
    }

    private static GameWindowSize _GetGameWindowSize()
    {
        switch (Config.Launch.GameWindowMode)
        {
            case GameWindowSizeMode.Launcher:
            {
                var pan = LaunchUi.GetMainPanSize();
                return new GameWindowSize(
                    LauncherCommon.GetPixelSize(pan.Width),
                    LauncherCommon.GetPixelSize(pan.Height) - 29.5d * LauncherCommon.Dpi / 96d);
            }
            case GameWindowSizeMode.Custom:
                return new GameWindowSize(
                    Math.Max(100, (double)Config.Launch.GameWindowWidth),
                    Math.Max(100, (double)Config.Launch.GameWindowHeight));
            case GameWindowSizeMode.Fullscreen:
            case GameWindowSizeMode.Default:
            case GameWindowSizeMode.Maximized:
            default:
                return new GameWindowSize(854d, 480d);
        }
    }

    private static void _ApplyLegacyWindowSizeFix(ref GameWindowSize gameSize, McInstance instance)
    {
        if (instance.Info.Drop > 120
            || mcLaunchJavaSelected!.Installation
                is not { MajorVersion: <= 8, Version.Revision: >= 200 and <= 321 }
            || instance.Info.HasOptiFine
            || instance.Info.HasForge)
            return;

        McLaunchLog(
            $"已应用窗口大小过大修复（{mcLaunchJavaSelected.Installation.Version.Revision}）");
        gameSize.Width /= LauncherCommon.Dpi / 96d;
        gameSize.Height /= LauncherCommon.Dpi / 96d;
    }

    private static string _BuildClassPath(McInstance instance, List<Libraries.McLibToken> libList)
    {
        var cpStrings = new List<string>();
        string? optiFineCp = null;

        if (McLaunchNeedsLegacyFix(instance))
        {
            var legacyFixPath = Path.Combine(LauncherPaths.PureAsciiDirectory, "legacyfix.jar");
            try
            {
                LauncherCommon.WriteFile(
                    legacyFixPath,
                    LauncherCommon.GetResourceStream("Resources/legacyfix.jar")!);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "LegacyFix 释放失败");
            }
        }

        if (McLaunchUsesLwjglUnsafeAgent(instance))
        {
            var agentPath = Path.Combine(
                LauncherPaths.PureAsciiDirectory,
                "lwjgl-unsafe-agent.jar");
            try
            {
                LauncherCommon.WriteFile(
                    agentPath,
                    LauncherCommon.GetResourceStream("Resources/lwjgl-unsafe-agent.jar")!);
                cpStrings.Add(agentPath);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "LWJGL Unsafe Agent 释放失败");
            }
        }

        foreach (var library in libList
                     .Where(library => !library.IsNatives)
                     .Where(library =>
                         !InstanceList.McMcInstanceSelected!.Info.HasCleanroom
                         || library.OriginalName is null
                         || (
                             !library.OriginalName.Contains("org.lwjgl.lwjgl:lwjgl:2.9.4")
                             && !library.OriginalName.Contains("net.java.dev.jna:platform:3.4.0")
                             && !library.OriginalName.Contains(
                                 "com.ibm.icu:icu4j-core-mojang:51.2"))))
            if (library.Name is not null && library.Name == "optifine:OptiFine")
                optiFineCp = library.LocalPath;
            else
                cpStrings.Add(library.LocalPath);

        foreach (var library in Config.Instance.ClasspathHead[instance.PathInstance].Split(";"))
        {
            if (string.IsNullOrWhiteSpace(library))
                continue;
            cpStrings.Insert(0, library);
        }

        if (optiFineCp is not null)
            cpStrings.Insert(cpStrings.Count - 2, optiFineCp);

        return cpStrings.Select(c => LauncherCommon.ShortenPath(c)).Join(";");
    }

    public struct GameWindowSize(double width, double height)
    {
        public double Width = width;
        public double Height = height;
    }
}