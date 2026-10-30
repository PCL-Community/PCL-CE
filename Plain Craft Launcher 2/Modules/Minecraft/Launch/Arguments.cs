using System.Globalization;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class GameLaunch
{
    private static string _mcLaunchArgument = "";

    private static readonly Lock ExtractJavaWrapperLock = new();

    private static readonly Lock ExtractLinkDLock = new();

    internal static void SecretLaunchJvmArgs(ref List<string> dataList)
    {
        var dataJvmCustom = Config.Instance.JvmArgs[
            InstanceList.McMcInstanceSelected!.PathInstance
        ];
        dataList.Insert(
            0,
            string.IsNullOrEmpty(dataJvmCustom) ? Config.Launch.JvmArgs : dataJvmCustom);
        _ApplyPreferredIpStackArgs(dataList);

        var availableGb = KernelInterop.GetAvailablePhysicalMemoryBytes() / 1073741824.0;
        McLaunchLog($"当前剩余内存：{availableGb.ToString("N1", CultureInfo.InvariantCulture)}G");

        _ApplyMemoryArgs(dataList);

        if (!dataList.Any(d => d.Contains("-Dlog4j2.formatMsgNoLookups=true")))
            dataList.Add("-Dlog4j2.formatMsgNoLookups=true");
    }

    private static void _ApplyPreferredIpStackArgs(List<string> dataList)
    {
        switch (Config.Launch.PreferredIpStack)
        {
            case JvmPreferredIpStack.PreferV4:
                dataList.Add("-Djava.net.preferIPv4Stack=true");
                dataList.Add("-Djava.net.preferIPv4Addresses=true");
                break;

            case JvmPreferredIpStack.PreferV6:
                dataList.Add("-Djava.net.preferIPv6Stack=true");
                dataList.Add("-Djava.net.preferIPv6Addresses=true");
                break;

            case JvmPreferredIpStack.Default:
                break;
        }
    }

    private static void _ApplyMemoryArgs(List<string> dataList)
    {
        var totalRamMb = PageInstanceSetup.GetRam(InstanceList.McMcInstanceSelected!) * 1024d;
        var maxHeapArg = Math.Floor(totalRamMb).ToString(CultureInfo.InvariantCulture);

        dataList.Add(
            $"-Xmn{Math.Floor(totalRamMb * 0.15).ToString(CultureInfo.InvariantCulture)}m");
        dataList.Add($"-Xmx{maxHeapArg}m");

        if (dataList.Any(d => d.Contains("-Xms", StringComparison.OrdinalIgnoreCase)))
            return;

        if (Config.Launch.LockMemory)
        {
            dataList.Add($"-Xms{maxHeapArg}m");
            return;
        }

        var initialRam = PageInstanceSetup.GetInitialRam(InstanceList.McMcInstanceSelected!);
        if (initialRam.HasValue)
            dataList.Add(
                $"-Xms{Math.Floor(initialRam.Value * 1024d)
                    .ToString(CultureInfo.InvariantCulture)}m");
    }

    /// <summary>
    ///     释放 Java Wrapper 并返回完整文件路径。
    /// </summary>
    public static string ExtractJavaWrapper()
    {
        var wrapperPath = Path.Combine(Common.pathPure, "JavaWrapper.jar");
        Common.Log($"[Java] 选定的 Java Wrapper 路径：{wrapperPath}");
        lock (ExtractJavaWrapperLock)
        {
            try
            {
                WriteJavaWrapper(wrapperPath);
            }
            catch (Exception ex)
            {
                wrapperPath = _RecoverJavaWrapper(wrapperPath, ex);
            }
        }

        return wrapperPath;
    }

    private static string _RecoverJavaWrapper(string wrapperPath, Exception ex)
    {
        if (!File.Exists(wrapperPath))
            throw new FileNotFoundException("释放 Java Wrapper 失败", ex);

        Common.Log(
            ex,
            "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成",
            Common.LogLevel.Developer);
        try
        {
            File.Delete(wrapperPath);
            WriteJavaWrapper(wrapperPath);
            return wrapperPath;
        }
        catch (Exception ex2)
        {
            Common.Log(
                ex2,
                "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成",
                Common.LogLevel.Developer);
            var fallbackPath = Path.Combine(Common.pathPure, "JavaWrapper2.jar");
            try
            {
                WriteJavaWrapper(fallbackPath);
                return fallbackPath;
            }
            catch (Exception ex3)
            {
                throw new FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3);
            }
        }
    }

    private static void WriteJavaWrapper(string path)
    {
        Common.WriteFile(path, Common.GetResourceStream("Resources/java-wrapper.jar")!);
    }

    /// <summary>
    ///     释放 linkd 并返回完整文件路径。
    /// </summary>
    public static string ExtractLinkD()
    {
        var linkDPath = Path.Combine(Common.pathPure, "linkd.exe");
        lock (ExtractLinkDLock)
        {
            try
            {
                WriteLinkD(linkDPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(linkDPath))
                {
                    Common.Log(
                        ex,
                        "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成",
                        Common.LogLevel.Developer);
                    try
                    {
                        File.Delete(linkDPath);
                        WriteLinkD(linkDPath);
                    }
                    catch (Exception ex2)
                    {
                        throw new FileNotFoundException("释放 linkd 失败", ex2);
                    }
                }
                else
                {
                    throw new FileNotFoundException("释放 linkd 失败", ex);
                }
            }
        }

        return linkDPath;
    }

    private static void WriteLinkD(string path)
    {
        Common.WriteFile(path, Common.GetResourceStream("Resources/linkd.exe")!);
    }

    /// <summary>
    ///     判断是否使用 LegacyFix。
    /// </summary>
    private static bool McLaunchNeedsLegacyFix(McInstance mc)
    {
        if (!Config.Launch.DisableLF && !Config.Instance.DisableLF[mc.PathInstance])
            return mc.releaseTime < new DateTime(2013, 6, 25)
                && mc.releaseTime.Year > 2000;

        Common.Log("[Launch] LegacyFix 已被禁用");
        return false;
    }

    /// <summary>
    ///     获取实例所依赖的 LWJGL 版本
    /// </summary>
    private static string? McLaunchGetLwjglVersion(McInstance mc)
    {
        return (
            from library in Libraries.McLibListGet(mc, false)
            where !string.IsNullOrWhiteSpace(library.OriginalName)
            select library.OriginalName.Split(':')
            into parts
            where
                parts.Length >= 3
                && parts[0].Equals("org.lwjgl", StringComparison.OrdinalIgnoreCase)
                && parts[1].Equals("lwjgl", StringComparison.OrdinalIgnoreCase)
            select parts[2]
        ).FirstOrDefault();
    }

    /// <summary>
    ///     判断是否启用了针对 Minecraft 26.1 的性能问题补丁
    /// </summary>
    private static bool McLaunchUsesLwjglUnsafeAgent(McInstance mc)
    {
        if (McLaunchGetLwjglVersion(mc) != "3.4.1")
            return false;

        var globalDisabled = Config.Launch.DisableLwjglUnsafeAgent;
        var instanceDisabled = Config.Instance.DisableLwjglUnsafeAgent[mc.PathInstance];
        return !globalDisabled && !instanceDisabled;
    }

    private static void McLaunchArgumentMain(
        Loading.LoaderTask<string, List<Libraries.McLibToken>> loader)
    {
        McLaunchLog("开始获取 Minecraft 启动参数");
        var mc = InstanceList.McMcInstanceSelected!;
        var arguments = _CollectJvmArguments(mc);
        McLaunchLog(arguments);

        arguments = _AppendGameArguments(arguments, mc);
        arguments = _ApplyEncodingArguments(arguments);
        arguments = arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"");
        arguments = _ApplyExtraArguments(arguments, mc);

        var replaceArguments = McLaunchArgumentsReplace(mc, ref loader);
        arguments = _RemoveEmptyVersionType(arguments, replaceArguments);
        var finalArguments = _ApplyReplacements(arguments, replaceArguments);
        finalArguments = _AppendWorldAndServerArguments(finalArguments, mc);

        McLaunchLog("Minecraft 启动参数：");
        McLaunchLog(finalArguments);
        _mcLaunchArgument = finalArguments;
    }

    private static string _CollectJvmArguments(McInstance mc)
    {
        if (mc.JsonObject["arguments"]?["jvm"] is not null)
        {
            McLaunchLog("获取新版 JVM 参数");
            var arguments = McLaunchArgumentsJvmNew(mc);
            McLaunchLog("新版 JVM 参数获取成功：");
            return arguments;
        }

        McLaunchLog("获取旧版 JVM 参数");
        var oldArguments = McLaunchArgumentsJvmOld(mc);
        McLaunchLog("旧版 JVM 参数获取成功：");
        return oldArguments;
    }

    private static string _AppendGameArguments(string arguments, McInstance mc)
    {
        if (!string.IsNullOrEmpty(mc.JsonObject["minecraftArguments"]?.ToString()))
        {
            McLaunchLog("获取旧版 Game 参数");
            arguments += $" {McLaunchArgumentsGameOld(mc)}";
            McLaunchLog("旧版 Game 参数获取成功");
        }

        if (mc.JsonObject["arguments"]?["game"] is null)
            return arguments;

        McLaunchLog("获取新版 Game 参数");
        arguments += $" {McLaunchArgumentsGameNew(mc)}";
        McLaunchLog("新版 Game 参数获取成功");
        return arguments;
    }

    private static string _ApplyEncodingArguments(string arguments)
    {
        if (mcLaunchJavaSelected!.Installation.MajorVersion > 8)
        {
            if (!arguments.Contains("-Dstdout.encoding="))
                arguments = $"-Dstdout.encoding=UTF-8 {arguments}";
            if (!arguments.Contains("-Dstderr.encoding="))
                arguments = $"-Dstderr.encoding=UTF-8 {arguments}";
        }

        if (mcLaunchJavaSelected.Installation.MajorVersion >= 18
            && !arguments.Contains("-Dfile.encoding="))
            arguments = $"-Dfile.encoding=COMPAT {arguments}";
        return arguments;
    }

    private static string _ApplyExtraArguments(string arguments, McInstance mc)
    {
        if (Config.Launch.GameWindowMode == 0)
            arguments += " --fullscreen";

        arguments = currentLaunchOptions!.ExtraArgs.Aggregate(
            arguments,
            (current, arg) => current + $" {arg.Trim()}");

        var argumentGame = Config.Instance.GameArgs[mc.PathInstance];
        return $"{arguments} {(string.IsNullOrEmpty(argumentGame)
            ? Config.Launch.GameArgs
            : argumentGame)}";
    }

    private static string _RemoveEmptyVersionType(
        string arguments,
        Dictionary<string, string> replaceArguments)
    {
        if (!string.IsNullOrWhiteSpace(replaceArguments["${version_type}"]))
            return arguments;
        arguments = arguments.Replace(" --versionType ${version_type}", "");
        replaceArguments["${version_type}"] = "\"\"";
        return arguments;
    }

    private static string _ApplyReplacements(
        string arguments,
        Dictionary<string, string> replaceArguments)
    {
        var finalArguments = "";
        foreach (var argumentRaw in arguments.Split(" "))
        {
            var argument = replaceArguments.Aggregate(
                argumentRaw,
                (current, entry) => current.Replace(entry.Key, entry.Value));
            if ((argument.Contains(' ') || argument.Contains(@":\"))
                && !argument.EndsWithF("\""))
                argument = $"\"{argument}\"";
            finalArguments += $"{argument} ";
        }

        return finalArguments.TrimEnd();
    }

    private static string _AppendWorldAndServerArguments(string finalArguments, McInstance mc)
    {
        var options = currentLaunchOptions!;
        var worldName = options.WorldName;
        if (worldName is not null)
            finalArguments += $" --quickPlaySingleplayer \"{worldName}\"";

        var server = string.IsNullOrEmpty(options.ServerIp)
            ? Config.Instance.ServerToEnter[mc.PathInstance]
            : options.ServerIp;
        if (string.IsNullOrWhiteSpace(worldName) && !string.IsNullOrWhiteSpace(server))
        {
            if (mc.releaseTime > new DateTime(2023, 4, 4))
            {
                finalArguments += $" --quickPlayMultiplayer \"{server}\"";
                return finalArguments;
            }

            if (server.Contains(':'))
                finalArguments += $" --server {server.Split(":")[0]} --port {server.Split(":")[1]}";
            else
                finalArguments += $" --server {server} --port 25565";
            if (mc.Info.HasOptiFine)
                HintService.Hint(
                    Lang.Text("Minecraft.Launch.Error.OptiFineAutoJoinWarning"),
                    HintType.Error);
        }

        return finalArguments;
    }

    public class LaunchArgument
    {
        private readonly List<string> _features = [];

        public LaunchArgument(McInstance minecraft)
        {
            if (minecraft.IsOldJson)
                _features = [.. minecraft.JsonObject["minecraftArguments"]!.ToString().Split(' ')];
            else
                foreach (var item in minecraft.JsonObject["arguments"]!["game"]!.AsArray())
                    if (item!.GetValueKind() == JsonValueKind.String)
                    {
                        _features.Add(item.ToString());
                    }
                    else if (item.GetValueKind() == JsonValueKind.Object)
                    {
                        var valueNode = item["value"]!;
                        if (valueNode.GetValueKind() == JsonValueKind.Array)
                            _features.AddRange(valueNode.AsArray().Select(x => x!.ToString()));
                        else if (valueNode.GetValueKind() == JsonValueKind.String)
                            _features.Add(valueNode.ToString());
                    }
        }

        public object HasArguments(string key)
        {
            return _features.Contains(key);
        }
    }
}